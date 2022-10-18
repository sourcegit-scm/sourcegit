using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.Views.Widgets {

    /// <summary>
    ///     主窗体标题栏的标签页容器控件
    /// </summary>
    public partial class PageTabBar : UserControl {

        /// <summary>
        ///     标签数据
        /// </summary>
        public class Tab : Controls.BindableBase {
            public string Id { get; set; }
            public bool IsRepository { get; set; }
            
            private string title;
            public string Title {
                get => title;
                set => SetProperty(ref title, value);
            }

            public string Tooltip { get; set; }

            private int bookmark = 0;
            public int Bookmark {
                get => bookmark;
                set => SetProperty(ref bookmark, value);
            }

            private bool isSeperatorVisible = false;
            public bool IsSeperatorVisible {
                get => isSeperatorVisible;
                set => SetProperty(ref isSeperatorVisible, value);
            }
        }

        /// <summary>
        ///     仓库标签页编辑事件参数
        /// </summary>
        public event Action<Tab> OnTabEdited;

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
                IsRepository = true,
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
                IsRepository = true,
                Title = title,
                Tooltip = repo,
                Bookmark = bookmark,
            };

            Tabs.Insert(idx, replaced);
            if (curTab.Id == id) container.SelectedItem = replaced;
        }

        public void Update(string id, int bookmark, string title) {
            foreach (var one in Tabs) {
                if (one.Id == id) {
                    one.Bookmark = bookmark;
                    one.Title = title;
                    break;
                }
            }
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
                startSeperator.Visibility = Visibility.Hidden;
                leftScroller.Visibility = Visibility.Visible;
                rightScroller.Visibility = Visibility.Visible;
            } else {
                leftScroller.Visibility = Visibility.Collapsed;
                rightScroller.Visibility = Visibility.Collapsed;
                if (container.SelectedIndex == 0) {
                    startSeperator.Visibility = Visibility.Hidden;
                } else {
                    startSeperator.Visibility = Visibility.Visible;
                }
            }
        }

        private void NewTab(object sender, RoutedEventArgs e) {
            var id = Guid.NewGuid().ToString();
            var tab = new Tab() {
                Id = id,
                IsRepository = false,
                Title = App.Text("PageTabBar.Welcome.Title"),
                Tooltip = App.Text("PageTabBar.Welcome.Tip"),
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
            UpdateSeperators(tab);
            RaiseEvent(new TabEventArgs(TabSelectedEvent, this, tab.Id));
        }

        private void CloseTab(object sender, RoutedEventArgs e) {
            var tab = (sender as Button).DataContext as Tab;
            if (tab == null) return;
            CloseTab(tab);
        }

        private void CloseTab(Tab tab) {
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
                UpdateSeperators(curTab);
            }
            RaiseEvent(new TabEventArgs(TabClosedEvent, this, tab.Id));
        }

        private void OnMouseMove(object sender, MouseEventArgs e) {
            var item = sender as ListBoxItem;
            if (item == null) return;

            var tab = item.DataContext as Tab;
            if (tab == null || tab != container.SelectedItem) return;

            if (e.LeftButton == MouseButtonState.Pressed) {
                DragDrop.DoDragDrop(item, item.DataContext, DragDropEffects.Move);
            }
        }

        private void OnGiveFeedback(object sender, GiveFeedbackEventArgs e) {
            if (e.Effects == DragDropEffects.Move) {
                e.UseDefaultCursors = false;
                Mouse.SetCursor(Cursors.Hand);
            } else {
                e.UseDefaultCursors = true;
            }

            e.Handled = true;
        }

        private void OnDragOver(object sender, DragEventArgs e) {
            OnDrop(sender, e);
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

        private void OnTabContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var tab = (sender as ListBoxItem).DataContext as Tab;
            if (tab == null) return;

            var menu = new ContextMenu();

            var close = new MenuItem();
            close.Header = App.Text("PageTabBar.Tab.Close");
            close.Click += (_, __) => {
                CloseTab(tab);
            };

            var closeOther = new MenuItem();
            closeOther.Header = App.Text("PageTabBar.Tab.CloseOther");
            closeOther.Click += (_, __) => {
                Tabs.ToList().ForEach(t => { if (tab != t) CloseTab(t); });
            };

            var closeRight = new MenuItem();
            closeRight.Header = App.Text("PageTabBar.Tab.CloseRight");
            closeRight.Click += (_, __) => {
                var tabs = Tabs.ToList();
                tabs.RemoveRange(0, tabs.IndexOf(tab) + 1);
                tabs.ForEach(t => CloseTab(t));
            };

            menu.Items.Add(close);
            menu.Items.Add(closeOther);
            menu.Items.Add(closeRight);

            if (tab.IsRepository) {
                var bookmark = new MenuItem();
                bookmark.Header = App.Text("PageTabBar.Tab.Bookmark");
                for (int i = 0; i < Converters.IntToBookmarkBrush.COLORS.Length; i++) {
                    var icon = new System.Windows.Shapes.Path();
                    icon.Data = new EllipseGeometry(new Point(0, 0), 12, 12);
                    icon.Fill = Converters.IntToBookmarkBrush.COLORS[i];
                    icon.Width = 12;

                    var mark = new MenuItem();
                    mark.Icon = icon;
                    mark.Header = $"{i}";

                    var refIdx = i;
                    mark.Click += (o, ev) => {
                        var repo = Models.Preference.Instance.FindRepository(tab.Id);
                        if (repo != null) {
                            repo.Bookmark = refIdx;
                            tab.Bookmark = refIdx;
                            OnTabEdited?.Invoke(tab);
                        }
                        ev.Handled = true;
                    };
                    bookmark.Items.Add(mark);
                }
                menu.Items.Add(new Separator());
                menu.Items.Add(bookmark);

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("PageTabBar.Tab.CopyPath");
                copyPath.Click += (_, __) => {
                    Clipboard.SetDataObject(tab.Id);
                };
                menu.Items.Add(new Separator());
                menu.Items.Add(copyPath);
            }            

            menu.IsOpen = true;
            e.Handled = true;
        }

        private void UpdateSeperators(Tab actived) {
            int curIdx = 0;
            for (int i = 0; i < Tabs.Count; i++) {
                if (Tabs[i] == actived) {
                    curIdx = i;
                    actived.IsSeperatorVisible = false;
                    if (i > 0) Tabs[i - 1].IsSeperatorVisible = false;
                } else {
                    Tabs[i].IsSeperatorVisible = true;
                }
            }

            if (leftScroller.Visibility == Visibility.Visible || curIdx == 0) {
                startSeperator.Visibility = Visibility.Hidden;
            } else {
                startSeperator.Visibility = Visibility.Visible;
            }
        }
    }
}
