using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace SourceGit.Views
{
    public partial class CommitSubjectPresenter : TextBlock
    {
        public static readonly StyledProperty<string> SubjectProperty =
            AvaloniaProperty.Register<CommitSubjectPresenter, string>(nameof(Subject));

        public string Subject
        {
            get => GetValue(SubjectProperty);
            set => SetValue(SubjectProperty, value);
        }

        public static readonly StyledProperty<AvaloniaList<Models.IssueTrackerRule>> IssueTrackerRulesProperty =
            AvaloniaProperty.Register<CommitSubjectPresenter, AvaloniaList<Models.IssueTrackerRule>>(nameof(IssueTrackerRules));

        public AvaloniaList<Models.IssueTrackerRule> IssueTrackerRules
        {
            get => GetValue(IssueTrackerRulesProperty);
            set => SetValue(IssueTrackerRulesProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextBlock);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SubjectProperty || change.Property == IssueTrackerRulesProperty)
            {
                Inlines!.Clear();
                _matches = null;
                ClearHoveredIssueLink();

                var subject = Subject;
                if (string.IsNullOrEmpty(subject))
                    return;

                var keywordMatch = REG_KEYWORD_FORMAT1().Match(subject);
                if (!keywordMatch.Success)
                    keywordMatch = REG_KEYWORD_FORMAT2().Match(subject);

                var rules = IssueTrackerRules ?? [];
                var matches = new List<Models.Hyperlink>();
                foreach (var rule in rules)
                    rule.Matches(matches, subject);

                if (matches.Count == 0)
                {
                    if (keywordMatch.Success)
                    {
                        Inlines.Add(new Run(subject.Substring(0, keywordMatch.Length)) { FontWeight = FontWeight.Bold });
                        Inlines.Add(new Run(subject.Substring(keywordMatch.Length)));
                    }
                    else
                    {
                        Inlines.Add(new Run(subject));
                    }
                    return;
                }

                matches.Sort((l, r) => l.Start - r.Start);
                _matches = matches;

                var inlines = new List<Inline>();
                var pos = 0;
                foreach (var match in matches)
                {
                    if (match.Start > pos)
                    {
                        if (keywordMatch.Success && pos < keywordMatch.Length)
                        {
                            if (keywordMatch.Length < match.Start)
                            {
                                inlines.Add(new Run(subject.Substring(pos, keywordMatch.Length - pos)) { FontWeight = FontWeight.Bold });
                                inlines.Add(new Run(subject.Substring(keywordMatch.Length, match.Start - keywordMatch.Length)));
                            }
                            else
                            {
                                inlines.Add(new Run(subject.Substring(pos, match.Start - pos)) { FontWeight = FontWeight.Bold });
                            }
                        }
                        else
                        {
                            inlines.Add(new Run(subject.Substring(pos, match.Start - pos)));
                        }
                    }

                    var link = new Run(subject.Substring(match.Start, match.Length));
                    link.Classes.Add("issue_link");
                    inlines.Add(link);

                    pos = match.Start + match.Length;
                }

                if (pos < subject.Length)
                {
                    if (keywordMatch.Success && pos < keywordMatch.Length)
                    {
                        inlines.Add(new Run(subject.Substring(pos, keywordMatch.Length - pos)) { FontWeight = FontWeight.Bold });
                        inlines.Add(new Run(subject.Substring(keywordMatch.Length)));
                    }
                    else
                    {
                        inlines.Add(new Run(subject.Substring(pos)));
                    }
                }

                Inlines.AddRange(inlines);
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_matches != null)
            {
                var point = e.GetPosition(this) - new Point(Padding.Left, Padding.Top);
                var x = Math.Min(Math.Max(point.X, 0), Math.Max(TextLayout.WidthIncludingTrailingWhitespace, 0));
                var y = Math.Min(Math.Max(point.Y, 0), Math.Max(TextLayout.Height, 0));
                point = new Point(x, y);

                var textPosition = TextLayout.HitTestPoint(point).TextPosition;
                foreach (var match in _matches)
                {
                    if (!match.Intersect(textPosition, 1))
                        continue;

                    if (match == _lastHover)
                        return;

                    _lastHover = match;
                    SetCurrentValue(CursorProperty, Cursor.Parse("Hand"));
                    ToolTip.SetTip(this, match.Link);
                    ToolTip.SetIsOpen(this, true);
                    e.Handled = true;
                    return;
                }

                ClearHoveredIssueLink();
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (_lastHover != null)
                Native.OS.OpenBrowser(_lastHover.Link);
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
                SetCurrentValue(CursorProperty, Cursor.Parse("Arrow"));
                _lastHover = null;
            }
        }

        [GeneratedRegex(@"^\[[\w\s]+\]")]
        private static partial Regex REG_KEYWORD_FORMAT1();

        [GeneratedRegex(@"^\S+([\<\(][\w\s_\-\*,]+[\>\)])?\!?\s?:\s")]
        private static partial Regex REG_KEYWORD_FORMAT2();

        private List<Models.Hyperlink> _matches = null;
        private Models.Hyperlink _lastHover = null;
    }
}
