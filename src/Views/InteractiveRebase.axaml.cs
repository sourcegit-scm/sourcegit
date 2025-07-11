using System;
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

        /// <summary>
        ///     Prevent ListBox handle the arrow keys.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (DataContext is not ViewModels.InteractiveRebase vm)
                return;

            var item = vm.SelectedItem;
            if (item == null)
            {
                base.OnKeyDown(e);
                return;
            }

            if (e.Key == Key.P)
            {
                vm.ChangeAction(item, Models.InteractiveRebaseAction.Pick);
                e.Handled = true;
            }
            else if (e.Key == Key.E)
            {
                vm.ChangeAction(item, Models.InteractiveRebaseAction.Edit);
                e.Handled = true;
            }
            else if (e.Key == Key.R)
            {
                vm.ChangeAction(item, Models.InteractiveRebaseAction.Reword);
                e.Handled = true;
            }
            else if (e.Key == Key.S)
            {
                vm.ChangeAction(item, Models.InteractiveRebaseAction.Squash);
                e.Handled = true;
            }
            else if (e.Key == Key.F)
            {
                vm.ChangeAction(item, Models.InteractiveRebaseAction.Fixup);
                e.Handled = true;
            }
            else if (e.Key == Key.D)
            {
                vm.ChangeAction(item, Models.InteractiveRebaseAction.Drop);
                e.Handled = true;
            }
            else if (e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
            {
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
            if (DataContext is not ViewModels.InteractiveRebase vm)
                return;

            if (sender is not Button { DataContext: ViewModels.InteractiveRebaseItem item } button)
                return;

            var flyout = new MenuFlyout();
            flyout.Placement = PlacementMode.BottomEdgeAlignedLeft;
            flyout.VerticalOffset = -4;

            CreateActionMenuItem(flyout, Brushes.Green, Models.InteractiveRebaseAction.Pick, App.Text("InteractiveRebase.Pick.Message"), item);
            CreateActionMenuItem(flyout, Brushes.Orange, Models.InteractiveRebaseAction.Edit, App.Text("InteractiveRebase.Edit.Message"), item);
            CreateActionMenuItem(flyout, Brushes.Orange, Models.InteractiveRebaseAction.Reword, App.Text("InteractiveRebase.Reword.Message"), item);

            if (item.CanSquashOrFixup)
            {
                CreateActionMenuItem(flyout, Brushes.LightGray, Models.InteractiveRebaseAction.Squash, App.Text("InteractiveRebase.Squash.Message"), item);
                CreateActionMenuItem(flyout, Brushes.LightGray, Models.InteractiveRebaseAction.Fixup, App.Text("InteractiveRebase.Fixup.Message"), item);
            }

            CreateActionMenuItem(flyout, Brushes.Red, Models.InteractiveRebaseAction.Drop, App.Text("InteractiveRebase.Drop.Message"), item);

            flyout.ShowAt(button);
            e.Handled = true;
        }

        private void OnOpenCommitMessageEditor(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: ViewModels.InteractiveRebaseItem item })
            {
                var dialog = new CommitMessageEditor();
                dialog.AsBuiltin(item.FullMessage, msg => item.FullMessage = msg);
                dialog.ShowDialog(this);
            }

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

        private void CreateActionMenuItem(MenuFlyout flyout, IBrush iconBrush, Models.InteractiveRebaseAction action, string desc, ViewModels.InteractiveRebaseItem item)
        {
            var name = action.ToString();
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
                        Text = name
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
            menuItem.Tag = name[0];
            menuItem.Click += (_, e) =>
            {
                if (DataContext is ViewModels.InteractiveRebase vm)
                    vm.ChangeAction(item, action);

                e.Handled = true;
            };

            flyout.Items.Add(menuItem);
        }
    }
}
