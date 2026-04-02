using System.Collections.Generic;
using Timberborn.InputSystem;
using Timberborn.KeyBindingSystem;
using UnityEngine;

namespace SequencedKeybindings.Services
{
    /// <summary>
    /// Core service that manages the sequenced keybinding mode.
    ///
    /// State machine:
    ///   Inactive  -- (Activate key) --> Active
    ///   Active    -- (Escape/Exit key) --> Inactive
    ///   Active    -- (Sequence key) --> click button -->
    ///                if submenu opened: stay Active at deeper level
    ///                if leaf tool selected: Inactive
    ///   Active    -- (NextPage key) --> cycle page, stay Active
    /// </summary>
    public class SequencedKeyService : ILoadableSingleton, IUnloadableSingleton, IInputProcessor
    {
        const int MaxSequenceKeys = 8;
        const string ActivateBindingId = "SequencedKeys.Activate";
        const string ExitBindingId = "SequencedKeys.Exit";
        const string NextPageBindingId = "SequencedKeys.NextPage";
        const string KeyBindingPrefix = "SequencedKeys.Key";

        readonly InputService _inputService;
        readonly KeyBindingRegistry _keyBindingRegistry;
        readonly ToolbarNavigator _toolbarNavigator;
        readonly BadgeOverlayService _badgeOverlayService;

        bool _active;
        bool _pendingRefresh;
        int _activeSequenceKeyCount;

        public bool IsActive => _active;

        public SequencedKeyService(
            InputService inputService,
            KeyBindingRegistry keyBindingRegistry,
            ToolbarNavigator toolbarNavigator,
            BadgeOverlayService badgeOverlayService)
        {
            _inputService = inputService;
            _keyBindingRegistry = keyBindingRegistry;
            _toolbarNavigator = toolbarNavigator;
            _badgeOverlayService = badgeOverlayService;
        }

        public void Load()
        {
            _inputService.AddInputProcessor(this);
            _activeSequenceKeyCount = CountActiveSequenceKeys();
        }

        public void Unload()
        {
            Deactivate();
            _inputService.RemoveInputProcessor(this);
        }

        public bool ProcessInput()
        {
            // Handle pending refresh from previous frame's button click.
            // We wait one frame so the UI has time to update submenus.
            if (_pendingRefresh)
            {
                _pendingRefresh = false;
                bool toolbarChanged = _toolbarNavigator.RefreshAfterClick();
                if (!toolbarChanged)
                {
                    // The toolbar didn't change, so we activated a leaf tool
                    Deactivate();
                }
                else
                {
                    ShowOverlays();
                }
                return true;
            }

            // Check activate/toggle
            if (_keyBindingRegistry.IsDown(ActivateBindingId))
            {
                if (_active)
                    Deactivate();
                else
                    Activate();
                return true;
            }

            if (!_active)
                return false;

            // Check exit
            if (_keyBindingRegistry.IsDown(ExitBindingId))
            {
                Deactivate();
                return true;
            }

            // Check next page
            if (_keyBindingRegistry.IsDown(NextPageBindingId))
            {
                _toolbarNavigator.NextPage();
                ShowOverlays();
                return true;
            }

            // Check sequence keys (Q/W/E/R/...)
            for (int i = 0; i < _activeSequenceKeyCount; i++)
            {
                string bindingId = KeyBindingPrefix + i;
                if (_keyBindingRegistry.IsDown(bindingId))
                {
                    OnSequenceKey(i);
                    return true;
                }
            }

            return false;
        }

        void Activate()
        {
            _active = true;
            _activeSequenceKeyCount = CountActiveSequenceKeys();
            _toolbarNavigator.BeginSequence(_activeSequenceKeyCount);
            ShowOverlays();
        }

        void Deactivate()
        {
            _active = false;
            _pendingRefresh = false;
            _badgeOverlayService.HideAll();
            _toolbarNavigator.EndSequence();
        }

        void OnSequenceKey(int keyIndex)
        {
            bool clicked = _toolbarNavigator.ActivateItem(keyIndex);
            if (!clicked)
                return;

            // Wait one frame for the UI to settle, then check if a
            // submenu opened or if we selected a leaf tool.
            _pendingRefresh = true;
            _badgeOverlayService.HideAll();
        }

        void ShowOverlays()
        {
            var pageButtons = _toolbarNavigator.GetCurrentPageButtons();
            var keyLabels = GetSequenceKeyLabels();
            bool hasNextPage = _toolbarNavigator.HasNextPage;
            int totalPages = _toolbarNavigator.TotalPages;
            int currentPage = _toolbarNavigator.CurrentPage;
            _badgeOverlayService.ShowBadges(pageButtons, keyLabels,
                hasNextPage, currentPage, totalPages);
        }

        List<string> GetSequenceKeyLabels()
        {
            var labels = new List<string>();
            for (int i = 0; i < _activeSequenceKeyCount; i++)
            {
                string bindingId = KeyBindingPrefix + i;
                KeyBinding binding = _keyBindingRegistry.Get(bindingId);
                string label = GetBindingLabel(binding, i);
                labels.Add(label);
            }
            return labels;
        }

        static string GetBindingLabel(KeyBinding binding, int fallbackIndex)
        {
            // Try to extract a short display name from the binding path.
            // Paths look like "<Keyboard>/q" - we want "Q".
            if (binding?.PrimaryInputBinding != null &&
                binding.PrimaryInputBinding.InputBindingSpec.IsDefined)
            {
                string path = binding.PrimaryInputBinding.InputBindingSpec.Path;
                int slashIndex = path.LastIndexOf('/');
                if (slashIndex >= 0 && slashIndex < path.Length - 1)
                {
                    return path.Substring(slashIndex + 1).ToUpperInvariant();
                }
            }
            return (fallbackIndex + 1).ToString();
        }

        int CountActiveSequenceKeys()
        {
            int count = 0;
            for (int i = 0; i < MaxSequenceKeys; i++)
            {
                KeyBinding binding = _keyBindingRegistry.Get(KeyBindingPrefix + i);
                if (binding == null)
                    break;
                if (binding.PrimaryInputBinding == null ||
                    !binding.PrimaryInputBinding.InputBindingSpec.IsDefined)
                    break;
                count++;
            }
            return Mathf.Max(count, 2);
        }
    }
}
