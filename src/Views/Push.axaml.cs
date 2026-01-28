using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class Push : UserControl
    {
        public Push()
        {
            InitializeComponent();
        }

        private async void OnPushToNewBranch(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.Push push)
                return;

            var launcher = this.FindAncestorOfType<Launcher>();
            if (launcher == null)
                return;

            var dialog = new PushToNewBranch();
            dialog.SetRemote(push.SelectedRemote.Name);

            var name = await dialog.ShowDialog<string>(launcher);
            if (!string.IsNullOrEmpty(name))
                push.PushToNewBranch(name);
        }
    }
}
