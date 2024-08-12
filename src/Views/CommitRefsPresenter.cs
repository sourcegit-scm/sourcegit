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
            public bool IsTag { get; set; } = false;
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

        public static readonly StyledProperty<IBrush> IconBackgroundProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, IBrush>(nameof(IconBackground), Brushes.White);

        public IBrush IconBackground
        {
            get => GetValue(IconBackgroundProperty);
            set => SetValue(IconBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> IconForegroundProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, IBrush>(nameof(IconForeground), Brushes.White);

        public IBrush IconForeground
        {
            get => GetValue(IconForegroundProperty);
            set => SetValue(IconForegroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> LabelForegroundProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, IBrush>(nameof(LabelForeground), Brushes.White);

        public IBrush LabelForeground
        {
            get => GetValue(LabelForegroundProperty);
            set => SetValue(LabelForegroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> BranchNameBackgroundProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, IBrush>(nameof(BranchNameBackground), Brushes.White);

        public IBrush BranchNameBackground
        {
            get => GetValue(BranchNameBackgroundProperty);
            set => SetValue(BranchNameBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> TagNameBackgroundProperty =
            AvaloniaProperty.Register<CommitRefsPresenter, IBrush>(nameof(TagNameBackground), Brushes.White);

        public IBrush TagNameBackground
        {
            get => GetValue(TagNameBackgroundProperty);
            set => SetValue(TagNameBackgroundProperty, value);
        }

        static CommitRefsPresenter()
        {
            AffectsMeasure<CommitRefsPresenter>(
                FontFamilyProperty,
                FontSizeProperty,
                LabelForegroundProperty);

            AffectsRender<CommitRefsPresenter>(
                IconBackgroundProperty,
                IconForegroundProperty,
                BranchNameBackgroundProperty,
                TagNameBackgroundProperty);
        }

        public override void Render(DrawingContext context)
        {
            if (_items.Count == 0)
                return;

            var iconFG = IconForeground;
            var iconBG = IconBackground;
            var branchBG = BranchNameBackground;
            var tagBG = TagNameBackground;
            var x = 0.0;

            foreach (var item in _items)
            {
                var iconRect = new RoundedRect(new Rect(x, 0, 16, 16), new CornerRadius(2, 0, 0, 2));
                var labelRect = new RoundedRect(new Rect(x + 16, 0, item.Label.Width + 8, 16), new CornerRadius(0, 2, 2, 0));

                context.DrawRectangle(iconBG, null, iconRect);
                context.DrawRectangle(item.IsTag ? tagBG : branchBG, null, labelRect);
                context.DrawText(item.Label, new Point(x + 20, 8.0 - item.Label.Height * 0.5));

                using (context.PushTransform(Matrix.CreateTranslation(x + 4, 4)))
                    context.DrawGeometry(iconFG, null, item.Icon);

                x += item.Label.Width + 16 + 8 + 4;
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

            if (DataContext is Models.Commit commit && commit.HasDecorators)
            {
                var typeface = new Typeface(FontFamily);
                var typefaceBold = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Bold);
                var labelFG = LabelForeground;
                var labelSize = FontSize;
                var requiredWidth = 0.0;

                foreach (var decorator in commit.Decorators)
                {
                    var isHead = decorator.Type == Models.DecoratorType.CurrentBranchHead ||
                        decorator.Type == Models.DecoratorType.CurrentCommitHead;

                    var label = new FormattedText(
                        decorator.Name,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        isHead ? typefaceBold : typeface,
                        labelSize,
                        labelFG);

                    var item = new RenderItem()
                    {
                        Label = label,
                        IsTag = decorator.Type == Models.DecoratorType.Tag,
                    };

                    StreamGeometry geo;
                    switch (decorator.Type)
                    {
                        case Models.DecoratorType.CurrentBranchHead:
                        case Models.DecoratorType.CurrentCommitHead:
                            geo = this.FindResource("Icons.Check") as StreamGeometry;
                            break;
                        case Models.DecoratorType.RemoteBranchHead:
                            geo = this.FindResource("Icons.Remote") as StreamGeometry;
                            break;
                        case Models.DecoratorType.Tag:
                            geo = this.FindResource("Icons.Tag") as StreamGeometry;
                            break;
                        default:
                            geo = this.FindResource("Icons.Branch") as StreamGeometry;
                            break;
                    }

                    var drawGeo = geo!.Clone();
                    var iconBounds = drawGeo.Bounds;
                    var translation = Matrix.CreateTranslation(-(Vector)iconBounds.Position);
                    var scale = Math.Min(8.0 / iconBounds.Width, 8.0 / iconBounds.Height);
                    var transform = translation * Matrix.CreateScale(scale, scale);
                    if (drawGeo.Transform == null || drawGeo.Transform.Value == Matrix.Identity)
                        drawGeo.Transform = new MatrixTransform(transform);
                    else
                        drawGeo.Transform = new MatrixTransform(drawGeo.Transform.Value * transform);

                    item.Icon = drawGeo;
                    _items.Add(item);
                    requiredWidth += label.Width + 16 /* icon */ + 8 /* label margin */ + 4 /* item right margin */;
                }

                return new Size(requiredWidth, 16);
            }

            return new Size(0, 0);
        }

        private List<RenderItem> _items = new List<RenderItem>();
    }
}
