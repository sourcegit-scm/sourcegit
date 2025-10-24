using System;
using System.Collections.Generic;
using System.Text;

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
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (e.Key == Key.Up || e.Key == Key.Down)
                    return;
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

        private async void OnRowPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is not Control { DataContext: ViewModels.InteractiveRebaseItem item })
                return;

            var cmdKeyModifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(cmdKeyModifier))
                return;

            var builder = new StringBuilder();
            var selected = IRItemListBox.SelectedItems ?? new List<object>();
            if (selected.Count > 0 && !selected.Contains(item))
            {
                IRItemListBox.SelectedItem = item;
                builder.Append(item.Commit.SHA).Append(';');
            }
            else
            {
                foreach (var one in selected)
                {
                    if (one is ViewModels.InteractiveRebaseItem rebaseItem)
                        builder.Append(rebaseItem.Commit.SHA).Append(';');
                }
            }

            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(_dndItemFormat, builder.ToString()));
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        }

        private void OnRowDragOver(object sender, DragEventArgs e)
        {
            if (DataContext is not ViewModels.InteractiveRebase vm)
                return;

            if (e.DataTransfer.TryGetValue(_dndItemFormat) is not { Length: > 0 } hashes)
                return;

            if (sender is not Control { DataContext: ViewModels.InteractiveRebaseItem dst } control)
                return;

            if (hashes.IndexOf(dst.Commit.SHA, StringComparison.Ordinal) >= 0)
                return;

            var p = e.GetPosition(control);
            var before = p.Y < control.Bounds.Height * 0.5;

            dst.IsDropBeforeVisible = before;
            dst.IsDropAfterVisible = !before;
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void OnRowDragLeave(object sender, DragEventArgs e)
        {
            if (sender is not Control { DataContext: ViewModels.InteractiveRebaseItem dst })
                return;

            dst.IsDropBeforeVisible = false;
            dst.IsDropAfterVisible = false;
            e.Handled = true;
        }

        private void OnRowDrop(object sender, DragEventArgs e)
        {
            if (DataContext is not ViewModels.InteractiveRebase vm)
                return;

            if (e.DataTransfer.TryGetValue(_dndItemFormat) is not { Length: > 0 } hashes)
                return;

            if (sender is not Control { DataContext: ViewModels.InteractiveRebaseItem dst } control)
                return;

            if (hashes.IndexOf(dst.Commit.SHA, StringComparison.Ordinal) >= 0)
                return;

            var selected = IRItemListBox.SelectedItems ?? new List<object>();
            if (selected.Count == 0)
                return;

            var p = e.GetPosition(control);
            var before = p.Y < control.Bounds.Height * 0.5;
            var idx = vm.Items.IndexOf(dst);

            var commits = new List<ViewModels.InteractiveRebaseItem>();
            foreach (var item in selected)
            {
                if (item is ViewModels.InteractiveRebaseItem irItem)
                    commits.Add(irItem);
            }

            vm.Move(commits, before ? idx : idx + 1);
            IRItemListBox.SelectedItems = commits;

            dst.IsDropBeforeVisible = false;
            dst.IsDropAfterVisible = false;
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
        }

        private void OnMoveSelectedUp(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.InteractiveRebase vm)
                return;

            if (IRItemListBox.SelectedItems is not { Count: > 0 } selected)
                return;

            var hashes = new HashSet<string>();
            var items = new List<ViewModels.InteractiveRebaseItem>();
            foreach (var item in selected)
            {
                if (item is ViewModels.InteractiveRebaseItem irItem)
                {
                    hashes.Add(irItem.Commit.SHA);
                    items.Add(irItem);
                }
            }

            var idx = 0;
            for (int i = 0; i < vm.Items.Count; i++)
            {
                if (hashes.Contains(vm.Items[i].Commit.SHA))
                {
                    idx = Math.Max(0, i - 1);
                    break;
                }
            }

            vm.Move(items, idx);
            IRItemListBox.SelectedItems = items;
            e.Handled = true;
        }

        private void OnMoveSelectedDown(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.InteractiveRebase vm)
                return;

            if (IRItemListBox.SelectedItems is not { Count: > 0 } selected)
                return;

            var hashes = new HashSet<string>();
            var items = new List<ViewModels.InteractiveRebaseItem>();
            foreach (var item in selected)
            {
                if (item is ViewModels.InteractiveRebaseItem irItem)
                {
                    hashes.Add(irItem.Commit.SHA);
                    items.Add(irItem);
                }
            }

            var idx = 0;
            for (int i = vm.Items.Count - 1; i >= 0; i--)
            {
                if (hashes.Contains(vm.Items[i].Commit.SHA))
                {
                    idx = Math.Min(vm.Items.Count, i + 2);
                    break;
                }
            }

            vm.Move(items, idx);
            IRItemListBox.SelectedItems = items;
            e.Handled = true;
        }

        private void OnShowActionsDropdownMenu(object sender, RoutedEventArgs e)
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
