using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SourceGit.Views {
    /// <summary>
    ///     逐行追溯
    /// </summary>
    public partial class Blame : Window {
        private static readonly Brush[] BG = new Brush[] {
            Brushes.Transparent,
            new SolidColorBrush(Color.FromArgb(128, 0, 0, 0))
        };

        private string repo = null;
        private string lastSHA = null;
        private int lastBG = 1;

        public class Record : INotifyPropertyChanged {
            private Brush bg = null;

            public event PropertyChangedEventHandler PropertyChanged;

            public Models.BlameLine Line { get; set; }
            public Brush OrgBG { get; set; }
            public Brush BG {
                get { return bg; }
                set {
                    if (value != bg) {
                        bg = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("BG"));
                    }
                }
            }
        }

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
                    foreach (var line in rs.Lines) {
                        var r = new Record();
                        r.Line = line;
                        r.BG = GetBG(line.CommitSHA);
                        r.OrgBG = r.BG;
                        Records.Add(r);
                    }

                    Dispatcher.Invoke(() => {
                        loading.IsAnimating = false;
                        loading.Visibility = Visibility.Collapsed;

                        var formatted = new FormattedText(
                            $"{Records.Count}",
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            new Typeface(blame.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                            12.0,
                            Brushes.Black,
                            VisualTreeHelper.GetDpi(this).PixelsPerDip);

                        var lineNumberWidth = formatted.Width + 16;
                        var minWidth = blame.ActualWidth - lineNumberWidth;
                        if (Records.Count * 16 > blame.ActualHeight) minWidth -= 8;
                        blame.Columns[0].Width = lineNumberWidth;
                        blame.Columns[1].MinWidth = minWidth;
                        blame.ItemsSource = Records;
                        blame.UpdateLayout();
                    });
                }
            });
        }

        private Brush GetBG(string sha) {
            if (lastSHA != sha) {
                lastSHA = sha;
                lastBG = 1 - lastBG;
            }

            return BG[lastBG];
        }

        #region WINDOW_COMMANDS
        private void Minimize(object sender, RoutedEventArgs e) {
            SystemCommands.MinimizeWindow(this);
        }

        private void MaximizeOrRestore(object sender, RoutedEventArgs e) {
            if (WindowState == WindowState.Normal) {
                SystemCommands.MaximizeWindow(this);
            } else {
                SystemCommands.RestoreWindow(this);
            }
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

            blame.Columns[1].MinWidth = minWidth;
            blame.Columns[1].Width = DataGridLength.SizeToCells;
            blame.UpdateLayout();
        }

        private void OnViewerRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) {
            e.Handled = true;
        }

        private void OnViewerContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var record = (sender as DataGridRow).DataContext as Record;
            if (record == null) return;

            foreach (var r in Records) {
                if (r.Line.CommitSHA == record.Line.CommitSHA) {
                    r.BG = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));
                } else {
                    r.BG = r.OrgBG;
                }
            }

            Hyperlink link = new Hyperlink(new Run(record.Line.CommitSHA));
            link.ToolTip = App.Text("Goto");
            link.Click += (o, e) => {
                Models.Watcher.Get(repo).NavigateTo(record.Line.CommitSHA);
                e.Handled = true;
            };

            commitID.Content = link;
            authorName.Text = record.Line.Author;
            authorTime.Text = record.Line.Time;
            popup.IsOpen = true;
            ev.Handled = true;
        }
        #endregion
    }
}
