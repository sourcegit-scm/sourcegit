using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class CompareCommandPalette : UserControl
    {
        public CompareCommandPalette()
        {
            InitializeComponent();
            RefsListBox.AddHandler(ListBox.KeyDownEvent, OnRefsListBoxKeyDown, handledEventsToo: true);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (DataContext is not ViewModels.CompareCommandPalette vm)
                return;

            if (e.Key == Key.Enter)
            {
                this.ShowWindow(vm.Launch());
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (RefsListBox.IsKeyboardFocusWithin)
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
                    if (vm.Refs.Count > 0)
                        RefsListBox.Focus(NavigationMethod.Directional);

                    e.Handled = true;
                    return;
                }

                if (RefsListBox.IsKeyboardFocusWithin && e.Key == Key.Tab)
                {
                    FilterTextBox.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                    return;
                }
            }
        }

        private void OnRefsListBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is ViewModels.CompareCommandPalette vm)
            {
                this.ShowWindow(vm.Launch());
                e.Handled = true;
            }
        }

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (DataContext is ViewModels.CompareCommandPalette vm)
            {
                this.ShowWindow(vm.Launch());
                e.Handled = true;
            }
        }
    }
}
