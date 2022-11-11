using System;
using System.Windows;

namespace SourceGit.Views {

    /// <summary>
    ///     通用的确认弹出框
    /// </summary>
    public partial class ConfirmDialog : Controls.Window {
        private Action cbOK;
        private Action cbCancel;

        public ConfirmDialog(string title, string message, Action onOk, Action onCancel = null) {
            InitializeComponent();
            
            txtTitle.Text = title;
            txtMessage.Text = message;

            cbOK = onOk;
            cbCancel = onCancel;
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
