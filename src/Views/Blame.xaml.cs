using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;

namespace SourceGit.Views {
    /// <summary>
    ///     逐行追溯
    /// </summary>
    public partial class Blame : Controls.Window {
        /// <summary>
        ///     DataGrid数据源结构
        /// </summary>
        public class Record : INotifyPropertyChanged {
            public event PropertyChangedEventHandler PropertyChanged;

            /// <summary>
            ///     原始Blame行数据
            /// </summary>
            public Models.BlameLine Line { get; set; }

            /// <summary>
            ///     是否是第一行
            /// </summary>
            public bool IsFirstLine { get; set; } = false;

            /// <summary>
            ///     前一行与本行的提交不同
            /// </summary>
            public bool IsFirstLineInGroup { get; set; } = false;

            /// <summary>
            ///     是否当前选中，会影响背景色
            /// </summary>
            private bool isSelected = false;
            public bool IsSelected {
                get { return isSelected; }
                set {
                    if (isSelected != value) {
                        isSelected = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected"));
                    }
                }
            }
        }

        /// <summary>
        ///     Blame数据
        /// </summary>
        public ObservableCollection<Record> Records { get; set; }

        public Blame(string repo, string file, string revision) {
            InitializeComponent();

            this.repo = repo;
            Records = new ObservableCollection<Record>();
            txtFile.Text = $"{file}@{revision.Substring(0, 8)}";

            Task.Run(() => {
                var lfs = new Commands.LFS(repo).IsFiltered(file);
                if (lfs) {
                    Dispatcher.Invoke(() => {
                        loading.IsAnimating = false;
                        loading.Visibility = Visibility.Collapsed;
                        notSupport.Visibility = Visibility.Visible;
                    });
                    return;
                }

                var rs = new Commands.Blame(repo, file, revision).Result();
                if (rs.IsBinary) {
                    Dispatcher.Invoke(() => {
                        loading.IsAnimating = false;
                        loading.Visibility = Visibility.Collapsed;
                        notSupport.Visibility = Visibility.Visible;
                    });
                } else {
                    string lastSHA = null;
                    foreach (var line in rs.Lines) {
                        var r = new Record();
                        r.Line = line;
                        r.IsSelected = false;

                        if (line.CommitSHA != lastSHA) {
                            lastSHA = line.CommitSHA;
                            r.IsFirstLineInGroup = true;
                        } else {
                            r.IsFirstLineInGroup = false;
                        }

                        Records.Add(r);
                    }

                    if (Records.Count > 0) Records[0].IsFirstLine = true;

                    Dispatcher.Invoke(() => {
                        loading.IsAnimating = false;
                        loading.Visibility = Visibility.Collapsed;
                        blame.ItemsSource = Records;
                    });
                }
            });
        }

        #region WINDOW_COMMANDS
        private void Minimize(object sender, RoutedEventArgs e) {
            SystemCommands.MinimizeWindow(this);
        }

        private void Quit(object sender, RoutedEventArgs e) {
            Close();
        }
        #endregion

        #region EVENTS
        private T GetVisualChild<T>(DependencyObject parent) where T : Visual {
            T child = null;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++) {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;

                if (child == null) {
                    child = GetVisualChild<T>(v);
                }

                if (child != null) {
                    break;
                }
            }

            return child;
        }

        private void OnViewerSizeChanged(object sender, SizeChangedEventArgs e) {
            var total = blame.ActualWidth;
            var offset = blame.NonFrozenColumnsViewportHorizontalOffset;
            var minWidth = total - offset;

            var scroller = GetVisualChild<ScrollViewer>(blame);
            if (scroller != null && scroller.ComputedVerticalScrollBarVisibility == Visibility.Visible) minWidth -= 8;

            blame.Columns[2].MinWidth = minWidth;
            blame.Columns[2].Width = DataGridLength.SizeToCells;
            blame.UpdateLayout();
        }

        private void OnViewerRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) {
            e.Handled = true;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var r = blame.SelectedItem as Record;
            if (r == null) return;

            foreach (var one in Records) {
                one.IsSelected = one.Line.CommitSHA == r.Line.CommitSHA;
            }
        }

        private void GotoCommit(object sender, RequestNavigateEventArgs e) {
            Models.Watcher.Get(repo).NavigateTo(e.Uri.OriginalString);
            e.Handled = true;
        }
        #endregion

        private string repo = null;
    }
}
