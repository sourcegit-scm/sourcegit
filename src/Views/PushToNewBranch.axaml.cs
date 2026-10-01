using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class PushToNewBranch : ChromelessWindow
    {
        public PushToNewBranch()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            TxtName.Focus(NavigationMethod.Directional);
        }

        private void OnSure(object _1, RoutedEventArgs _2)
        {
            if (DataContext is ViewModels.PushToNewBranch { HasErrors: false } vm && vm.Check())
                Close(vm.BranchName);
        }

        private void OnCancel(object _1, RoutedEventArgs _2)
        {
            Close(string.Empty);
        }
    }
}
