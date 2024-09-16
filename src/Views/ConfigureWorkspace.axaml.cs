using Avalonia.Controls;
using Avalonia.Input;

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
            ViewModels.Preference.Instance.Save();
            base.OnClosing(e);
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }
    }
}
