using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DevBoard.Views
{
    public partial class DevSpacesPreferences : UserControl
    {
        public DevSpacesPreferences()
        {
            InitializeComponent();
            McpSettingsPanel.DataContext = Mcp.DevBoardMcpSettings.Instance;
            DataContextChanged += (_, _) => NormalizeLegacyLayout();
        }

        private void OnEnableChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.Preferences preferences || sender is not CheckBox checkBox)
                return;

            preferences.EnableDevSpaces = checkBox.IsChecked == true;
            if (!preferences.EnableDevSpaces)
                DevBoard.DevSpaces.DevSpaceRegistry.DisableAll();

            e.Handled = true;
        }

        private void OnRegenerateMcpToken(object sender, RoutedEventArgs e)
        {
            Mcp.DevBoardMcpSettings.Instance.RegenerateAuthToken();
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
