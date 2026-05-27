using System.Collections.Generic;
using Timberborn.InputSystem;
using Timberborn.KeyBindingSystem;
using Timberborn.SingletonSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace SequencedKeys
{
    public class SequencedKeysService : ILoadableSingleton, IUnloadableSingleton, IUpdatableSingleton, IInputProcessor
    {
        private readonly InputService _inputService;
        private readonly KeyBindingRegistry _keyBindingRegistry;
        private readonly ToolbarScanner _toolbarScanner;

        private SequencedKeysOverlay _overlay;

        private string _activateKeyId;
        private string[] _selectKeyIds;
        private string[] _selectKeyLabels;

        private bool _isActive;
        private bool _showingCategories;
        private List<ToolbarScanner.ButtonInfo> _currentButtons;
        private List<List<ToolbarScanner.ButtonInfo>> _currentGroups;
        private string _breadcrumb;

        private VisualElement _uiRoot;

        private int _deferredScanCountdown;

        private bool _keysRegistered;

        private int _processInputCallCount;
        private bool _loggedProcessInputAlive;

        public bool IsActive => _isActive;

        public SequencedKeysService(
            InputService inputService,
            KeyBindingRegistry keyBindingRegistry,
            ToolbarScanner toolbarScanner)
        {
            _inputService = inputService;
            _keyBindingRegistry = keyBindingRegistry;
            _toolbarScanner = toolbarScanner;
        }

        public void Load()
        {
            Debug.Log("[SequencedKeys] Load() called.");
            InitializeKeyBindings();
            _inputService.AddInputProcessor(this);
            Debug.Log("[SequencedKeys] Load() complete — input processor registered.");
        }

        public void Unload()
        {
            _inputService.RemoveInputProcessor(this);
            Deactivate();
        }

        public void SetUIRoot(VisualElement root)
        {
            _uiRoot = root;
            _overlay = new SequencedKeysOverlay(root);
            Debug.Log($"[SequencedKeys] SetUIRoot(): name='{root?.name}', childCount={root?.childCount}");
        }

        public void UpdateSingleton()
        {
            if (!_isActive || _deferredScanCountdown <= 0)
                return;

            _deferredScanCountdown--;
            if (_deferredScanCountdown <= 0)
                RescanAfterClick();
        }

        public bool ProcessInput()
        {
            _processInputCallCount++;
            if (!_loggedProcessInputAlive && _processInputCallCount >= 60)
            {
                _loggedProcessInputAlive = true;
                Debug.Log($"[SequencedKeys] ProcessInput() alive — " +
                          $"isActive={_isActive}, uiRoot={_uiRoot != null}, " +
                          $"keys={_selectKeyIds?.Length ?? 0}");
            }

            if (!_keysRegistered)
                return false;

            if (_isActive)
            {
                if (SafeIsKeyDown(_activateKeyId))
                {
                    Deactivate();
                    return true;
                }

                if (_deferredScanCountdown > 0)
                    return true;

                for (int i = 0; i < _selectKeyIds.Length; i++)
                {
                    if (SafeIsKeyDown(_selectKeyIds[i]))
                    {
                        OnSelectionKeyPressed(i);
                        return true;
                    }
                }

                return true;
            }

            if (SafeIsKeyDown(_activateKeyId))
            {
                Activate();
                return true;
            }

            return false;
        }

        private void InitializeKeyBindings()
        {
            _activateKeyId = SequencedKeysConstants.ActivateKeyId;

            var selectKeys = new List<string>();
            for (int i = 1; i <= 26; i++)
            {
                var keyId = SequencedKeysConstants.SelectKeyIdPrefix + i;
                try
                {
                    if (_keyBindingRegistry.Get(keyId) != null)
                        selectKeys.Add(keyId);
                }
                catch
                {
                    break;
                }
            }

            _selectKeyIds = selectKeys.ToArray();
            _selectKeyLabels = new string[_selectKeyIds.Length];
            for (int i = 0; i < _selectKeyIds.Length; i++)
                _selectKeyLabels[i] = GetKeyLabel(_selectKeyIds[i]);

            _keysRegistered = false;
            try
            {
                _keyBindingRegistry.Get(_activateKeyId);
                _keysRegistered = true;
            }
            catch
            {
                Debug.LogError("[SequencedKeys] Keybindings not found in registry.");
            }

            Debug.Log($"[SequencedKeys] Init: registered={_keysRegistered}, " +
                      $"{_selectKeyIds.Length} selection keys, " +
                      $"labels=[{string.Join(", ", _selectKeyLabels)}]");
        }

        private bool SafeIsKeyDown(string keyId)
        {
            try { return _inputService.IsKeyDown(keyId); }
            catch (KeyNotFoundException) { return false; }
        }

        private static readonly string[] DefaultKeyLabels =
            { "Q", "W", "E", "R", "A", "S", "D", "F", "Z", "X", "C", "V" };

        private string GetKeyLabel(string keyId)
        {
            try
            {
                var binding = _keyBindingRegistry.Get(keyId);
                var primary = binding?.PrimaryInputBinding;
                if (primary != null && primary.IsDefined)
                {
                    var control = primary.InputControl;
                    if (control != null)
                    {
                        var displayName = control.displayName;
                        if (!string.IsNullOrEmpty(displayName))
                            return displayName;
                    }
                }
            }
            catch { }

            if (keyId.StartsWith(SequencedKeysConstants.SelectKeyIdPrefix))
            {
                string numPart = keyId.Substring(SequencedKeysConstants.SelectKeyIdPrefix.Length);
                if (int.TryParse(numPart, out int idx) && idx >= 1 && idx <= DefaultKeyLabels.Length)
                    return DefaultKeyLabels[idx - 1];
            }
            return keyId;
        }

        private void Activate()
        {
            if (_uiRoot == null)
            {
                Debug.LogWarning("[SequencedKeys] Activate() — UI root not set.");
                return;
            }

            _isActive = true;
            _showingCategories = true;
            _breadcrumb = "SEQUENCED KEYS";
            _deferredScanCountdown = 0;
            ScanAndShow();
        }

        private void Deactivate()
        {
            _isActive = false;
            _deferredScanCountdown = 0;
            _currentButtons = null;
            _currentGroups = null;
            _breadcrumb = "";
            _overlay?.Hide();
        }

        private void ScanAndShow()
        {
            if (_uiRoot == null)
                return;

            _currentButtons = _showingCategories
                ? _toolbarScanner.ScanCategories(_uiRoot)
                : _toolbarScanner.ScanToolButtons(_uiRoot);

            Debug.Log($"[SequencedKeys] ScanAndShow: showingCategories={_showingCategories}, " +
                      $"found {_currentButtons.Count} button(s).");

            if (_currentButtons.Count == 0)
            {
                if (_showingCategories)
                {
                    _showingCategories = false;
                    _currentButtons = _toolbarScanner.ScanToolButtons(_uiRoot);
                    Debug.Log($"[SequencedKeys] No categories found, falling back to tools: {_currentButtons.Count}.");
                    if (_currentButtons.Count == 0)
                    {
                        _overlay?.Hide();
                        var activateLabel = GetKeyLabel(_activateKeyId);
                        _overlay?.ShowStatusBar(_breadcrumb + " (no buttons)  |  " + activateLabel + " to close");
                        return;
                    }
                }
                else
                {
                    Deactivate();
                    return;
                }
            }

            if (_currentButtons.Count == 1)
            {
                ClickButton(_currentButtons[0]);
                return;
            }

            SubdivideAndShow();
        }

        private void SubdivideAndShow()
        {
            int keyCount = _selectKeyIds.Length;
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

            _overlay?.ShowHints(_currentGroups, _selectKeyLabels);

            var activateLabel = GetKeyLabel(_activateKeyId);
            _overlay?.ShowStatusBar(_breadcrumb + "  |  " + activateLabel + " to close");
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
                ClickButton(selectedGroup[0]);
            }
            else
            {
                _breadcrumb += " > " + _selectKeyLabels[keyIndex];
                _currentButtons = selectedGroup;
                SubdivideAndShow();
            }
        }

        private void ClickButton(ToolbarScanner.ButtonInfo buttonInfo)
        {
            _breadcrumb += " > " + buttonInfo.Label;
            _overlay?.Hide();

            Debug.Log($"[SequencedKeys] ClickButton: '{buttonInfo.Label}', showingCategories={_showingCategories}");

            var button = buttonInfo.ClickableButton;
            using (var clickEvt = ClickEvent.GetPooled())
            {
                clickEvt.target = button;
                button.SendEvent(clickEvt);
            }

            if (_showingCategories)
                _deferredScanCountdown = 5;
            else
                Deactivate();
        }

        private void RescanAfterClick()
        {
            if (!_isActive || _uiRoot == null)
                return;

            _showingCategories = false;
            ScanAndShow();
        }
    }
}
