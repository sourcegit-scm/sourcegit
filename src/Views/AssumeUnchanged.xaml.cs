using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.Views {
    /// <summary>
    ///     管理不跟踪变更的文件
    /// </summary>
    public partial class AssumeUnchanged : Controls.Window {
        private string repo = null;

        public ObservableCollection<string> Files { get; set; }

        public AssumeUnchanged(string repo) {
            this.repo = repo;
            this.Files = new ObservableCollection<string>();

            InitializeComponent();

            Task.Run(() => {
                var unchanged = new Commands.AssumeUnchanged(repo).View();
                Dispatcher.Invoke(() => {
                    if (unchanged.Count > 0) {
                        foreach (var file in unchanged) Files.Add(file);

                        mask.Visibility = Visibility.Collapsed;
                        list.Visibility = Visibility.Visible;
                        list.ItemsSource = Files;
                    } else {
                        list.Visibility = Visibility.Collapsed;
                        mask.Visibility = Visibility.Visible;
                    }
                });
            });
        }

        private void OnQuit(object sender, RoutedEventArgs e) {
            Close();
        }

        private void OnRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) {
            e.Handled = true;
        }

        private void Remove(object sender, RoutedEventArgs e) {
            var btn = sender as Button;
            if (btn == null) return;

            var file = btn.DataContext as string;
            if (file == null) return;

            new Commands.AssumeUnchanged(repo).Remove(file);
            Files.Remove(file);

            if (Files.Count == 0) {
                list.Visibility = Visibility.Collapsed;
                mask.Visibility = Visibility.Visible;
            }

            e.Handled = true;
        }
    }
}
