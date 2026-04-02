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
        }

        public void Load()
        {
            // Create a small invisible anchor element and add it to the UI
            // so we can walk up the tree to find the root VisualElement.
            var anchor = new VisualElement();
            anchor.name = "SequencedKeysAnchor";
            anchor.pickingMode = PickingMode.Ignore;
            anchor.style.position = Position.Absolute;
            anchor.style.width = 0;
            anchor.style.height = 0;
            _uiLayout.AddBottomRight(anchor, 9999);

            // Schedule finding the root after the element is attached
            anchor.schedule.Execute(() =>
            {
                var root = FindPanelRoot(anchor);
                if (root != null)
                {
                    _service.SetUIRoot(root);
                }
                else
                {
                    Debug.LogWarning("[SequencedKeys] Could not find UI root element.");
                }
            });
        }

        /// <summary>
        /// Walks up the visual element tree to find the topmost panel root.
        /// </summary>
        private VisualElement FindPanelRoot(VisualElement element)
        {
            var current = element;
            VisualElement root = null;
            while (current != null)
            {
                root = current;
                current = current.parent;
            }
            return root;
        }
    }
}
