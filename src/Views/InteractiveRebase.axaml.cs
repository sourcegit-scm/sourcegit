using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class InteractiveRebaseListBox : ListBox
    {
        protected override Type StyleKeyOverride => typeof(ListBox);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (DataContext is not ViewModels.InteractiveRebase vm || SelectedItems == null)
                return;

            var items = new List<ViewModels.InteractiveRebaseItem>();
            foreach (var item in SelectedItems)
            {
                if (item is ViewModels.InteractiveRebaseItem rebaseItem)
                    items.Add(rebaseItem);
            }

            if (items.Count == 0)
            {
                base.OnKeyDown(e);
                return;
            }

            if (e.Key == Key.P)
            {
                vm.ChangeAction(items, Models.InteractiveRebaseAction.Pick);
                MoveSelection(NavigationDirection.Next);
                e.Handled = true;
            }
            else if (e.Key == Key.E)
            {
                vm.ChangeAction(items, Models.InteractiveRebaseAction.Edit);
                MoveSelection(NavigationDirection.Next);
                e.Handled = true;
            }
            else if (e.Key == Key.R)
            {
                vm.ChangeAction(items, Models.InteractiveRebaseAction.Reword);
                if (items.Count == 1)
                    this.FindAncestorOfType<InteractiveRebase>()?.OpenCommitMessageEditor(items[0]);
                else
                    MoveSelection(NavigationDirection.Next);

                e.Handled = true;
            }
            else if (e.Key == Key.S)
            {
                vm.ChangeAction(items, Models.InteractiveRebaseAction.Squash);
                MoveSelection(NavigationDirection.Next);
                e.Handled = true;
            }
            else if (e.Key == Key.F)
            {
                vm.ChangeAction(items, Models.InteractiveRebaseAction.Fixup);
                MoveSelection(NavigationDirection.Next);
                e.Handled = true;
            }
            else if (e.Key == Key.D)
            {
                vm.ChangeAction(items, Models.InteractiveRebaseAction.Drop);
                MoveSelection(NavigationDirection.Next);
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) && items.Count == 1)
            {
                var item = items[0];
                if (e.Key == Key.Up)
                {
                    vm.MoveItemUp(item);
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    vm.MoveItemDown(item);
                    e.Handled = true;
                }
            }

            if (!e.Handled)
                base.OnKeyDown(e);
        }
    }

    public partial class InteractiveRebase : ChromelessWindow
    {
        public InteractiveRebase()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        public void OpenCommitMessageEditor(ViewModels.InteractiveRebaseItem item)
        {
            var dialog = new CommitMessageEditor();
            dialog.AsBuiltin(item.FullMessage, msg => item.FullMessage = msg);
            dialog.ShowDialog(this);
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            var list = this.FindDescendantOfType<InteractiveRebaseListBox>();
            list?.Focus();
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close();
        }

        private void OnRowsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not ViewModels.InteractiveRebase vm || sender is not InteractiveRebaseListBox listBox)
                return;

            var isFirstTimeHere = !_firstSelectionChangedHandled;
            if (isFirstTimeHere)
                _firstSelectionChangedHandled = true;

            var selected = listBox.SelectedItems ?? new List<object>();
            var items = new List<ViewModels.InteractiveRebaseItem>();
            foreach (var item in selected)
            {
                if (item is ViewModels.InteractiveRebaseItem rebaseItem)
                    items.Add(rebaseItem);
            }

            vm.SelectCommits(items);

            if (items.Count == 1 && isFirstTimeHere && items[0].Action == Models.InteractiveRebaseAction.Reword)
                OpenCommitMessageEditor(items[0]);
        }

        private void OnSetupRowHeaderDragDrop(object sender, RoutedEventArgs e)
        {
            if (sender is Border border)
            {
                DragDrop.SetAllowDrop(border, true);
                border.AddHandler(DragDrop.DragOverEvent, OnRowHeaderDragOver);
            }
        }

        private void OnRowHeaderPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Border { DataContext: ViewModels.InteractiveRebaseItem item })
            {
                var data = new DataObject();
                data.Set("InteractiveRebaseItem", item);
                DragDrop.DoDragDrop(e, data, DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link);
            }
        }

        private void OnRowHeaderDragOver(object sender, DragEventArgs e)
        {
            if (DataContext is ViewModels.InteractiveRebase vm &&
                e.Data.Contains("InteractiveRebaseItem") &&
                e.Data.Get("InteractiveRebaseItem") is ViewModels.InteractiveRebaseItem src &&
                sender is Border { DataContext: ViewModels.InteractiveRebaseItem dst } border &&
                src != dst)
            {
                e.DragEffects = DragDropEffects.Move | DragDropEffects.Copy | DragDropEffects.Link;

                var p = e.GetPosition(border);
                if (p.Y > border.Bounds.Height * 0.33 && p.Y < border.Bounds.Height * 0.67)
                {
                    var srcIdx = vm.Items.IndexOf(src);
                    var dstIdx = vm.Items.IndexOf(dst);
                    if (srcIdx < dstIdx)
                    {
                        for (var i = srcIdx; i < dstIdx; i++)
                            vm.MoveItemDown(src);
                    }
                    else
                    {
                        for (var i = srcIdx; i > dstIdx; i--)
                            vm.MoveItemUp(src);
                    }
                }

                e.Handled = true;
            }
        }

        private void OnButtonActionClicked(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: ViewModels.InteractiveRebaseItem item } button)
                return;

            var flyout = new MenuFlyout();
            flyout.Placement = PlacementMode.BottomEdgeAlignedLeft;
            flyout.VerticalOffset = -4;

            CreateActionMenuItem(flyout, item, Models.InteractiveRebaseAction.Pick, Brushes.Green, "Use this commit", "P");
            CreateActionMenuItem(flyout, item, Models.InteractiveRebaseAction.Edit, Brushes.Orange, "Stop for amending", "E");
            CreateActionMenuItem(flyout, item, Models.InteractiveRebaseAction.Reword, Brushes.Orange, "Edit the commit message", "R");

            if (item.CanSquashOrFixup)
            {
                CreateActionMenuItem(flyout, item, Models.InteractiveRebaseAction.Squash, Brushes.LightGray, "Meld into previous commit", "S");
                CreateActionMenuItem(flyout, item, Models.InteractiveRebaseAction.Fixup, Brushes.LightGray, "Like 'Squash' but discard message", "F");
            }

            CreateActionMenuItem(flyout, item, Models.InteractiveRebaseAction.Drop, Brushes.Red, "Remove commit", "D");

            flyout.ShowAt(button);
            e.Handled = true;
        }

        private void OnOpenCommitMessageEditor(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: ViewModels.InteractiveRebaseItem item })
                OpenCommitMessageEditor(item);

            e.Handled = true;
        }

        private async void OnStartJobs(object _1, RoutedEventArgs _2)
        {
            if (DataContext is not ViewModels.InteractiveRebase vm)
                return;

            Running.IsVisible = true;
            Running.IsIndeterminate = true;
            await vm.Start();
            Running.IsIndeterminate = false;
            Running.IsVisible = false;
            Close();
        }

        private void CreateActionMenuItem(MenuFlyout flyout, ViewModels.InteractiveRebaseItem item, Models.InteractiveRebaseAction action, IBrush iconBrush, string desc, string hotkey)
        {
            var header = new Grid()
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(64, GridUnitType.Pixel),
                    new ColumnDefinition(240, GridUnitType.Pixel),
                ],
                Children =
                {
                    new TextBlock()
                    {
                        [Grid.ColumnProperty] = 0,
                        Margin = new Thickness(4, 0),
                        Text = action.ToString()
                    },
                    new TextBlock()
                    {
                        [Grid.ColumnProperty] = 1,
                        Text = desc,
                        Foreground = this.FindResource("Brush.FG2") as SolidColorBrush,
                    }
                }
            };

            var menuItem = new MenuItem();
            menuItem.Icon = new Ellipse() { Width = 14, Height = 14, Fill = iconBrush };
            menuItem.Header = header;
            menuItem.Tag = hotkey;
            menuItem.Click += (_, e) =>
            {
                if (DataContext is ViewModels.InteractiveRebase vm)
                {
                    vm.ChangeAction([item], action);

                    if (action == Models.InteractiveRebaseAction.Reword)
                        OpenCommitMessageEditor(item);
                }

                e.Handled = true;
            };

            flyout.Items.Add(menuItem);
        }

        private bool _firstSelectionChangedHandled = false;
    }
}
