using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SequencedKeys
{
    public class SequencedKeysOverlay
    {
        private readonly VisualElement _rootVisualElement;
        private VisualElement _overlayContainer;
        private readonly List<VisualElement> _groupBadges = new List<VisualElement>();
        private VisualElement _statusBar;
        private Label _statusLabel;

        private static readonly Color HintBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        private static readonly Color HintTextColor = new Color(1f, 0.9f, 0.2f, 1f);
        private static readonly Color HintHighlightBg = new Color(0.15f, 0.45f, 0.1f, 0.92f);
        private static readonly Color HintHighlightBorder = new Color(0.3f, 0.9f, 0.2f, 1f);
        private static readonly Color StatusBackgroundColor = new Color(0.05f, 0.05f, 0.15f, 0.9f);
        private static readonly Color StatusTextColor = new Color(0.8f, 0.9f, 1f, 1f);
        private static readonly Color StatusActiveColor = new Color(0.2f, 0.8f, 0.4f, 1f);

        public SequencedKeysOverlay(VisualElement rootVisualElement)
        {
            _rootVisualElement = rootVisualElement;
        }

        public void ShowHints(
            List<List<ToolbarScanner.ButtonInfo>> groups,
            string[] keyLabels)
        {
            ClearHints();
            EnsureOverlayContainer();

            for (int groupIndex = 0; groupIndex < groups.Count && groupIndex < keyLabels.Length; groupIndex++)
            {
                var group = groups[groupIndex];
                if (group.Count == 0) continue;

                var keyLabel = keyLabels[groupIndex];
                var badge = CreateGroupBadge(keyLabel, group);
                _groupBadges.Add(badge);
                _overlayContainer.Add(badge);
            }
        }

        public void HighlightGroup(int groupIndex)
        {
            ClearHighlight();

            if (groupIndex < 0 || groupIndex >= _groupBadges.Count)
                return;

            var badge = _groupBadges[groupIndex];
            badge.style.backgroundColor = HintHighlightBg;
            badge.style.borderLeftColor = HintHighlightBorder;
            badge.style.borderRightColor = HintHighlightBorder;
            badge.style.borderTopColor = HintHighlightBorder;
            badge.style.borderBottomColor = HintHighlightBorder;
        }

        public void ClearHighlight()
        {
            foreach (var badge in _groupBadges)
            {
                badge.style.backgroundColor = HintBackgroundColor;
                badge.style.borderLeftColor = HintTextColor;
                badge.style.borderRightColor = HintTextColor;
                badge.style.borderTopColor = HintTextColor;
                badge.style.borderBottomColor = HintTextColor;
            }
        }

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
            foreach (var badge in _groupBadges)
            {
                if (badge.parent != null)
                    badge.RemoveFromHierarchy();
            }
            _groupBadges.Clear();
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

        private VisualElement CreateGroupBadge(string keyText, List<ToolbarScanner.ButtonInfo> buttons)
        {
            var badge = new VisualElement();
            badge.pickingMode = PickingMode.Ignore;
            badge.style.position = Position.Absolute;
            badge.style.backgroundColor = HintBackgroundColor;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            badge.style.borderLeftWidth = 1;
            badge.style.borderRightWidth = 1;
            badge.style.borderTopWidth = 1;
            badge.style.borderBottomWidth = 1;
            badge.style.borderLeftColor = HintTextColor;
            badge.style.borderRightColor = HintTextColor;
            badge.style.borderTopColor = HintTextColor;
            badge.style.borderBottomColor = HintTextColor;
            badge.style.alignItems = Align.Center;
            badge.style.justifyContent = Justify.Center;
            badge.style.overflow = Overflow.Hidden;

            var label = new Label(keyText.ToUpperInvariant());
            label.pickingMode = PickingMode.Ignore;
            label.style.color = HintTextColor;
            label.style.fontSize = 16;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.Add(label);

            badge.RegisterCallback<GeometryChangedEvent>(_ => PositionGroupBadge(badge, buttons));
            badge.schedule.Execute(() => PositionGroupBadge(badge, buttons));

            return badge;
        }

        private void PositionGroupBadge(VisualElement badge, List<ToolbarScanner.ButtonInfo> buttons)
        {
            if (_overlayContainer == null || buttons.Count == 0)
                return;

            var overlayRect = _overlayContainer.worldBound;
            if (float.IsNaN(overlayRect.x))
                return;

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            foreach (var btn in buttons)
            {
                var rect = btn.Root.worldBound;
                if (float.IsNaN(rect.x))
                    continue;
                if (rect.x < minX) minX = rect.x;
                if (rect.y < minY) minY = rect.y;
                if (rect.xMax > maxX) maxX = rect.xMax;
                if (rect.yMax > maxY) maxY = rect.yMax;
            }

            if (minX == float.MaxValue)
                return;

            badge.style.left = minX - overlayRect.x;
            badge.style.top = minY - overlayRect.y;
            badge.style.width = maxX - minX;
            badge.style.height = maxY - minY;
        }
    }
}
