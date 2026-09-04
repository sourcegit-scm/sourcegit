using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class RepositoryViewSwitcher : UserControl
    {
        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<RepositoryViewSwitcher, Orientation>(nameof(Orientation), Orientation.Vertical);

        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public static readonly StyledProperty<bool> ShowInlineActionsProperty =
            AvaloniaProperty.Register<RepositoryViewSwitcher, bool>(nameof(ShowInlineActions), true);

        public bool ShowInlineActions
        {
            get => GetValue(ShowInlineActionsProperty);
            set => SetValue(ShowInlineActionsProperty, value);
        }

        public static readonly StyledProperty<double> ItemMinWidthProperty =
            AvaloniaProperty.Register<RepositoryViewSwitcher, double>(nameof(ItemMinWidth));

        public double ItemMinWidth
        {
            get => GetValue(ItemMinWidthProperty);
            set => SetValue(ItemMinWidthProperty, value);
        }

        public RepositoryViewSwitcher()
        {
            InitializeComponent();
            ViewSelector.AddHandler(PointerPressedEvent, OnViewSelectorPointerPressed, RoutingStrategies.Tunnel);
        }

        private void OnViewSelectorPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(ViewSelector).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
                e.Handled = true;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.FindAncestorOfType<Repository>()?.CloseCompactSidebar();
            e.Handled = true;
        }

        private void OnRepositoryViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            this.FindAncestorOfType<Repository>()?.OpenRepositoryViewContextMenu(sender, e, !ShowInlineActions);
        }

        private void OnOpenAdvancedHistoriesOption(object sender, RoutedEventArgs e)
        {
            this.FindAncestorOfType<Repository>()?.OpenAdvancedHistoriesOption(sender, e);
        }
    }
}
