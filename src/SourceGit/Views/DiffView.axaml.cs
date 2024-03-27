using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

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
            var x = Bounds.Width * Alpha;

            var left = OldImage;
            if (left != null && alpha > 0)
            {
                var src = new Rect(0, 0, left.Size.Width * Alpha, left.Size.Height);
                var dst = new Rect(0, 0, x, Bounds.Height);
                context.DrawImage(left, src, dst);
            }

            var right = NewImage;
            if (right != null)
            {
                var src = new Rect(right.Size.Width * Alpha, 0, right.Size.Width - right.Size.Width * Alpha, right.Size.Height);
                var dst = new Rect(x, 0, Bounds.Width - x, Bounds.Height);
                context.DrawImage(right, src, dst);
            }
            
            context.DrawLine(new Pen(Brushes.DarkGreen, 2), new Point(x, 0), new Point(x, Bounds.Height));
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
            if (img.Width <= available.Width)
            {
                if (img.Height <= available.Height)
                {
                    return img;
                }
                else
                {
                    return new Size(available.Height * img.Width / img.Height, available.Height);
                }
            }
            else
            {
                var s = Math.Max(img.Width / available.Width, img.Height / available.Height);
                return new Size(img.Width / s, img.Height / s);
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