using System.Collections.Generic;
using System.Linq;
using Timberborn.ToolSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace SequencedKeybindings.Services
{
    /// <summary>
    /// Discovers toolbar buttons in the game UI and handles navigation
    /// through groups and submenus.
    ///
    /// Approach: We query the live UIDocument for interactive button
    /// elements within the bottom bar and any open tool panels. This
    /// is a UI-driven approach that works regardless of which toolbar
    /// groups or tools are registered, and automatically adapts when
    /// submenus open or close.
    /// </summary>
    public class ToolbarNavigator
    {
        readonly ToolGroupService _toolGroupService;
        readonly ToolService _toolService;

        int _keysPerPage = 4;
        int _currentPage;
        List<ToolbarEntry> _currentEntries = new List<ToolbarEntry>();
        List<ToolbarEntry> _previousEntries = new List<ToolbarEntry>();

        // Visual element names/classes used by Timberborn's toolbar.
        // These may need adjustment for different game versions.
        static readonly string[] ToolPanelClassNames =
        {
            "toolbar-panel",
            "tool-panel",
            "bottom-bar-panel"
        };

        static readonly string[] ButtonClassNames =
        {
            "tool-group-button",
            "tool-button",
            "bottom-bar-button"
        };

        public int CurrentPage => _currentPage;
        public int TotalPages => _currentEntries.Count > 0
            ? Mathf.CeilToInt((float)_currentEntries.Count / _keysPerPage)
            : 1;
        public bool HasNextPage => (_currentPage + 1) * _keysPerPage < _currentEntries.Count;

        public ToolbarNavigator(ToolGroupService toolGroupService, ToolService toolService)
        {
            _toolGroupService = toolGroupService;
            _toolService = toolService;
        }

        /// <summary>
        /// Called when entering sequence mode. Snapshots the current
        /// toolbar state.
        /// </summary>
        public void BeginSequence(int keysPerPage)
        {
            _keysPerPage = Mathf.Max(keysPerPage, 2);
            _currentPage = 0;
            SnapshotToolbar();
        }

        /// <summary>
        /// Called when leaving sequence mode.
        /// </summary>
        public void EndSequence()
        {
            _currentPage = 0;
            _currentEntries.Clear();
            _previousEntries.Clear();
        }

        /// <summary>
        /// Advance to the next page of buttons, wrapping to page 0.
        /// </summary>
        public void NextPage()
        {
            if (_currentEntries.Count == 0)
                return;

            _currentPage++;
            if (_currentPage * _keysPerPage >= _currentEntries.Count)
                _currentPage = 0;
        }

        /// <summary>
        /// Returns the buttons visible on the current page.
        /// </summary>
        public List<ToolbarEntry> GetCurrentPageButtons()
        {
            int start = _currentPage * _keysPerPage;
            return _currentEntries
                .Skip(start)
                .Take(_keysPerPage)
                .ToList();
        }

        /// <summary>
        /// Activate the button at the given key index on the current page.
        /// Returns true if a button was clicked.
        /// </summary>
        public bool ActivateItem(int keyIndex)
        {
            int actualIndex = _currentPage * _keysPerPage + keyIndex;
            if (actualIndex >= _currentEntries.Count)
                return false;

            var entry = _currentEntries[actualIndex];
            _previousEntries = new List<ToolbarEntry>(_currentEntries);
            entry.Click();
            return true;
        }

        /// <summary>
        /// Called one frame after ActivateItem. Re-snapshots the toolbar
        /// and returns true if the toolbar state changed (submenu opened).
        /// </summary>
        public bool RefreshAfterClick()
        {
            SnapshotToolbar();
            return !EntriesMatch(_currentEntries, _previousEntries);
        }

        void SnapshotToolbar()
        {
            _currentEntries = DiscoverToolbarEntries();
            _currentPage = 0;
        }

        /// <summary>
        /// Discovers all interactive toolbar buttons currently visible
        /// in the game UI. Finds the deepest open toolbar panel and
        /// returns its interactive children.
        /// </summary>
        List<ToolbarEntry> DiscoverToolbarEntries()
        {
            var entries = new List<ToolbarEntry>();

            // Try to find the toolbar via the PanelStack / VisualElement tree.
            // The game's UI root is accessible via the active UIDocument panels.
            var panels = GetActiveUIPanels();
            if (panels == null || panels.Count == 0)
                return entries;

            // Find the deepest visible toolbar panel
            VisualElement targetPanel = null;
            foreach (var panel in panels)
            {
                var toolPanels = FindToolPanels(panel);
                if (toolPanels.Count > 0)
                {
                    // Use the last (deepest) visible panel
                    targetPanel = toolPanels.Last();
                }
            }

            // If no explicit tool panel found, look for the bottom bar itself
            if (targetPanel == null)
            {
                foreach (var panel in panels)
                {
                    targetPanel = FindBottomBar(panel);
                    if (targetPanel != null)
                        break;
                }
            }

            if (targetPanel == null)
                return entries;

            // Get all interactive button children
            foreach (var child in targetPanel.Children())
            {
                if (!IsVisibleElement(child))
                    continue;

                var button = FindInteractiveButton(child);
                if (button != null)
                {
                    entries.Add(new ToolbarEntry(button));
                }
            }

            return entries;
        }

        List<VisualElement> GetActiveUIPanels()
        {
            var result = new List<VisualElement>();

            // Access all active UI panels via UIDocument iteration.
            // In Unity UIToolkit, each UIDocument has a rootVisualElement.
            var uiDocuments = Object.FindObjectsOfType<UIDocument>();
            foreach (var doc in uiDocuments)
            {
                if (doc.rootVisualElement != null)
                    result.Add(doc.rootVisualElement);
            }

            return result;
        }

        List<VisualElement> FindToolPanels(VisualElement root)
        {
            var panels = new List<VisualElement>();

            // Query for known tool panel class names
            foreach (var className in ToolPanelClassNames)
            {
                root.Query<VisualElement>(className: className)
                    .ForEach(panel =>
                    {
                        if (IsVisibleElement(panel))
                            panels.Add(panel);
                    });
            }

            // Also try querying by name patterns
            root.Query<VisualElement>()
                .ForEach(el =>
                {
                    if (el.name != null &&
                        (el.name.Contains("ToolPanel") ||
                         el.name.Contains("ToolBar")) &&
                        IsVisibleElement(el) &&
                        HasInteractiveChildren(el))
                    {
                        if (!panels.Contains(el))
                            panels.Add(el);
                    }
                });

            return panels;
        }

        VisualElement FindBottomBar(VisualElement root)
        {
            // Try known names for the bottom bar
            var names = new[]
            {
                "BottomBar", "GameBottomBar", "BottomBarPanel",
                "ToolBar", "MainToolBar"
            };

            foreach (var name in names)
            {
                var element = root.Q<VisualElement>(name);
                if (element != null && IsVisibleElement(element))
                    return element;
            }

            // Try known classes
            var classes = new[] { "bottom-bar", "toolbar", "main-toolbar" };
            foreach (var cls in classes)
            {
                var element = root.Q<VisualElement>(className: cls);
                if (element != null && IsVisibleElement(element))
                    return element;
            }

            return null;
        }

        VisualElement FindInteractiveButton(VisualElement element)
        {
            // Check if this element itself is a button
            if (element is Button)
                return element;

            // Check for known button classes
            foreach (var cls in ButtonClassNames)
            {
                if (element.ClassListContains(cls))
                    return element;
            }

            // Check if the element contains a Button child (wrapped buttons)
            var childButton = element.Q<Button>();
            if (childButton != null)
                return element;

            // Check if element has any registered click callbacks
            // by checking if it has a Clickable manipulator
            if (element.IsInteractable())
                return element;

            return null;
        }

        bool HasInteractiveChildren(VisualElement element)
        {
            foreach (var child in element.Children())
            {
                if (IsVisibleElement(child) && FindInteractiveButton(child) != null)
                    return true;
            }
            return false;
        }

        static bool IsVisibleElement(VisualElement element)
        {
            return element.resolvedStyle.display != DisplayStyle.None
                && element.resolvedStyle.visibility != Visibility.Hidden
                && element.enabledInHierarchy;
        }

        static bool EntriesMatch(List<ToolbarEntry> a, List<ToolbarEntry> b)
        {
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Element != b[i].Element)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Wrapper around a toolbar button's VisualElement, providing
    /// click simulation.
    /// </summary>
    public class ToolbarEntry
    {
        public VisualElement Element { get; }

        public ToolbarEntry(VisualElement element)
        {
            Element = element;
        }

        /// <summary>
        /// Simulates a click on this toolbar button.
        /// Tries multiple strategies to ensure the click registers
        /// with Timberborn's UI system.
        /// </summary>
        public void Click()
        {
            // Strategy 1: If the element is a Button, use its click event
            if (Element is Button button)
            {
                using var evt = ClickEvent.GetPooled();
                evt.target = button;
                button.SendEvent(evt);
                return;
            }

            // Strategy 2: Find a Button child and click it
            var childButton = Element.Q<Button>();
            if (childButton != null)
            {
                using var evt = ClickEvent.GetPooled();
                evt.target = childButton;
                childButton.SendEvent(evt);
                return;
            }

            // Strategy 3: Send ClickEvent to the element directly
            {
                using var evt = ClickEvent.GetPooled();
                evt.target = Element;
                Element.SendEvent(evt);
            }
        }
    }

    /// <summary>
    /// Extension for checking if a VisualElement is interactable
    /// (has click handlers).
    /// </summary>
    public static class VisualElementExtensions
    {
        public static bool IsInteractable(this VisualElement element)
        {
            // Check for Clickable manipulator (used by Button and similar)
            if (element.clickable != null)
                return true;

            // If it responds to focus, it's likely interactive
            if (element.focusable)
                return true;

            return false;
        }
    }
}
