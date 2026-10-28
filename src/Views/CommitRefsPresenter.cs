using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class CommitRefsIconCache
    {
        public static CommitRefsIconCache Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new CommitRefsIconCache();
                return _instance;
            }
        }

        public CommitRefsIconCache()
        {
            _head = LoadIcon("Icons.Head");
            _branch = LoadIcon("Icons.Branch");
            _remote = LoadIcon("Icons.Remote");
            _tag = LoadIcon("Icons.Tag");
        }

        public Geometry GetIcon(Models.DecoratorType type)
        {
            return type switch
            {
                Models.DecoratorType.CurrentBranchHead => _head,
                Models.DecoratorType.CurrentCommitHead => _head,
                Models.DecoratorType.LocalBranchHead => _branch,
                Models.DecoratorType.RemoteBranchHead => _remote,
                Models.DecoratorType.Tag => _tag,
                _ => null,
            };
        }

        private Geometry LoadIcon(string resourceKey)
        {
            var geo = App.Current.FindResource(resourceKey) as StreamGeometry;
            var drawGeo = geo!.Clone();
            var iconBounds = drawGeo.Bounds;
            var translation = Matrix.CreateTranslation(-(Vector)iconBounds.Position);
            var scale = Math.Min(10.0 / iconBounds.Width, 10.0 / iconBounds.Height);
            var transform = translation * Matrix.CreateScale(scale, scale);
            if (drawGeo.Transform == null || drawGeo.Transform.Value == Matrix.Identity)
                drawGeo.Transform = new MatrixTransform(transform);
            else
                drawGeo.Transform = new MatrixTransform(drawGeo.Transform.Value * transform);

            return drawGeo;
        }

        private static CommitRefsIconCache _instance = null;
        private Geometry _head = null;
        private Geometry _branch = null;
        private Geometry _remote = null;
        private Geometry _tag = null;
    }

    public class CommitRefsPresenter : Control
    {
        public class RenderItem
        {
            public Models.Decorator Decorator { get; set; } = null;
            public FormattedText Label { get; set; } = null;
            public IBrush Brush { get; set; } = null;
            public bool IsHead { get; set; } = false;
            public double Width { get; set; } = 0.0;
            public bool HasMatchingRemote { get; set; } = false;
            public List<FormattedText> Remotes { get; set; } = [];
        }

        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            TextBlock.FontFamilyProperty.AddOwner<CommitRefsPresenter>();

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public static readonly StyledProperty<double> FontSizeProperty =
           TextBlock.FontSizeProperty.AddOwner<CommitRefsPresenter>();

        public double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public static readonly StyledProperty<IBrush> BackgroundProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, IBrush>(nameof(Background), Brushes.Transparent);

        public IBrush Background
        {
            get => GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, IBrush>(nameof(Foreground), Brushes.White);

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly StyledProperty<bool> UseCompactBranchNamesProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(UseCompactBranchNames));

        public bool UseCompactBranchNames
        {
            get => GetValue(UseCompactBranchNamesProperty);
            set => SetValue(UseCompactBranchNamesProperty, value);
        }

        public static readonly StyledProperty<bool> HasSingleRemoteProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(HasSingleRemote));

        public bool HasSingleRemote
        {
            get => GetValue(HasSingleRemoteProperty);
            set => SetValue(HasSingleRemoteProperty, value);
        }

        public static readonly StyledProperty<bool> UseGraphColorProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(UseGraphColor));

        public bool UseGraphColor
        {
            get => GetValue(UseGraphColorProperty);
            set => SetValue(UseGraphColorProperty, value);
        }

        public static readonly StyledProperty<bool> AllowWrapProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(AllowWrap));

        public bool AllowWrap
        {
            get => GetValue(AllowWrapProperty);
            set => SetValue(AllowWrapProperty, value);
        }

        public static readonly StyledProperty<bool> ShowTagsProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(ShowTags), true);

        public bool ShowTags
        {
            get => GetValue(ShowTagsProperty);
            set => SetValue(ShowTagsProperty, value);
        }

        public static readonly StyledProperty<bool> ShowBranchesProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(ShowBranches), true);

        public bool ShowBranches
        {
            get => GetValue(ShowBranchesProperty);
            set => SetValue(ShowBranchesProperty, value);
        }

        public static readonly StyledProperty<bool> ShowBranchSummaryProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(ShowBranchSummary));

        public bool ShowBranchSummary
        {
            get => GetValue(ShowBranchSummaryProperty);
            set => SetValue(ShowBranchSummaryProperty, value);
        }

        public static readonly StyledProperty<List<Models.Decorator>> BranchSummaryRefsProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, List<Models.Decorator>>(nameof(BranchSummaryRefs));

        public List<Models.Decorator> BranchSummaryRefs
        {
            get => GetValue(BranchSummaryRefsProperty);
            set => SetValue(BranchSummaryRefsProperty, value);
        }

        public double BranchSummaryRight => ShowBranchSummary && _items.Count > 0 ? _items[0].Width + 1.5 : 0;
        public IBrush BranchSummaryBrush => ShowBranchSummary && _items.Count > 0 ? _items[0].Brush : null;
        public double BranchSummarySuffixWidth => _branchCountLabel != null ? _branchCountLabel.WidthIncludingTrailingWhitespace + 8 : 0;
        public bool BranchSummaryHasRemote => ShowBranchSummary && _items.Count > 0 && _items[0].HasMatchingRemote;
        public string BranchSummaryText => _summaryText;
        public int BranchSummaryAdditionalCount => _summaryAdditionalCount;

        public Models.Decorator DecoratorAt(Point point)
        {
            var x = 0.0;
            foreach (var item in _items)
            {
                x += item.Width;
                if (point.X < x)
                    return item.Decorator;
            }

            return null;
        }

        public override void Render(DrawingContext context)
        {
            if (_items.Count == 0)
                return;

            var useGraphColor = UseGraphColor;
            var fg = Foreground;
            var bg = Background;
            var allowWrap = AllowWrap;
            var x = 1.5;
            var y = 0.5;
            var remoteIcon = CommitRefsIconCache.Instance.GetIcon(Models.DecoratorType.RemoteBranchHead);
            var hasSingleRemote = HasSingleRemote;

            context.FillRectangle(Brushes.Transparent, Bounds);

            foreach (var item in _items)
            {
                if (allowWrap && x > 1.5 && x + item.Width > Bounds.Width)
                {
                    x = 1.5;
                    y += 20.0;
                }

                var iconWidth = item.HasMatchingRemote ? 28 : 16;
                var entireRect = new RoundedRect(new Rect(x, y, item.Width, 16), new CornerRadius(4));
                if (item.IsHead)
                {
                    if (useGraphColor)
                    {
                        if (bg != null)
                            context.DrawRectangle(bg, null, entireRect);

                        using (context.PushOpacity(.6))
                            context.DrawRectangle(item.Brush, null, entireRect);
                    }
                }
                else
                {
                    if (bg != null)
                        context.DrawRectangle(bg, null, entireRect);

                    var labelRect = new RoundedRect(new Rect(x + iconWidth, y, item.Width - iconWidth, 16), new CornerRadius(4, 0, 0, 4));
                    using (context.PushOpacity(.2))
                        context.DrawRectangle(item.Brush, null, labelRect);
                }

                context.DrawLine(new Pen(item.Brush), new Point(x + iconWidth, y), new Point(x + iconWidth, y + 16));
                context.DrawText(item.Label, new Point(x + iconWidth + 4, y + 8.0 - item.Label.Height * 0.5));

                if (item.Remotes.Count > 0)
                {
                    var rx = x + 20 + item.Label.WidthIncludingTrailingWhitespace + 4;

                    if (hasSingleRemote)
                    {
                        context.DrawLine(new Pen(item.Brush), new Point(rx, y), new Point(rx, y + 16));
                        using (context.PushTransform(Matrix.CreateTranslation(rx + 4, y + 4)))
                            context.DrawGeometry(fg, null, remoteIcon);
                    }
                    else
                    {
                        foreach (var remote in item.Remotes)
                        {
                            context.DrawLine(new Pen(item.Brush), new Point(rx, y), new Point(rx, y + 16));
                            using (context.PushTransform(Matrix.CreateTranslation(rx + 4, y + 4)))
                                context.DrawGeometry(fg, null, remoteIcon);
                            context.DrawText(remote, new Point(rx + 16, y + 8.0 - remote.Height * 0.5));
                            rx += remote.WidthIncludingTrailingWhitespace + 22;
                        }
                    }
                }

                context.DrawRectangle(null, new Pen(item.Brush), entireRect);

                var icon = CommitRefsIconCache.Instance.GetIcon(item.HasMatchingRemote ? Models.DecoratorType.LocalBranchHead : item.Decorator.Type);
                using (context.PushTransform(Matrix.CreateTranslation(x + 3, y + 3)))
                    context.DrawGeometry(fg, null, icon);

                if (item.HasMatchingRemote)
                {
                    using (context.PushTransform(Matrix.CreateTranslation(x + 15, y + 3)))
                        context.DrawGeometry(fg, null, remoteIcon);
                }

                if (_branchCountLabel != null)
                    context.DrawText(_branchCountLabel, new Point(x + item.Width + 4, y + 8 - _branchCountLabel.Height * 0.5));

                x += item.Width + 4;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == FontFamilyProperty ||
                change.Property == FontSizeProperty ||
                change.Property == ForegroundProperty ||
                change.Property == UseGraphColorProperty ||
                change.Property == UseCompactBranchNamesProperty ||
                change.Property == HasSingleRemoteProperty ||
                change.Property == BackgroundProperty ||
                change.Property == ShowTagsProperty ||
                change.Property == ShowBranchSummaryProperty ||
                change.Property == ShowBranchesProperty ||
                change.Property == BranchSummaryRefsProperty)
                InvalidateMeasure();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (BranchSummaryRefs != null)
            {
                SetCurrentValue(BranchSummaryRefsProperty, null);
                SetCurrentValue(OpacityProperty, 1.0);
            }
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _items.Clear();
            _branchCountLabel = null;
            _summaryText = string.Empty;
            _summaryName = string.Empty;
            _summaryAdditionalCount = 0;

            var commit = DataContext as Models.Commit;
            List<Models.Decorator> refs = DataContext switch
            {
                Models.Commit c => c.Decorators,
                Models.Decorator decorator => [decorator],
                _ => [],
            };
            if (ShowBranchSummary && BranchSummaryRefs != null)
                refs = BranchSummaryRefs;
            var branchCount = 0;
            var primaryHasRemote = false;
            if (ShowBranchSummary)
            {
                Models.Decorator primary = null;
                var localNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var decorator in refs)
                {
                    if (decorator.IsBranch)
                    {
                        if (decorator.Type is Models.DecoratorType.CurrentBranchHead or Models.DecoratorType.LocalBranchHead)
                            localNames.Add(decorator.Name);
                        if (primary == null || decorator.Type < primary.Type)
                            primary = decorator;
                    }
                }

                foreach (var decorator in refs)
                {
                    if (!decorator.IsBranch)
                        continue;

                    if (decorator.Type == Models.DecoratorType.RemoteBranchHead)
                    {
                        var slash = decorator.Name.IndexOf('/');
                        var name = slash >= 0 ? decorator.Name.Substring(slash + 1) : decorator.Name;
                        if (slash >= 0 && localNames.Contains(name))
                        {
                            primaryHasRemote |= primary?.Name == name;
                            continue;
                        }
                    }

                    branchCount++;
                }

                _summaryAdditionalCount = Math.Max(0, branchCount - 1);
                refs = primary != null ? [primary] : [];
            }
            var count = refs.Count;
            if (count == 0)
            {
                InvalidateVisual();
                return new Size(0, 0);
            }

            var useCompactBranchNames = UseCompactBranchNames;
            var hasSingleRemote = HasSingleRemote;
            var typeface = new Typeface(FontFamily);
            var typefaceHead = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Bold);
            var typefaceRemote = new Typeface(FontFamily, FontStyle.Italic, FontWeight.Bold);
            var fg = Foreground;
            var normalBG = UseGraphColor && commit != null ? Models.CommitGraph.Pens[commit.Color].Brush : Brushes.Gray;
            var labelSize = FontSize;
            if (branchCount > 1)
                _branchCountLabel = new FormattedText($"+{branchCount - 1}", CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, labelSize, fg);
            var requiredHeight = 16.0;
            var x = 0.0;
            var allowWrap = AllowWrap;
            var showTags = ShowTags;
            var showBranches = ShowBranches;
            var skippedIdx = new HashSet<int>();

            for (var i = 0; i < count; i++)
            {
                if (skippedIdx.Contains(i))
                    continue;

                var decorator = refs[i];
                if (!showBranches && decorator.IsBranch)
                    continue;
                if (!showTags && decorator.Type == Models.DecoratorType.Tag)
                    continue;

                var item = new RenderItem()
                {
                    Decorator = decorator,
                    Brush = decorator.Type == Models.DecoratorType.Tag ? Brushes.Gray : normalBG,
                    IsHead = decorator.Type is Models.DecoratorType.CurrentBranchHead or Models.DecoratorType.CurrentCommitHead,
                    HasMatchingRemote = ShowBranchSummary && primaryHasRemote,
                };
                _items.Add(item);

                var label = decorator.Name;
                if (item.IsHead)
                {
                    item.Label = new FormattedText(
                        label,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typefaceHead,
                        labelSize + 1,
                        fg);
                }
                else
                {
                    item.Label = new FormattedText(
                        label,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        labelSize,
                        fg);
                }

                item.Width = item.Label.Width + 24;
                if (ShowBranchSummary)
                {
                    _summaryName = decorator.Name;
                    FitBranchSummary(availableSize.Width);
                }

                var findRemotes = !ShowBranchSummary && useCompactBranchNames && (decorator.Type == Models.DecoratorType.CurrentBranchHead || decorator.Type == Models.DecoratorType.LocalBranchHead);
                if (findRemotes)
                {
                    for (var j = i + 1; j < count; j++)
                    {
                        var test = refs[j];
                        if (test.Type != Models.DecoratorType.RemoteBranchHead)
                            continue;

                        var idxOfSlash = test.Name.IndexOf('/');
                        if (idxOfSlash < 1 || idxOfSlash == test.Name.Length - 1)
                            continue;

                        var name = test.Name.Substring(idxOfSlash + 1);
                        if (decorator.Name.Equals(name, StringComparison.Ordinal))
                        {
                            var remote = new FormattedText(
                                test.Name.Substring(0, idxOfSlash),
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                typefaceRemote,
                                labelSize,
                                fg);

                            item.Remotes.Add(remote);

                            if (hasSingleRemote)
                                item.Width += 18;
                            else
                                item.Width += remote.WidthIncludingTrailingWhitespace + 22;

                            skippedIdx.Add(j);
                        }
                    }
                }

                x += item.Width + 4;
                if (allowWrap)
                {
                    if (x > availableSize.Width)
                    {
                        requiredHeight += 20.0;
                        x = item.Width;
                    }
                }
            }

            double requiredWidth = 0;
            if (_items.Count > 0)
            {
                if (allowWrap && requiredHeight > 16.0)
                    requiredWidth = double.IsInfinity(availableSize.Width) ? x + 2 : availableSize.Width;
                else
                    requiredWidth = x + 2;
            }

            InvalidateVisual();
            if (ShowBranchSummary && _items.Count > 0)
                return new Size(_items[0].Width + SummaryOuterSpacing + BranchSummarySuffixWidth, requiredHeight);
            return new Size(requiredWidth + BranchSummarySuffixWidth, requiredHeight);
        }

        private List<RenderItem> _items = new List<RenderItem>();
        private FormattedText _branchCountLabel;
        private string _summaryName = string.Empty;
        private string _summaryText = string.Empty;
        private int _summaryAdditionalCount;
        private const double SummaryOuterSpacing = 3;

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (ShowBranchSummary && _items.Count > 0)
            {
                FitBranchSummary(finalSize.Width);
                InvalidateVisual();
            }

            return base.ArrangeOverride(finalSize);
        }

        private void FitBranchSummary(double availableWidth)
        {
            var item = _items[0];
            var fixedWidth = item.HasMatchingRemote ? 36 : 24;
            var textWidth = Math.Max(0, availableWidth - BranchSummarySuffixWidth - fixedWidth - SummaryOuterSpacing);
            var typeface = new Typeface(FontFamily, FontStyle.Normal, item.IsHead ? FontWeight.Bold : FontWeight.Normal);
            var fontSize = FontSize + (item.IsHead ? 1 : 0);
            FormattedText Format(string value) => new(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, fontSize, Foreground);

            _summaryText = _summaryName;
            item.Label = Format(_summaryText);
            if (item.Label.WidthIncludingTrailingWhitespace > textWidth)
            {
                // Truncate at grapheme boundaries so Unicode branch names remain valid.
                var boundaries = StringInfo.ParseCombiningCharacters(_summaryName);
                var low = 0;
                var high = boundaries.Length;
                while (low < high)
                {
                    var mid = (low + high + 1) / 2;
                    var end = mid < boundaries.Length ? boundaries[mid] : _summaryName.Length;
                    if (Format(_summaryName.Substring(0, end) + "...").WidthIncludingTrailingWhitespace <= textWidth)
                        low = mid;
                    else
                        high = mid - 1;
                }

                var prefixEnd = low < boundaries.Length ? boundaries[low] : _summaryName.Length;
                _summaryText = Format("...").WidthIncludingTrailingWhitespace <= textWidth ? _summaryName.Substring(0, prefixEnd) + "..." : string.Empty;
                item.Label = Format(_summaryText);
            }

            item.Width = item.Label.WidthIncludingTrailingWhitespace + fixedWidth;
        }
    }
}
