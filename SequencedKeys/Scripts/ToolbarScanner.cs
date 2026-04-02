using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SequencedKeys
{
    /// <summary>
    /// Scans the UI tree for clickable toolbar buttons.
    /// Works with both the main bottom bar and any submenus/tool-group panels
    /// that appear when a group button is clicked.
    /// </summary>
    public class ToolbarScanner
    {
        /// <summary>
        /// Represents a single clickable button found in the toolbar.
        /// </summary>
        public class ButtonInfo
        {
            public VisualElement Root { get; }
            public Button ClickableButton { get; }
            public string Label { get; }

            public ButtonInfo(VisualElement root, Button clickableButton, string label)
            {
                Root = root;
                ClickableButton = clickableButton;
                Label = label;
            }
        }

        /// <summary>
        /// Finds all visible, interactable tool buttons in the bottom bar area.
        /// Looks for elements with the standard Timberborn tool button structure
        /// (a Button named "ToolButton" inside a tool button wrapper).
        /// </summary>
        public List<ButtonInfo> FindVisibleButtons(VisualElement searchRoot)
        {
            var results = new List<ButtonInfo>();
            if (searchRoot == null)
                return results;

            CollectToolButtons(searchRoot, results);
            return results;
        }

        /// <summary>
        /// Finds visible buttons in all bottom bar panels.
        /// This searches the entire panel stack for tool button patterns.
        /// </summary>
        public List<ButtonInfo> FindVisibleButtonsInBottomBar(VisualElement rootVisualElement)
        {
            var results = new List<ButtonInfo>();
            if (rootVisualElement == null)
                return results;

            // Look for the bottom bar container and any active tool group panels
            var allButtons = new List<ButtonInfo>();

            // Search for tool buttons using Timberborn's naming conventions
            CollectToolButtons(rootVisualElement, allButtons);

            // Filter to only visible, enabled buttons
            foreach (var btn in allButtons)
            {
                if (IsEffectivelyVisible(btn.Root) && btn.ClickableButton.enabledSelf)
                {
                    results.Add(btn);
                }
            }

            return results;
        }

        private void CollectToolButtons(VisualElement element, List<ButtonInfo> results)
        {
            if (element == null || element.resolvedStyle.display == DisplayStyle.None)
                return;

            // Check if this element IS a tool button or contains one
            var toolButton = element.Q<Button>("ToolButton");
            if (toolButton != null && element.ClassListContains("tool-button"))
            {
                var label = ExtractLabel(element);
                results.Add(new ButtonInfo(element, toolButton, label));
                return; // Don't recurse into found buttons
            }

            // Also look for tool group buttons (these open sub-menus)
            var groupButton = element.Q<Button>("ToolGroupButton");
            if (groupButton != null && element.ClassListContains("tool-group-button"))
            {
                var label = ExtractLabel(element);
                results.Add(new ButtonInfo(element, groupButton, label));
                return;
            }

            // Generic fallback: any Button that is a direct interactive element
            // in a bottom-bar-like container
            if (element is Button btn && IsToolbarButton(btn))
            {
                var label = ExtractLabel(element);
                results.Add(new ButtonInfo(element, btn, label));
                return;
            }

            // Recurse into children
            for (int i = 0; i < element.childCount; i++)
            {
                CollectToolButtons(element[i], results);
            }
        }

        private bool IsToolbarButton(Button button)
        {
            // Match buttons that look like toolbar entries:
            // - Has a bottom-bar-button class, OR
            // - Is inside a bottom-bar element, OR
            // - Has a ToolImage child (icon-based button)
            if (button.ClassListContains("bottom-bar-button--red") ||
                button.ClassListContains("bottom-bar-button--blue") ||
                button.ClassListContains("bottom-bar-button--green"))
                return true;

            if (button.Q("ToolImage") != null)
                return true;

            return false;
        }

        private string ExtractLabel(VisualElement element)
        {
            // Try to get the tooltip first, then any text content
            if (!string.IsNullOrEmpty(element.tooltip))
                return element.tooltip;

            var label = element.Q<Label>();
            if (label != null && !string.IsNullOrEmpty(label.text))
                return label.text;

            var textEl = element.Q<TextElement>();
            if (textEl != null && !string.IsNullOrEmpty(textEl.text))
                return textEl.text;

            // Fall back to the name
            return element.name ?? "?";
        }

        private bool IsEffectivelyVisible(VisualElement element)
        {
            var current = element;
            while (current != null)
            {
                if (current.resolvedStyle.display == DisplayStyle.None)
                    return false;
                if (current.resolvedStyle.visibility == Visibility.Hidden)
                    return false;
                if (current.resolvedStyle.opacity < 0.01f)
                    return false;
                current = current.parent;
            }
            return true;
        }
    }
}
