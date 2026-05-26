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
    ///    (where N = number of selection keys, default 12)
    /// 3. Each group is labeled with a key hint overlay
    /// 4. Player presses a selection key -> if the group has one button, click it;
    ///    if it has multiple, subdivide again and GOTO 2
    /// 5. If the clicked button opens a submenu/tool group, re-scan for new buttons
    ///    and GOTO 2
    /// 6. Cancel key (G) exits sequenced mode at any time
    /// </summary>
    public class SequencedKeysService : ILoadableSingleton, IUnloadableSingleton, IUpdatableSingleton, IInputProcessor
    {
        private readonly InputService _inputService;
        private readonly KeyBindingRegistry _keyBindingRegistry;
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

        // Whether our keybindings were found in the registry
        private bool _keysRegistered;

        // Debug: throttle "ProcessInput called" logging to avoid spam
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
            Debug.Log("[SequencedKeys] Service constructor called.");
        }

        public void Load()
        {
            Debug.Log("[SequencedKeys] Load() called — beginning initialization.");

            // Log all registered keybinding IDs to see what the game knows about
            try
            {
                Debug.Log("[SequencedKeys] Dumping KeyBindingRegistry contents...");
                var registryType = _keyBindingRegistry.GetType();
                foreach (var field in registryType.GetFields(
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance))
                {
                    Debug.Log($"[SequencedKeys]   Registry field: {field.Name} " +
                              $"(type={field.FieldType.Name})");
                    if (field.FieldType.Name.Contains("Dictionary") ||
                        field.FieldType.Name.Contains("IDictionary"))
                    {
                        try
                        {
                            var dict = field.GetValue(_keyBindingRegistry)
                                as System.Collections.IDictionary;
                            if (dict != null)
                            {
                                Debug.Log($"[SequencedKeys]   Dictionary has {dict.Count} entries.");
                                int logged = 0;
                                foreach (var key in dict.Keys)
                                {
                                    Debug.Log($"[SequencedKeys]     key='{key}'");
                                    if (++logged >= 30) break;
                                }
                                if (dict.Count > 30)
                                    Debug.Log($"[SequencedKeys]     ... and {dict.Count - 30} more");
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.Log($"[SequencedKeys] Could not dump registry: {ex.Message}");
            }

            InitializeKeyBindings();
            _inputService.AddInputProcessor(this);
            Debug.Log("[SequencedKeys] Load() complete — input processor registered.");
        }

        public void Unload()
        {
            Debug.Log("[SequencedKeys] Unload() called.");
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
            Debug.Log($"[SequencedKeys] SetUIRoot() called. Root element: " +
                      $"name='{root?.name}', childCount={root?.childCount}");
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
                Debug.Log("[SequencedKeys] Deferred re-scan executing now.");
                RescanAfterClick();
            }
        }

        public bool ProcessInput()
        {
            // Log once that ProcessInput is being called, to confirm the
            // input processor is wired up correctly.
            _processInputCallCount++;
            if (!_loggedProcessInputAlive && _processInputCallCount >= 60)
            {
                _loggedProcessInputAlive = true;
                Debug.Log($"[SequencedKeys] ProcessInput() is alive — " +
                          $"called {_processInputCallCount} times so far. " +
                          $"isActive={_isActive}, uiRoot set={_uiRoot != null}, " +
                          $"activateKeyId='{_activateKeyId}', " +
                          $"selectKeyCount={_selectKeyIds?.Length ?? 0}");
            }

            if (!_keysRegistered)
            {
                // Keys not in the registry — nothing we can do
                return false;
            }

            if (_isActive)
            {
                // Activate key toggles off
                if (SafeIsKeyDown(_activateKeyId))
                {
                    Debug.Log("[SequencedKeys] Activate key pressed while active — toggling off.");
                    Deactivate();
                    return true;
                }

                // Cancel key
                if (SafeIsKeyDown(_cancelKeyId))
                {
                    Debug.Log("[SequencedKeys] Cancel key pressed — deactivating.");
                    Deactivate();
                    return true;
                }

                // Don't process selection keys while waiting for a deferred scan
                if (_deferredScanCountdown > 0)
                    return true;

                // Selection keys
                for (int i = 0; i < _selectKeyIds.Length; i++)
                {
                    if (SafeIsKeyDown(_selectKeyIds[i]))
                    {
                        Debug.Log($"[SequencedKeys] Selection key {i} " +
                                  $"('{_selectKeyLabels[i]}') pressed.");
                        OnSelectionKeyPressed(i);
                        return true;
                    }
                }

                // Consume keyboard input while active to prevent game actions
                return true;
            }

            // Activation key (only when not active)
            if (SafeIsKeyDown(_activateKeyId))
            {
                Debug.Log("[SequencedKeys] Activation key pressed! Activating...");
                Activate();
                return true;
            }

            return false;
        }

        private void InitializeKeyBindings()
        {
            _activateKeyId = SequencedKeysConstants.ActivateKeyId;
            _cancelKeyId = SequencedKeysConstants.CancelKeyId;

            Debug.Log($"[SequencedKeys] InitializeKeyBindings: " +
                      $"activateKeyId='{_activateKeyId}', cancelKeyId='{_cancelKeyId}'");

            // Try to resolve the activate key binding to verify it exists
            try
            {
                var activateBinding = _keyBindingRegistry.Get(_activateKeyId);
                Debug.Log($"[SequencedKeys] Activate binding found: {activateBinding}");
                Debug.Log($"[SequencedKeys] Activate binding type: {activateBinding?.GetType().FullName}");

                // Log available properties for debugging API shape
                if (activateBinding != null)
                {
                    foreach (var prop in activateBinding.GetType().GetProperties())
                    {
                        try
                        {
                            var val = prop.GetValue(activateBinding);
                            Debug.Log($"[SequencedKeys]   Activate.{prop.Name} = {val}");
                        }
                        catch { }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SequencedKeys] FAILED to get activate key binding " +
                               $"'{_activateKeyId}': {ex.Message}");
            }

            // Try to resolve the cancel key binding
            try
            {
                var cancelBinding = _keyBindingRegistry.Get(_cancelKeyId);
                Debug.Log($"[SequencedKeys] Cancel binding found: {cancelBinding}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SequencedKeys] FAILED to get cancel key binding " +
                               $"'{_cancelKeyId}': {ex.Message}");
            }

            // Discover how many selection keys are registered by probing the registry
            var selectKeys = new List<string>();
            for (int i = 1; i <= 26; i++)
            {
                var keyId = SequencedKeysConstants.SelectKeyIdPrefix + i;
                try
                {
                    var binding = _keyBindingRegistry.Get(keyId);
                    if (binding != null)
                    {
                        selectKeys.Add(keyId);
                        Debug.Log($"[SequencedKeys] Found selection key {i}: '{keyId}'");
                    }
                }
                catch
                {
                    Debug.Log($"[SequencedKeys] No binding for '{keyId}' — " +
                              $"stopping discovery at {selectKeys.Count} keys.");
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

            // Check if the activate key was actually found
            _keysRegistered = false;
            try
            {
                _keyBindingRegistry.Get(_activateKeyId);
                _keysRegistered = true;
            }
            catch
            {
                Debug.LogError("[SequencedKeys] KEYBINDINGS NOT REGISTERED! " +
                               "The KeyBindingSpec JSON files are not being loaded by the game. " +
                               "Check that the Mod Builder copied them to the mod folder. " +
                               "Expected location: <Mods>/SequencedKeys/Blueprints/KeyBindings/");
            }

            Debug.Log($"[SequencedKeys] Initialization complete: " +
                      $"keysRegistered={_keysRegistered}, " +
                      $"{_selectKeyIds.Length} selection keys. " +
                      $"Labels: [{string.Join(", ", _selectKeyLabels)}]");
        }

        private bool SafeIsKeyDown(string keyId)
        {
            try
            {
                return _inputService.IsKeyDown(keyId);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return false;
            }
        }

        private static readonly string[] SelectionKeyLabels =
            { "Q", "W", "E", "R", "A", "S", "D", "F", "Z", "X", "C", "V" };

        private string GetKeyLabel(string keyId)
        {
            if (keyId == SequencedKeysConstants.ActivateKeyId) return "B";
            if (keyId == SequencedKeysConstants.CancelKeyId) return "G";
            if (keyId.StartsWith(SequencedKeysConstants.SelectKeyIdPrefix))
            {
                string numPart = keyId.Substring(SequencedKeysConstants.SelectKeyIdPrefix.Length);
                if (int.TryParse(numPart, out int index) &&
                    index >= 1 && index <= SelectionKeyLabels.Length)
                    return SelectionKeyLabels[index - 1];
            }
            return keyId;
        }

        private void Activate()
        {
            if (_uiRoot == null)
            {
                Debug.LogWarning("[SequencedKeys] Activate() — UI root not set. Cannot activate.");
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
                Debug.LogWarning("[SequencedKeys] No toolbar buttons found! " +
                                 "The scanner may not be matching the game's UI structure.");
                return;
            }

            for (int i = 0; i < _currentButtons.Count; i++)
            {
                var b = _currentButtons[i];
                Debug.Log($"[SequencedKeys]   Button[{i}]: label='{b.Label}', " +
                          $"root.name='{b.Root.name}', " +
                          $"button.name='{b.ClickableButton.name}'");
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

            Debug.Log($"[SequencedKeys] Subdivided {buttonCount} buttons into " +
                      $"{_currentGroups.Count} groups " +
                      $"(keyCount={keyCount}).");

            _overlay?.ShowHints(_currentGroups, _selectKeyLabels);

            var cancelLabel = GetKeyLabel(_cancelKeyId);
            _overlay?.ShowStatusBar(_breadcrumb + "  |  " + cancelLabel + " to cancel");
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
                // Narrow down: this group becomes the new button set
                _breadcrumb += " > " + _selectKeyLabels[keyIndex];
                _currentButtons = selectedGroup;
                Debug.Log($"[SequencedKeys] Group {keyIndex} has {selectedGroup.Count} " +
                          $"buttons — narrowing down.");
                SubdivideAndShow();
            }
        }

        private void ClickButton(ToolbarScanner.ButtonInfo buttonInfo)
        {
            _breadcrumb += " > " + buttonInfo.Label;
            _overlay?.Hide();

            Debug.Log($"[SequencedKeys] Clicking button: '{buttonInfo.Label}' " +
                      $"(name='{buttonInfo.ClickableButton.name}')");

            // Record how many buttons exist before the click
            _previousButtonCount = _toolbarScanner.FindVisibleButtonsInBottomBar(_uiRoot).Count;

            var button = buttonInfo.ClickableButton;

            // UI Toolkit Button responds to ClickEvent.
            using (var clickEvt = ClickEvent.GetPooled())
            {
                clickEvt.target = button;
                button.SendEvent(clickEvt);
            }

            Debug.Log($"[SequencedKeys] ClickEvent sent. Previous button count: " +
                      $"{_previousButtonCount}. Waiting 5 frames to re-scan.");

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
            Debug.Log($"[SequencedKeys] Re-scan: {newButtons.Count} buttons " +
                      $"(was {_previousButtonCount}).");

            if (newButtons.Count > 0 && newButtons.Count != _previousButtonCount)
            {
                // New buttons appeared - a submenu/tool group was opened
                Debug.Log("[SequencedKeys] Button count changed — continuing sequence.");
                _currentButtons = newButtons;
                SubdivideAndShow();
            }
            else
            {
                // No change or no buttons - selection is complete
                Debug.Log("[SequencedKeys] No change in buttons — selection complete.");
                Deactivate();
            }
        }
    }
}
