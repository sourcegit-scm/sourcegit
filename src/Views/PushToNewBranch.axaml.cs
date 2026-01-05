using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class PushToNewBranch : ChromelessWindow
    {
        public PushToNewBranch()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        public void SetRemote(string remote)
        {
            TxtPrefix.Text = remote;
        }

        private void OnSure(object _1, RoutedEventArgs _2)
        {
            Close(TxtName.Text);
        }

        private void OnCancel(object _1, RoutedEventArgs _2)
        {
            Close(string.Empty);
        }
    }
}
