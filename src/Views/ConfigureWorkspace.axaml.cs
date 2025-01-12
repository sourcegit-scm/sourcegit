using Avalonia.Controls;

namespace SourceGit.Views
{
    public partial class ConfigureWorkspace : ChromelessWindow
    {
        public ConfigureWorkspace()
        {
            InitializeComponent();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            ViewModels.Preferences.Instance.Save();
            base.OnClosing(e);
        }
    }
}
