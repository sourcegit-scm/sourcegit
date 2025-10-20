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
                if (e.Key == Key.Up)
                {
                    vm.MoveItemUp(items[0]);
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    vm.MoveItemDown(items[0]);
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
            IRItemListBox?.Focus();
        }

        public void OpenCommitMessageEditor(ViewModels.InteractiveRebaseItem item)
        {
            if (DataContext is not ViewModels.InteractiveRebase vm)
                return;

            var dialog = new CommitMessageEditor();
            dialog.AsBuiltin(vm.ConventionalTypesOverride, item.FullMessage, msg => item.FullMessage = msg);
            dialog.ShowDialog(this);
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close();
        }

        private void OnRowsSelectionChanged(object _, SelectionChangedEventArgs e)
        {
            if (DataContext is not ViewModels.InteractiveRebase vm)
                return;

            var isFirstTimeHere = !_firstSelectionChangedHandled;
            if (isFirstTimeHere)
                _firstSelectionChangedHandled = true;

            var selected = IRItemListBox.SelectedItems ?? new List<object>();
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

        private async void OnRowHeaderPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Border { DataContext: ViewModels.InteractiveRebaseItem item })
            {
                var data = new DataTransfer();
                data.Add(DataTransferItem.Create(_dndItemFormat, item.Commit.SHA));
                await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
            }
        }

        private void OnRowHeaderDragOver(object sender, DragEventArgs e)
        {
            if (DataContext is not ViewModels.InteractiveRebase vm)
                return;

            if (e.DataTransfer.TryGetValue(_dndItemFormat) is not { Length: > 6 } sha)
                return;

            ViewModels.InteractiveRebaseItem src = null;
            foreach (var item in vm.Items)
            {
                if (item.Commit.SHA.Equals(sha, StringComparison.Ordinal))
                {
                    src = item;
                    break;
                }
            }

            if (src == null)
                return;

            if (sender is not Border { DataContext: ViewModels.InteractiveRebaseItem dst } border)
                return;

            if (src == dst)
                return;

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

            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
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
            menuItem.Click += (_, __) => ChangeItemsAction(item, action);

            flyout.Items.Add(menuItem);
        }

        private void ChangeItemsAction(ViewModels.InteractiveRebaseItem target, Models.InteractiveRebaseAction action)
        {
            if (DataContext is not ViewModels.InteractiveRebase vm)
                return;

            var selected = IRItemListBox.SelectedItems ?? new List<object>();
            var items = new List<ViewModels.InteractiveRebaseItem>();
            foreach (var item in selected)
            {
                if (item is ViewModels.InteractiveRebaseItem rebaseItem)
                    items.Add(rebaseItem);
            }

            if (!items.Contains(target))
            {
                items.Clear();
                items.Add(target);
            }

            vm.ChangeAction(items, action);

            if (items.Count == 1 && action == Models.InteractiveRebaseAction.Reword)
                OpenCommitMessageEditor(items[0]);
        }

        private bool _firstSelectionChangedHandled = false;
        private readonly DataFormat<string> _dndItemFormat = DataFormat.CreateStringApplicationFormat("sourcegit-dnd-ir-item");
    }
}
