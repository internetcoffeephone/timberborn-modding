using System.Collections.Generic;
using Timberborn.InputSystem;
using Timberborn.KeyBindingSystem;
using Timberborn.SingletonSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace SequencedKeys
{
    /// <summary>
    /// Core service implementing the sequenced keybinding state machine.
    ///
    /// Flow:
    /// 1. Player presses the activation hotkey -> enters sequenced mode
    /// 2. Visible buttons in the current toolbar are divided into N groups
    ///    (where N = number of selection keys, default 4)
    /// 3. Each group is labeled with a key hint overlay
    /// 4. Player presses a selection key -> if the group has one button, click it;
    ///    if it has multiple, subdivide again and GOTO 2
    /// 5. If the clicked button opens a submenu/tool group, re-scan for new buttons
    ///    and GOTO 2
    /// 6. Escape or cancel key exits sequenced mode at any time
    /// </summary>
    public class SequencedKeysService : ILoadableSingleton, IUnloadableSingleton, IUpdatableSingleton, IInputProcessor
    {
        private readonly InputService _inputService;
        private readonly KeyBindingRegistry _keyBindingRegistry;
        private readonly InputBindingDescriber _inputBindingDescriber;
        private readonly ToolbarScanner _toolbarScanner;

        private SequencedKeysOverlay _overlay;

        // Keybinding IDs
        private string _activateKeyId;
        private string _cancelKeyId;
        private string[] _selectKeyIds;
        private string[] _selectKeyLabels;

        // State
        private bool _isActive;
        private List<ToolbarScanner.ButtonInfo> _currentButtons;
        private List<List<ToolbarScanner.ButtonInfo>> _currentGroups;
        private string _breadcrumb;

        // Root UI element reference - set during initialization
        private VisualElement _uiRoot;

        // Deferred re-scan state: after clicking a button that may open a submenu,
        // wait a few frames for the UI to update before scanning again.
        private int _deferredScanCountdown;
        private int _previousButtonCount;

        public bool IsActive => _isActive;

        public SequencedKeysService(
            InputService inputService,
            KeyBindingRegistry keyBindingRegistry,
            InputBindingDescriber inputBindingDescriber,
            ToolbarScanner toolbarScanner)
        {
            _inputService = inputService;
            _keyBindingRegistry = keyBindingRegistry;
            _inputBindingDescriber = inputBindingDescriber;
            _toolbarScanner = toolbarScanner;
        }

        public void Load()
        {
            InitializeKeyBindings();
            _inputService.AddInputProcessor(this);
        }

        public void Unload()
        {
            _inputService.RemoveInputProcessor(this);
            Deactivate();
        }

        /// <summary>
        /// Must be called after the UI is loaded to provide the root visual element.
        /// </summary>
        public void SetUIRoot(VisualElement root)
        {
            _uiRoot = root;
            _overlay = new SequencedKeysOverlay(root);
        }

        /// <summary>
        /// Called every frame. Handles deferred re-scanning after a button click.
        /// </summary>
        public void UpdateSingleton()
        {
            if (!_isActive || _deferredScanCountdown <= 0)
                return;

            _deferredScanCountdown--;
            if (_deferredScanCountdown <= 0)
            {
                RescanAfterClick();
            }
        }

        public bool ProcessInput()
        {
            if (_isActive)
            {
                // Cancel key
                if (_inputService.IsKeyDown(_cancelKeyId))
                {
                    Deactivate();
                    return true;
                }

                // Don't process selection keys while waiting for a deferred scan
                if (_deferredScanCountdown > 0)
                    return true;

                // Selection keys
                for (int i = 0; i < _selectKeyIds.Length; i++)
                {
                    if (_inputService.IsKeyDown(_selectKeyIds[i]))
                    {
                        OnSelectionKeyPressed(i);
                        return true;
                    }
                }

                // Consume keyboard input while active to prevent game actions
                return true;
            }

            // Activation key (only when not active)
            if (_inputService.IsKeyDown(_activateKeyId))
            {
                Activate();
                return true;
            }

            return false;
        }

        private void InitializeKeyBindings()
        {
            _activateKeyId = SequencedKeysConstants.ActivateKeyId;
            _cancelKeyId = SequencedKeysConstants.CancelKeyId;

            // Discover how many selection keys are registered by probing the registry
            var selectKeys = new List<string>();
            for (int i = 1; i <= 26; i++)
            {
                var keyId = SequencedKeysConstants.SelectKeyIdPrefix + i;
                try
                {
                    var binding = _keyBindingRegistry.Get(keyId);
                    if (binding != null)
                        selectKeys.Add(keyId);
                }
                catch
                {
                    break;
                }
            }

            if (selectKeys.Count < SequencedKeysConstants.MinSelectionKeys)
            {
                Debug.LogWarning(
                    $"[SequencedKeys] Found {selectKeys.Count} selection key(s), " +
                    $"minimum is {SequencedKeysConstants.MinSelectionKeys}. " +
                    "Ensure you have enough KeyBindingSpec JSON files.");
            }

            _selectKeyIds = selectKeys.ToArray();
            _selectKeyLabels = new string[_selectKeyIds.Length];
            for (int i = 0; i < _selectKeyIds.Length; i++)
            {
                _selectKeyLabels[i] = GetKeyLabel(_selectKeyIds[i]);
            }

            Debug.Log($"[SequencedKeys] Initialized with {_selectKeyIds.Length} selection keys.");
        }

        private string GetKeyLabel(string keyId)
        {
            try
            {
                var binding = _keyBindingRegistry.Get(keyId);
                var primary = binding.PrimaryInputBinding ?? binding.SecondaryInputBinding;
                if (primary != null)
                    return _inputBindingDescriber.GetInputBindingText(primary);
            }
            catch
            {
                // Fallback to key ID
            }
            return keyId;
        }

        private void Activate()
        {
            if (_uiRoot == null)
            {
                Debug.LogWarning("[SequencedKeys] UI root not set yet. Cannot activate.");
                return;
            }

            _isActive = true;
            _breadcrumb = "SEQUENCED KEYS";
            _deferredScanCountdown = 0;

            ScanAndSubdivide();
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

        private void ScanAndSubdivide()
        {
            if (_uiRoot == null)
                return;

            _currentButtons = _toolbarScanner.FindVisibleButtonsInBottomBar(_uiRoot);

            if (_currentButtons.Count == 0)
            {
                _overlay?.Hide();
                _overlay?.ShowStatusBar(_breadcrumb + " (scanning...)");
                return;
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
                // 1:1 mapping - each button gets its own key
                foreach (var btn in _currentButtons)
                {
                    _currentGroups.Add(new List<ToolbarScanner.ButtonInfo> { btn });
                }
            }
            else
            {
                // Divide evenly into keyCount groups
                int baseSize = buttonCount / keyCount;
                int remainder = buttonCount % keyCount;
                int index = 0;

                for (int g = 0; g < keyCount; g++)
                {
                    int groupSize = baseSize + (g < remainder ? 1 : 0);
                    var group = new List<ToolbarScanner.ButtonInfo>();
                    for (int j = 0; j < groupSize && index < buttonCount; j++)
                    {
                        group.Add(_currentButtons[index++]);
                    }
                    _currentGroups.Add(group);
                }
            }

            _overlay?.ShowHints(_currentGroups, _selectKeyLabels);
            _overlay?.ShowStatusBar(_breadcrumb + "  |  ESC to cancel");
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
                // Narrow down: this group becomes the new button set
                _breadcrumb += " > " + _selectKeyLabels[keyIndex];
                _currentButtons = selectedGroup;
                SubdivideAndShow();
            }
        }

        private void ClickButton(ToolbarScanner.ButtonInfo buttonInfo)
        {
            _breadcrumb += " > " + buttonInfo.Label;
            _overlay?.Hide();

            // Record how many buttons exist before the click
            _previousButtonCount = _toolbarScanner.FindVisibleButtonsInBottomBar(_uiRoot).Count;

            var button = buttonInfo.ClickableButton;

            // UI Toolkit Button responds to ClickEvent.
            // We also send NavigationSubmitEvent as a fallback, which is the
            // keyboard-accessible equivalent of clicking a focused element.
            using (var clickEvt = ClickEvent.GetPooled())
            {
                clickEvt.target = button;
                button.SendEvent(clickEvt);
            }

            // Schedule a deferred re-scan to check if new buttons appeared
            // (e.g., clicking a tool group opens a submenu with more buttons)
            _deferredScanCountdown = 5; // Wait 5 frames for UI to update
        }

        /// <summary>
        /// After a button click, re-scan the toolbar. If new buttons appeared
        /// (submenu opened), continue the sequence. Otherwise, the selection
        /// is complete and we deactivate.
        /// </summary>
        private void RescanAfterClick()
        {
            if (!_isActive || _uiRoot == null)
                return;

            var newButtons = _toolbarScanner.FindVisibleButtonsInBottomBar(_uiRoot);

            if (newButtons.Count > 0 && newButtons.Count != _previousButtonCount)
            {
                // New buttons appeared - a submenu/tool group was opened
                // Continue the sequencing with the new buttons
                _currentButtons = newButtons;
                SubdivideAndShow();
            }
            else
            {
                // No change or no buttons - selection is complete
                Deactivate();
            }
        }
    }
}
