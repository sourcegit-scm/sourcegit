using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.Views.Widgets {

    /// <summary>
    ///     错误提示面板
    /// </summary>
    public partial class Exceptions : UserControl {
        public ObservableCollection<string> Messages { get; set; }

        public Exceptions() {
            Messages = new ObservableCollection<string>();
            Models.Exception.Handler = e => Dispatcher.Invoke(() => Messages.Add(e));
            InitializeComponent();
        }

        private void Dismiss(object sender, RoutedEventArgs e) {
            var data = (sender as Button).DataContext as string;
            Messages.Remove(data);
        }
    }
}
