using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.Helpers {

    /// <summary>
    ///     Helper class to enable multi-selection of TreeView
    /// </summary>
    public static class TreeViewHelper {

        /// <summary>
        ///     Definition of EnableMultiSelection property.
        /// </summary>
        public static readonly DependencyProperty EnableMultiSelectionProperty =
            DependencyProperty.RegisterAttached(
                "EnableMultiSelection", 
                typeof(bool), 
                typeof(TreeViewHelper), 
                new FrameworkPropertyMetadata(false, OnEnableMultiSelectionChanged));

        /// <summary>
        ///     Getter of EnableMultiSelection
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static bool GetEnableMultiSelection(DependencyObject obj) {
            return (bool)obj.GetValue(EnableMultiSelectionProperty);
        }

        /// <summary>
        ///     Setter of EnableMultiSelection
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="value"></param>
        public static void SetEnableMultiSelection(DependencyObject obj, bool value) {
            obj.SetValue(EnableMultiSelectionProperty, value);
        }

        /// <summary>
        ///     Definition of SelectedItems
        /// </summary>
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItems", 
                typeof(ObservableCollection<TreeViewItem>), 
                typeof(TreeViewHelper), 
                new FrameworkPropertyMetadata(null));

        /// <summary>
        ///     Getter of SelectedItems
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static ObservableCollection<TreeViewItem> GetSelectedItems(DependencyObject obj) {
            return (ObservableCollection<TreeViewItem>)obj.GetValue(SelectedItemsProperty);
        }

        /// <summary>
        ///     Setter of SelectedItems
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="value"></param>
        public static void SetSelectedItems(DependencyObject obj, ObservableCollection<TreeViewItem> value) {
            obj.SetValue(SelectedItemsProperty, value);
        }

        /// <summary>
        ///     Definition of IsChecked property.
        /// </summary>
        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.RegisterAttached(
                "IsChecked", 
                typeof(bool), 
                typeof(TreeViewHelper), 
                new FrameworkPropertyMetadata(false));

        /// <summary>
        ///     Getter of IsChecked Property.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static bool GetIsChecked(DependencyObject obj) {
            return (bool)obj.GetValue(IsCheckedProperty);
        }

        /// <summary>
        ///     Setter of IsChecked property
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="value"></param>
        public static void SetIsChecked(DependencyObject obj, bool value) {
            obj.SetValue(IsCheckedProperty, value);
        }

        /// <summary>
        ///     Definition of MultiSelectionChangedEvent
        /// </summary>
        public static readonly RoutedEvent MultiSelectionChangedEvent =
            EventManager.RegisterRoutedEvent("MultiSelectionChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TreeViewHelper)); 

        /// <summary>
        ///     Add handler for MultiSelectionChanged event.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="handler"></param>
        public static void AddMultiSelectionChangedHandler(DependencyObject d, RoutedEventHandler handler) {
            var tree = d as TreeView;
            if (tree != null) tree.AddHandler(MultiSelectionChangedEvent, handler);
        }

        /// <summary>
        ///     Remove handler for MultiSelectionChanged event.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="handler"></param>
        public static void RemoveMultiSelectionChangedHandler(DependencyObject d, RoutedEventHandler handler) {
            var tree = d as TreeView;
            if (tree != null) tree.RemoveHandler(MultiSelectionChangedEvent, handler);
        }

        /// <summary>
        ///     Find ScrollViewer of a tree view
        /// </summary>
        /// <param name="owner"></param>
        /// <returns></returns>
        public static ScrollViewer GetScrollViewer(FrameworkElement owner) {
            if (owner == null) return null;
            if (owner is ScrollViewer) return owner as ScrollViewer;

            int n = VisualTreeHelper.GetChildrenCount(owner);
            for (int i = 0; i < n; i++) {
                var child = VisualTreeHelper.GetChild(owner, i) as FrameworkElement;
                var deep = GetScrollViewer(child);
                if (deep != null) return deep;
            }

            return null;
        }

        /// <summary>
        ///     Select all items in tree.
        /// </summary>
        /// <param name="tree"></param>
        public static void SelectWholeTree(TreeView tree) {
            var selected = GetSelectedItems(tree);
            selected.Clear();
            SelectAll(selected, tree);
            tree.RaiseEvent(new RoutedEventArgs(MultiSelectionChangedEvent));
        }

        /// <summary>
        ///     Selected one item by DataContext
        /// </summary>
        /// <param name="tree"></param>
        /// <param name="obj"></param>
        public static void SelectOneByContext(TreeView tree, object obj) {
            var item = FindTreeViewItemByDataContext(tree, obj);
            if (item != null) {
                var selected = GetSelectedItems(tree);
                selected.Add(item);
                item.SetValue(IsCheckedProperty, true);
                tree.RaiseEvent(new RoutedEventArgs(MultiSelectionChangedEvent));
            }
        }

        /// <summary>
        ///     Unselect the whole tree.
        /// </summary>
        /// <param name="tree"></param>
        public static void UnselectTree(TreeView tree) {
            var selected = GetSelectedItems(tree);
            if (selected.Count == 0) return;

            foreach (var old in selected) old.SetValue(IsCheckedProperty, false);
            selected.Clear();
            tree.RaiseEvent(new RoutedEventArgs(MultiSelectionChangedEvent));
        }

        /// <summary>
        ///     Hooks when EnableMultiSelection changed.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnEnableMultiSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var tree = d as TreeView;
            if (tree != null && (bool)e.NewValue) {
                tree.SetValue(SelectedItemsProperty, new ObservableCollection<TreeViewItem>());
                tree.PreviewMouseDown += OnTreeMouseDown;
            }
        }

        /// <summary>
        ///     Preview mouse button select.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void OnTreeMouseDown(object sender, MouseButtonEventArgs e) {
            var tree = sender as TreeView;
            if (tree == null) return;

            var hit = VisualTreeHelper.HitTest(tree, e.GetPosition(tree));
            if (hit == null || hit.VisualHit is null) return;

            var item = FindTreeViewItem(hit.VisualHit as UIElement);
            if (item == null) return;

            var selected = GetSelectedItems(tree);
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) {
                if (GetIsChecked(item)) {
                    selected.Remove(item);
                    item.SetValue(IsCheckedProperty, false);
                } else {
                    selected.Add(item);
                    item.SetValue(IsCheckedProperty, true);
                }
            } else if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && selected.Count > 0) {
                var last = selected.Last();
                if (last == item) return;

                var lastPos = last.PointToScreen(new Point(0, 0));
                var curPos = item.PointToScreen(new Point(0, 0));
                if (lastPos.Y > curPos.Y) {
                    SelectRange(selected, tree, item, last);
                } else {
                    SelectRange(selected, tree, last, item);
                }
                
                selected.Add(item);
                item.SetValue(IsCheckedProperty, true);
            } else if (e.RightButton == MouseButtonState.Pressed) {
                if (GetIsChecked(item)) return;

                foreach (var old in selected) old.SetValue(IsCheckedProperty, false);
                selected.Clear();
                selected.Add(item);
                item.SetValue(IsCheckedProperty, true);
            } else {
                foreach (var old in selected) old.SetValue(IsCheckedProperty, false);
                selected.Clear();
                selected.Add(item);
                item.SetValue(IsCheckedProperty, true);
            }

            tree.RaiseEvent(new RoutedEventArgs(MultiSelectionChangedEvent));
        }

        /// <summary>
        ///     Find TreeViewItem by child element.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="child"></param>
        /// <returns></returns>
        public static TreeViewItem FindTreeViewItem(DependencyObject child) {
            if (child == null) return null;
            if (child is TreeViewItem) return child as TreeViewItem;
            if (child is TreeView) return null;
            return FindTreeViewItem(VisualTreeHelper.GetParent(child));
        }

        /// <summary>
        ///     Find TreeViewItem by DataContext
        /// </summary>
        /// <param name="control"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        private static TreeViewItem FindTreeViewItemByDataContext(ItemsControl control, object obj) {
            if (control == null) return null;
            if (control.DataContext == obj) return control as TreeViewItem;            

            for (int i = 0; i < control.Items.Count; i++) {
                var child = control.ItemContainerGenerator.ContainerFromIndex(i) as ItemsControl;
                var found = FindTreeViewItemByDataContext(child, obj);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        ///     Select all items.
        /// </summary>
        /// <param name="selected"></param>
        /// <param name="control"></param>
        private static void SelectAll(ObservableCollection<TreeViewItem> selected, ItemsControl control) {
            for (int i = 0; i < control.Items.Count; i++) {
                var child = control.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (child == null) continue;

                selected.Add(child);
                child.SetValue(IsCheckedProperty, true);
                SelectAll(selected, child);
            }
        }

        /// <summary>
        ///     Select range items between given.
        /// </summary>
        /// <param name="selected"></param>
        /// <param name="control"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="started"></param>
        private static int SelectRange(ObservableCollection<TreeViewItem> selected, ItemsControl control, TreeViewItem from, TreeViewItem to, int matches = 0) {
            for (int i = 0; i < control.Items.Count; i++) {
                var child = control.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (child == null) continue;

                if (matches == 1) {
                    if (child == to) return 2;
                    selected.Add(child);
                    child.SetValue(IsCheckedProperty, true);
                    if (TryEndRangeSelection(selected, child, to)) return 2;
                } else if (child == from) {
                    matches = 1;
                    if (TryEndRangeSelection(selected, child, to)) return 2;
                } else {
                    matches = SelectRange(selected, child, from, to, matches);
                    if (matches == 2) return 2;
                }
            }

            return matches;
        }

        private static bool TryEndRangeSelection(ObservableCollection<TreeViewItem> selected, TreeViewItem control, TreeViewItem end) {
            for (int i = 0; i < control.Items.Count; i++) {
                var child = control.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (child == null) continue;

                if (child == end) {
                    return true;
                } else {
                    selected.Add(child);
                    child.SetValue(IsCheckedProperty, true);

                    var ended = TryEndRangeSelection(selected, child, end);
                    if (ended) return true;
                }
            }

            return false;
        }
    }
}