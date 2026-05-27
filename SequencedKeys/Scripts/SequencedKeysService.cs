using System.Collections.Generic;
using System.Text;
using Timberborn.InputSystem;
using Timberborn.KeyBindingSystem;
using Timberborn.SingletonSystem;
using UnityEngine;
using UnityEngine.InputSystem;
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
        private string[] _registeredKeyIds;

        private string[] _boundKeyIds;
        private string[] _boundKeyLabels;

        private bool _isActive;
        private bool _showingCategories;
        private List<ToolbarScanner.ButtonInfo> _currentButtons;
        private List<List<ToolbarScanner.ButtonInfo>> _currentGroups;
        private string _breadcrumb;

        private VisualElement _uiRoot;

        private int _deferredScanCountdown;

        private int _heldKeyIndex = -1;
        private InputControl _heldControl;

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
            DiscoverRegisteredKeys();
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
                          $"registered={_registeredKeyIds?.Length ?? 0}");
            }

            if (!_keysRegistered)
                return false;

            if (_isActive)
            {
                if (SafeIsKeyDown(_activateKeyId))
                {
                    Debug.Log("[SequencedKeys] Activate key pressed while active → deactivating.");
                    _heldKeyIndex = -1;
                    _heldControl = null;
                    Deactivate();
                    return true;
                }

                if (_deferredScanCountdown > 0)
                    return true;

                if (_heldKeyIndex >= 0)
                {
                    if (_heldControl == null || !_heldControl.IsPressed())
                    {
                        int idx = _heldKeyIndex;
                        _heldKeyIndex = -1;
                        _heldControl = null;
                        _overlay?.ClearHighlight();
                        Debug.Log($"[SequencedKeys] Key released: {_boundKeyLabels[idx]} → selecting group {idx}.");
                        OnSelectionKeyPressed(idx);
                    }
                    return true;
                }

                for (int i = 0; i < _boundKeyIds.Length; i++)
                {
                    if (SafeIsKeyDown(_boundKeyIds[i]))
                    {
                        _heldKeyIndex = i;
                        _heldControl = GetInputControl(_boundKeyIds[i]);
                        _overlay?.HighlightGroup(i);

                        if (_heldControl != null)
                        {
                            Debug.Log($"[SequencedKeys] Key down: {_boundKeyLabels[i]} — holding for release.");
                        }
                        else
                        {
                            Debug.Log($"[SequencedKeys] Key down: {_boundKeyLabels[i]} — no InputControl, selecting immediately.");
                            _heldKeyIndex = -1;
                            _overlay?.ClearHighlight();
                            OnSelectionKeyPressed(i);
                        }
                        return true;
                    }
                }

                return true;
            }

            if (SafeIsKeyDown(_activateKeyId))
            {
                Debug.Log("[SequencedKeys] Activate key pressed → activating.");
                Activate();
                return true;
            }

            return false;
        }

        private void DiscoverRegisteredKeys()
        {
            _activateKeyId = SequencedKeysConstants.ActivateKeyId;

            var keys = new List<string>();
            for (int i = 1; i <= 26; i++)
            {
                var keyId = SequencedKeysConstants.SelectKeyIdPrefix + i;
                try
                {
                    if (_keyBindingRegistry.Get(keyId) != null)
                        keys.Add(keyId);
                }
                catch
                {
                    break;
                }
            }

            _registeredKeyIds = keys.ToArray();
            _boundKeyIds = new string[0];
            _boundKeyLabels = new string[0];

            _keysRegistered = false;
            try
            {
                _keyBindingRegistry.Get(_activateKeyId);
                _keysRegistered = true;
            }
            catch
            {
                Debug.LogError("[SequencedKeys] Activate keybinding not found in registry.");
            }

            Debug.Log($"[SequencedKeys] DiscoverRegisteredKeys: activate={_keysRegistered}, " +
                      $"{_registeredKeyIds.Length} selection key slots.");
        }

        private void RefreshBoundKeys()
        {
            var ids = new List<string>();
            var labels = new List<string>();

            for (int i = 0; i < _registeredKeyIds.Length; i++)
            {
                var keyId = _registeredKeyIds[i];
                try
                {
                    var binding = _keyBindingRegistry.Get(keyId);
                    var primary = binding?.PrimaryInputBinding;
                    if (primary != null && primary.IsDefined)
                    {
                        ids.Add(keyId);
                        var displayName = primary.InputControl?.displayName;
                        if (!string.IsNullOrEmpty(displayName))
                            labels.Add(displayName);
                        else
                            labels.Add(GetFallbackLabel(keyId));
                    }
                }
                catch { }
            }

            _boundKeyIds = ids.ToArray();
            _boundKeyLabels = labels.ToArray();

            Debug.Log($"[SequencedKeys] RefreshBoundKeys: {_boundKeyIds.Length}/{_registeredKeyIds.Length} bound, " +
                      $"labels=[{string.Join(", ", _boundKeyLabels)}]");
        }

        private bool SafeIsKeyDown(string keyId)
        {
            try { return _inputService.IsKeyDown(keyId); }
            catch (KeyNotFoundException) { return false; }
        }

        private InputControl GetInputControl(string keyId)
        {
            try
            {
                var binding = _keyBindingRegistry.Get(keyId);
                var primary = binding?.PrimaryInputBinding;
                if (primary != null && primary.IsDefined)
                    return primary.InputControl;
            }
            catch { }
            return null;
        }

        private static readonly string[] DefaultKeyLabels =
            { "Q", "W", "E", "R", "A", "S", "D", "F", "Z", "X", "C", "V" };

        private string GetFallbackLabel(string keyId)
        {
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

            RefreshBoundKeys();

            _isActive = true;
            _showingCategories = true;
            _heldKeyIndex = -1;
            _heldControl = null;
            _breadcrumb = "SEQUENCED KEYS";
            _deferredScanCountdown = 0;

            if (_boundKeyIds.Length < SequencedKeysConstants.MinSelectionKeys)
            {
                Debug.LogWarning($"[SequencedKeys] Only {_boundKeyIds.Length} key(s) bound " +
                                 $"(min {SequencedKeysConstants.MinSelectionKeys}). Cannot subdivide.");
                var activateLabel = GetActivateLabel();
                _overlay?.ShowStatusBar(
                    $"SEQUENCED KEYS — bind at least {SequencedKeysConstants.MinSelectionKeys} selection keys  |  " +
                    activateLabel + " to close");
                return;
            }

            ScanAndShow();
        }

        private string GetActivateLabel()
        {
            try
            {
                var binding = _keyBindingRegistry.Get(_activateKeyId);
                var primary = binding?.PrimaryInputBinding;
                if (primary != null && primary.IsDefined)
                {
                    var dn = primary.InputControl?.displayName;
                    if (!string.IsNullOrEmpty(dn)) return dn;
                }
            }
            catch { }
            return "B";
        }

        private void Deactivate()
        {
            Debug.Log("[SequencedKeys] Deactivate().");
            _isActive = false;
            _deferredScanCountdown = 0;
            _heldKeyIndex = -1;
            _heldControl = null;
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
            LogButtonList(_currentButtons);

            if (_currentButtons.Count == 0)
            {
                if (_showingCategories)
                {
                    _showingCategories = false;
                    _currentButtons = _toolbarScanner.ScanToolButtons(_uiRoot);
                    Debug.Log($"[SequencedKeys] No categories, falling back to tools: {_currentButtons.Count}.");
                    LogButtonList(_currentButtons);
                    if (_currentButtons.Count == 0)
                    {
                        _overlay?.Hide();
                        var activateLabel = GetActivateLabel();
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

        private void LogButtonList(List<ToolbarScanner.ButtonInfo> buttons)
        {
            if (buttons.Count == 0) return;
            for (int i = 0; i < buttons.Count; i += 10)
            {
                var sb = new StringBuilder();
                int end = System.Math.Min(i + 10, buttons.Count);
                sb.Append($"[SequencedKeys]   [{i}-{end - 1}]: ");
                for (int j = i; j < end; j++)
                {
                    if (j > i) sb.Append(", ");
                    sb.Append('"').Append(buttons[j].Label).Append("\" (");
                    sb.Append(buttons[j].ClickableButton.name).Append(')');
                }
                Debug.Log(sb.ToString());
            }
        }

        private void SubdivideAndShow()
        {
            int keyCount = _boundKeyIds.Length;
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

            _overlay?.ShowHints(_currentGroups, _boundKeyLabels);

            var activateLabel = GetActivateLabel();
            _overlay?.ShowStatusBar(_breadcrumb + "  |  " + activateLabel + " to close");
        }

        private void OnSelectionKeyPressed(int keyIndex)
        {
            if (_currentGroups == null || keyIndex >= _currentGroups.Count)
            {
                Debug.Log($"[SequencedKeys] Key {keyIndex} out of range (groups={_currentGroups?.Count ?? 0}).");
                return;
            }

            var selectedGroup = _currentGroups[keyIndex];
            if (selectedGroup.Count == 0)
                return;

            if (selectedGroup.Count == 1)
            {
                Debug.Log($"[SequencedKeys] Selected [{_boundKeyLabels[keyIndex]}] → " +
                          $"clicking '{selectedGroup[0].Label}'.");
                ClickButton(selectedGroup[0]);
            }
            else
            {
                var sb = new StringBuilder();
                for (int i = 0; i < selectedGroup.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append('"').Append(selectedGroup[i].Label).Append('"');
                }
                Debug.Log($"[SequencedKeys] Selected [{_boundKeyLabels[keyIndex]}] → " +
                          $"subdividing {selectedGroup.Count} buttons: [{sb}].");

                _breadcrumb += " > " + _boundKeyLabels[keyIndex];
                _currentButtons = selectedGroup;
                SubdivideAndShow();
            }
        }

        private void ClickButton(ToolbarScanner.ButtonInfo buttonInfo)
        {
            _breadcrumb += " > " + buttonInfo.Label;
            _overlay?.Hide();

            Debug.Log($"[SequencedKeys] ClickButton: '{buttonInfo.Label}', " +
                      $"showingCategories={_showingCategories}, " +
                      $"btnName='{buttonInfo.ClickableButton.name}'.");

            var button = buttonInfo.ClickableButton;
            using (var clickEvt = ClickEvent.GetPooled())
            {
                clickEvt.target = button;
                button.SendEvent(clickEvt);
            }

            if (_showingCategories)
            {
                Debug.Log("[SequencedKeys] Category clicked — scheduling tool scan in 5 frames.");
                _deferredScanCountdown = 5;
            }
            else
            {
                Debug.Log("[SequencedKeys] Tool clicked — deactivating.");
                Deactivate();
            }
        }

        private void RescanAfterClick()
        {
            if (!_isActive || _uiRoot == null)
                return;

            Debug.Log("[SequencedKeys] RescanAfterClick — switching to tool buttons.");
            _showingCategories = false;
            ScanAndShow();
        }
    }
}
