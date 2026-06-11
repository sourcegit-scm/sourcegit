using Avalonia;
using Avalonia.Controls;

namespace SourceGit.Views
{
    public class MacOSTrafficLightsSpacer : Control
    {
        public static readonly StyledProperty<double> ZoomProperty =
            AvaloniaProperty.Register<MacOSTrafficLightsSpacer, double>(nameof(Zoom), 1.0);

        public double Zoom
        {
            get => GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        public MacOSTrafficLightsSpacer()
        {
            IsHitTestVisible = false;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ZoomProperty)
                InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(76.0 / Zoom, 24.0);
        }
    }
}
