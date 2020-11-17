using System;
using System.Globalization;
using System.Text;
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
                if (opts.RevisionRange.Length > 1) args += $"{opts.RevisionRange[1]} ";

                args += "-- ";

                if (!string.IsNullOrEmpty(opts.OrgPath)) args += $"\"{opts.OrgPath}\" ";
                args += $"\"{opts.Path}\"";

                if (repo.IsLFSFiltered(opts.Path)) {
                    var lc = Git.Diff.GetLFSChange(repo, args);
                    if (lc.IsValid) {
                        SetLFSChange(lc);
                    } else {
                        SetSame();
                    }

                    return;
                }

                var rs = Git.Diff.GetTextChange(repo, args);
                if (rs.IsBinary) {
                    SetBinaryChange(Git.Diff.GetSizeChange(repo, opts.RevisionRange, opts.Path, opts.OrgPath));
                } else if (rs.Blocks.Count > 0) {
                    SetTextChange(rs);
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
            if (!string.IsNullOrEmpty(orgFile) && orgFile != "/dev/null") {
                orgFileNamePanel.Visibility = Visibility.Visible;
                orgFileName.Text = orgFile;
            } else {
                orgFileNamePanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        ///     Show diff content.
        /// </summary>
        /// <param name="rs"></param>
        private void SetTextChange(Git.Diff.TextChange rs) {
            Dispatcher.Invoke(() => {
                loading.Visibility = Visibility.Collapsed;
                textChange.Visibility = Visibility.Visible;
                diffNavigation.Visibility = Visibility.Visible;

                minWidth = Math.Max(leftText.ActualWidth, rightText.ActualWidth) - 16;

                leftLineNumber.Text = "";
                rightLineNumber.Text = "";
                leftText.Document.Blocks.Clear();
                rightText.Document.Blocks.Clear();

                var leftLineNumberBuilder = new StringBuilder();
                var rightLineNumberBuilder = new StringBuilder();

                foreach (var b in rs.Blocks) ShowBlock(b, leftLineNumberBuilder, rightLineNumberBuilder);

                leftText.Document.PageWidth = minWidth + 16;
                rightText.Document.PageWidth = minWidth + 16;
                leftLineNumber.Text = leftLineNumberBuilder.ToString();
                rightLineNumber.Text = rightLineNumberBuilder.ToString();
                leftText.ScrollToHome();
            });
        }

        /// <summary>
        ///     Show size changes.
        /// </summary>
        /// <param name="bc"></param>
        private void SetBinaryChange(Git.Diff.BinaryChange bc) {
            Dispatcher.Invoke(() => {
                loading.Visibility = Visibility.Collapsed;
                sizeChange.Visibility = Visibility.Visible;
                diffNavigation.Visibility = Visibility.Collapsed;
                txtSizeChangeTitle.Content = "BINARY DIFF";
                txtNewSize.Content = $"{bc.Size} Bytes";
                txtOldSize.Content = $"{bc.PreSize} Bytes";
            });
        }

        /// <summary>
        ///     Show size changes.
        /// </summary>
        /// <param name="lc"></param>
        private void SetLFSChange(Git.Diff.LFSChange lc) {
            Dispatcher.Invoke(() => {
                var oldSize = lc.Old == null ? 0 : lc.Old.Size;
                var newSize = lc.New == null ? 0 : lc.New.Size;

                loading.Visibility = Visibility.Collapsed;
                sizeChange.Visibility = Visibility.Visible;
                diffNavigation.Visibility = Visibility.Collapsed;
                txtSizeChangeTitle.Content = "LFS OBJECT CHANGE";
                txtNewSize.Content = $"{newSize} Bytes";
                txtOldSize.Content = $"{oldSize} Bytes";
            });
        }

        /// <summary>
        ///     Show no changes or only EOL changes.
        /// </summary>
        private void SetSame() {
            Dispatcher.Invoke(() => {
                loading.Visibility = Visibility.Collapsed;
                noChange.Visibility = Visibility.Visible;
                diffNavigation.Visibility = Visibility.Collapsed;
            });
        }

        /// <summary>
        ///     Make paragraph.
        /// </summary>
        /// <param name="b"></param>
        private void ShowBlock(Git.Diff.Block b, StringBuilder leftNumber, StringBuilder rightNumber) {
            var content = b.Builder.ToString();

            Paragraph p = new Paragraph(new Run(content));
            p.Margin = new Thickness(0);
            p.Padding = new Thickness();
            p.LineHeight = 1;
            p.Background = Brushes.Transparent;
            p.Foreground = FindResource("Brush.FG") as SolidColorBrush;
            p.FontStyle = FontStyles.Normal;
            p.DataContext = b;

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
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            if (minWidth < formatter.Width) minWidth = formatter.Width;
            
            switch (b.Side) {
            case Git.Diff.Side.Left:
                leftText.Document.Blocks.Add(p);
                for (int i = 0; i < b.Count; i++) {
                    if (b.CanShowNumber) leftNumber.AppendLine($"{i + b.LeftStart}");
                    else leftNumber.AppendLine();
                }
                break;
            case Git.Diff.Side.Right:
                rightText.Document.Blocks.Add(p);
                for (int i = 0; i < b.Count; i++) {
                    if (b.CanShowNumber) rightNumber.AppendLine($"{i + b.RightStart}");
                    else rightNumber.AppendLine();
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
                cp.DataContext = b;
                rightText.Document.Blocks.Add(cp);

                for (int i = 0; i < b.Count; i++) {
                    if (b.Mode != Git.Diff.LineMode.Indicator) {
                        leftNumber.AppendLine($"{i + b.LeftStart}");
                        rightNumber.AppendLine($"{i + b.RightStart}");
                    } else {
                        leftNumber.AppendLine();
                        rightNumber.AppendLine();
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

                leftLineNumber.Margin = new Thickness(0, -e.VerticalOffset, 0, 0);
                rightLineNumber.Margin = new Thickness(0, -e.VerticalOffset, 0, 0);
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

        /// <summary>
        ///     Go to next difference.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Go2Next(object sender, RoutedEventArgs e) {
            Paragraph next = null;
            double minTop = 0;
            
            foreach (var p in leftText.Document.Blocks) {
                var rect = p.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                var block = p.DataContext as Git.Diff.Block;
                if (rect.Top > 17 && block.IsLeftDelete) {
                    next = p as Paragraph;
                    minTop = rect.Top;
                    break;
                }
            }

            foreach (var p in rightText.Document.Blocks) {
                var rect = p.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                var block = p.DataContext as Git.Diff.Block;
                if (rect.Top > 17 && block.IsRightAdded) {
                    if (next == null || minTop > rect.Top) {
                        next = p as Paragraph;
                        minTop = rect.Top;
                    }

                    break;
                }
            }

            if (next != null) {
                rightText.ScrollToVerticalOffset(rightText.VerticalOffset + minTop - 16);
            }
        }

        /// <summary>
        ///     Go to previous difference.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Go2Prev(object sender, RoutedEventArgs e) {
            Paragraph next = null;
            double maxTop = 0;

            var p = leftText.Document.Blocks.LastBlock as Paragraph;
            do {
                var rect = p.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                var block = p.DataContext as Git.Diff.Block;
                if (rect.Top < 15 && block.IsLeftDelete) {
                    next = p;
                    maxTop = rect.Top;
                    break;
                }

                p = p.PreviousBlock as Paragraph;
            } while (p != null);

            p = rightText.Document.Blocks.LastBlock as Paragraph;
            do {
                var rect = p.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                var block = p.DataContext as Git.Diff.Block;
                if (rect.Top < 15 && block.IsRightAdded) {
                    if (next == null || maxTop < rect.Top) {
                        next = p;
                        maxTop = rect.Top;
                    }

                    break;
                }

                p = p.PreviousBlock as Paragraph;
            } while (p != null);

            if (next != null) {
                rightText.ScrollToVerticalOffset(rightText.VerticalOffset + maxTop - 16);
            }
        }
        #endregion
    }
}
