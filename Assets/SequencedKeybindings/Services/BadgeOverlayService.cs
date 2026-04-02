using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SequencedKeybindings.Services
{
    /// <summary>
    /// Manages visual key-hint badges overlaid on toolbar buttons
    /// when sequenced keybinding mode is active.
    ///
    /// Each badge shows which key (Q/W/E/R/...) activates which
    /// button or group of buttons. A page indicator shows when
    /// multiple pages are available.
    /// </summary>
    public class BadgeOverlayService
    {
        const string BadgeClassName = "sequenced-key-badge";
        const string PageIndicatorClassName = "sequenced-key-page-indicator";

        static readonly Color BadgeBgColor = new Color(0.08f, 0.08f, 0.08f, 0.88f);
        static readonly Color BadgeTextColor = new Color(1f, 0.92f, 0.23f, 1f);
        static readonly Color PageIndicatorBgColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        static readonly Color PageIndicatorTextColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        readonly List<VisualElement> _activeBadges = new List<VisualElement>();
        VisualElement _pageIndicator;

        /// <summary>
        /// Shows key-hint badges on the given toolbar buttons.
        /// </summary>
        /// <param name="buttons">The buttons on the current page.</param>
        /// <param name="keyLabels">Display labels for each key (e.g. "Q","W","E","R").</param>
        /// <param name="hasNextPage">Whether there are more pages (shows Tab hint).</param>
        /// <param name="currentPage">Zero-based current page index.</param>
        /// <param name="totalPages">Total number of pages.</param>
        public void ShowBadges(
            List<ToolbarEntry> buttons,
            List<string> keyLabels,
            bool hasNextPage,
            int currentPage,
            int totalPages)
        {
            HideAll();

            for (int i = 0; i < buttons.Count && i < keyLabels.Count; i++)
            {
                var badge = CreateKeyBadge(keyLabels[i]);
                var target = buttons[i].Element;
                target.Add(badge);
                _activeBadges.Add(badge);
            }

            // Show page indicator if there are multiple pages
            if (totalPages > 1 && buttons.Count > 0)
            {
                string pageText = $"{currentPage + 1}/{totalPages}";
                if (hasNextPage)
                    pageText += " [Tab]";
                _pageIndicator = CreatePageIndicator(pageText);

                // Attach to the parent of the first button (the toolbar panel)
                var parent = buttons[0].Element.parent;
                if (parent != null)
                {
                    parent.Add(_pageIndicator);
                }
            }
        }

        /// <summary>
        /// Removes all badges and indicators from the UI.
        /// </summary>
        public void HideAll()
        {
            foreach (var badge in _activeBadges)
            {
                badge.parent?.Remove(badge);
            }
            _activeBadges.Clear();

            if (_pageIndicator != null)
            {
                _pageIndicator.parent?.Remove(_pageIndicator);
                _pageIndicator = null;
            }
        }

        static VisualElement CreateKeyBadge(string keyLabel)
        {
            var container = new VisualElement();
            container.AddToClassList(BadgeClassName);
            container.pickingMode = PickingMode.Ignore;

            // Position in upper-left corner of the parent button
            container.style.position = Position.Absolute;
            container.style.top = 2;
            container.style.left = 2;
            container.style.minWidth = 20;
            container.style.minHeight = 20;
            container.style.paddingLeft = 3;
            container.style.paddingRight = 3;
            container.style.paddingTop = 1;
            container.style.paddingBottom = 1;

            container.style.backgroundColor = new StyleColor(BadgeBgColor);
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;

            // Subtle border for visibility
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopColor = new StyleColor(BadgeTextColor * 0.6f);
            container.style.borderBottomColor = new StyleColor(BadgeTextColor * 0.6f);
            container.style.borderLeftColor = new StyleColor(BadgeTextColor * 0.6f);
            container.style.borderRightColor = new StyleColor(BadgeTextColor * 0.6f);

            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;

            var label = new Label(keyLabel);
            label.pickingMode = PickingMode.Ignore;
            label.style.color = new StyleColor(BadgeTextColor);
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.paddingTop = 0;
            label.style.paddingBottom = 0;
            label.style.paddingLeft = 0;
            label.style.paddingRight = 0;
            label.style.marginTop = 0;
            label.style.marginBottom = 0;
            label.style.marginLeft = 0;
            label.style.marginRight = 0;

            container.Add(label);
            return container;
        }

        static VisualElement CreatePageIndicator(string text)
        {
            var container = new VisualElement();
            container.AddToClassList(PageIndicatorClassName);
            container.pickingMode = PickingMode.Ignore;

            // Position above the toolbar
            container.style.position = Position.Absolute;
            container.style.bottom = new StyleLength(StyleKeyword.Auto);
            container.style.top = -28;
            container.style.left = 0;
            container.style.right = 0;
            container.style.height = 24;

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;

            container.style.backgroundColor = new StyleColor(PageIndicatorBgColor);
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;

            var label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            label.style.color = new StyleColor(PageIndicatorTextColor);
            label.style.fontSize = 11;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.paddingLeft = 8;
            label.style.paddingRight = 8;

            container.Add(label);
            return container;
        }
    }
}
