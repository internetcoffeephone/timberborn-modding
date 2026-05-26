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
            var results = new List<ButtonInfo>();
            if (rootVisualElement == null)
                return results;

            var seen = new HashSet<Button>();

            rootVisualElement.Query<Button>("ToolButton").ForEach(btn =>
            {
                if (seen.Add(btn) && IsEffectivelyVisible(btn) && btn.enabledSelf)
                {
                    var wrapper = FindButtonWrapper(btn);
                    results.Add(new ButtonInfo(wrapper, btn, ExtractLabel(wrapper)));
                }
            });

            rootVisualElement.Query<Button>("ToolGroupButton").ForEach(btn =>
            {
                if (seen.Add(btn) && IsEffectivelyVisible(btn) && btn.enabledSelf)
                {
                    var wrapper = FindButtonWrapper(btn);
                    results.Add(new ButtonInfo(wrapper, btn, ExtractLabel(wrapper)));
                }
            });

            CollectFallbackButtons(rootVisualElement, results, seen);

            if (!_loggedStructure)
            {
                _loggedStructure = true;
                Debug.Log($"[SequencedKeys] Scanner first scan: {results.Count} buttons.");
                foreach (var r in results)
                {
                    Debug.Log($"[SequencedKeys]   btn: label='{r.Label}', " +
                              $"root.name='{r.Root.name}', " +
                              $"clickable.name='{r.ClickableButton.name}', " +
                              $"classes='{GetClasses(r.Root)}'");
                }
            }

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
                    current.ClassListContains("tool-group-button"))
                    return current;
                if (current.parent != null && current.parent.childCount > 15)
                    return current;
                current = current.parent;
                depth++;
            }
            return button.parent ?? button;
        }

        private void CollectFallbackButtons(VisualElement element, List<ButtonInfo> results,
            HashSet<Button> seen)
        {
            if (element == null || element.resolvedStyle.display == DisplayStyle.None)
                return;

            if (element is Button btn && seen.Add(btn) && IsToolbarButton(btn))
            {
                if (IsEffectivelyVisible(btn) && btn.enabledSelf)
                    results.Add(new ButtonInfo(btn, btn, ExtractLabel(btn)));
                return;
            }

            for (int i = 0; i < element.childCount; i++)
                CollectFallbackButtons(element[i], results, seen);
        }

        private bool IsToolbarButton(Button button)
        {
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
            if (!string.IsNullOrEmpty(element.tooltip))
                return element.tooltip;

            var label = element.Q<Label>();
            if (label != null && !string.IsNullOrEmpty(label.text))
                return label.text;

            var textEl = element.Q<TextElement>();
            if (textEl != null && !string.IsNullOrEmpty(textEl.text))
                return textEl.text;

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

        private static string GetClasses(VisualElement element)
        {
            var classes = new List<string>();
            foreach (var cls in element.GetClasses())
                classes.Add(cls);
            return string.Join(" ", classes);
        }
    }
}
