using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class LayoutableGrid : Grid
    {
        public static readonly StyledProperty<bool> UseHorizontalProperty =
            AvaloniaProperty.Register<LayoutableGrid, bool>(nameof(UseHorizontal));

        public bool UseHorizontal
        {
            get => GetValue(UseHorizontalProperty);
            set => SetValue(UseHorizontalProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(Grid);

        static LayoutableGrid()
        {
            UseHorizontalProperty.Changed.AddClassHandler<LayoutableGrid>((o, _) => o.RefreshLayout());
        }

        public override void ApplyTemplate()
        {
            base.ApplyTemplate();
            RefreshLayout();
        }

        private void RefreshLayout()
        {
            if (UseHorizontal)
            {
                var rowSpan = RowDefinitions.Count;
                for (int i = 0; i < Children.Count; i++)
                {
                    var child = Children[i];
                    child.SetValue(RowProperty, 0);
                    child.SetValue(RowSpanProperty, rowSpan);
                    child.SetValue(ColumnProperty, i);
                    child.SetValue(ColumnSpanProperty, 1);

                    if (child is GridSplitter splitter)
                        splitter.BorderThickness = new Thickness(1, 0, 0, 0);
                }
            }
            else
            {
                var colSpan = ColumnDefinitions.Count;
                for (int i = 0; i < Children.Count; i++)
                {
                    var child = Children[i];
                    child.SetValue(RowProperty, i);
                    child.SetValue(RowSpanProperty, 1);
                    child.SetValue(ColumnProperty, 0);
                    child.SetValue(ColumnSpanProperty, colSpan);

                    if (child is GridSplitter splitter)
                        splitter.BorderThickness = new Thickness(0, 1, 0, 0);
                }
            }
        }
    }

    public class CommitStatusIndicator : Control
    {
        public static readonly StyledProperty<Models.Branch> CurrentBranchProperty =
            AvaloniaProperty.Register<CommitStatusIndicator, Models.Branch>(nameof(CurrentBranch));

        public Models.Branch CurrentBranch
        {
            get => GetValue(CurrentBranchProperty);
            set => SetValue(CurrentBranchProperty, value);
        }

        public static readonly StyledProperty<IBrush> AheadBrushProperty =
            AvaloniaProperty.Register<CommitStatusIndicator, IBrush>(nameof(AheadBrush));

        public IBrush AheadBrush
        {
            get => GetValue(AheadBrushProperty);
            set => SetValue(AheadBrushProperty, value);
        }

        public static readonly StyledProperty<IBrush> BehindBrushProperty =
            AvaloniaProperty.Register<CommitStatusIndicator, IBrush>(nameof(BehindBrush));

        public IBrush BehindBrush
        {
            get => GetValue(BehindBrushProperty);
            set => SetValue(BehindBrushProperty, value);
        }

        enum Status
        {
            Normal,
            Ahead,
            Behind,
        }

        public override void Render(DrawingContext context)
        {
            if (_status == Status.Normal)
                return;

            context.DrawEllipse(_status == Status.Ahead ? AheadBrush : BehindBrush, null, new Rect(0, 0, 5, 5));
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (DataContext is Models.Commit commit && CurrentBranch is not null)
            {
                var sha = commit.SHA;
                var track = CurrentBranch.TrackStatus;

                if (track.Ahead.Contains(sha))
                    _status = Status.Ahead;
                else if (track.Behind.Contains(sha))
                    _status = Status.Behind;
                else
                    _status = Status.Normal;
            }
            else
            {
                _status = Status.Normal;
            }

            return _status == Status.Normal ? new Size(0, 0) : new Size(9, 5);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            InvalidateMeasure();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == CurrentBranchProperty)
                InvalidateMeasure();
        }

        private Status _status = Status.Normal;
    }

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

    public class CommitTimeTextBlock : TextBlock
    {
        public static readonly StyledProperty<bool> ShowAsDateTimeProperty =
            AvaloniaProperty.Register<CommitTimeTextBlock, bool>(nameof(ShowAsDateTime), true);

        public bool ShowAsDateTime
        {
            get => GetValue(ShowAsDateTimeProperty);
            set => SetValue(ShowAsDateTimeProperty, value);
        }

        public static readonly StyledProperty<bool> UseAuthorTimeProperty =
            AvaloniaProperty.Register<CommitTimeTextBlock, bool>(nameof(UseAuthorTime), true);

        public bool UseAuthorTime
        {
            get => GetValue(UseAuthorTimeProperty);
            set => SetValue(UseAuthorTimeProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextBlock);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == UseAuthorTimeProperty)
            {
                SetCurrentValue(TextProperty, GetDisplayText());
            }
            else if (change.Property == ShowAsDateTimeProperty)
            {
                SetCurrentValue(TextProperty, GetDisplayText());

                if (ShowAsDateTime)
                    StopTimer();
                else
                    StartTimer();
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (!ShowAsDateTime)
                StartTimer();
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            StopTimer();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            SetCurrentValue(TextProperty, GetDisplayText());
        }

        private void StartTimer()
        {
            if (_refreshTimer != null)
                return;

            _refreshTimer = DispatcherTimer.Run(() =>
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    var text = GetDisplayText();
                    if (!text.Equals(Text, StringComparison.Ordinal))
                        Text = text;
                });

                return true;
            }, TimeSpan.FromSeconds(10));
        }

        private void StopTimer()
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Dispose();
                _refreshTimer = null;
            }
        }

        private string GetDisplayText()
        {
            var commit = DataContext as Models.Commit;
            if (commit == null)
                return string.Empty;

            var timestamp = UseAuthorTime ? commit.AuthorTime : commit.CommitterTime;
            if (ShowAsDateTime)
                return DateTime.UnixEpoch.AddSeconds(timestamp).ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");

            var now = DateTime.Now;
            var localTime = DateTime.UnixEpoch.AddSeconds(timestamp).ToLocalTime();
            var span = now - localTime;
            if (span.TotalMinutes < 1)
                return App.Text("Period.JustNow");

            if (span.TotalHours < 1)
                return App.Text("Period.MinutesAgo", (int)span.TotalMinutes);

            if (span.TotalDays < 1)
                return App.Text("Period.HoursAgo", (int)span.TotalHours);

            var lastDay = now.AddDays(-1).Date;
            if (localTime >= lastDay)
                return App.Text("Period.Yesterday");

            if ((localTime.Year == now.Year && localTime.Month == now.Month) || span.TotalDays < 28)
            {
                var diffDay = now.Date - localTime.Date;
                return App.Text("Period.DaysAgo", (int)diffDay.TotalDays);
            }

            var lastMonth = now.AddMonths(-1).Date;
            if (localTime.Year == lastMonth.Year && localTime.Month == lastMonth.Month)
                return App.Text("Period.LastMonth");

            if (localTime.Year == now.Year || localTime > now.AddMonths(-11))
            {
                var diffMonth = (12 + now.Month - localTime.Month) % 12;
                return App.Text("Period.MonthsAgo", diffMonth);
            }

            var diffYear = now.Year - localTime.Year;
            if (diffYear == 1)
                return App.Text("Period.LastYear");

            return App.Text("Period.YearsAgo", diffYear);
        }

        private IDisposable _refreshTimer = null;
    }

    public class CommitGraph : Control
    {
        public static readonly StyledProperty<Models.CommitGraph> GraphProperty =
            AvaloniaProperty.Register<CommitGraph, Models.CommitGraph>(nameof(Graph));

        public Models.CommitGraph Graph
        {
            get => GetValue(GraphProperty);
            set => SetValue(GraphProperty, value);
        }

        public static readonly StyledProperty<IBrush> DotBrushProperty =
            AvaloniaProperty.Register<CommitGraph, IBrush>(nameof(DotBrush), Brushes.Transparent);

        public IBrush DotBrush
        {
            get => GetValue(DotBrushProperty);
            set => SetValue(DotBrushProperty, value);
        }

        static CommitGraph()
        {
            AffectsRender<CommitGraph>(GraphProperty, DotBrushProperty);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var graph = Graph;
            if (graph == null)
                return;

            var histories = this.FindAncestorOfType<Histories>();
            if (histories == null)
                return;

            var list = histories.CommitListContainer;
            if (list == null)
                return;

            // Calculate drawing area.
            double width = Bounds.Width - 273 - histories.AuthorNameColumnWidth.Value;
            double height = Bounds.Height;
            double startY = list.Scroll?.Offset.Y ?? 0;
            double endY = startY + height + 28;

            // Apply scroll offset and clip.
            using (context.PushClip(new Rect(0, 0, width, height)))
            using (context.PushTransform(Matrix.CreateTranslation(0, -startY)))
            {
                // Draw contents
                DrawCurves(context, graph, startY, endY);
                DrawAnchors(context, graph, startY, endY);
            }
        }

        private void DrawCurves(DrawingContext context, Models.CommitGraph graph, double top, double bottom)
        {
            foreach (var line in graph.Paths)
            {
                var last = line.Points[0];
                var size = line.Points.Count;

                if (line.Points[size - 1].Y < top)
                    continue;
                if (last.Y > bottom)
                    break;

                var geo = new StreamGeometry();
                var pen = Models.CommitGraph.Pens[line.Color];

                using (var ctx = geo.Open())
                {
                    var started = false;
                    var ended = false;
                    for (int i = 1; i < size; i++)
                    {
                        var cur = line.Points[i];
                        if (cur.Y < top)
                        {
                            last = cur;
                            continue;
                        }

                        if (!started)
                        {
                            ctx.BeginFigure(last, false);
                            started = true;
                        }

                        if (cur.Y > bottom)
                        {
                            cur = new Point(cur.X, bottom);
                            ended = true;
                        }

                        if (cur.X > last.X)
                        {
                            ctx.QuadraticBezierTo(new Point(cur.X, last.Y), cur);
                        }
                        else if (cur.X < last.X)
                        {
                            if (i < size - 1)
                            {
                                var midY = (last.Y + cur.Y) / 2;
                                ctx.CubicBezierTo(new Point(last.X, midY + 4), new Point(cur.X, midY - 4), cur);
                            }
                            else
                            {
                                ctx.QuadraticBezierTo(new Point(last.X, cur.Y), cur);
                            }
                        }
                        else
                        {
                            ctx.LineTo(cur);
                        }

                        if (ended)
                            break;
                        last = cur;
                    }
                }

                context.DrawGeometry(null, pen, geo);
            }

            foreach (var link in graph.Links)
            {
                if (link.End.Y < top)
                    continue;
                if (link.Start.Y > bottom)
                    break;

                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(link.Start, false);
                    ctx.QuadraticBezierTo(link.Control, link.End);
                }

                context.DrawGeometry(null, Models.CommitGraph.Pens[link.Color], geo);
            }
        }

        private void DrawAnchors(DrawingContext context, Models.CommitGraph graph, double top, double bottom)
        {
            IBrush dotFill = DotBrush;
            Pen dotFillPen = new Pen(dotFill, 2);

            foreach (var dot in graph.Dots)
            {
                if (dot.Center.Y < top)
                    continue;
                if (dot.Center.Y > bottom)
                    break;

                var pen = Models.CommitGraph.Pens[dot.Color];
                switch (dot.Type)
                {
                    case Models.CommitGraph.DotType.Head:
                        context.DrawEllipse(dotFill, pen, dot.Center, 6, 6);
                        context.DrawEllipse(pen.Brush, null, dot.Center, 3, 3);
                        break;
                    case Models.CommitGraph.DotType.Merge:
                        context.DrawEllipse(pen.Brush, null, dot.Center, 6, 6);
                        context.DrawLine(dotFillPen, new Point(dot.Center.X, dot.Center.Y - 3), new Point(dot.Center.X, dot.Center.Y + 3));
                        context.DrawLine(dotFillPen, new Point(dot.Center.X - 3, dot.Center.Y), new Point(dot.Center.X + 3, dot.Center.Y));
                        break;
                    default:
                        context.DrawEllipse(dotFill, pen, dot.Center, 3, 3);
                        break;
                }
            }
        }
    }

    public partial class Histories : UserControl
    {
        public static readonly StyledProperty<GridLength> AuthorNameColumnWidthProperty =
            AvaloniaProperty.Register<Histories, GridLength>(nameof(AuthorNameColumnWidth), new GridLength(120));

        public GridLength AuthorNameColumnWidth
        {
            get => GetValue(AuthorNameColumnWidthProperty);
            set => SetValue(AuthorNameColumnWidthProperty, value);
        }

        public static readonly StyledProperty<Models.Branch> CurrentBranchProperty =
            AvaloniaProperty.Register<Histories, Models.Branch>(nameof(CurrentBranch));

        public Models.Branch CurrentBranch
        {
            get => GetValue(CurrentBranchProperty);
            set => SetValue(CurrentBranchProperty, value);
        }

        public static readonly StyledProperty<AvaloniaList<Models.IssueTrackerRule>> IssueTrackerRulesProperty =
            AvaloniaProperty.Register<Histories, AvaloniaList<Models.IssueTrackerRule>>(nameof(IssueTrackerRules));

        public AvaloniaList<Models.IssueTrackerRule> IssueTrackerRules
        {
            get => GetValue(IssueTrackerRulesProperty);
            set => SetValue(IssueTrackerRulesProperty, value);
        }

        public static readonly StyledProperty<long> NavigationIdProperty =
            AvaloniaProperty.Register<Histories, long>(nameof(NavigationId));

        public long NavigationId
        {
            get => GetValue(NavigationIdProperty);
            set => SetValue(NavigationIdProperty, value);
        }

        static Histories()
        {
            NavigationIdProperty.Changed.AddClassHandler<Histories>((h, _) =>
            {
                if (h.DataContext == null)
                    return;

                // Force scroll selected item (current head) into view. see issue #58
                var list = h.CommitListContainer;
                if (list != null && list.SelectedItems.Count == 1)
                    list.ScrollIntoView(list.SelectedIndex);
            });

            AuthorNameColumnWidthProperty.Changed.AddClassHandler<Histories>((h, _) =>
            {
                h.CommitGraph.InvalidateVisual();
            });
        }

        public Histories()
        {
            InitializeComponent();
        }

        private void OnCommitListLayoutUpdated(object _1, EventArgs _2)
        {
            var y = CommitListContainer.Scroll?.Offset.Y ?? 0;
            if (y != _lastScrollY)
            {
                _lastScrollY = y;
                CommitGraph.InvalidateVisual();
            }
        }

        private void OnCommitListSelectionChanged(object _, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.Histories histories)
            {
                histories.Select(CommitListContainer.SelectedItems);
            }
            e.Handled = true;
        }

        private void OnCommitListContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (DataContext is ViewModels.Histories histories && sender is ListBox { SelectedItems: { Count: > 0 } } list)
            {
                var menu = histories.MakeContextMenu(list);
                menu?.Open(list);
            }
            e.Handled = true;
        }

        private void OnCommitListDoubleTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.Histories histories && sender is ListBox { SelectedItems: { Count: 1 } })
            {
                var source = e.Source as Control;
                var item = source.FindAncestorOfType<ListBoxItem>();
                if (item is { DataContext: Models.Commit commit })
                    histories.DoubleTapped(commit);
            }
            e.Handled = true;
        }

        private void OnCommitListKeyDown(object sender, KeyEventArgs e)
        {
            if (!e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
                return;

            // These shortcuts are not mentioned in the Shortcut Reference window. Is this expected?
            if (sender is ListBox { SelectedItems: { Count: > 0 } selected })
            {
                // CTRL/COMMAND + C -> Copy selected commit SHA and subject.
                if (e.Key == Key.C)
                {
                    var builder = new StringBuilder();
                    foreach (var item in selected)
                    {
                        if (item is Models.Commit commit)
                            builder.AppendLine($"{commit.SHA.Substring(0, 10)} - {commit.Subject}");
                    }

                    App.CopyText(builder.ToString());
                    e.Handled = true;
                    return;
                }

                // CTRL/COMMAND + B -> shows Create Branch pop-up at selected commit.
                if (e.Key == Key.B)
                {
                    if (selected.Count == 1 &&
                        selected[0] is Models.Commit commit &&
                        DataContext is ViewModels.Histories histories &&
                        ViewModels.PopupHost.CanCreatePopup())
                    {
                        ViewModels.PopupHost.ShowPopup(new ViewModels.CreateBranch(histories.Repo, commit));
                        e.Handled = true;
                    }
                }
            }
        }

        private double _lastScrollY = 0;
    }
}
