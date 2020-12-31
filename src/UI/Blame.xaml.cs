using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SourceGit.UI {

    /// <summary>
    ///     Viewer to show git-blame
    /// </summary>
    public partial class Blame : Window {
        private Git.Repository repo = null;
        private string lastSHA = null;
        private int lastBG = 1;

        /// <summary>
        ///     Background color for blocks.
        /// </summary>
        public static Brush[] BG = new Brush[] {
            Brushes.Transparent,
            new SolidColorBrush(Color.FromArgb(128, 0, 0, 0))
        };

        /// <summary>
        ///     Record
        /// </summary>
        public class Record {
            public Git.Blame.Line Line { get; set; }
            public Brush BG { get; set; }
            public int LineNumber { get; set; }
        }

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="open"></param>
        /// <param name="file"></param>
        /// <param name="revision"></param>
        public Blame(Git.Repository open, string file, string revision) {
            InitializeComponent();

            // Move to center.
            var parent = App.Current.MainWindow;
            Left = parent.Left + (parent.Width - Width) * 0.5;
            Top = parent.Top + (parent.Height - Height) * 0.5;

            // Show loading.
            DoubleAnimation anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            loading.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, anim);
            loading.Visibility = Visibility.Visible;

            // Layout content
            blameFile.Content = $"{file}@{revision.Substring(0, 8)}";
            repo = open;

            Task.Run(() => {
                var result = repo.BlameFile(file, revision);
                var records = new List<Record>();

                if (result.IsBinary) {
                    var error = new Record();
                    error.Line = new Git.Blame.Line() { Content = "BINARY FILE BLAME NOT SUPPORTED!!!", CommitSHA = null };
                    error.BG = Brushes.Red;
                    error.LineNumber = 0;
                    records.Add(error);
                } else {
                    int count = 1;
                    foreach (var line in result.Lines) {
                        var r = new Record();
                        r.Line = line;
                        r.BG = GetBG(line);
                        r.LineNumber = count;

                        records.Add(r);
                        count++;
                    }
                }

                Dispatcher.Invoke(() => {
                    loading.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, null);
                    loading.Visibility = Visibility.Collapsed;

                    var formatted = new FormattedText(
                        $"{records.Count}",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(blame.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                        12.0,
                        Brushes.Black);

                    var lineNumberWidth = formatted.Width + 16;
                    var minWidth = area.ActualWidth - lineNumberWidth;

                    if (records.Count * 16 > area.ActualHeight) minWidth -= 8;

                    blame.Columns[0].Width = lineNumberWidth;
                    blame.Columns[1].MinWidth = minWidth;
                    blame.ItemsSource = records;
                    blame.UpdateLayout();
                });
            });
        }

        /// <summary>
        ///     Get background brush.
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private Brush GetBG(Git.Blame.Line line) {
            if (lastSHA != line.CommitSHA) {
                lastSHA = line.CommitSHA;
                lastBG = 1 - lastBG;
            }

            return BG[lastBG];
        }

        /// <summary>
        ///     Click logo
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogoMouseButtonDown(object sender, MouseButtonEventArgs e) {
            var element = e.OriginalSource as FrameworkElement;
            if (element == null) return;

            var pos = PointToScreen(new Point(0, 33));
            SystemCommands.ShowSystemMenu(this, pos);
        }

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
            Close();
        }

        /// <summary>
        ///     Content size changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            var total = area.ActualWidth;
            var offset = blame.NonFrozenColumnsViewportHorizontalOffset;
            var minWidth = total - offset;

            var scroller = GetVisualChild<ScrollViewer>(blame);
            if (scroller != null && scroller.ComputedVerticalScrollBarVisibility == Visibility.Visible) minWidth -= 8;

            blame.Columns[1].MinWidth = minWidth;
            blame.Columns[1].Width = DataGridLength.SizeToCells;
            blame.UpdateLayout();
        }

        /// <summary>
        ///     Context menu opening.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnBlameContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var item = sender as DataGridRow;
            if (item == null) return;

            var record = item.DataContext as Record;
            if (record == null || record.Line.CommitSHA == null) return;

            Hyperlink link = new Hyperlink(new Run(record.Line.CommitSHA));
            link.ToolTip = "CLICK TO GO";
            link.Click += (o, e) => {
                repo.OnNavigateCommit?.Invoke(record.Line.CommitSHA);
                e.Handled = true;
            };

            commitID.Content = link;
            authorName.Content = record.Line.Author;
            authorTime.Content = record.Line.Time;
            popup.IsOpen = true;
            ev.Handled = true;
        }

        /// <summary>
        ///     Prevent auto scroll.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnBlameRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) {
            e.Handled = true;
        }

        /// <summary>
        ///     Find child element of type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parent"></param>
        /// <returns></returns>
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
    }
}
