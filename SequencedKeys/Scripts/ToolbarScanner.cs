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

        public List<ButtonInfo> ScanCategories(VisualElement rootVisualElement)
        {
            var bottomBar = FindBottomBar(rootVisualElement);
            if (bottomBar == null)
                return new List<ButtonInfo>();

            var toolPanel = bottomBar.Q("ToolPanel");
            if (toolPanel == null && bottomBar.childCount > 0)
                toolPanel = bottomBar[0];
            if (toolPanel == null)
                return new List<ButtonInfo>();

            var results = ScanElement(toolPanel);
            Debug.Log($"[SequencedKeys] ScanCategories: found {results.Count} in '{toolPanel.name}'.");
            return results;
        }

        public List<ButtonInfo> ScanToolButtons(VisualElement rootVisualElement)
        {
            var bottomBar = FindBottomBar(rootVisualElement);
            if (bottomBar == null)
                return new List<ButtonInfo>();

            var toolArea = bottomBar.Q("BottomBar");
            if (toolArea == null && bottomBar.childCount > 1)
                toolArea = bottomBar[1];
            if (toolArea == null)
                return new List<ButtonInfo>();

            var results = ScanElement(toolArea);
            Debug.Log($"[SequencedKeys] ScanToolButtons: found {results.Count} in '{toolArea.name}'.");
            return results;
        }

        private VisualElement FindBottomBar(VisualElement rootVisualElement)
        {
            if (rootVisualElement == null)
                return null;

            var bottomBar = rootVisualElement.Q("Bottom-bar");
            if (bottomBar == null)
            {
                Debug.Log("[SequencedKeys] Bottom-bar not found.");
                return null;
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

            return bottomBar;
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

            results.Sort((a, b) =>
            {
                float ax = a.Root.worldBound.x;
                float bx = b.Root.worldBound.x;
                if (float.IsNaN(ax) || float.IsNaN(bx))
                    return 0;
                return ax.CompareTo(bx);
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
