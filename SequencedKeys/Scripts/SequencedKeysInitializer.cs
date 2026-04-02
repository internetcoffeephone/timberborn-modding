using Timberborn.CoreUI;
using Timberborn.SingletonSystem;
using Timberborn.UILayoutSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace SequencedKeys
{
    /// <summary>
    /// Initializes the SequencedKeys system by finding the root UI element
    /// and connecting it to the service. Also places a tiny anchor element
    /// in the game UI via UILayout so we can traverse up to find the real root.
    /// </summary>
    public class SequencedKeysInitializer : ILoadableSingleton
    {
        private readonly SequencedKeysService _service;
        private readonly UILayout _uiLayout;

        public SequencedKeysInitializer(
            SequencedKeysService service,
            UILayout uiLayout)
        {
            _service = service;
            _uiLayout = uiLayout;
            Debug.Log("[SequencedKeys] Initializer constructor called.");
        }

        public void Load()
        {
            Debug.Log("[SequencedKeys] Initializer.Load() called — adding anchor to UI.");

            // Create a small invisible anchor element and add it to the UI
            // so we can walk up the tree to find the root VisualElement.
            var anchor = new VisualElement();
            anchor.name = "SequencedKeysAnchor";
            anchor.pickingMode = PickingMode.Ignore;
            anchor.style.position = Position.Absolute;
            anchor.style.width = 0;
            anchor.style.height = 0;

            try
            {
                _uiLayout.AddBottomRight(anchor, 9999);
                Debug.Log("[SequencedKeys] Anchor added to UILayout.AddBottomRight().");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SequencedKeys] Failed to add anchor to UILayout: {ex}");
                return;
            }

            // Schedule finding the root after the element is attached
            anchor.schedule.Execute(() =>
            {
                Debug.Log("[SequencedKeys] Scheduled callback executing — walking up to find root.");
                var root = FindPanelRoot(anchor);
                if (root != null)
                {
                    Debug.Log($"[SequencedKeys] Found root: name='{root.name}', " +
                              $"type={root.GetType().Name}, childCount={root.childCount}");

                    // Log the first few levels of the tree for debugging
                    LogChildren(root, 0, 2);

                    _service.SetUIRoot(root);
                    Debug.Log("[SequencedKeys] UI root set on service successfully.");
                }
                else
                {
                    Debug.LogWarning("[SequencedKeys] Could not find UI root element — " +
                                     "anchor may not be attached to a panel.");
                }
            });
        }

        private VisualElement FindPanelRoot(VisualElement element)
        {
            var current = element;
            VisualElement root = null;
            int depth = 0;
            while (current != null)
            {
                root = current;
                current = current.parent;
                depth++;
            }
            Debug.Log($"[SequencedKeys] Walked {depth} levels up from anchor to root.");
            return root;
        }

        private void LogChildren(VisualElement element, int depth, int maxDepth)
        {
            if (depth > maxDepth || element == null)
                return;

            var indent = new string(' ', depth * 2);
            for (int i = 0; i < element.childCount && i < 10; i++)
            {
                var child = element[i];
                Debug.Log($"[SequencedKeys] {indent}child[{i}]: " +
                          $"name='{child.name}', type={child.GetType().Name}, " +
                          $"visible={child.resolvedStyle.display != DisplayStyle.None}, " +
                          $"children={child.childCount}");
                LogChildren(child, depth + 1, maxDepth);
            }
            if (element.childCount > 10)
            {
                Debug.Log($"[SequencedKeys] {indent}... and {element.childCount - 10} more children");
            }
        }
    }
}
