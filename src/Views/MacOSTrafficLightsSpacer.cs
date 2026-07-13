using System;
using Avalonia;
using Avalonia.Controls;

namespace SourceGit.Views
{
    public class MacOSTrafficLightsSpacer : Control
    {
        public static readonly DirectProperty<MacOSTrafficLightsSpacer, double> ZoomProperty =
            AvaloniaProperty.RegisterDirect<MacOSTrafficLightsSpacer, double>(
                nameof(Zoom),
                static o => o.Zoom,
                static (o, v) => o.Zoom = v);

        public double Zoom
        {
            get => _zoom;
            set => SetAndRaise(ZoomProperty, ref _zoom, value);
        }

        public static readonly DirectProperty<MacOSTrafficLightsSpacer, bool> IsFullScreenProperty =
            AvaloniaProperty.RegisterDirect<MacOSTrafficLightsSpacer, bool>(
                nameof(IsFullScreen),
                static o => o.IsFullScreen,
                static (o, v) => o.IsFullScreen = v);

        public bool IsFullScreen
        {
            get => _isFullScreen;
            set => SetAndRaise(IsFullScreenProperty, ref _isFullScreen, value);
        }

        public MacOSTrafficLightsSpacer()
        {
            IsHitTestVisible = false;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ZoomProperty || change.Property == IsFullScreenProperty)
                InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return _isFullScreen ? new Size(4, 24) : new Size(76.0 / Math.Max(_zoom, 1.0), 24.0);
        }

        private double _zoom = 1.0;
        private bool _isFullScreen = false;
    }
}
