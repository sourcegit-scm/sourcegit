using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class CommitRefsPresenter : Control
    {
        public class RenderItem
        {
            public Geometry Icon { get; set; } = null;
            public FormattedText Label { get; set; } = null;
            public IBrush Brush { get; set; } = null;
            public bool IsHead { get; set; } = false;
            public double Width { get; set; } = 0.0;
            public Models.Decorator Decorator { get; set; } = null;
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

        static CommitRefsPresenter()
        {
            AffectsMeasure<CommitRefsPresenter>(
                FontFamilyProperty,
                FontSizeProperty,
                ForegroundProperty,
                UseGraphColorProperty,
                BackgroundProperty,
                ShowTagsProperty);
        }

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
            var x = 1.0;
            var y = 0.0;

            foreach (var item in _items)
            {
                if (allowWrap && x > 1.0 && x + item.Width > Bounds.Width)
                {
                    x = 1.0;
                    y += 20.0;
                }

                var entireRect = new RoundedRect(new Rect(x, y, item.Width, 16), new CornerRadius(2));

                if (item.IsHead)
                {
                    if (useGraphColor)
                    {
                        if (bg != null)
                            context.DrawRectangle(bg, null, entireRect);

                        using (context.PushOpacity(.6))
                            context.DrawRectangle(item.Brush, null, entireRect);
                    }

                    context.DrawText(item.Label, new Point(x + 16, y + 8.0 - item.Label.Height * 0.5));
                }
                else
                {
                    if (bg != null)
                        context.DrawRectangle(bg, null, entireRect);

                    var labelRect = new RoundedRect(new Rect(x + 16, y, item.Label.Width + 8, 16), new CornerRadius(0, 2, 2, 0));
                    using (context.PushOpacity(.2))
                        context.DrawRectangle(item.Brush, null, labelRect);

                    context.DrawLine(new Pen(item.Brush), new Point(x + 16, y), new Point(x + 16, y + 16));
                    context.DrawText(item.Label, new Point(x + 20, y + 8.0 - item.Label.Height * 0.5));
                }

                context.DrawRectangle(null, new Pen(item.Brush), entireRect);

                using (context.PushTransform(Matrix.CreateTranslation(x + 3, y + 3)))
                    context.DrawGeometry(fg, null, item.Icon);

                x += item.Width + 4;
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _items.Clear();

            if (DataContext is not Models.Commit commit)
                return new Size(0, 0);

            var refs = commit.Decorators;
            if (refs is { Count: > 0 })
            {
                var typeface = new Typeface(FontFamily);
                var typefaceBold = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Bold);
                var fg = Foreground;
                var normalBG = UseGraphColor ? Models.CommitGraph.Pens[commit.Color].Brush : Brushes.Gray;
                var labelSize = FontSize;
                var requiredHeight = 16.0;
                var x = 0.0;
                var allowWrap = AllowWrap;
                var showTags = ShowTags;

                foreach (var decorator in refs)
                {
                    if (!showTags && decorator.Type == Models.DecoratorType.Tag)
                        continue;

                    var isHead = decorator.Type is Models.DecoratorType.CurrentBranchHead or Models.DecoratorType.CurrentCommitHead;

                    var label = new FormattedText(
                        decorator.Name,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        isHead ? typefaceBold : typeface,
                        isHead ? labelSize + 1 : labelSize,
                        fg);

                    var item = new RenderItem()
                    {
                        Label = label,
                        Brush = normalBG,
                        IsHead = isHead,
                        Decorator = decorator,
                    };

                    StreamGeometry geo;
                    switch (decorator.Type)
                    {
                        case Models.DecoratorType.CurrentBranchHead:
                        case Models.DecoratorType.CurrentCommitHead:
                            geo = this.FindResource("Icons.Head") as StreamGeometry;
                            break;
                        case Models.DecoratorType.RemoteBranchHead:
                            geo = this.FindResource("Icons.Remote") as StreamGeometry;
                            break;
                        case Models.DecoratorType.Tag:
                            item.Brush = Brushes.Gray;
                            geo = this.FindResource("Icons.Tag") as StreamGeometry;
                            break;
                        default:
                            geo = this.FindResource("Icons.Branch") as StreamGeometry;
                            break;
                    }

                    var drawGeo = geo!.Clone();
                    var iconBounds = drawGeo.Bounds;
                    var translation = Matrix.CreateTranslation(-(Vector)iconBounds.Position);
                    var scale = Math.Min(10.0 / iconBounds.Width, 10.0 / iconBounds.Height);
                    var transform = translation * Matrix.CreateScale(scale, scale);
                    if (drawGeo.Transform == null || drawGeo.Transform.Value == Matrix.Identity)
                        drawGeo.Transform = new MatrixTransform(transform);
                    else
                        drawGeo.Transform = new MatrixTransform(drawGeo.Transform.Value * transform);

                    item.Icon = drawGeo;
                    item.Width = 16 + (isHead ? 0 : 4) + label.Width + 4;
                    _items.Add(item);

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

                var requiredWidth = allowWrap && requiredHeight > 16.0
                    ? availableSize.Width
                    : x + 2;
                InvalidateVisual();
                return new Size(requiredWidth, requiredHeight);
            }

            InvalidateVisual();
            return new Size(0, 0);
        }

        private List<RenderItem> _items = new List<RenderItem>();
    }
}
