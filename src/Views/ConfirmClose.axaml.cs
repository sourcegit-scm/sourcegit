using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class ConfirmClose : ChromelessWindow
    {
        public ConfirmClose()
        {
            InitializeComponent();
        }

        private void ExitApp(object _1, RoutedEventArgs _2)
        {
            Close(Models.CloseAppDecision.Yes);
        }

        private void KeepOpen(object _1, RoutedEventArgs _2)
        {
            Close(Models.CloseAppDecision.No);
        }

        private void AddToTray(object _1, RoutedEventArgs _2)
        {
            Close(Models.CloseAppDecision.AddToTray);
        }
    }
}
