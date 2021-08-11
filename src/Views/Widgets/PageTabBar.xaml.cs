using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SourceGit.Views.Widgets {

    /// <summary>
    ///     主窗体标题栏的标签页容器控件
    /// </summary>
    public partial class PageTabBar : UserControl {

        /// <summary>
        ///     标签数据
        /// </summary>
        public class Tab {
            public string Id { get; set; }
            public bool IsWelcomePage { get; set; }
            public string Title { get; set; }
            public string Tooltip { get; set; }
            public int Bookmark { get; set; }
        }

        /// <summary>
        ///     标签相关事件参数
        /// </summary>
        public class TabEventArgs : RoutedEventArgs {
            public string TabId { get; set; }
            public TabEventArgs(RoutedEvent e, object o, string id) : base(e, o) { TabId = id; }
        }

        public static readonly RoutedEvent TabAddEvent = EventManager.RegisterRoutedEvent(
            "TabAdd",
            RoutingStrategy.Bubble,
            typeof(EventHandler<TabEventArgs>),
            typeof(PageTabBar));

        public event RoutedEventHandler TabAdd {
            add { AddHandler(TabAddEvent, value); }
            remove { RemoveHandler(TabAddEvent, value); }
        }

        public static readonly RoutedEvent TabSelectedEvent = EventManager.RegisterRoutedEvent(
            "TabSelected",
            RoutingStrategy.Bubble,
            typeof(EventHandler<TabEventArgs>),
            typeof(PageTabBar));

        public event RoutedEventHandler TabSelected {
            add { AddHandler(TabSelectedEvent, value); }
            remove { RemoveHandler(TabSelectedEvent, value); }
        }

        public static readonly RoutedEvent TabClosedEvent = EventManager.RegisterRoutedEvent(
            "TabClosed",
            RoutingStrategy.Bubble,
            typeof(EventHandler<TabEventArgs>),
            typeof(PageTabBar));

        public event RoutedEventHandler TabClosed {
            add { AddHandler(TabClosedEvent, value); }
            remove { RemoveHandler(TabClosedEvent, value); }
        }

        public ObservableCollection<Tab> Tabs {
            get;
            private set;
        }

        public string Current {
            get { return (container.SelectedItem as Tab).Id; }
        }

        public PageTabBar() {
            Tabs = new ObservableCollection<Tab>();
            InitializeComponent();
        }

        public void Add() {
            NewTab(null, null);
        }

        public void Add(string title, string repo, int bookmark) {
            var tab = new Tab() {
                Id = repo,
                IsWelcomePage = false,
                Title = title,
                Tooltip = repo,
                Bookmark = bookmark,
            };

            Tabs.Add(tab);
            container.SelectedItem = tab;
        }

        public void Replace(string id, string title, string repo, int bookmark) {
            var tab = null as Tab;
            var curTab = container.SelectedItem as Tab;

            foreach (var one in Tabs) {
                if (one.Id == id) {
                    tab = one;
                    break;
                }
            }

            if (tab == null) return;

            var idx = Tabs.IndexOf(tab);
            Tabs.RemoveAt(idx);
            RaiseEvent(new TabEventArgs(TabClosedEvent, this, tab.Id));

            var replaced = new Tab() {
                Id = repo,
                IsWelcomePage = false,
                Title = title,
                Tooltip = repo,
                Bookmark = bookmark,
            };

            Tabs.Insert(idx, replaced);
            if (curTab.Id == id) container.SelectedItem = replaced;
        }

        public bool Goto(string id) {
            foreach (var tab in Tabs) {
                if (tab.Id == id) {
                    container.SelectedItem = tab;
                    return true;
                }
            }

            return false;
        }

        public void Next() {
            container.SelectedIndex = (container.SelectedIndex + 1) % Tabs.Count;
        }

        public void CloseCurrent() {
            var curTab = container.SelectedItem as Tab;
            var idx = container.SelectedIndex;
            Tabs.Remove(curTab);
            if (Tabs.Count == 0) {
                Application.Current.Shutdown();
            } else {
                var last = Tabs.Count - 1;
                var next = idx > last ? Tabs[last] : Tabs[idx];
                container.SelectedItem = next;
                RaiseEvent(new TabEventArgs(TabClosedEvent, this, curTab.Id));
                RaiseEvent(new TabEventArgs(TabSelectedEvent, this, next.Id));
            }
        }

        private void CalcScrollerVisibilty(object sender, SizeChangedEventArgs e) {
            if ((sender as StackPanel).ActualWidth > scroller.ActualWidth) {
                leftScroller.Visibility = Visibility.Visible;
                rightScroller.Visibility = Visibility.Visible;
            } else {
                leftScroller.Visibility = Visibility.Collapsed;
                rightScroller.Visibility = Visibility.Collapsed;
            }
        }

        private void NewTab(object sender, RoutedEventArgs e) {
            var id = Guid.NewGuid().ToString();
            var tab = new Tab() {
                Id = id,
                IsWelcomePage = true,
                Title = App.Text("PageSwitcher.Welcome.Title"),
                Tooltip = App.Text("PageSwitcher.Welcome.Tip"),
                Bookmark = 0,
            };

            Tabs.Add(tab);
            RaiseEvent(new TabEventArgs(TabAddEvent, this, id));
            container.SelectedItem = tab;
        }

        private void ScrollLeft(object sender, RoutedEventArgs e) {
            scroller.LineLeft();
        }

        private void ScrollRight(object sender, RoutedEventArgs e) {
            scroller.LineRight();
        }

        private void SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var tab = container.SelectedItem as Tab;
            if (tab == null) return;
            RaiseEvent(new TabEventArgs(TabSelectedEvent, this, tab.Id));
        }

        private void CloseTab(object sender, RoutedEventArgs e) {
            var tab = (sender as Button).DataContext as Tab;
            if (tab == null) return;

            var curTab = container.SelectedItem as Tab;
            if (curTab != null && tab.Id == curTab.Id) {
                var idx = Tabs.IndexOf(tab);
                Tabs.Remove(tab);

                if (Tabs.Count == 0) {
                    Application.Current.Shutdown();
                    return;
                }

                var last = Tabs.Count - 1;
                var next = idx > last ? Tabs[last] : Tabs[idx];
                container.SelectedItem = next;
                RaiseEvent(new TabEventArgs(TabSelectedEvent, this, next.Id));
            } else {
                Tabs.Remove(tab);
            }

            RaiseEvent(new TabEventArgs(TabClosedEvent, this, tab.Id));
        }

        private void OnMouseMove(object sender, MouseEventArgs e) {
            var item = sender as ListBoxItem;
            if (item == null) return;

            if (Mouse.LeftButton == MouseButtonState.Pressed) {
                var dragging = new Controls.DragDropAdorner(item);
                DragDrop.DoDragDrop(item, item.DataContext, DragDropEffects.Move);
                dragging.Remove();
            }
        }

        private void OnDrop(object sender, DragEventArgs e) {
            var tabSrc = e.Data.GetData(typeof(Tab)) as Tab;
            if (tabSrc == null) return;

            var dst = e.Source as FrameworkElement;
            if (dst == null) return;

            var tabDst = dst.DataContext as Tab;
            if (tabSrc.Id == tabDst.Id) return;

            int dstIdx = Tabs.IndexOf(tabDst);
            Tabs.Remove(tabSrc);
            Tabs.Insert(dstIdx, tabSrc);
            container.SelectedItem = tabSrc;
            e.Handled = true;
        }
    }
}
