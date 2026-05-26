using System.Collections.Generic;
using Timberborn.InputSystem;
using Timberborn.SingletonSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SequencedKeys
{
    public class SequencedKeysService : ILoadableSingleton, IUnloadableSingleton, IUpdatableSingleton, IInputProcessor
    {
        private readonly InputService _inputService;
        private readonly ToolbarScanner _toolbarScanner;

        private SequencedKeysOverlay _overlay;

        private Key _activateKey;
        private Key _cancelKey;
        private Key[] _selectKeys;
        private string[] _selectKeyLabels;

        private bool _isActive;
        private List<ToolbarScanner.ButtonInfo> _currentButtons;
        private List<List<ToolbarScanner.ButtonInfo>> _currentGroups;
        private string _breadcrumb;

        private VisualElement _uiRoot;

        private int _deferredScanCountdown;
        private int _previousButtonCount;

        private int _processInputCallCount;
        private bool _loggedProcessInputAlive;

        public bool IsActive => _isActive;

        public SequencedKeysService(
            InputService inputService,
            ToolbarScanner toolbarScanner)
        {
            _inputService = inputService;
            _toolbarScanner = toolbarScanner;
            Debug.Log("[SequencedKeys] Service constructor called.");
        }

        public void Load()
        {
            Debug.Log("[SequencedKeys] Load() called — beginning initialization.");
            InitializeKeys();
            _inputService.AddInputProcessor(this);
            Debug.Log("[SequencedKeys] Load() complete — input processor registered.");
        }

        public void Unload()
        {
            Debug.Log("[SequencedKeys] Unload() called.");
            _inputService.RemoveInputProcessor(this);
            Deactivate();
        }

        public void SetUIRoot(VisualElement root)
        {
            _uiRoot = root;
            _overlay = new SequencedKeysOverlay(root);
            Debug.Log($"[SequencedKeys] SetUIRoot() called. Root: name='{root?.name}', childCount={root?.childCount}");
        }

        public void UpdateSingleton()
        {
            if (!_isActive || _deferredScanCountdown <= 0)
                return;

            _deferredScanCountdown--;
            if (_deferredScanCountdown <= 0)
            {
                Debug.Log("[SequencedKeys] Deferred re-scan executing now.");
                RescanAfterClick();
            }
        }

        public bool ProcessInput()
        {
            _processInputCallCount++;
            if (!_loggedProcessInputAlive && _processInputCallCount >= 60)
            {
                _loggedProcessInputAlive = true;
                Debug.Log($"[SequencedKeys] ProcessInput() alive — called {_processInputCallCount}x, isActive={_isActive}, uiRoot={_uiRoot != null}, keys={_selectKeys?.Length ?? 0}");
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            if (_isActive)
            {
                if (keyboard[_cancelKey].wasPressedThisFrame)
                {
                    Debug.Log("[SequencedKeys] Cancel key pressed — deactivating.");
                    Deactivate();
                    return true;
                }

                if (_deferredScanCountdown > 0)
                    return true;

                for (int i = 0; i < _selectKeys.Length; i++)
                {
                    if (keyboard[_selectKeys[i]].wasPressedThisFrame)
                    {
                        Debug.Log($"[SequencedKeys] Selection key {i} ('{_selectKeyLabels[i]}') pressed.");
                        OnSelectionKeyPressed(i);
                        return true;
                    }
                }

                return true;
            }

            if (keyboard[_activateKey].wasPressedThisFrame)
            {
                Debug.Log("[SequencedKeys] Activation key pressed! Activating...");
                Activate();
                return true;
            }

            return false;
        }

        private void InitializeKeys()
        {
            _activateKey = Key.B;
            _cancelKey = Key.G;
            _selectKeys = new[]
            {
                Key.Q, Key.W, Key.E, Key.R,
                Key.A, Key.S, Key.D, Key.F,
                Key.Z, Key.X, Key.C, Key.V
            };

            _selectKeyLabels = new string[_selectKeys.Length];
            for (int i = 0; i < _selectKeys.Length; i++)
                _selectKeyLabels[i] = _selectKeys[i].ToString();

            Debug.Log($"[SequencedKeys] Keys: activate={_activateKey}, cancel={_cancelKey}, select=[{string.Join(", ", _selectKeyLabels)}]");
        }

        private void Activate()
        {
            if (_uiRoot == null)
            {
                Debug.LogWarning("[SequencedKeys] Activate() — UI root not set.");
                return;
            }

            _isActive = true;
            _breadcrumb = "SEQUENCED KEYS";
            _deferredScanCountdown = 0;

            Debug.Log("[SequencedKeys] Activated! Scanning toolbar...");
            ScanAndSubdivide();
        }

        private void Deactivate()
        {
            Debug.Log("[SequencedKeys] Deactivated.");
            _isActive = false;
            _deferredScanCountdown = 0;
            _currentButtons = null;
            _currentGroups = null;
            _breadcrumb = "";
            _overlay?.Hide();
        }

        private void ScanAndSubdivide()
        {
            if (_uiRoot == null)
                return;

            _currentButtons = _toolbarScanner.FindVisibleButtonsInBottomBar(_uiRoot);
            Debug.Log($"[SequencedKeys] Scan found {_currentButtons.Count} visible button(s).");

            if (_currentButtons.Count == 0)
            {
                _overlay?.Hide();
                _overlay?.ShowStatusBar(_breadcrumb + " (no buttons found)");
                Debug.LogWarning("[SequencedKeys] No toolbar buttons found!");
                return;
            }

            for (int i = 0; i < _currentButtons.Count; i++)
            {
                var b = _currentButtons[i];
                Debug.Log($"[SequencedKeys]   Button[{i}]: label='{b.Label}', root='{b.Root.name}', btn='{b.ClickableButton.name}'");
            }

            if (_currentButtons.Count == 1)
            {
                Debug.Log("[SequencedKeys] Only 1 button — clicking directly.");
                ClickButton(_currentButtons[0]);
                return;
            }

            SubdivideAndShow();
        }

        private void SubdivideAndShow()
        {
            int keyCount = _selectKeys.Length;
            int buttonCount = _currentButtons.Count;

            _currentGroups = new List<List<ToolbarScanner.ButtonInfo>>();

            if (buttonCount <= keyCount)
            {
                foreach (var btn in _currentButtons)
                    _currentGroups.Add(new List<ToolbarScanner.ButtonInfo> { btn });
            }
            else
            {
                int baseSize = buttonCount / keyCount;
                int remainder = buttonCount % keyCount;
                int index = 0;

                for (int g = 0; g < keyCount; g++)
                {
                    int groupSize = baseSize + (g < remainder ? 1 : 0);
                    var group = new List<ToolbarScanner.ButtonInfo>();
                    for (int j = 0; j < groupSize && index < buttonCount; j++)
                        group.Add(_currentButtons[index++]);
                    _currentGroups.Add(group);
                }
            }

            Debug.Log($"[SequencedKeys] Subdivided {buttonCount} buttons into {_currentGroups.Count} groups.");

            _overlay?.ShowHints(_currentGroups, _selectKeyLabels);
            _overlay?.ShowStatusBar(_breadcrumb + "  |  " + _cancelKey + " to cancel");
        }

        private void OnSelectionKeyPressed(int keyIndex)
        {
            if (_currentGroups == null || keyIndex >= _currentGroups.Count)
                return;

            var selectedGroup = _currentGroups[keyIndex];
            if (selectedGroup.Count == 0)
                return;

            if (selectedGroup.Count == 1)
            {
                Debug.Log($"[SequencedKeys] Group {keyIndex} has 1 button — clicking it.");
                ClickButton(selectedGroup[0]);
            }
            else
            {
                _breadcrumb += " > " + _selectKeyLabels[keyIndex];
                _currentButtons = selectedGroup;
                Debug.Log($"[SequencedKeys] Group {keyIndex} has {selectedGroup.Count} buttons — narrowing down.");
                SubdivideAndShow();
            }
        }

        private void ClickButton(ToolbarScanner.ButtonInfo buttonInfo)
        {
            _breadcrumb += " > " + buttonInfo.Label;
            _overlay?.Hide();

            Debug.Log($"[SequencedKeys] Clicking button: '{buttonInfo.Label}' (name='{buttonInfo.ClickableButton.name}')");

            _previousButtonCount = _toolbarScanner.FindVisibleButtonsInBottomBar(_uiRoot).Count;

            var button = buttonInfo.ClickableButton;
            using (var clickEvt = ClickEvent.GetPooled())
            {
                clickEvt.target = button;
                button.SendEvent(clickEvt);
            }

            Debug.Log($"[SequencedKeys] ClickEvent sent. Previous count: {_previousButtonCount}. Waiting 5 frames to re-scan.");
            _deferredScanCountdown = 5;
        }

        private void RescanAfterClick()
        {
            if (!_isActive || _uiRoot == null)
                return;

            var newButtons = _toolbarScanner.FindVisibleButtonsInBottomBar(_uiRoot);
            Debug.Log($"[SequencedKeys] Re-scan: {newButtons.Count} buttons (was {_previousButtonCount}).");

            if (newButtons.Count > 0 && newButtons.Count != _previousButtonCount)
            {
                Debug.Log("[SequencedKeys] Button count changed — continuing sequence.");
                _currentButtons = newButtons;
                SubdivideAndShow();
            }
            else
            {
                Debug.Log("[SequencedKeys] No change in buttons — selection complete.");
                Deactivate();
            }
        }
    }
}
