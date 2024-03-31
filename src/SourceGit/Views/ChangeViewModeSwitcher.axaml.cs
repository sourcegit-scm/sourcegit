using Avalonia;
using Avalonia.Controls;

namespace SourceGit.Views
{
    public partial class ChangeViewModeSwitcher : UserControl
    {
        public static readonly StyledProperty<Models.ChangeViewMode> ViewModeProperty =
            AvaloniaProperty.Register<ChangeViewModeSwitcher, Models.ChangeViewMode>(nameof(ViewMode));

        public Models.ChangeViewMode ViewMode
        {
            get => GetValue(ViewModeProperty);
            set => SetValue(ViewModeProperty, value);
        }

        public ChangeViewModeSwitcher()
        {
            DataContext = this;
            InitializeComponent();
        }

        public void SwitchMode(object param)
        {
            ViewMode = (Models.ChangeViewMode)param;
        }
    }
}
