using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class Statistics : ChromelessWindow
    {
        public Statistics()
        {
            InitializeComponent();
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }
    }
}
