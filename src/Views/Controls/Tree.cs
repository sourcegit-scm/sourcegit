using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.Views.Controls {

    /// <summary>
    ///     树
    /// </summary>
    public class Tree : TreeView {
        public static readonly DependencyProperty MultiSelectionProperty = DependencyProperty.Register(
            "MultiSelection", 
            typeof(bool), 
            typeof(Tree), 
            new PropertyMetadata(false));

        public bool MultiSelection {
            get { return (bool)GetValue(MultiSelectionProperty); }
            set { SetValue(MultiSelectionProperty, value); }
        }

        public static readonly DependencyProperty IndentProperty = DependencyProperty.Register(
            "Indent",
            typeof(double),
            typeof(TreeItem),
            new PropertyMetadata(16.0));

        public double Indent {
            get { return (double)GetValue(IndentProperty); }
            set { SetValue(IndentProperty, value); }
        }

        public static readonly RoutedEvent SelectionChangedEvent = EventManager.RegisterRoutedEvent(
            "SelectionChanged",
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(Tree));

        public event RoutedEventHandler SelectionChanged {
            add { AddHandler(SelectionChangedEvent, value); }
            remove { RemoveHandler(SelectionChangedEvent, value); }
        }

        public List<object> Selected {
            get;
            set;
        }

        public Tree() {
            Selected = new List<object>();
            PreviewMouseDown += OnPreviewMouseDown;
        }

        public TreeItem FindItem(DependencyObject elem) {
            if (elem == null) return null;
            if (elem is TreeItem) return elem as TreeItem;
            if (elem is Tree) return null;
            return FindItem(VisualTreeHelper.GetParent(elem));
        }

        public void SelectAll() {
            SelectAllChildren(this);
            RaiseEvent(new RoutedEventArgs(SelectionChangedEvent));
        }

        public void UnselectAll() {
            if (Selected.Count == 0) return;

            UnselectAllChildren(this);
            Selected.Clear();
            RaiseEvent(new RoutedEventArgs(SelectionChangedEvent));
        }

        protected override DependencyObject GetContainerForItemOverride() {
            return new TreeItem(0, Indent);
        }

        protected override bool IsItemItsOwnContainerOverride(object item) {
            return item is TreeItem;
        }

        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue) {
            base.OnItemsSourceChanged(oldValue, newValue);
            
            if (Selected.Count > 0) {
                Selected.Clear();
                RaiseEvent(new RoutedEventArgs(SelectionChangedEvent));
            }
        }

        private TreeItem FindItemByDataContext(ItemsControl control, object data) {
            if (control == null) return null;

            for (int i = 0; i < control.Items.Count; i++) {
                var child = control.ItemContainerGenerator.ContainerFromIndex(i) as TreeItem;
                if (control.Items[i] == data) return child;

                var found = FindItemByDataContext(child, data);
                if (found != null) return found;
            }

            return null;
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            var hit = VisualTreeHelper.HitTest(this, e.GetPosition(this));
            if (hit == null || hit.VisualHit == null) return;

            var item = FindItem(hit.VisualHit);
            if (item == null) return;

            if (!MultiSelection) {
                if (item.IsChecked) return;
                AddSelected(item, true);
                return;
            }

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) {
                if (item.IsChecked) {
                    RemoveSelected(item);
                } else {
                    AddSelected(item, false);
                }
            } else if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && Selected.Count > 0) {
                var last = FindItemByDataContext(this, Selected.Last());
                if (last == item) return;

                var lastPos = last.PointToScreen(new Point(0, 0));
                var curPos = item.PointToScreen(new Point(0, 0));
                if (lastPos.Y > curPos.Y) {
                    SelectRange(this, item, last);
                } else {
                    SelectRange(this, last, item);
                }

                AddSelected(item, false);
            } else if (e.RightButton == MouseButtonState.Pressed) {
                if (item.IsChecked) return;
                AddSelected(item, true);
            } else {
                if (item.IsChecked && Selected.Count == 1) return;
                AddSelected(item, true);
            }
        }

        private void AddSelected(TreeItem item, bool removeOthers) {
            if (removeOthers && Selected.Count > 0) {
                UnselectAllChildren(this);
                Selected.Clear();
            }

            item.IsChecked = true;
            Selected.Add(item.DataContext);
            RaiseEvent(new RoutedEventArgs(SelectionChangedEvent));
        }

        private void RemoveSelected(TreeItem item) {
            item.IsChecked = false;
            Selected.Remove(item.DataContext);
            RaiseEvent(new RoutedEventArgs(SelectionChangedEvent));
        }

        private void SelectAllChildren(ItemsControl control) {
            for (int i = 0; i < control.Items.Count; i++) {
                var child = control.ItemContainerGenerator.ContainerFromIndex(i) as TreeItem;
                if (child == null) continue;

                child.IsChecked = true;
                Selected.Add(control.Items[i]);
                SelectAllChildren(child);
            }
        }

        private void UnselectAllChildren(ItemsControl control) {
            for (int i = 0; i < control.Items.Count; i++) {
                var child = control.ItemContainerGenerator.ContainerFromIndex(i) as TreeItem;
                if (child == null) continue;
                if (child.IsChecked) child.IsChecked = false;
                UnselectAllChildren(child);
            }
        }

        private int SelectRange(ItemsControl control, TreeItem from, TreeItem to, int matches = 0) {
            for (int i = 0; i < control.Items.Count; i++) {
                var child = control.ItemContainerGenerator.ContainerFromIndex(i) as TreeItem;
                if (child == null) continue;

                if (matches == 1) {
                    if (child == to) return 2;
                    Selected.Add(control.Items[i]);
                    child.IsChecked = true;
                    if (TryEndRangeSelection(child, to)) return 2;
                } else if (child == from) {
                    matches = 1;
                    if (TryEndRangeSelection(child, to)) return 2;
                } else {
                    matches = SelectRange(child, from, to, matches);
                    if (matches == 2) return 2;
                }
            }

            return matches;
        }

        private bool TryEndRangeSelection(ItemsControl control, TreeItem end) {
            for (int i = 0; i < control.Items.Count; i++) {
                var child = control.ItemContainerGenerator.ContainerFromIndex(i) as TreeItem;
                if (child == null) continue;

                if (child == end) {
                    return true;
                } else {
                    Selected.Add(control.Items[i]);
                    child.IsChecked = true;

                    var ended = TryEndRangeSelection(child, end);
                    if (ended) return true;
                }
            }

            return false;
        }
    }
}
