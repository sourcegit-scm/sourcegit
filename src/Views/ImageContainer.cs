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
        public static readonly DirectProperty<ImageView, Bitmap> ImageProperty =
            AvaloniaProperty.RegisterDirect<ImageView, Bitmap>(
                nameof(Image),
                static o => o.Image,
                static (o, v) => o.Image = v);

        public Bitmap Image
        {
            get => _image;
            set => SetAndRaise(ImageProperty, ref _image, value);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_image != null)
                context.DrawImage(_image, new Rect(0, 0, Bounds.Width, Bounds.Height));
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ImageProperty)
                InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_image != null)
            {
                var imageSize = _image.Size;
                var scaleW = availableSize.Width / imageSize.Width;
                var scaleH = availableSize.Height / imageSize.Height;
                var scale = Math.Min(1, Math.Min(scaleW, scaleH));
                return new Size(scale * imageSize.Width, scale * imageSize.Height);
            }

            return new Size(0, 0);
        }

        private Bitmap _image = null;
    }

    public class ImageSwipeControl : ImageContainer
    {
        public static readonly DirectProperty<ImageSwipeControl, double> AlphaProperty =
            AvaloniaProperty.RegisterDirect<ImageSwipeControl, double>(
                nameof(Alpha),
                static o => o.Alpha,
                static (o, v) => o.Alpha = v);

        public double Alpha
        {
            get => _alpha;
            set => SetAndRaise(AlphaProperty, ref _alpha, value);
        }

        public static readonly DirectProperty<ImageSwipeControl, Bitmap> OldImageProperty =
            AvaloniaProperty.RegisterDirect<ImageSwipeControl, Bitmap>(
                nameof(OldImage),
                static o => o.OldImage,
                static (o, v) => o.OldImage = v);

        public Bitmap OldImage
        {
            get => _oldImage;
            set => SetAndRaise(OldImageProperty, ref _oldImage, value);
        }

        public static readonly DirectProperty<ImageSwipeControl, Bitmap> NewImageProperty =
            AvaloniaProperty.RegisterDirect<ImageSwipeControl, Bitmap>(
                nameof(NewImage),
                static o => o.NewImage,
                static (o, v) => o.NewImage = v);

        public Bitmap NewImage
        {
            get => _newImage;
            set => SetAndRaise(NewImageProperty, ref _newImage, value);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var w = Bounds.Width;
            var h = Bounds.Height;
            var x = w * _alpha;

            if (_oldImage != null && _alpha > 0)
                RenderSingleSide(context, _oldImage, new Rect(0, 0, x, h));

            if (_newImage != null && _alpha < 1)
                RenderSingleSide(context, _newImage, new Rect(x, 0, w - x, h));

            context.DrawLine(new Pen(Brushes.DarkGreen, 2), new Point(x, 0), new Point(x, Bounds.Height));
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == OldImageProperty ||
                change.Property == NewImageProperty)
                InvalidateMeasure();
            else if (change.Property == AlphaProperty)
                InvalidateVisual();
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
                Alpha = Math.Clamp(p.X, 0, w) / w;
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
            if (_oldImage == null)
                return _newImage == null ? new Size(0, 0) : GetDesiredSize(_newImage.Size, availableSize);

            if (_newImage == null)
                return GetDesiredSize(_oldImage.Size, availableSize);

            var ls = GetDesiredSize(_oldImage.Size, availableSize);
            var rs = GetDesiredSize(_newImage.Size, availableSize);
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

        private Bitmap _oldImage = null;
        private Bitmap _newImage = null;
        private double _alpha = 0.5;
        private bool _pressedOnSlider = false;
        private bool _lastInSlider = false;
    }

    public class ImageBlendControl : ImageContainer
    {
        public static readonly DirectProperty<ImageBlendControl, double> AlphaProperty =
            AvaloniaProperty.RegisterDirect<ImageBlendControl, double>(
                nameof(Alpha),
                static o => o.Alpha,
                static (o, v) => o.Alpha = v);

        public double Alpha
        {
            get => _alpha;
            set => SetAndRaise(AlphaProperty, ref _alpha, value);
        }

        public static readonly DirectProperty<ImageBlendControl, Bitmap> OldImageProperty =
            AvaloniaProperty.RegisterDirect<ImageBlendControl, Bitmap>(
                nameof(OldImage),
                static o => o.OldImage,
                static (o, v) => o.OldImage = v);

        public Bitmap OldImage
        {
            get => _oldImage;
            set => SetAndRaise(OldImageProperty, ref _oldImage, value);
        }

        public static readonly DirectProperty<ImageBlendControl, Bitmap> NewImageProperty =
            AvaloniaProperty.RegisterDirect<ImageBlendControl, Bitmap>(
                nameof(NewImage),
                static o => o.NewImage,
                static (o, v) => o.NewImage = v);

        public Bitmap NewImage
        {
            get => _newImage;
            set => SetAndRaise(NewImageProperty, ref _newImage, value);
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

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == OldImageProperty ||
                change.Property == NewImageProperty)
                InvalidateMeasure();
            else if (change.Property == AlphaProperty)
                InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var left = OldImage;
            var right = NewImage;

            if (left == null)
                return right == null ? new Size(0, 0) : GetDesiredSize(right.Size, availableSize);

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

        private Bitmap _oldImage = null;
        private Bitmap _newImage = null;
        private double _alpha = 0.5;
        private static readonly RenderOptions RO_SRC = new() { BitmapBlendingMode = BitmapBlendingMode.Source, BitmapInterpolationMode = BitmapInterpolationMode.HighQuality };
        private static readonly RenderOptions RO_DST = new() { BitmapBlendingMode = BitmapBlendingMode.Plus, BitmapInterpolationMode = BitmapInterpolationMode.HighQuality };
    }

    public class ImageDifferenceControl : ImageContainer
    {
        public static readonly DirectProperty<ImageDifferenceControl, double> AlphaProperty =
            AvaloniaProperty.RegisterDirect<ImageDifferenceControl, double>(
                nameof(Alpha),
                static o => o.Alpha,
                static (o, v) => o.Alpha = v);

        public double Alpha
        {
            get => _alpha;
            set => SetAndRaise(AlphaProperty, ref _alpha, value);
        }

        public static readonly DirectProperty<ImageDifferenceControl, Bitmap> OldImageProperty =
            AvaloniaProperty.RegisterDirect<ImageDifferenceControl, Bitmap>(
                nameof(OldImage),
                static o => o.OldImage,
                static (o, v) => o.OldImage = v);

        public Bitmap OldImage
        {
            get => _oldImage;
            set => SetAndRaise(OldImageProperty, ref _oldImage, value);
        }

        public static readonly DirectProperty<ImageDifferenceControl, Bitmap> NewImageProperty =
            AvaloniaProperty.RegisterDirect<ImageDifferenceControl, Bitmap>(
                nameof(NewImage),
                static o => o.NewImage,
                static (o, v) => o.NewImage = v);

        public Bitmap NewImage
        {
            get => _newImage;
            set => SetAndRaise(NewImageProperty, ref _newImage, value);
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

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == OldImageProperty ||
                change.Property == NewImageProperty)
                InvalidateMeasure();
            else if (change.Property == AlphaProperty)
                InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var left = OldImage;
            var right = NewImage;

            if (left == null)
                return right == null ? new Size(0, 0) : GetDesiredSize(right.Size, availableSize);

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

        private Bitmap _oldImage = null;
        private Bitmap _newImage = null;
        private double _alpha = 0.5;
        private static readonly RenderOptions RO_SRC = new() { BitmapBlendingMode = BitmapBlendingMode.Source, BitmapInterpolationMode = BitmapInterpolationMode.HighQuality };
        private static readonly RenderOptions RO_DST = new() { BitmapBlendingMode = BitmapBlendingMode.Difference, BitmapInterpolationMode = BitmapInterpolationMode.HighQuality };
    }
}
