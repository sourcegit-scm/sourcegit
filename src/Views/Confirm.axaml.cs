using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class Confirm : ChromelessWindow
    {
        public Confirm()
        {
            InitializeComponent();
        }

        public void SetData(string message, Models.ConfirmButtonType buttonType)
        {
            Message.Text = message;

            switch (buttonType)
            {
                case Models.ConfirmButtonType.OkCancel:
                    BtnYes.Content = App.Text("Sure");
                    BtnNo.Content = App.Text("Cancel");
                    break;
                case Models.ConfirmButtonType.YesNo:
                    BtnYes.Content = App.Text("Yes");
                    BtnNo.Content = App.Text("No");
                    break;
            }
        }

        private void Sure(object _1, RoutedEventArgs _2)
        {
            Close(true);
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close(false);
        }
    }
}
