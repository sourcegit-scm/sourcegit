using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace SourceGit.Views
{
    public class ImageContainer : Control
    {
        public override void Render(DrawingContext context)
        {
            if (_bgBrush == null)
            {
                var maskBrush = new SolidColorBrush(ActualThemeVariant == ThemeVariant.Dark ? 0xFF404040 : 0xFFBBBBBB);
                var bg = new DrawingGroup()
                {
                    Children =
                    {
                        new GeometryDrawing() { Brush = maskBrush, Geometry = new RectangleGeometry(new Rect(0, 0, 12, 12)) },
                        new GeometryDrawing() { Brush = maskBrush, Geometry = new RectangleGeometry(new Rect(12, 12, 12, 12)) },
                    }
                };

                _bgBrush = new DrawingBrush(bg)
                {
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top,
                    DestinationRect = new RelativeRect(new Size(24, 24), RelativeUnit.Absolute),
                    Stretch = Stretch.None,
                    TileMode = TileMode.Tile,
                };
            }

            context.FillRectangle(_bgBrush, new Rect(Bounds.Size));
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue != null)
            {
                _bgBrush = null;
                InvalidateVisual();
            }
        }

        private DrawingBrush _bgBrush = null;
    }

    public class ImageView : ImageContainer
    {
        public static readonly StyledProperty<Bitmap> ImageProperty =
            AvaloniaProperty.Register<ImageView, Bitmap>(nameof(Image));

        public Bitmap Image
        {
            get => GetValue(ImageProperty);
            set => SetValue(ImageProperty, value);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (Image is { } image)
                context.DrawImage(image, new Rect(0, 0, Bounds.Width, Bounds.Height));
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ImageProperty)
                InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Image is { } image)
            {
                var imageSize = image.Size;
                var scaleW = availableSize.Width / imageSize.Width;
                var scaleH = availableSize.Height / imageSize.Height;
                var scale = Math.Min(1, Math.Min(scaleW, scaleH));
                return new Size(scale * imageSize.Width, scale * imageSize.Height);
            }

            return availableSize;
        }
    }

    public class ImageSwipeControl : ImageContainer
    {
        public static readonly StyledProperty<double> AlphaProperty =
            AvaloniaProperty.Register<ImageSwipeControl, double>(nameof(Alpha), 0.5);

        public double Alpha
        {
            get => GetValue(AlphaProperty);
            set => SetValue(AlphaProperty, value);
        }

        public static readonly StyledProperty<Bitmap> OldImageProperty =
            AvaloniaProperty.Register<ImageSwipeControl, Bitmap>(nameof(OldImage));

        public Bitmap OldImage
        {
            get => GetValue(OldImageProperty);
            set => SetValue(OldImageProperty, value);
        }

        public static readonly StyledProperty<Bitmap> NewImageProperty =
            AvaloniaProperty.Register<ImageSwipeControl, Bitmap>(nameof(NewImage));

        public Bitmap NewImage
        {
            get => GetValue(NewImageProperty);
            set => SetValue(NewImageProperty, value);
        }

        static ImageSwipeControl()
        {
            AffectsMeasure<ImageSwipeControl>(OldImageProperty, NewImageProperty);
            AffectsRender<ImageSwipeControl>(AlphaProperty);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var alpha = Alpha;
            var w = Bounds.Width;
            var h = Bounds.Height;
            var x = w * alpha;

            if (OldImage is { } left && alpha > 0)
                RenderSingleSide(context, left, new Rect(0, 0, x, h));

            if (NewImage is { } right && alpha < 1)
                RenderSingleSide(context, right, new Rect(x, 0, w - x, h));

            context.DrawLine(new Pen(Brushes.DarkGreen, 2), new Point(x, 0), new Point(x, Bounds.Height));
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            var p = e.GetPosition(this);
            var hitbox = new Rect(Math.Max(Bounds.Width * Alpha - 2, 0), 0, 4, Bounds.Height);
            var pointer = e.GetCurrentPoint(this);
            if (pointer.Properties.IsLeftButtonPressed && hitbox.Contains(p))
            {
                _pressedOnSlider = true;
                Cursor = new Cursor(StandardCursorType.SizeWestEast);
                e.Pointer.Capture(this);
                e.Handled = true;
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            _pressedOnSlider = false;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            var w = Bounds.Width;
            var p = e.GetPosition(this);

            if (_pressedOnSlider)
            {
                SetCurrentValue(AlphaProperty, Math.Clamp(p.X, 0, w) / w);
            }
            else
            {
                var hitbox = new Rect(Math.Max(w * Alpha - 2, 0), 0, 4, Bounds.Height);
                if (hitbox.Contains(p))
                {
                    if (!_lastInSlider)
                    {
                        _lastInSlider = true;
                        Cursor = new Cursor(StandardCursorType.SizeWestEast);
                    }
                }
                else
                {
                    if (_lastInSlider)
                    {
                        _lastInSlider = false;
                        Cursor = null;
                    }
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var left = OldImage;
            var right = NewImage;

            if (left == null)
                return right == null ? availableSize : GetDesiredSize(right.Size, availableSize);

            if (right == null)
                return GetDesiredSize(left.Size, availableSize);

            var ls = GetDesiredSize(left.Size, availableSize);
            var rs = GetDesiredSize(right.Size, availableSize);
            return ls.Width > rs.Width ? ls : rs;
        }

        private Size GetDesiredSize(Size img, Size available)
        {
            var sw = available.Width / img.Width;
            var sh = available.Height / img.Height;
            var scale = Math.Min(1, Math.Min(sw, sh));
            return new Size(scale * img.Width, scale * img.Height);
        }

        private void RenderSingleSide(DrawingContext context, Bitmap img, Rect clip)
        {
            var w = Bounds.Width;
            var h = Bounds.Height;

            var imgW = img.Size.Width;
            var imgH = img.Size.Height;
            var scale = Math.Min(1, Math.Min(w / imgW, h / imgH));

            var scaledW = img.Size.Width * scale;
            var scaledH = img.Size.Height * scale;

            var src = new Rect(0, 0, imgW, imgH);
            var dst = new Rect((w - scaledW) * 0.5, (h - scaledH) * 0.5, scaledW, scaledH);

            using (context.PushClip(clip))
                context.DrawImage(img, src, dst);
        }

        private bool _pressedOnSlider = false;
        private bool _lastInSlider = false;
    }

    public class ImageBlendControl : ImageContainer
    {
        public static readonly StyledProperty<double> AlphaProperty =
            AvaloniaProperty.Register<ImageBlendControl, double>(nameof(Alpha), 1.0);

        public double Alpha
        {
            get => GetValue(AlphaProperty);
            set => SetValue(AlphaProperty, value);
        }

        public static readonly StyledProperty<Bitmap> OldImageProperty =
            AvaloniaProperty.Register<ImageBlendControl, Bitmap>(nameof(OldImage));

        public Bitmap OldImage
        {
            get => GetValue(OldImageProperty);
            set => SetValue(OldImageProperty, value);
        }

        public static readonly StyledProperty<Bitmap> NewImageProperty =
            AvaloniaProperty.Register<ImageBlendControl, Bitmap>(nameof(NewImage));

        public Bitmap NewImage
        {
            get => GetValue(NewImageProperty);
            set => SetValue(NewImageProperty, value);
        }

        static ImageBlendControl()
        {
            AffectsMeasure<ImageBlendControl>(OldImageProperty, NewImageProperty);
            AffectsRender<ImageBlendControl>(AlphaProperty);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var alpha = Alpha;
            var left = OldImage;
            var right = NewImage;
            var drawLeft = left != null && alpha < 1.0;
            var drawRight = right != null && alpha > 0;

            if (drawLeft && drawRight)
            {
                using (var rt = new RenderTargetBitmap(new PixelSize((int)Bounds.Width, (int)Bounds.Height), right.Dpi))
                {
                    using (var dc = rt.CreateDrawingContext())
                    {
                        using (dc.PushRenderOptions(RO_SRC))
                            RenderSingleSide(dc, left, rt.Size.Width, rt.Size.Height, 1 - alpha);

                        using (dc.PushRenderOptions(RO_DST))
                            RenderSingleSide(dc, right, rt.Size.Width, rt.Size.Height, alpha);
                    }

                    context.DrawImage(rt, new Rect(0, 0, Bounds.Width, Bounds.Height));
                }
            }
            else if (drawLeft)
            {
                RenderSingleSide(context, left, Bounds.Width, Bounds.Height, 1 - alpha);
            }
            else if (drawRight)
            {
                RenderSingleSide(context, right, Bounds.Width, Bounds.Height, alpha);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var left = OldImage;
            var right = NewImage;

            if (left == null)
                return right == null ? availableSize : GetDesiredSize(right.Size, availableSize);

            if (right == null)
                return GetDesiredSize(left.Size, availableSize);

            var ls = GetDesiredSize(left.Size, availableSize);
            var rs = GetDesiredSize(right.Size, availableSize);
            return ls.Width > rs.Width ? ls : rs;
        }

        private Size GetDesiredSize(Size img, Size available)
        {
            var sw = available.Width / img.Width;
            var sh = available.Height / img.Height;
            var scale = Math.Min(1, Math.Min(sw, sh));
            return new Size(scale * img.Width, scale * img.Height);
        }

        private void RenderSingleSide(DrawingContext context, Bitmap img, double w, double h, double alpha)
        {
            var imgW = img.Size.Width;
            var imgH = img.Size.Height;
            var scale = Math.Min(1, Math.Min(w / imgW, h / imgH));

            var scaledW = img.Size.Width * scale;
            var scaledH = img.Size.Height * scale;

            var src = new Rect(0, 0, imgW, imgH);
            var dst = new Rect((w - scaledW) * 0.5, (h - scaledH) * 0.5, scaledW, scaledH);

            using (context.PushOpacity(alpha))
                context.DrawImage(img, src, dst);
        }

        private static readonly RenderOptions RO_SRC = new RenderOptions() { BitmapBlendingMode = BitmapBlendingMode.Source, BitmapInterpolationMode = BitmapInterpolationMode.HighQuality };
        private static readonly RenderOptions RO_DST = new RenderOptions() { BitmapBlendingMode = BitmapBlendingMode.Plus, BitmapInterpolationMode = BitmapInterpolationMode.HighQuality };
    }

    public class ImageDifferenceControl : ImageContainer
    {
        public static readonly StyledProperty<double> AlphaProperty =
            AvaloniaProperty.Register<ImageDifferenceControl, double>(nameof(Alpha), 1.0);

        public double Alpha
        {
            get => GetValue(AlphaProperty);
            set => SetValue(AlphaProperty, value);
        }

        public static readonly StyledProperty<Bitmap> OldImageProperty =
            AvaloniaProperty.Register<ImageDifferenceControl, Bitmap>(nameof(OldImage));

        public Bitmap OldImage
        {
            get => GetValue(OldImageProperty);
            set => SetValue(OldImageProperty, value);
        }

        public static readonly StyledProperty<Bitmap> NewImageProperty =
            AvaloniaProperty.Register<ImageDifferenceControl, Bitmap>(nameof(NewImage));

        public Bitmap NewImage
        {
            get => GetValue(NewImageProperty);
            set => SetValue(NewImageProperty, value);
        }

        static ImageDifferenceControl()
        {
            AffectsMeasure<ImageDifferenceControl>(OldImageProperty, NewImageProperty);
            AffectsRender<ImageDifferenceControl>(AlphaProperty);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var alpha = Alpha;
            var left = OldImage;
            var right = NewImage;
            var drawLeft = left != null && alpha < 1.0;
            var drawRight = right != null && alpha > 0.0;

            if (drawLeft && drawRight)
            {
                using (var rt = new RenderTargetBitmap(new PixelSize((int)Bounds.Width, (int)Bounds.Height), right.Dpi))
                {
                    using (var dc = rt.CreateDrawingContext())
                    {
                        using (dc.PushRenderOptions(RO_SRC))
                            RenderSingleSide(dc, left, rt.Size.Width, rt.Size.Height, Math.Min(1.0, 2.0 - 2.0 * alpha));

                        using (dc.PushRenderOptions(RO_DST))
                            RenderSingleSide(dc, right, rt.Size.Width, rt.Size.Height, Math.Min(1.0, 2.0 * alpha));
                    }

                    context.DrawImage(rt, new Rect(0, 0, Bounds.Width, Bounds.Height));
                }
            }
            else if (drawLeft)
            {
                RenderSingleSide(context, left, Bounds.Width, Bounds.Height, 1 - alpha);
            }
            else if (drawRight)
            {
                RenderSingleSide(context, right, Bounds.Width, Bounds.Height, alpha);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var left = OldImage;
            var right = NewImage;

            if (left == null)
                return right == null ? availableSize : GetDesiredSize(right.Size, availableSize);

            if (right == null)
                return GetDesiredSize(left.Size, availableSize);

            var ls = GetDesiredSize(left.Size, availableSize);
            var rs = GetDesiredSize(right.Size, availableSize);
            return ls.Width > rs.Width ? ls : rs;
        }

        private Size GetDesiredSize(Size img, Size available)
        {
            var sw = available.Width / img.Width;
            var sh = available.Height / img.Height;
            var scale = Math.Min(1, Math.Min(sw, sh));
            return new Size(scale * img.Width, scale * img.Height);
        }

        private void RenderSingleSide(DrawingContext context, Bitmap img, double w, double h, double alpha)
        {
            var imgW = img.Size.Width;
            var imgH = img.Size.Height;
            var scale = Math.Min(1, Math.Min(w / imgW, h / imgH));

            var scaledW = img.Size.Width * scale;
            var scaledH = img.Size.Height * scale;

            var src = new Rect(0, 0, imgW, imgH);
            var dst = new Rect((w - scaledW) * 0.5, (h - scaledH) * 0.5, scaledW, scaledH);

            using (context.PushOpacity(alpha))
                context.DrawImage(img, src, dst);
        }

        private static readonly RenderOptions RO_SRC = new RenderOptions() { BitmapBlendingMode = BitmapBlendingMode.Source, BitmapInterpolationMode = BitmapInterpolationMode.HighQuality };
        private static readonly RenderOptions RO_DST = new RenderOptions() { BitmapBlendingMode = BitmapBlendingMode.Difference, BitmapInterpolationMode = BitmapInterpolationMode.HighQuality };
    }
}
