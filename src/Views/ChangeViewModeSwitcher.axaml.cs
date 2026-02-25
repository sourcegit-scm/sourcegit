using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

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

        public static readonly StyledProperty<Models.ChangeSortMode> SortModeProperty =
            AvaloniaProperty.Register<ChangeViewModeSwitcher, Models.ChangeSortMode>(nameof(SortMode));

        public Models.ChangeSortMode SortMode
        {
            get => GetValue(SortModeProperty);
            set => SetValue(SortModeProperty, value);
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
    }
}
