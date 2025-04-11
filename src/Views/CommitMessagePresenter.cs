﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class CommitMessagePresenter : SelectableTextBlock
    {
        public static readonly StyledProperty<Models.CommitFullMessage> FullMessageProperty =
            AvaloniaProperty.Register<CommitMessagePresenter, Models.CommitFullMessage>(nameof(FullMessage));

        public Models.CommitFullMessage FullMessage
        {
            get => GetValue(FullMessageProperty);
            set => SetValue(FullMessageProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == FullMessageProperty)
            {
                Inlines!.Clear();
                _inlineCommits.Clear();
                _lastHover = null;
                ClearHoveredIssueLink();

                var message = FullMessage?.Message;
                if (string.IsNullOrEmpty(message))
                    return;

                var links = FullMessage?.Links;
                if (links == null || links.Count == 0)
                {
                    Inlines.Add(new Run(message));
                    return;
                }

                var inlines = new List<Inline>();
                var pos = 0;
                foreach (var link in links)
                {
                    if (link.Start > pos)
                        inlines.Add(new Run(message.Substring(pos, link.Start - pos)));

                    var run = new Run(message.Substring(link.Start, link.Length));
                    run.Classes.Add(link.IsCommitSHA ? "commit_link" : "issue_link");
                    inlines.Add(run);

                    pos = link.Start + link.Length;
                }

                if (pos < message.Length)
                    inlines.Add(new Run(message.Substring(pos)));

                Inlines.AddRange(inlines);
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (Equals(e.Pointer.Captured, this))
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
            else if (FullMessage is { Links: { Count: > 0 } links })
            {
                var point = e.GetPosition(this) - new Point(Padding.Left, Padding.Top);
                var x = Math.Min(Math.Max(point.X, 0), Math.Max(TextLayout.WidthIncludingTrailingWhitespace, 0));
                var y = Math.Min(Math.Max(point.Y, 0), Math.Max(TextLayout.Height, 0));
                point = new Point(x, y);

                var pos = TextLayout.HitTestPoint(point).TextPosition;
                foreach (var link in links)
                {
                    if (!link.Intersect(pos, 1))
                        continue;

                    if (link == _lastHover)
                        return;

                    SetCurrentValue(CursorProperty, Cursor.Parse("Hand"));

                    _lastHover = link;
                    if (!link.IsCommitSHA)
                    {
                        ToolTip.SetTip(this, link.Link);
                        ToolTip.SetIsOpen(this, true);
                    }
                    else
                    {
                        ProcessHoverCommitLink(link);
                    }

                    return;
                }

                ClearHoveredIssueLink();
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);

            if (_lastHover != null)
            {
                var link = _lastHover.Link;
                e.Pointer.Capture(null);

                if (_lastHover.IsCommitSHA)
                {
                    var parentView = this.FindAncestorOfType<CommitBaseInfo>();
                    if (parentView is { DataContext: ViewModels.CommitDetail detail })
                    {
                        if (point.Properties.IsLeftButtonPressed)
                        {
                            detail.NavigateTo(_lastHover.Link);
                        }
                        else if (point.Properties.IsRightButtonPressed)
                        {
                            var open = new MenuItem();
                            open.Header = App.Text("SHALinkCM.NavigateTo");
                            open.Icon = App.CreateMenuIcon("Icons.Commit");
                            open.Click += (_, ev) =>
                            {
                                detail.NavigateTo(link);
                                ev.Handled = true;
                            };

                            var copy = new MenuItem();
                            copy.Header = App.Text("SHALinkCM.CopySHA");
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
                }
                else
                {
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
                            Native.OS.OpenBrowser(link);
                            ev.Handled = true;
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

            if (point.Properties.IsLeftButtonPressed && e.ClickCount == 3)
            {
                var text = Inlines?.Text;
                if (string.IsNullOrEmpty(text))
                {
                    e.Handled = true;
                    return;
                }

                var position = e.GetPosition(this) - new Point(Padding.Left, Padding.Top);
                var x = Math.Min(Math.Max(position.X, 0), Math.Max(TextLayout.WidthIncludingTrailingWhitespace, 0));
                var y = Math.Min(Math.Max(position.Y, 0), Math.Max(TextLayout.Height, 0));
                position = new Point(x, y);

                var textPos = TextLayout.HitTestPoint(position).TextPosition;
                var lineStart = 0;
                var lineEnd = text.IndexOf('\n', lineStart);
                if (lineEnd <= 0)
                {
                    lineEnd = text.Length;
                }
                else
                {
                    while (lineEnd < textPos)
                    {
                        lineStart = lineEnd + 1;
                        lineEnd = text.IndexOf('\n', lineStart);
                        if (lineEnd == -1)
                        {
                            lineEnd = text.Length;
                            break;
                        }
                    }
                }

                SetCurrentValue(SelectionStartProperty, lineStart);
                SetCurrentValue(SelectionEndProperty, lineEnd);

                e.Pointer.Capture(this);
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

        private void ProcessHoverCommitLink(Models.Hyperlink link)
        {
            var sha = link.Link;

            // If we have already queried this SHA, just use it.
            if (_inlineCommits.TryGetValue(sha, out var exist))
            {
                if (exist != null)
                {
                    ToolTip.SetTip(this, exist);
                    ToolTip.SetIsOpen(this, true);
                }

                return;
            }

            var parentView = this.FindAncestorOfType<CommitBaseInfo>();
            if (parentView is { DataContext: ViewModels.CommitDetail detail })
            {
                // Record the SHA of current viewing commit in the CommitDetail panel to determine if it is changed after
                // asynchronous queries.
                var lastDetailCommit = detail.Commit.SHA;
                Task.Run(() =>
                {
                    var c = detail.GetParent(sha);
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        // Make sure the DataContext of CommitBaseInfo is not changed.
                        var currentParent = this.FindAncestorOfType<CommitBaseInfo>();
                        if (currentParent is { DataContext: ViewModels.CommitDetail currentDetail } &&
                            currentDetail.Commit.SHA == lastDetailCommit)
                        {
                            if (!_inlineCommits.ContainsKey(sha))
                                _inlineCommits.Add(sha, c);

                            // Make sure user still hovers the target SHA.
                            if (_lastHover == link && c != null)
                            {
                                ToolTip.SetTip(this, c);
                                ToolTip.SetIsOpen(this, true);
                            }
                        }
                    });
                });
            }
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

        private Models.Hyperlink _lastHover = null;
        private Dictionary<string, Models.Commit> _inlineCommits = new();
    }
}
