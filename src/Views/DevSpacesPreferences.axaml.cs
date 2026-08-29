using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class DevSpacesPreferences : UserControl
    {
        public DevSpacesPreferences()
        {
            InitializeComponent();
            McpSettingsPanel.DataContext = Mcp.SourceGitMcpSettings.Instance;
            DataContextChanged += (_, _) => NormalizeLegacyLayout();
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

        private void OnRegenerateMcpToken(object sender, RoutedEventArgs e)
        {
            Mcp.SourceGitMcpSettings.Instance.RegenerateAuthToken();
            e.Handled = true;
        }

        private void NormalizeLegacyLayout()
        {
            if (DataContext is ViewModels.Preferences preferences &&
                preferences.DevSpacesDefaultLayout == Models.DevSpaceLayout.FourByFour)
            {
                preferences.DevSpacesDefaultLayout = Models.DevSpaceLayout.ThreeByThree;
            }
        }
    }
}
