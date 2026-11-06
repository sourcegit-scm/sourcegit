using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
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

        public static readonly DirectProperty<ChangeViewModeSwitcher, Models.ChangeSortMode> SortModeProperty =
            AvaloniaProperty.RegisterDirect<ChangeViewModeSwitcher, Models.ChangeSortMode>(
                nameof(SortMode),
                static o => o.SortMode,
                static (o, v) => o.SortMode = v);

        public Models.ChangeSortMode SortMode
        {
            get => _sortMode;
            set => SetAndRaise(SortModeProperty, ref _sortMode, value);
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

        private void SortByPath(object sender, RoutedEventArgs e)
        {
            SortMode = Models.ChangeSortMode.Path;
            e.Handled = true;
        }

        private void SortByStatus(object sender, RoutedEventArgs e)
        {
            SortMode = Models.ChangeSortMode.Status;
            e.Handled = true;
        }

        private Models.ChangeViewMode _viewMode = Models.ChangeViewMode.List;
        private Models.ChangeSortMode _sortMode = Models.ChangeSortMode.Path;
    }
}
