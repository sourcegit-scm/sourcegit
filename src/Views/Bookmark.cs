using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class Bookmark : Control
    {
        public static readonly DirectProperty<Bookmark, int> ValueProperty =
            AvaloniaProperty.RegisterDirect<Bookmark, int>(
                nameof(Value),
                static o => o.Value,
                static (o, v) => o.Value = v);

        public int Value
        {
            get => _value;
            set => SetAndRaise(ValueProperty, ref _value, value);
        }

        public Bookmark()
        {
            IsHitTestVisible = false;
        }

        public override void Render(DrawingContext context)
        {
            if (_icon == null)
                LoadIcon();

            var brush = Models.Bookmarks.Get(_value) ?? (this.FindResource("Brush.FG1") as IBrush);
            var startX = (Bounds.Width - 12.0) * 0.5;
            var startY = (Bounds.Height - 12.0) * 0.5;
            using (context.PushTransform(Matrix.CreateTranslation(startX, startY)))
                context.DrawGeometry(brush, null, _icon);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ValueProperty)
                InvalidateVisual();
            else if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue != null)
                InvalidateVisual();
        }

        private void LoadIcon()
        {
            var geo = this.FindResource("Icons.Bookmark") as StreamGeometry;
            _icon = geo!.Clone();
            var iconBounds = _icon.Bounds;
            var translation = Matrix.CreateTranslation(-(Vector)iconBounds.Position);
            var scale = Math.Min(12.0 / iconBounds.Width, 12.0 / iconBounds.Height);
            var transform = translation * Matrix.CreateScale(scale, scale);
            if (_icon.Transform == null || _icon.Transform.Value == Matrix.Identity)
                _icon.Transform = new MatrixTransform(transform);
            else
                _icon.Transform = new MatrixTransform(_icon.Transform.Value * transform);
        }

        private int _value = 0;
        private Geometry _icon = null;
    }
}
