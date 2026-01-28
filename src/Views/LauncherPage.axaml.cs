using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class LauncherPage : UserControl
    {
        public LauncherPage()
        {
            InitializeComponent();
        }

        private async void OnPopupSureByHotKey(object sender, RoutedEventArgs e)
        {
            var children = PopupPanel.GetLogicalDescendants();
            foreach (var child in children)
            {
                if (child is Control { IsKeyboardFocusWithin: true, Tag: StealHotKey steal } control &&
                    steal is { Key: Key.Enter, KeyModifiers: KeyModifiers.None })
                {
                    var fake = new KeyEventArgs()
                    {
                        RoutedEvent = KeyDownEvent,
                        Route = RoutingStrategies.Direct,
                        Source = control,
                        Key = Key.Enter,
                        KeyModifiers = KeyModifiers.None,
                        PhysicalKey = PhysicalKey.Enter,
                    };

                    if (control is AvaloniaEdit.TextEditor editor)
                        editor.TextArea.TextView.RaiseEvent(fake);
                    else
                        control.RaiseEvent(fake);

                    e.Handled = false;
                    return;
                }
            }

            if (DataContext is ViewModels.LauncherPage page)
                await page.ProcessPopupAsync();

            e.Handled = true;
        }

        private async void OnPopupSure(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.LauncherPage page)
                await page.ProcessPopupAsync();

            e.Handled = true;
        }

        private void OnPopupCancel(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.LauncherPage page)
                page.CancelPopup();

            e.Handled = true;
        }

        private void OnMaskClicked(object sender, PointerPressedEventArgs e)
        {
            OnPopupCancel(sender, e);
        }

        private async void OnCopyNotification(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Models.Notification notice })
                await App.CopyTextAsync(notice.Message);

            e.Handled = true;
        }

        private void OnDismissNotification(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Models.Notification notice } &&
                DataContext is ViewModels.LauncherPage page)
                page.Notifications.Remove(notice);

            e.Handled = true;
        }

        private void OnToolBarPointerPressed(object sender, PointerPressedEventArgs e)
        {
            this.FindAncestorOfType<ChromelessWindow>()?.BeginMoveWindow(sender, e);
        }
    }
}
