using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace SourceGit.Views.Widgets {

    /// <summary>
    ///     错误提示面板
    /// </summary>
    public partial class Exceptions : UserControl {
        public ObservableCollection<string> Messages { get; set; }

        /// <summary>
        ///     用于判断异常是否属于自己的上下文属性
        /// </summary>
        public static readonly DependencyProperty ContextProperty = DependencyProperty.Register(
            "Context",
            typeof(string),
            typeof(Exceptions),
            new PropertyMetadata(null));

        /// <summary>
        ///     上下文
        /// </summary>
        public string Context {
            get { return (string)GetValue(ContextProperty); }
            set { SetValue(ContextProperty, value); }
        }

        public Exceptions() {
            App.ExceptionRaised += (ctx, detail) => {
                Dispatcher.Invoke(() => {
                    if (ctx == Context) Messages.Add(detail);
                });
            };

            Messages = new ObservableCollection<string>();
            InitializeComponent();
        }

        private void Dismiss(object sender, RoutedEventArgs e) {
            var data = (sender as Button).DataContext as string;
            Messages.Remove(data);
        }
    }
}
