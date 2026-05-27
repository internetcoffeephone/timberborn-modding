using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SequencedKeys
{
    /// <summary>
    /// Manages the visual overlay that shows key hints on toolbar buttons
    /// during sequenced key navigation.
    /// </summary>
    public class SequencedKeysOverlay
    {
        private readonly VisualElement _rootVisualElement;
        private VisualElement _overlayContainer;
        private readonly List<VisualElement> _activeHints = new List<VisualElement>();
        private VisualElement _statusBar;
        private Label _statusLabel;

        private static readonly Color HintBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        private static readonly Color HintTextColor = new Color(1f, 0.9f, 0.2f, 1f);
        private static readonly Color StatusBackgroundColor = new Color(0.05f, 0.05f, 0.15f, 0.9f);
        private static readonly Color StatusTextColor = new Color(0.8f, 0.9f, 1f, 1f);
        private static readonly Color StatusActiveColor = new Color(0.2f, 0.8f, 0.4f, 1f);

        public SequencedKeysOverlay(VisualElement rootVisualElement)
        {
            _rootVisualElement = rootVisualElement;
        }

        /// <summary>
        /// Shows key hint badges on the given buttons.
        /// Each button in the list gets a label showing which key activates it.
        /// Buttons are grouped: groups[0] gets keys[0], groups[1] gets keys[1], etc.
        /// </summary>
        public void ShowHints(
            List<List<ToolbarScanner.ButtonInfo>> groups,
            string[] keyLabels)
        {
            ClearHints();
            EnsureOverlayContainer();

            for (int groupIndex = 0; groupIndex < groups.Count && groupIndex < keyLabels.Length; groupIndex++)
            {
                var group = groups[groupIndex];
                var keyLabel = keyLabels[groupIndex];

                foreach (var buttonInfo in group)
                {
                    var hint = CreateHintBadge(keyLabel, buttonInfo.Root);
                    _activeHints.Add(hint);
                    _overlayContainer.Add(hint);
                }
            }
        }

        /// <summary>
        /// Shows the status bar indicating sequenced key mode is active.
        /// </summary>
        public void ShowStatusBar(string breadcrumb)
        {
            EnsureOverlayContainer();

            if (_statusBar == null)
            {
                _statusBar = new VisualElement();
                _statusBar.name = "SequencedKeysStatusBar";
                _statusBar.style.position = Position.Absolute;
                _statusBar.style.top = 0;
                _statusBar.style.left = new Length(50, LengthUnit.Percent);
                _statusBar.style.translate =
                    new StyleTranslate(new Translate(new Length(-50, LengthUnit.Percent), 0));
                _statusBar.style.backgroundColor = StatusBackgroundColor;
                _statusBar.style.paddingLeft = 12;
                _statusBar.style.paddingRight = 12;
                _statusBar.style.paddingTop = 6;
                _statusBar.style.paddingBottom = 6;
                _statusBar.style.borderBottomLeftRadius = 6;
                _statusBar.style.borderBottomRightRadius = 6;
                _statusBar.style.flexDirection = FlexDirection.Row;
                _statusBar.style.alignItems = Align.Center;

                var indicator = new VisualElement();
                indicator.style.width = 8;
                indicator.style.height = 8;
                indicator.style.borderTopLeftRadius = 4;
                indicator.style.borderTopRightRadius = 4;
                indicator.style.borderBottomLeftRadius = 4;
                indicator.style.borderBottomRightRadius = 4;
                indicator.style.backgroundColor = StatusActiveColor;
                indicator.style.marginRight = 8;
                _statusBar.Add(indicator);

                _statusLabel = new Label();
                _statusLabel.style.color = StatusTextColor;
                _statusLabel.style.fontSize = 13;
                _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _statusBar.Add(_statusLabel);
            }

            _statusLabel.text = breadcrumb;

            if (_statusBar.parent == null)
                _overlayContainer.Add(_statusBar);
        }

        /// <summary>
        /// Removes all hints and the status bar.
        /// </summary>
        public void Hide()
        {
            ClearHints();
            if (_statusBar?.parent != null)
                _statusBar.RemoveFromHierarchy();
            if (_overlayContainer?.parent != null)
                _overlayContainer.RemoveFromHierarchy();
        }

        private void ClearHints()
        {
            foreach (var hint in _activeHints)
            {
                if (hint.parent != null)
                    hint.RemoveFromHierarchy();
            }
            _activeHints.Clear();
        }

        private void EnsureOverlayContainer()
        {
            if (_overlayContainer?.parent != null)
                return;

            _overlayContainer = new VisualElement();
            _overlayContainer.name = "SequencedKeysOverlay";
            _overlayContainer.pickingMode = PickingMode.Ignore;
            _overlayContainer.style.position = Position.Absolute;
            _overlayContainer.style.left = 0;
            _overlayContainer.style.top = 0;
            _overlayContainer.style.right = 0;
            _overlayContainer.style.bottom = 0;

            _rootVisualElement.Add(_overlayContainer);
        }

        private VisualElement CreateHintBadge(string keyText, VisualElement targetButton)
        {
            var badge = new VisualElement();
            badge.pickingMode = PickingMode.Ignore;
            badge.style.position = Position.Absolute;
            badge.style.backgroundColor = HintBackgroundColor;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            badge.style.paddingLeft = 5;
            badge.style.paddingRight = 5;
            badge.style.paddingTop = 2;
            badge.style.paddingBottom = 2;
            badge.style.borderLeftWidth = 1;
            badge.style.borderRightWidth = 1;
            badge.style.borderTopWidth = 1;
            badge.style.borderBottomWidth = 1;
            badge.style.borderLeftColor = HintTextColor;
            badge.style.borderRightColor = HintTextColor;
            badge.style.borderTopColor = HintTextColor;
            badge.style.borderBottomColor = HintTextColor;

            var label = new Label(keyText.ToUpperInvariant());
            label.pickingMode = PickingMode.Ignore;
            label.style.color = HintTextColor;
            label.style.fontSize = 14;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.Add(label);

            // Position the badge over the target button using a schedule callback
            // to ensure layout is resolved
            badge.RegisterCallback<GeometryChangedEvent>(_ => PositionBadge(badge, targetButton));
            badge.schedule.Execute(() => PositionBadge(badge, targetButton));

            return badge;
        }

        private void PositionBadge(VisualElement badge, VisualElement target)
        {
            if (_overlayContainer == null || target == null)
                return;

            var targetRect = target.worldBound;
            var overlayRect = _overlayContainer.worldBound;

            if (float.IsNaN(targetRect.x) || float.IsNaN(overlayRect.x))
                return;

            // Position at top-left corner of the target button
            badge.style.left = targetRect.x - overlayRect.x;
            badge.style.top = targetRect.y - overlayRect.y;
        }
    }
}
