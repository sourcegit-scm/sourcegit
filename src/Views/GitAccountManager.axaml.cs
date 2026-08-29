using Avalonia.Controls;

namespace DevBoard.Views
{
    public partial class GitAccountManager : ChromelessWindow
    {
        public GitAccountManager()
        {
            DataContext = new ViewModels.GitAccountManager();
            CloseOnESC = true;
            InitializeComponent();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (!Design.IsDesignMode && DataContext is ViewModels.GitAccountManager manager)
                manager.Save();
        }
    }
}
