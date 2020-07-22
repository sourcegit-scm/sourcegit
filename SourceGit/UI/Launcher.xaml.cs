using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SourceGit.UI {

    /// <summary>
    ///     Main window for this app.
    /// </summary>
    public partial class Launcher : Window {

        /// <summary>
        ///     Alert data.
        /// </summary>
        public class Alert {
            public string Title { get; set; }
            public string Message { get; set; }
        }

        /// <summary>
        ///     Alerts.
        /// </summary>
        public ObservableCollection<Alert> Alerts { get; set; } = new ObservableCollection<Alert>();

        /// <summary>
        ///     Constructor
        /// </summary>
        public Launcher() {
            Git.Repository.OnOpen = ShowDashboard;
            Git.Repository.OnClose = ShowManager;

            App.OnError = msg => ShowAlert(new Alert() { Title = "ERROR", Message = msg });

            InitializeComponent();
            ShowManager();
        }

        #region LAYOUT_CONTENT
        /// <summary>
        ///     Show manager.
        /// </summary>
        private void ShowManager() {
            Dispatcher.Invoke(() => {
                body.Content = new Manager();
            });
        }

        /// <summary>
        ///     Show dashboard.
        /// </summary>
        /// <param name="repo"></param>
        private void ShowDashboard(Git.Repository repo) {
            Dispatcher.Invoke(() => {
                body.Content = new Dashboard(repo);
            });
        }

        /// <summary>
        ///     Open preference dialog.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowPreference(object sender, RoutedEventArgs e) {
            Preference.Show();
        }

        /// <summary>
        ///     Open about dialog.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowAbout(object sender, RoutedEventArgs e) {
            var about = new About();
            about.Owner = this;
            about.ShowDialog();
        }

        /// <summary>
        ///     Show alert.
        /// </summary>
        /// <param name="alert"></param>
        private void ShowAlert(Alert alert) {
            Dispatcher.Invoke(() => Alerts.Add(alert));
        }

        /// <summary>
        ///     Remove an alert.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RemoveAlert(object sender, RoutedEventArgs e) {
            var alert = (sender as Button).DataContext as Alert;
            Alerts.Remove(alert);
        }
        #endregion

        #region WINDOW_COMMANDS
        /// <summary>
        ///     Minimize
        /// </summary>
        private void Minimize(object sender, RoutedEventArgs e) {
            SystemCommands.MinimizeWindow(this);
        }

        /// <summary>
        ///     Maximize/Restore
        /// </summary>
        private void MaximizeOrRestore(object sender, RoutedEventArgs e) {
            if (WindowState == WindowState.Normal) {
                SystemCommands.MaximizeWindow(this);
            } else {
                SystemCommands.RestoreWindow(this);
            }
        }

        /// <summary>
        ///     Quit
        /// </summary>
        private void Quit(object sender, RoutedEventArgs e) {
            App.Current.Shutdown();
        }

        /// <summary>
        ///     Show system menu when user click logo.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogoMouseButtonDown(object sender, MouseButtonEventArgs e) {
            var element = e.OriginalSource as FrameworkElement;
            if (element == null) return;

            var pos = PointToScreen(new Point(0, 33));
            SystemCommands.ShowSystemMenu(this, pos);
        }
        #endregion
    }
}
