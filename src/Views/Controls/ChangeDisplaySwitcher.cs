using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SourceGit.Views.Controls {
    /// <summary>
    ///     用于切换变更显示模式的按钮
    /// </summary>
    public class ChangeDisplaySwitcher : Button {

        public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
            "Mode",
            typeof(Models.Change.DisplayMode),
            typeof(ChangeDisplaySwitcher),
            new PropertyMetadata(Models.Change.DisplayMode.Tree, OnModeChanged));

        public Models.Change.DisplayMode Mode {
            get { return (Models.Change.DisplayMode)GetValue(ModeProperty); }
            set { SetValue(ModeProperty, value); }
        }

        public static readonly RoutedEvent ModeChangedEvent = EventManager.RegisterRoutedEvent(
            "ModeChanged",
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(ChangeDisplaySwitcher));

        public event RoutedEventHandler ModeChanged {
            add { AddHandler(ModeChangedEvent, value); }
            remove { RemoveHandler(ModeChangedEvent, value); }
        }

        private Path icon = null;

        public ChangeDisplaySwitcher() {
            icon = new Path();
            icon.Fill = FindResource("Brush.FG2") as Brush;
            icon.Data = FindResource("Icon.Tree") as Geometry;

            Content = icon;
            Style = FindResource("Style.Button") as Style;
            BorderThickness = new Thickness(0);
            ToolTip = App.Text("ChangeDisplayMode");

            Click += OnClicked;
        }

        private void OnClicked(object sender, RoutedEventArgs e) {
            if (ContextMenu != null) {
                ContextMenu.IsOpen = true;
                e.Handled = true;
                return;
            }

            var menu = new ContextMenu();
            menu.Placement = PlacementMode.Bottom;
            menu.PlacementTarget = this;
            menu.StaysOpen = false;
            menu.Focusable = true;

            FillMenu(menu, "ChangeDisplayMode.Tree", "Icon.Tree", Models.Change.DisplayMode.Tree);
            FillMenu(menu, "ChangeDisplayMode.List", "Icon.List", Models.Change.DisplayMode.List);
            FillMenu(menu, "ChangeDisplayMode.Grid", "Icon.Grid", Models.Change.DisplayMode.Grid);

            ContextMenu = menu;
            ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void FillMenu(ContextMenu menu, string header, string icon, Models.Change.DisplayMode useMode) {
            var iconMode = new Path();
            iconMode.Width = 12;
            iconMode.Height = 12;
            iconMode.Fill = FindResource("Brush.FG2") as Brush;
            iconMode.Data = FindResource(icon) as Geometry;

            var item = new MenuItem();
            item.Icon = iconMode;
            item.Header = App.Text(header);
            item.Click += (o, e) => Mode = useMode;

            menu.Items.Add(item);
        }

        private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var elem = d as ChangeDisplaySwitcher;
            if (elem != null) {
                switch (elem.Mode) {
                case Models.Change.DisplayMode.Tree:
                    elem.icon.Data = elem.FindResource("Icon.Tree") as Geometry;
                    break;
                case Models.Change.DisplayMode.List:
                    elem.icon.Data = elem.FindResource("Icon.List") as Geometry;
                    break;
                case Models.Change.DisplayMode.Grid:
                    elem.icon.Data = elem.FindResource("Icon.Grid") as Geometry;
                    break;
                }
                elem.RaiseEvent(new RoutedEventArgs(ModeChangedEvent));
            }
        }
    }
}
