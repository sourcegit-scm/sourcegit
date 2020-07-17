using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SourceGit.UI {

    /// <summary>
    ///     Viewer for git diff
    /// </summary>
    public partial class DiffViewer : UserControl {
        private double minWidth = 0;

        /// <summary>
        ///     Diff options.
        /// </summary>
        public class Option {
            public string[] RevisionRange = new string[] { };
            public string Path = "";
            public string OrgPath = null;
            public string ExtraArgs = "";
        }

        /// <summary>
        ///     Constructor
        /// </summary>
        public DiffViewer() {
            InitializeComponent();
            Reset();
        }

        /// <summary>
        ///     Reset data.
        /// </summary>
        public void Reset() {
            mask.Visibility = Visibility.Visible;
        }

        /// <summary>
        ///     Diff with options.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="opts"></param>
        public void Diff(Git.Repository repo, Option opts) {
            SetTitle(opts.Path, opts.OrgPath);

            loading.Visibility = Visibility.Visible;
            mask.Visibility = Visibility.Collapsed;
            textChange.Visibility = Visibility.Collapsed;
            sizeChange.Visibility = Visibility.Collapsed;
            noChange.Visibility = Visibility.Collapsed;

            Task.Run(() => {
                var args = $"{opts.ExtraArgs} ";
                if (opts.RevisionRange.Length > 0) args += $"{opts.RevisionRange[0]} ";
                if (opts.RevisionRange.Length > 1) args += $"{opts.RevisionRange[1]} -- ";
                if (!string.IsNullOrEmpty(opts.OrgPath)) args += $"\"{opts.OrgPath}\" ";
                args += $"\"{opts.Path}\"";

                var rs = Git.Diff.Run(repo, args);
                if (rs.IsBinary) {
                    SetSizeChangeData(Git.Diff.GetSizeChange(repo, opts.RevisionRange, opts.Path, opts.OrgPath));
                } else if (rs.Blocks.Count > 0) {
                    SetData(rs);
                } else {
                    SetSame();
                }
            });
        }

        #region LAYOUT
        /// <summary>
        ///     Show diff title
        /// </summary>
        /// <param name="file"></param>
        /// <param name="orgFile"></param>
        private void SetTitle(string file, string orgFile) {
            fileName.Text = file;
            if (!string.IsNullOrEmpty(orgFile)) {
                orgFileNamePanel.Visibility = Visibility.Visible;
                orgFileName.Text = orgFile;
            } else {
                orgFileNamePanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        ///     Show size changes.
        /// </summary>
        /// <param name="bc"></param>
        private void SetSizeChangeData(Git.Diff.BinaryChange bc) {
            Dispatcher.Invoke(() => {
                loading.Visibility = Visibility.Collapsed;
                sizeChange.Visibility = Visibility.Visible;
                txtNewSize.Content = $"{bc.Size} Bytes";
                txtOldSize.Content = $"{bc.PreSize} Bytes";
            });
        }

        /// <summary>
        ///     Show no changes or only eol changes.
        /// </summary>
        private void SetSame() {
            Dispatcher.Invoke(() => {
                loading.Visibility = Visibility.Collapsed;
                noChange.Visibility = Visibility.Visible;
            });
        }

        /// <summary>
        ///     Show diff content.
        /// </summary>
        /// <param name="rs"></param>
        private void SetData(Git.Diff.Result rs) {
            Dispatcher.Invoke(() => {
                loading.Visibility = Visibility.Collapsed;
                textChange.Visibility = Visibility.Visible;

                minWidth = Math.Max(leftText.ActualWidth, rightText.ActualWidth) - 16;

                leftLineNumber.Text = "";
                rightLineNumber.Text = "";
                leftText.Document.Blocks.Clear();
                rightText.Document.Blocks.Clear();

                foreach (var b in rs.Blocks) ShowBlock(b);

                leftText.Document.PageWidth = minWidth + 16;
                rightText.Document.PageWidth = minWidth + 16;
                leftText.ScrollToHome();
            });
        }

        /// <summary>
        ///     Make paragraph.
        /// </summary>
        /// <param name="b"></param>
        private void ShowBlock(Git.Diff.Block b) {
            var content = b.Builder.ToString();

            Paragraph p = new Paragraph(new Run(content));
            p.Margin = new Thickness(0);
            p.Padding = new Thickness();
            p.LineHeight = 1;
            p.Background = Brushes.Transparent;
            p.Foreground = FindResource("Brush.FG") as SolidColorBrush;
            p.FontStyle = FontStyles.Normal;

            switch (b.Mode) {
            case Git.Diff.LineMode.Normal:
                break;
            case Git.Diff.LineMode.Indicator:
                p.Foreground = Brushes.Gray;
                p.FontStyle = FontStyles.Italic;
                break;
            case Git.Diff.LineMode.Empty:
                p.Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
                break;
            case Git.Diff.LineMode.Added:
                p.Background = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));
                break;
            case Git.Diff.LineMode.Deleted:
                p.Background = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0));
                break;
            }

            var formatter = new FormattedText(
                content,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(leftText.FontFamily, p.FontStyle, p.FontWeight, p.FontStretch),
                leftText.FontSize,
                Brushes.Black,
                new NumberSubstitution(),
                TextFormattingMode.Ideal);

            if (minWidth < formatter.Width) minWidth = formatter.Width;
            
            switch (b.Side) {
            case Git.Diff.Side.Left:
                leftText.Document.Blocks.Add(p);
                for (int i = 0; i < b.Count; i++) {
                    if (b.CanShowNumber) leftLineNumber.AppendText($"{i + b.LeftStart}\n");
                    else leftLineNumber.AppendText("\n");
                }
                break;
            case Git.Diff.Side.Right:
                rightText.Document.Blocks.Add(p);
                for (int i = 0; i < b.Count; i++) {
                    if (b.CanShowNumber) rightLineNumber.AppendText($"{i + b.RightStart}\n");
                    else rightLineNumber.AppendText("\n");
                }
                break;
            default:
                leftText.Document.Blocks.Add(p);

                var cp = new Paragraph(new Run(content));
                cp.Margin = new Thickness(0);
                cp.Padding = new Thickness();
                cp.LineHeight = 1;
                cp.Background = p.Background;
                cp.Foreground = p.Foreground;
                cp.FontStyle = p.FontStyle;
                rightText.Document.Blocks.Add(cp);

                for (int i = 0; i < b.Count; i++) {
                    if (b.Mode != Git.Diff.LineMode.Indicator) {
                        leftLineNumber.AppendText($"{i + b.LeftStart}\n");
                        rightLineNumber.AppendText($"{i + b.RightStart}\n");
                    } else {
                        leftLineNumber.AppendText("\n");
                        rightLineNumber.AppendText("\n");
                    }
                }
                break;
            }
        }
        #endregion

        #region EVENTS
        /// <summary>
        ///     Sync scroll both sides.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnViewerScroll(object sender, ScrollChangedEventArgs e) {
            if (e.VerticalChange != 0) {
                if (leftText.VerticalOffset != e.VerticalOffset) {
                    leftText.ScrollToVerticalOffset(e.VerticalOffset);
                }

                if (rightText.VerticalOffset != e.VerticalOffset) {
                    rightText.ScrollToVerticalOffset(e.VerticalOffset);
                }

                leftLineNumber.Margin = new Thickness(4, -e.VerticalOffset, 4, 0);
                rightLineNumber.Margin = new Thickness(4, -e.VerticalOffset, 4, 0);
            } else {
                if (leftText.HorizontalOffset != e.HorizontalOffset) {
                    leftText.ScrollToHorizontalOffset(e.HorizontalOffset);
                }

                if (rightText.HorizontalOffset != e.HorizontalOffset) {
                    rightText.ScrollToHorizontalOffset(e.HorizontalOffset);
                }
            }
        }

        /// <summary>
        ///     Scroll using mouse wheel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnViewerMouseWheel(object sender, MouseWheelEventArgs e) {
            var text = sender as RichTextBox;
            if (text == null) return;

            if (e.Delta > 0) {
                text.LineUp();
            } else {
                text.LineDown();
            }

            e.Handled = true;
        }

        /// <summary>
        ///     Fix document size for left side.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LeftSizeChanged(object sender, SizeChangedEventArgs e) {
            if (leftText.Document.PageWidth < leftText.ActualWidth) {
                leftText.Document.PageWidth = leftText.ActualWidth;
            }
        }

        /// <summary>
        ///     Fix document size for right side.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RightSizeChanged(object sender, SizeChangedEventArgs e) {
            if (rightText.Document.PageWidth < rightText.ActualWidth) {
                rightText.Document.PageWidth = rightText.ActualWidth;
            }
        }

        /// <summary>
        ///     Auto scroll when selection changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnViewerSelectionChanged(object sender, RoutedEventArgs e) {
            var doc = sender as RichTextBox;
            if (doc == null || doc.IsFocused == false) return;

            if (Mouse.LeftButton == MouseButtonState.Pressed && !doc.Selection.IsEmpty) {
                var p = Mouse.GetPosition(doc);

                if (p.X <= 8) {
                    doc.LineLeft();
                } else if (p.X >= doc.ActualWidth - 8) {
                    doc.LineRight();
                }

                if (p.Y <= 8) {
                    doc.LineUp();
                } else if (p.Y >= doc.ActualHeight - 8) {
                    doc.LineDown();
                }
            }
        }
        #endregion
    }
}
