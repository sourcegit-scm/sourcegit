using System;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class Confirm : ChromelessWindow
    {
        public Confirm()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            (DataContext as ViewModels.Confirm)?.Done(_isOkPressed);
            base.OnClosed(e);
        }

        private void Sure(object _1, RoutedEventArgs _2)
        {
            _isOkPressed = true;
            Close();
        }

        private void CloseWindow(object _1, RoutedEventArgs _2)
        {
            Close();
        }

        private bool _isOkPressed = false;
    }
}
