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

        private void OnToggleStatusCheckClicked(object sender, RoutedEventArgs e)
        {
            var pref = ViewModels.Preferences.Instance;
            pref.CheckRepositoryStatusOnWelcome = !pref.CheckRepositoryStatusOnWelcome;
        }
    }
}
