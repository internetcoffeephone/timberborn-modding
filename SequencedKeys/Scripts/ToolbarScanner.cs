using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SequencedKeys
{
    public class ToolbarScanner
    {
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

        private bool _loggedStructure;

        public List<ButtonInfo> FindVisibleButtons(VisualElement searchRoot)
        {
            return FindVisibleButtonsInBottomBar(searchRoot);
        }

        public List<ButtonInfo> FindVisibleButtonsInBottomBar(VisualElement rootVisualElement)
        {
            if (rootVisualElement == null)
                return new List<ButtonInfo>();

            var bottomBar = rootVisualElement.Q("Bottom-bar");
            if (bottomBar == null)
            {
                Debug.Log("[SequencedKeys] Bottom-bar not found, scanning from root.");
                return ScanElement(rootVisualElement);
            }

            if (!_loggedStructure)
            {
                _loggedStructure = true;
                Debug.Log($"[SequencedKeys] Bottom-bar has {bottomBar.childCount} children.");
                for (int i = 0; i < bottomBar.childCount; i++)
                {
                    var child = bottomBar[i];
                    Debug.Log($"[SequencedKeys]   Bottom-bar child[{i}]: name='{child.name}', " +
                              $"type={child.GetType().Name}, children={child.childCount}, " +
                              $"visible={child.resolvedStyle.display != DisplayStyle.None}");
                }
            }

            // Scan children of Bottom-bar in reverse order.
            // The last visible child with tool buttons is the active submenu.
            for (int i = bottomBar.childCount - 1; i >= 1; i--)
            {
                var child = bottomBar[i];
                if (child.resolvedStyle.display == DisplayStyle.None)
                    continue;

                var submenuButtons = ScanElement(child);
                if (submenuButtons.Count > 0)
                {
                    Debug.Log($"[SequencedKeys] Using submenu panel (child[{i}]) " +
                              $"with {submenuButtons.Count} buttons.");
                    return submenuButtons;
                }
            }

            // No submenu — scan the first child (main toolbar)
            if (bottomBar.childCount > 0)
                return ScanElement(bottomBar[0]);

            return ScanElement(bottomBar);
        }

        private List<ButtonInfo> ScanElement(VisualElement searchRoot)
        {
            var results = new List<ButtonInfo>();
            var seen = new HashSet<Button>();

            searchRoot.Query<Button>("ToolGroupButton").ForEach(btn =>
            {
                if (seen.Add(btn) && IsEffectivelyVisible(btn) && btn.enabledSelf)
                {
                    var wrapper = FindButtonWrapper(btn);
                    var label = ExtractLabel(btn, wrapper);
                    if (label != "Tooltip" && label != "Options")
                        results.Add(new ButtonInfo(wrapper, btn, label));
                }
            });

            searchRoot.Query<Button>("ToolButton").ForEach(btn =>
            {
                if (seen.Add(btn) && IsEffectivelyVisible(btn) && btn.enabledSelf)
                {
                    var wrapper = FindButtonWrapper(btn);
                    var label = ExtractLabel(btn, wrapper);
                    if (label != "Tooltip" && label != "Options")
                        results.Add(new ButtonInfo(wrapper, btn, label));
                }
            });

            return results;
        }

        private VisualElement FindButtonWrapper(Button button)
        {
            var current = button.parent;
            int depth = 0;
            while (current != null && depth < 5)
            {
                if (!string.IsNullOrEmpty(current.tooltip))
                    return current;
                if (current.ClassListContains("tool-button") ||
                    current.ClassListContains("tool-group-button") ||
                    current.ClassListContains("tool-group"))
                    return current;
                if (current.parent != null && current.parent.childCount > 15)
                    return current;
                current = current.parent;
                depth++;
            }
            return button.parent ?? button;
        }

        private string ExtractLabel(Button button, VisualElement wrapper)
        {
            if (!string.IsNullOrEmpty(button.tooltip))
                return button.tooltip;

            if (wrapper != button && !string.IsNullOrEmpty(wrapper.tooltip))
                return wrapper.tooltip;

            var label = wrapper.Q<Label>();
            if (label != null && !string.IsNullOrEmpty(label.text))
                return label.text;

            var textEl = wrapper.Q<TextElement>();
            if (textEl != null && !string.IsNullOrEmpty(textEl.text))
                return textEl.text;

            return wrapper.name ?? "?";
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
