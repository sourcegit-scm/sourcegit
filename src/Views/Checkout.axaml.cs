using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class Checkout : UserControl
    {
        public Checkout()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            var vm = DataContext as ViewModels.Checkout;
            if (vm == null)
                return;

            switch (vm.PreAction)
            {
                case Models.DealWithLocalChanges.DoNothing:
                    RadioDoNothing.IsChecked = true;
                    break;
                case Models.DealWithLocalChanges.StashAndReaply:
                    RadioStashAndReply.IsChecked = true;
                    break;
                default:
                    RadioDiscard.IsChecked = true;
                    break;
            }
        }

        private void OnLocalChangeActionIsCheckedChanged(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as ViewModels.Checkout;
            if (vm == null)
                return;

            if (RadioDoNothing.IsChecked == true)
            {
                if (vm.PreAction != Models.DealWithLocalChanges.DoNothing)
                    vm.PreAction = Models.DealWithLocalChanges.DoNothing;
                return;
            }

            if (RadioStashAndReply.IsChecked == true)
            {
                if (vm.PreAction != Models.DealWithLocalChanges.StashAndReaply)
                    vm.PreAction = Models.DealWithLocalChanges.StashAndReaply;
                return;
            }

            if (vm.PreAction != Models.DealWithLocalChanges.Discard)
                vm.PreAction = Models.DealWithLocalChanges.Discard;
        }
    }
}
