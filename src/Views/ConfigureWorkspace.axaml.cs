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
            base.OnClosing(e);

            if (!Design.IsDesignMode)
                ViewModels.Preferences.Instance.Save();
        }
    }
}
