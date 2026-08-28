using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class DevSpacesPreferences : UserControl
    {
        public DevSpacesPreferences()
        {
            InitializeComponent();
        }

        private void OnEnableChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.Preferences preferences || sender is not CheckBox checkBox)
                return;

            preferences.EnableDevSpaces = checkBox.IsChecked == true;
            if (!preferences.EnableDevSpaces)
                SourceGit.DevSpaces.DevSpaceRegistry.DisableAll();

            e.Handled = true;
        }
    }
}
