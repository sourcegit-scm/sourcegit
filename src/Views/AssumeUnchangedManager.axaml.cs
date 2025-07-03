using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class AssumeUnchangedManager : ChromelessWindow
    {
        public AssumeUnchangedManager()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            if (DataContext is ViewModels.AssumeUnchangedManager vm)
                vm.Load();
        }

        private void OnRemoveButtonClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.AssumeUnchangedManager vm && sender is Button button)
                vm.Remove(button.DataContext as string);

            e.Handled = true;
        }
    }
}
