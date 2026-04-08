using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class BlameCommandPalette : UserControl
    {
        public BlameCommandPalette()
        {
            InitializeComponent();
            FileListBox.AddHandler(ListBox.KeyDownEvent, OnFileListBoxKeyDown, handledEventsToo: true);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.BlameCommandPalette vm)
                return;

            if (e.Key == Key.Enter)
            {
                this.ShowWindow(vm.Launch());
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (FileListBox.IsKeyboardFocusWithin)
                {
                    FilterTextBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == Key.Down || e.Key == Key.Tab)
            {
                if (FilterTextBox.IsKeyboardFocusWithin)
                {
                    if (vm.VisibleFiles.Count > 0)
                        FileListBox.Focus(NavigationMethod.Directional);

                    e.Handled = true;
                    return;
                }

                if (FileListBox.IsKeyboardFocusWithin && e.Key == Key.Tab)
                {
                    FilterTextBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                    return;
                }
            }
        }

        private void OnFileListBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is ViewModels.BlameCommandPalette vm)
            {
                this.ShowWindow(vm.Launch());
                e.Handled = true;
            }
        }

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.BlameCommandPalette vm)
            {
                this.ShowWindow(vm.Launch());
                e.Handled = true;
            }
        }
    }
}
