using System;
using System.Collections.Generic;
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
        private Git.Diff.TextChange textChangeData = null;

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

            textChangeData = null;

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
                    textChangeData = rs;
                    SetTextChange();
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
        private void SetTextChange() {
            if (textChangeData == null) return;

            Dispatcher.Invoke(() => {
                loading.Visibility = Visibility.Collapsed;
                textChange.Visibility = Visibility.Visible;
                textChangeOptions.Visibility = Visibility.Visible;

                if (App.Preference.UIUseOneSideDiff) {
                    twoSideLeft.Width = new GridLength(0);
                    twoSideLeft.MinWidth = 0;
                    twoSideSplittter.Width = new GridLength(0);
                } else {
                    twoSideLeft.Width = new GridLength(1, GridUnitType.Star);
                    twoSideLeft.MinWidth = 100;
                    twoSideSplittter.Width = new GridLength(2);
                }

                minWidth = Math.Max(leftText.ActualWidth, rightText.ActualWidth) - 16;

                leftLineNumber.ItemsSource = null;
                rightLineNumber.ItemsSource = null;

                leftText.Document.Blocks.Clear();
                rightText.Document.Blocks.Clear();

                var lLineNumbers = new List<string>();
                var rLineNumbers = new List<string>();

                foreach (var b in textChangeData.Blocks) ShowBlock(b, lLineNumbers, rLineNumbers);

                if (!App.Preference.UIUseOneSideDiff) leftText.Document.PageWidth = minWidth + 16;
                rightText.Document.PageWidth = minWidth + 16;
                leftLineNumber.ItemsSource = lLineNumbers;
                rightLineNumber.ItemsSource = rLineNumbers;
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
                textChangeOptions.Visibility = Visibility.Collapsed;
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
                textChangeOptions.Visibility = Visibility.Collapsed;
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
                textChangeOptions.Visibility = Visibility.Collapsed;
            });
        }

        /// <summary>
        ///     Make paragraph for two-sides diff
        /// </summary>
        /// <param name="b"></param>
        /// <param name="leftNumber"></param>
        /// <param name="rightNumber"></param>
        private void ShowBlock(Git.Diff.Block b, List<string> leftNumber, List<string> rightNumber) {
            bool useOneSide = App.Preference.UIUseOneSideDiff;
            if (useOneSide && b.Mode == Git.Diff.LineMode.Empty) return;

            var content = b.Builder.ToString();

            // Make paragraph element
            Paragraph p = new Paragraph(new Run(content));
            p.Margin = new Thickness(0);
            p.Padding = new Thickness(0);
            p.LineHeight = 1;
            p.Background = GetBlockBackground(b);
            p.Foreground = b.Mode == Git.Diff.LineMode.Indicator ? Brushes.Gray : FindResource("Brush.FG") as SolidColorBrush;
            p.FontStyle = b.Mode == Git.Diff.LineMode.Indicator ? FontStyles.Italic : FontStyles.Normal;
            p.DataContext = b;
            p.ContextMenuOpening += OnParagraphContextMenuOpening;

            // Calculate with
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

            // Line numbers
            switch (b.Side) {
            case Git.Diff.Side.Left:
                for (int i = 0; i < b.Count; i++) {
                    if (b.CanShowNumber) leftNumber.Add($"{i + b.LeftStart}");
                    else leftNumber.Add("");

                    if (useOneSide) rightNumber.Add("");
                }
                break;
            case Git.Diff.Side.Right:
                for (int i = 0; i < b.Count; i++) {
                    if (b.CanShowNumber) rightNumber.Add($"{i + b.RightStart}");
                    else rightNumber.Add("");

                    if (useOneSide) leftNumber.Add("");
                }
                break;
            default:
                for (int i = 0; i < b.Count; i++) {
                    if (b.CanShowNumber) {
                        leftNumber.Add($"{i + b.LeftStart}");
                        rightNumber.Add($"{i + b.RightStart}");
                    } else {
                        leftNumber.Add("");
                        rightNumber.Add("");
                    }
                }
                break;
            }
            
            // Add this paragraph to document.
            if (App.Preference.UIUseOneSideDiff) {
                rightText.Document.Blocks.Add(p);
            } else {
                switch (b.Side) {
                case Git.Diff.Side.Left:
                    leftText.Document.Blocks.Add(p);
                    break;
                case Git.Diff.Side.Right:
                    rightText.Document.Blocks.Add(p);
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
                    cp.ContextMenuOpening += OnParagraphContextMenuOpening;

                    rightText.Document.Blocks.Add(cp);
                    break;
                }
            }
        }

        /// <summary>
        ///     Get background color of block.
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        private Brush GetBlockBackground(Git.Diff.Block b) {
            Border border = new Border();
            border.BorderThickness = new Thickness(0);
            border.BorderBrush = Brushes.LightBlue;
            border.Height = b.Count * 16 - 1;
            border.Width = minWidth - 1;

            switch (b.Mode) {
            case Git.Diff.LineMode.Empty:
                border.Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
                break;
            case Git.Diff.LineMode.Added:
                border.Background = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));
                break;
            case Git.Diff.LineMode.Deleted:
                border.Background = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0));
                break;
            default:
                border.Background = Brushes.Transparent;
                break;
            }

            VisualBrush highlight = new VisualBrush();
            highlight.TileMode = TileMode.None;
            highlight.Stretch = Stretch.Fill;
            highlight.Visual = border;
            return highlight;
        }
        #endregion

        #region EVENTS
        /// <summary>
        ///     Context menu for text-change paragraph
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnParagraphContextMenuOpening(object sender, ContextMenuEventArgs ev) {
            var paragraph = sender as Paragraph;

            var doc = (paragraph.Parent as FlowDocument);
            if (doc != null) {
                var textBox = doc.Parent as RichTextBox;
                if (textBox != null && !textBox.Selection.IsEmpty) {
                    var copyItem = new MenuItem();
                    copyItem.Header = "Copy";
                    copyItem.Click += (o, e) => {
                        Clipboard.SetText(textBox.Selection.Text);
                        e.Handled = true;
                    };

                    var copyMenu = new ContextMenu();
                    copyMenu.Items.Add(copyItem);
                    copyMenu.IsOpen = true;
                    ev.Handled = true;
                    return;
                }
            }

            var block = paragraph.DataContext as Git.Diff.Block;
            if (block.Mode == Git.Diff.LineMode.Empty || block.Mode == Git.Diff.LineMode.Indicator) {
                ev.Handled = true;
                return;
            }

            var highlight = paragraph.Background as VisualBrush;
            if (highlight != null) {
                (highlight.Visual as Border).BorderThickness = new Thickness(.5);
            }
            
            paragraph.ContextMenu = new ContextMenu();
            paragraph.ContextMenu.Closed += (o, e) => {
                if (paragraph.ContextMenu == (o as ContextMenu)) {
                    if (highlight != null) {
                        (highlight.Visual as Border).BorderThickness = new Thickness(0);
                    }
                    paragraph.ContextMenu = null;
                }
            };

            var copy = new MenuItem();
            copy.Header = "Copy";
            copy.Click += (o, e) => {
                Clipboard.SetText(block.Builder.ToString());
                e.Handled = true;
            };
            paragraph.ContextMenu.Items.Add(copy);

            paragraph.ContextMenu.IsOpen = true;
            ev.Handled = true;
        }

        /// <summary>
        ///     Fix document size.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            var text = sender as RichTextBox;
            if (text.Document.PageWidth < text.ActualWidth) {
                text.Document.PageWidth = text.ActualWidth;
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
            double minTop = 0;

            if (App.Preference.UIUseOneSideDiff) {
                foreach (var p in rightText.Document.Blocks) {
                    var rect = p.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                    var block = p.DataContext as Git.Diff.Block;
                    if (rect.Top > 17 && (block.IsLeftDelete || block.IsRightAdded)) {
                        minTop = rect.Top;
                        break;
                    }
                }
            } else {
                Paragraph next = null;

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
            }

            if (minTop > 0) {
                rightText.ScrollToVerticalOffset(rightText.VerticalOffset + minTop - 16);
            }
        }

        /// <summary>
        ///     Go to previous difference.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Go2Prev(object sender, RoutedEventArgs e) {
            double maxTop = double.MaxValue;

            if (App.Preference.UIUseOneSideDiff) {
                var p = rightText.Document.Blocks.LastBlock as Paragraph;
                do {
                    var rect = p.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                    var block = p.DataContext as Git.Diff.Block;
                    if (rect.Top < 15 && (block.IsLeftDelete || block.IsRightAdded)) {
                        maxTop = rect.Top;
                        break;
                    }

                    p = p.PreviousBlock as Paragraph;
                } while (p != null);
            } else {
                Paragraph next = null;

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
                        if (next == null || maxTop < rect.Top) maxTop = rect.Top;
                        break;
                    }

                    p = p.PreviousBlock as Paragraph;
                } while (p != null);
            }

            if (maxTop != double.MaxValue) {
                rightText.ScrollToVerticalOffset(rightText.VerticalOffset + maxTop - 16);
            }
        }

        /// <summary>
        ///     Chang diff mode.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChangeDiffMode(object sender, RoutedEventArgs e) {
            SetTextChange();
        }
        #endregion
    }
}
