using Avalonia.Input;

namespace SourceGit.Views
{
    public partial class Hotkeys : ChromelessWindow
    {
        public Hotkeys()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!e.Handled && e.Key == Key.Escape)
                Close();
        }
    }
}
