using System;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

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
            var vm = DataContext as ViewModels.InteractiveRebase;
            if (vm == null)
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
            else if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.Up)
            {
                vm.MoveItemUp(item);
                e.Handled = true;
            }
            else if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.Down)
            {
                vm.MoveItemDown(item);
                e.Handled = true;
            }
            else
            {
                base.OnKeyDown(e);
            }
        }
    }

    public partial class InteractiveRebase : ChromelessWindow
    {
        public InteractiveRebase()
        {
            InitializeComponent();
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
            if (sender is Border border && border.DataContext is ViewModels.InteractiveRebaseItem item)
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

        private void OnMoveItemUp(object sender, RoutedEventArgs e)
        {
            if (sender is Control control && DataContext is ViewModels.InteractiveRebase vm)
            {
                vm.MoveItemUp(control.DataContext as ViewModels.InteractiveRebaseItem);
                e.Handled = true;
            }
        }

        private void OnMoveItemDown(object sender, RoutedEventArgs e)
        {
            if (sender is Control control && DataContext is ViewModels.InteractiveRebase vm)
            {
                vm.MoveItemDown(control.DataContext as ViewModels.InteractiveRebaseItem);
                e.Handled = true;
            }
        }
        
        private void OnChangeRebaseAction(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.InteractiveRebase vm &&
                sender is Control
                {
                    DataContext: ViewModels.InteractiveRebaseItem item,
                    Tag: Models.InteractiveRebaseAction action
                })
                vm.ChangeAction(item, action);

            e.Handled = true;
        }

        private async void OnStartJobs(object _1, RoutedEventArgs _2)
        {
            var vm = DataContext as ViewModels.InteractiveRebase;
            if (vm == null)
                return;

            Running.IsVisible = true;
            Running.IsIndeterminate = true;
            await vm.Start();
            Running.IsIndeterminate = false;
            Running.IsVisible = false;
            Close();
        }
    }
}
