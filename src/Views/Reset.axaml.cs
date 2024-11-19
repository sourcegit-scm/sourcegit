using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class Reset : UserControl
    {
        public Reset()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            ResetMode.Focus();
        }

        private void OnResetModeKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                var key = e.Key.ToString();
                for (int i = 0; i < Models.ResetMode.Supported.Length; i++)
                {
                    if (key.Equals(Models.ResetMode.Supported[i].Key, System.StringComparison.OrdinalIgnoreCase))
                    {
                        comboBox.SelectedIndex = i;
                        e.Handled = true;
                        return;
                    }
                }
            }
        }
    }
}
