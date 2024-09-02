using Avalonia.Controls;
using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class RepositoryConfigure : ChromelessWindow
    {
        public RepositoryConfigure()
        {
            InitializeComponent();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            (DataContext as ViewModels.RepositoryConfigure)?.Save();
            base.OnClosing(e);
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }
    }
}
