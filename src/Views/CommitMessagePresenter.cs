using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Utilities;

namespace SourceGit.Views
{
    public class CommitMessagePresenter : SelectableTextBlock
    {
        public static readonly StyledProperty<string> MessageProperty =
            AvaloniaProperty.Register<CommitMessagePresenter, string>(nameof(Message));

        public string Message
        {
            get => GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public static readonly StyledProperty<AvaloniaList<Models.IssueTrackerRule>> IssueTrackerRulesProperty =
            AvaloniaProperty.Register<CommitMessagePresenter, AvaloniaList<Models.IssueTrackerRule>>(nameof(IssueTrackerRules));

        public AvaloniaList<Models.IssueTrackerRule> IssueTrackerRules
        {
            get => GetValue(IssueTrackerRulesProperty);
            set => SetValue(IssueTrackerRulesProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == MessageProperty || change.Property == IssueTrackerRulesProperty)
            {
                Inlines.Clear();
                _matches = null;
                ClearHoveredIssueLink();

                var message = Message;
                if (string.IsNullOrEmpty(message))
                    return;

                var rules = IssueTrackerRules;
                if (rules == null || rules.Count == 0)
                {
                    Inlines.Add(new Run(message));
                    return;
                }

                var matches = new List<Models.IssueTrackerMatch>();
                foreach (var rule in rules)
                    rule.Matches(matches, message);

                if (matches.Count == 0)
                {
                    Inlines.Add(new Run(message));
                    return;
                }

                matches.Sort((l, r) => l.Start - r.Start);
                _matches = matches;

                int pos = 0;
                foreach (var match in matches)
                {
                    if (match.Start > pos)
                        Inlines.Add(new Run(message.Substring(pos, match.Start - pos)));

                    match.Link = new Run(message.Substring(match.Start, match.Length));
                    match.Link.Classes.Add("issue_link");
                    Inlines.Add(match.Link);

                    pos = match.Start + match.Length;
                }

                if (pos < message.Length)
                    Inlines.Add(new Run(message.Substring(pos)));
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (e.Pointer.Captured == null && _matches != null)
            {
                var padding = Padding;
                var point = e.GetPosition(this) - new Point(padding.Left, padding.Top);
                point = new Point(
                    MathUtilities.Clamp(point.X, 0, Math.Max(TextLayout.WidthIncludingTrailingWhitespace, 0)),
                    MathUtilities.Clamp(point.Y, 0, Math.Max(TextLayout.Height, 0)));

                var pos = TextLayout.HitTestPoint(point).TextPosition;
                foreach (var match in _matches)
                {
                    if (!match.Intersect(pos, 1))
                        continue;

                    if (match == _lastHover)
                        return;

                    _lastHover = match;
                    //_lastHover.Link.Classes.Add("issue_link_hovered");

                    SetCurrentValue(CursorProperty, Cursor.Parse("Hand"));
                    ToolTip.SetTip(this, match.URL);
                    ToolTip.SetIsOpen(this, true);
                    return;
                }

                ClearHoveredIssueLink();
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            if (_lastHover != null)
            {
                e.Pointer.Capture(null);
                Native.OS.OpenBrowser(_lastHover.URL);
                e.Handled = true;
                return;
            }

            base.OnPointerPressed(e);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            ClearHoveredIssueLink();
        }

        private void ClearHoveredIssueLink()
        {
            if (_lastHover != null)
            {
                ToolTip.SetTip(this, null);
                SetCurrentValue(CursorProperty, Cursor.Parse("IBeam"));
                //_lastHover.Link.Classes.Remove("issue_link_hovered");
                _lastHover = null;
            }
        }

        private List<Models.IssueTrackerMatch> _matches = null;
        private Models.IssueTrackerMatch _lastHover = null;
    }
}
