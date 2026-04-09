using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class WelcomeToolbar : UserControl
    {
        public WelcomeToolbar()
        {
            InitializeComponent();
        }

        private async void OpenLocalRepository(object _1, RoutedEventArgs e)
        {
            var activePage = App.GetLauncher().ActivePage;
            if (activePage == null || !activePage.CanCreatePopup())
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is not Launcher launcher)
                return;

            await launcher.OpenLocalRepository();
            e.Handled = true;
        }
    }
}
