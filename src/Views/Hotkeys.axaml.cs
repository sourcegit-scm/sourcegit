using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class Hotkeys : ChromelessWindow
    {
        public Hotkeys()
        {
            InitializeComponent();
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }
    }
}
