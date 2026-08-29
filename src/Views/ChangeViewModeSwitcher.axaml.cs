using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DevBoard.Views
{
    public partial class ChangeViewModeSwitcher : UserControl
    {
        public static readonly DirectProperty<ChangeViewModeSwitcher, Models.ChangeViewMode> ViewModeProperty =
            AvaloniaProperty.RegisterDirect<ChangeViewModeSwitcher, Models.ChangeViewMode>(
                nameof(ViewMode),
                static o => o.ViewMode,
                static (o, v) => o.ViewMode = v);

        public Models.ChangeViewMode ViewMode
        {
            get => _viewMode;
            set => SetAndRaise(ViewModeProperty, ref _viewMode, value);
        }

        public ChangeViewModeSwitcher()
        {
            InitializeComponent();
        }

        private void SwitchToList(object sender, RoutedEventArgs e)
        {
            ViewMode = Models.ChangeViewMode.List;
            e.Handled = true;
        }

        private void SwitchToGrid(object sender, RoutedEventArgs e)
        {
            ViewMode = Models.ChangeViewMode.Grid;
            e.Handled = true;
        }

        private void SwitchToTree(object sender, RoutedEventArgs e)
        {
            ViewMode = Models.ChangeViewMode.Tree;
            e.Handled = true;
        }

        private Models.ChangeViewMode _viewMode = Models.ChangeViewMode.List;
    }
}
