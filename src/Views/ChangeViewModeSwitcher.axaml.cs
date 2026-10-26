using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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

        public ChangeViewModeSwitcher()
        {
            InitializeComponent();

            // Submenu parents do not raise Click. Keep selecting Tree on activation,
            // while hover/Right Arrow only opens its compact-folders option.
            TreeModeItem.AddHandler(PointerReleasedEvent, OnTreePointerReleased, RoutingStrategies.Tunnel);
            TreeModeItem.AddHandler(KeyDownEvent, OnTreeKeyDown, RoutingStrategies.Tunnel);
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

        private void OnTreePointerReleased(object sender, PointerReleasedEventArgs e)
        {
            // Submenu events route through their parent too; never select Tree
            // when the compact-folders checkbox is activated.
            if (e.InitialPressMouseButton == MouseButton.Left &&
                TreeModeItem.IsPointerOver &&
                new Rect(TreeModeItem.Bounds.Size).Contains(e.GetPosition(TreeModeItem)))
                SwitchToTree(sender, e);
        }

        private void OnTreeKeyDown(object sender, KeyEventArgs e)
        {
            if (ReferenceEquals(e.Source, TreeModeItem) && (e.Key == Key.Enter || e.Key == Key.Space))
                SwitchToTree(sender, e);
        }

        private void SwitchToTree(object sender, RoutedEventArgs e)
        {
            ViewMode = Models.ChangeViewMode.Tree;
            ModeButton.Flyout.Hide();
            e.Handled = true;
        }

        private Models.ChangeViewMode _viewMode = Models.ChangeViewMode.List;
    }
}
