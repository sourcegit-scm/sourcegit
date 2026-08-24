using System.Text;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class SSHKeyHelper : ChromelessWindow
    {
        public SSHKeyHelper()
        {
            InitializeComponent();
        }

        private void OnAddNewKey(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.SSHKeyHelper vm)
                return;

            vm.OpenGenerator();
            e.Handled = true;
        }

        private void OnGenerateKey(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.SSHKeyHelper vm)
                return;

            vm.Generate();
            e.Handled = true;
        }

        private void OnCancelGenerateKey(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.SSHKeyHelper vm)
                return;

            vm.CloseGenerator();
            e.Handled = true;
        }

        private async void OnDeleteSelectedKey(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.SSHKeyHelper { SelectedKey: { } key } vm)
                return;

            var message = new StringBuilder();
            message
                .AppendLine(App.Text("SSHKeyHelper.ConfirmDeletion"))
                .AppendLine()
                .Append("- ").Append(key.FullPath).AppendLine()
                .Append("- ").Append(key.FullPath).Append(".pub");

            var yes = await App.AskConfirmAsync(message.ToString(), Models.ConfirmButtonType.YesNo);
            if (yes)
                vm.DeleteSelected();

            e.Handled = true;
        }
    }
}
