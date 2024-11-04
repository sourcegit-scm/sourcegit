using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class CommitMessagePresenter : SelectableTextBlock
    {
        [GeneratedRegex(@"\b([0-9a-fA-F]{8,40})\b")]
        private static partial Regex REG_SHA_FORMAT();

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
                Inlines!.Clear();
                _matches = null;
                ClearHoveredIssueLink();

                var message = Message;
                if (string.IsNullOrEmpty(message))
                    return;

                var matches = new List<Models.Hyperlink>();
                if (IssueTrackerRules is { Count: > 0 } rules)
                {
                    foreach (var rule in rules)
                        rule.Matches(matches, message);
                }

                var shas = REG_SHA_FORMAT().Matches(message);
                for (int i = 0; i < shas.Count; i++)
                {
                    var sha = shas[i];
                    if (!sha.Success)
                        continue;

                    var start = sha.Index;
                    var len = sha.Length;
                    var intersect = false;
                    foreach (var match in matches)
                    {
                        if (match.Intersect(start, len))
                        {
                            intersect = true;
                            break;
                        }
                    }

                    if (!intersect)
                        matches.Add(new Models.Hyperlink(start, len, sha.Groups[1].Value, true));
                }

                if (matches.Count == 0)
                {
                    Inlines.Add(new Run(message));
                    return;
                }

                matches.Sort((l, r) => l.Start - r.Start);
                _matches = matches;

                var inlines = new List<Inline>();
                var pos = 0;
                foreach (var match in matches)
                {
                    if (match.Start > pos)
                        inlines.Add(new Run(message.Substring(pos, match.Start - pos)));

                    var link = new Run(message.Substring(match.Start, match.Length));
                    link.Classes.Add(match.IsCommitSHA ? "commit_link" : "issue_link");
                    inlines.Add(link);

                    pos = match.Start + match.Length;
                }

                if (pos < message.Length)
                    inlines.Add(new Run(message.Substring(pos)));

                Inlines.AddRange(inlines);
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (e.Pointer.Captured == this)
            {
                var relativeSelfY = e.GetPosition(this).Y;
                if (relativeSelfY <= 0 || relativeSelfY > Bounds.Height)
                    return;

                var scrollViewer = this.FindAncestorOfType<ScrollViewer>();
                if (scrollViewer != null)
                {
                    var relativeY = e.GetPosition(scrollViewer).Y;
                    if (relativeY <= 8)
                        scrollViewer.LineUp();
                    else if (relativeY >= scrollViewer.Bounds.Height - 8)
                        scrollViewer.LineDown();
                }
            }
            else if (_matches != null)
            {
                var point = e.GetPosition(this) - new Point(Padding.Left, Padding.Top);
                var x = Math.Min(Math.Max(point.X, 0), Math.Max(TextLayout.WidthIncludingTrailingWhitespace, 0));
                var y = Math.Min(Math.Max(point.Y, 0), Math.Max(TextLayout.Height, 0));
                point = new Point(x, y);

                var pos = TextLayout.HitTestPoint(point).TextPosition;
                foreach (var match in _matches)
                {
                    if (!match.Intersect(pos, 1))
                        continue;

                    if (match == _lastHover)
                        return;

                    SetCurrentValue(CursorProperty, Cursor.Parse("Hand"));

                    _lastHover = match;
                    if (!_lastHover.IsCommitSHA)
                    {
                        ToolTip.SetTip(this, match.Link);
                        ToolTip.SetIsOpen(this, true);
                    }

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

                if (_lastHover.IsCommitSHA)
                {
                    var parentView = this.FindAncestorOfType<CommitBaseInfo>();
                    if (parentView is { DataContext: ViewModels.CommitDetail detail })
                        detail.NavigateTo(_lastHover.Link);
                }
                else
                {
                    var point = e.GetCurrentPoint(this);
                    var link = _lastHover.Link;

                    if (point.Properties.IsLeftButtonPressed)
                    {
                        Native.OS.OpenBrowser(link);
                    }
                    else if (point.Properties.IsRightButtonPressed)
                    {
                        var open = new MenuItem();
                        open.Header = App.Text("IssueLinkCM.OpenInBrowser");
                        open.Icon = App.CreateMenuIcon("Icons.OpenWith");
                        open.Click += (_, ev) =>
                        {
                            ev.Handled = true;

                            var parentView = this.FindAncestorOfType<CommitBaseInfo>();
                            if (parentView is { DataContext: ViewModels.CommitDetail detail })
                                detail.NavigateTo(link);
                        };

                        var copy = new MenuItem();
                        copy.Header = App.Text("IssueLinkCM.CopyLink");
                        copy.Icon = App.CreateMenuIcon("Icons.Copy");
                        copy.Click += (_, ev) =>
                        {
                            App.CopyText(link);
                            ev.Handled = true;
                        };

                        var menu = new ContextMenu();
                        menu.Items.Add(open);
                        menu.Items.Add(copy);
                        menu.Open(this);
                    }
                }

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
                _lastHover = null;
            }
        }

        private List<Models.Hyperlink> _matches = null;
        private Models.Hyperlink _lastHover = null;
    }
}
