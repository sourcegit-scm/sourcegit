using System;
using System.Windows;

namespace SourceGit.Views {

    /// <summary>
    ///     通用的确认弹出框
    /// </summary>
    public partial class ConfirmDialog : Controls.Window {
        private Action cbOK;
        private Action cbCancel;

        public ConfirmDialog(string title, string message) {
            Owner = App.Current.MainWindow;

            cbOK = null;
            cbCancel = null;

            InitializeComponent();

            txtTitle.Text = title;
            txtMessage.Text = message;
            btnCancel.Visibility = Visibility.Collapsed;
        }

        public ConfirmDialog(string title, string message, Action onOk, Action onCancel = null) {
            Owner = App.Current.MainWindow;

            cbOK = onOk;
            cbCancel = onCancel;

            InitializeComponent();
            
            txtTitle.Text = title;
            txtMessage.Text = message;
            btnCancel.Visibility = Visibility.Visible;
        }

        private void OnSure(object sender, RoutedEventArgs e) {
            cbOK?.Invoke();
            Close();
        }

        private void OnQuit(object sender, RoutedEventArgs e) {
            cbCancel?.Invoke();
            Close();
        }
    }
}
