using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace SourceGit.Views
{
    public class ImageDiffView : Control
    {
        public static readonly StyledProperty<double> AlphaProperty =
            AvaloniaProperty.Register<ImageDiffView, double>(nameof(Alpha), 0.5);

        public double Alpha
        {
            get => GetValue(AlphaProperty);
            set => SetValue(AlphaProperty, value);
        }

        public static readonly StyledProperty<Bitmap> OldImageProperty =
            AvaloniaProperty.Register<ImageDiffView, Bitmap>(nameof(OldImage), null);

        public Bitmap OldImage
        {
            get => GetValue(OldImageProperty);
            set => SetValue(OldImageProperty, value);
        }

        public static readonly StyledProperty<Bitmap> NewImageProperty =
            AvaloniaProperty.Register<ImageDiffView, Bitmap>(nameof(NewImage), null);

        public Bitmap NewImage
        {
            get => GetValue(NewImageProperty);
            set => SetValue(NewImageProperty, value);
        }

        static ImageDiffView()
        {
            AffectsMeasure<ImageDiffView>(OldImageProperty, NewImageProperty);
            AffectsRender<ImageDiffView>(AlphaProperty);
        }

        public override void Render(DrawingContext context)
        {
            var alpha = Alpha;
            var bgMaskBrush = new SolidColorBrush(ActualThemeVariant == ThemeVariant.Dark ? 0xFF404040 : 0xFFBBBBBB);

            var bg = new DrawingGroup()
            {
                Children =
                {
                    new GeometryDrawing() { Brush = bgMaskBrush, Geometry = new RectangleGeometry(new Rect(0, 0, 12, 12)) },
                    new GeometryDrawing() { Brush = bgMaskBrush, Geometry = new RectangleGeometry(new Rect(12, 12, 12, 12)) },
                }
            };

            var brushBG = new DrawingBrush(bg)
            {
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
                DestinationRect = new RelativeRect(new Size(24, 24), RelativeUnit.Absolute),
                Stretch = Stretch.None,
                TileMode = TileMode.Tile,
            };

            context.FillRectangle(brushBG, new Rect(Bounds.Size));

            var left = OldImage;
            if (left != null && alpha > 0)
            {
                var src = new Rect(0, 0, left.Size.Width * Alpha, left.Size.Height);
                var dst = new Rect(8, 8, (Bounds.Width - 16) * Alpha, Bounds.Height - 16);
                context.DrawImage(left, src, dst);
            }

            var right = NewImage;
            if (right != null)
            {
                var src = new Rect(right.Size.Width * Alpha, 0, right.Size.Width - right.Size.Width * Alpha, right.Size.Height);
                var dst = new Rect((Bounds.Width - 16) * Alpha + 8, 8, (Bounds.Width - 16) * (1 - Alpha), Bounds.Height - 16);
                context.DrawImage(right, src, dst);
            }

            var x = (Bounds.Width - 16) * Alpha + 8;
            context.DrawLine(new Pen(Brushes.DarkGreen, 2), new Point(x, 0), new Point(x, Bounds.Height));
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property.Name == "ActualThemeVariant") InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var left = OldImage;
            var right = NewImage;

            if (left != null)
            {
                return GetDesiredSize(left.Size, availableSize);
            }
            else if (right != null)
            {
                return GetDesiredSize(right.Size, availableSize);
            }
            else
            {
                return availableSize;
            }
        }

        private Size GetDesiredSize(Size img, Size available)
        {
            var w = available.Width - 16;
            var h = available.Height - 16;

            if (img.Width <= w)
            {
                if (img.Height <= h)
                {
                    return new Size(img.Width + 16, img.Height + 16);
                }
                else
                {
                    return new Size(h * img.Width / img.Height + 16, available.Height);
                }
            }
            else
            {
                var s = Math.Max(img.Width / w, img.Height / h);
                return new Size(img.Width / s + 16, img.Height / s + 16);
            }
        }
    }

    public partial class DiffView : UserControl
    {
        public DiffView()
        {
            InitializeComponent();
        }
    }
}