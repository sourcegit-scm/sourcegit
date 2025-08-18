using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class Confirm : ChromelessWindow
    {
        public Confirm()
        {
            InitializeComponent();
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
