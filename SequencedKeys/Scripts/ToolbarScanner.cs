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
            var target = FindScanTarget(rootVisualElement);
            if (target == null)
                return new List<ButtonInfo>();

            var results = ScanByButtonName(target, "ToolGroupButton");
            Debug.Log($"[SequencedKeys] ScanCategories: found {results.Count} ToolGroupButton(s).");
            return results;
        }

        public List<ButtonInfo> ScanToolButtons(VisualElement rootVisualElement)
        {
            var target = FindScanTarget(rootVisualElement);
            if (target == null)
                return new List<ButtonInfo>();

            var results = ScanByButtonName(target, "ToolButton");
            Debug.Log($"[SequencedKeys] ScanToolButtons: found {results.Count} ToolButton(s).");
            return results;
        }

        private VisualElement FindScanTarget(VisualElement rootVisualElement)
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

            var inner = bottomBar.Q("BottomBar");
            if (inner != null) return inner;
            if (bottomBar.childCount > 1) return bottomBar[1];
            return bottomBar;
        }

        private List<ButtonInfo> ScanByButtonName(VisualElement searchRoot, string buttonName)
        {
            var results = new List<ButtonInfo>();
            var seen = new HashSet<Button>();

            searchRoot.Query<Button>(buttonName).ForEach(btn =>
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

            var current = button.parent;
            for (int d = 0; d < 6 && current != null; d++)
            {
                if (!string.IsNullOrEmpty(current.tooltip))
                    return current.tooltip;
                if (current.childCount > 10)
                    break;
                current = current.parent;
            }

            var label = button.Q<Label>();
            if (label != null && !string.IsNullOrEmpty(label.text))
                return label.text;

            if (wrapper != button)
            {
                label = wrapper.Q<Label>();
                if (label != null && !string.IsNullOrEmpty(label.text))
                    return label.text;

                var textEl = wrapper.Q<TextElement>();
                if (textEl != null && !string.IsNullOrEmpty(textEl.text))
                    return textEl.text;
            }

            return button.name ?? wrapper.name ?? "?";
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
