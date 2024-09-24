using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace SourceGit.Views
{
    public partial class Statistics : ChromelessWindow
    {
        public static readonly StyledProperty<uint> SampleFillColorProperty =
            AvaloniaProperty.Register<Statistics, uint>(nameof(SampleFillColor));

        public uint SampleFillColor
        {
            get => GetValue(SampleFillColorProperty);
            set => SetValue(SampleFillColorProperty, value);
        }

        public static readonly StyledProperty<IBrush> SampleFillBrushProperty =
            AvaloniaProperty.Register<Statistics, IBrush>(nameof(SampleFillBrush), Brushes.Transparent);

        public IBrush SampleFillBrush
        {
            get => GetValue(SampleFillBrushProperty);
            set => SetValue(SampleFillBrushProperty, value);
        }

        public Statistics()
        {
            SampleFillColor = ViewModels.Preference.Instance.StatisticsSampleColor;
            SampleFillBrush = new SolidColorBrush(SampleFillColor);
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SampleFillColorProperty)
                ChangeColor(SampleFillColor);
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }

        private void OnReportChanged(object sender, System.EventArgs e)
        {
            if (DataContext is ViewModels.Statistics { SelectedReport: Models.StatisticsReport report })
                report.ChangeColor(SampleFillColor);
        }

        private void ChangeColor(uint color)
        {
            if (color != ViewModels.Preference.Instance.StatisticsSampleColor)
            {
                ViewModels.Preference.Instance.StatisticsSampleColor = color;
                SetCurrentValue(SampleFillBrushProperty, new SolidColorBrush(color));

                if (DataContext is ViewModels.Statistics { SelectedReport: Models.StatisticsReport report })
                    report.ChangeColor(color);
            }
        }
    }
}
