using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class Alert : ChromelessWindow
    {
        public Alert()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        public async Task ShowAsync(Window owner, string message, bool isError)
        {
            var title = isError ? App.Text("Launcher.Error") : App.Text("Launcher.Info");
            Title = title;
            TxtTitle.Text = title;
            Message.Text = message;
            await ShowDialog(owner);
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
