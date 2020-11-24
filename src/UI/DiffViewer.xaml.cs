using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SourceGit.UI {

    /// <summary>
    ///     Viewer for git diff
    /// </summary>
    public partial class DiffViewer : UserControl {
        private List<Git.Diff.LineChange> lineChanges = null;
        private Brush bgEmpty = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
        private Brush bgAdded = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));
        private Brush bgDeleted = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0));
        private Brush bgNormal = Brushes.Transparent;

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
        ///     Change block.
        /// </summary>
        public class ChangeBlock {
            public string Content { get; set; }
            public Git.Diff.LineMode Mode { get; set; }
            public Brush BG { get; set; }
            public Brush FG { get; set; }
            public FontStyle Style { get; set; }
            public string OldLine { get; set; }
            public string NewLine { get; set; }
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

            lineChanges = null;

            loading.Visibility = Visibility.Visible;
            mask.Visibility = Visibility.Collapsed;
            textChangeOneSide.Visibility = Visibility.Collapsed;
            textChangeTwoSides.Visibility = Visibility.Collapsed;
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
                } else if (rs.Lines.Count > 0) {
                    lineChanges = rs.Lines;
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
            if (lineChanges == null) return;

            var fgCommon = FindResource("Brush.FG") as Brush;
            var fgIndicator = FindResource("Brush.FG2") as Brush;

            if (App.Preference.UIUseOneSideDiff) {
                var blocks = new List<ChangeBlock>();

                foreach (var line in lineChanges) {
                    var block = new ChangeBlock();
                    block.Content = line.Content;
                    block.Mode = line.Mode;
                    block.BG = GetLineBackground(line);
                    block.FG = line.Mode == Git.Diff.LineMode.Indicator ? fgIndicator : fgCommon;
                    block.Style = line.Mode == Git.Diff.LineMode.Indicator ? FontStyles.Italic : FontStyles.Normal;
                    block.OldLine = line.OldLine;
                    block.NewLine = line.NewLine;

                    blocks.Add(block);
                }

                Dispatcher.Invoke(() => {
                    loading.Visibility = Visibility.Collapsed;
                    textChangeOptions.Visibility = Visibility.Visible;
                    textChangeOneSide.Visibility = Visibility.Visible;
                    textChangeTwoSides.Visibility = Visibility.Collapsed;

                    ResetDataGrid(textChangeOneSide);
                    textChangeOneSide.ItemsSource = blocks;
                    OnSizeChanged(null, null);
                });
            } else {
                var oldSideBlocks = new List<ChangeBlock>();
                var newSideBlocks = new List<ChangeBlock>();

                foreach (var line in lineChanges) {
                    var block = new ChangeBlock();
                    block.Content = line.Content;
                    block.Mode = line.Mode;
                    block.BG = GetLineBackground(line);
                    block.FG = line.Mode == Git.Diff.LineMode.Indicator ? fgIndicator : fgCommon;
                    block.Style = line.Mode == Git.Diff.LineMode.Indicator ? FontStyles.Italic : FontStyles.Normal;
                    block.OldLine = line.OldLine;
                    block.NewLine = line.NewLine;

                    switch (line.Mode) {
                    case Git.Diff.LineMode.Added:
                        newSideBlocks.Add(block);

                        var oldEmpty = new ChangeBlock();
                        oldEmpty.Content = "";
                        oldEmpty.Mode = Git.Diff.LineMode.None;
                        oldEmpty.BG = bgEmpty;
                        oldEmpty.FG = fgCommon;
                        oldEmpty.Style = FontStyles.Normal;
                        oldEmpty.OldLine = block.OldLine;
                        oldEmpty.NewLine = block.NewLine;
                        oldSideBlocks.Add(oldEmpty);
                        break;
                    case Git.Diff.LineMode.Deleted:
                        oldSideBlocks.Add(block);

                        var newEmpty = new ChangeBlock();
                        newEmpty.Content = "";
                        newEmpty.Mode = Git.Diff.LineMode.None;
                        newEmpty.BG = bgEmpty;
                        newEmpty.FG = fgCommon;
                        newEmpty.Style = FontStyles.Normal;
                        newEmpty.OldLine = block.OldLine;
                        newEmpty.NewLine = block.NewLine;
                        newSideBlocks.Add(newEmpty);
                        break;
                    default:
                        oldSideBlocks.Add(block);
                        newSideBlocks.Add(block);
                        break;
                    }
                }

                Dispatcher.Invoke(() => {
                    loading.Visibility = Visibility.Collapsed;
                    textChangeOptions.Visibility = Visibility.Visible;
                    textChangeOneSide.Visibility = Visibility.Collapsed;
                    textChangeTwoSides.Visibility = Visibility.Visible;

                    ResetDataGrid(textChangeOldSide);
                    ResetDataGrid(textChangeNewSide);

                    textChangeOldSide.ItemsSource = oldSideBlocks;
                    textChangeNewSide.ItemsSource = newSideBlocks;

                    OnSizeChanged(null, null);
                });
            }
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
        ///     Get background color of line.
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        private Brush GetLineBackground(Git.Diff.LineChange line) {
            switch (line.Mode) {
            case Git.Diff.LineMode.Added:
                return bgAdded;
            case Git.Diff.LineMode.Deleted:
                return bgDeleted;
            default:
                return bgNormal;
            }
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

        private void ResetDataGrid(DataGrid dg) {
            dg.ItemsSource = null;
            dg.Items.Clear();

            foreach (var col in dg.Columns) {
                col.MinWidth = 0;
                col.Width = 0;
            }
        }
        #endregion

        #region EVENTS

        /// <summary>
        ///     Auto fit text change diff size.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            var total = area.ActualWidth;

            if (App.Preference.UIUseOneSideDiff) {
                textChangeOneSide.Columns[0].Width = DataGridLength.Auto;
                textChangeOneSide.Columns[1].Width = DataGridLength.Auto;
                textChangeOneSide.Columns[2].MinWidth = 1;
                textChangeOneSide.Columns[2].Width = 1;
                textChangeOneSide.UpdateLayout();

                var offset = textChangeOneSide.NonFrozenColumnsViewportHorizontalOffset;
                var minWidth = total - offset;

                var scroller = GetVisualChild<ScrollViewer>(textChangeOneSide);
                if (scroller.ComputedVerticalScrollBarVisibility == Visibility.Visible) minWidth -= 8;

                textChangeOneSide.Columns[2].MinWidth = minWidth;
                textChangeOneSide.Columns[2].Width = DataGridLength.Auto;
                textChangeOneSide.UpdateLayout();
            } else {
                textChangeOldSide.Columns[0].Width = DataGridLength.Auto;
                textChangeOldSide.Columns[1].MinWidth = 1;
                textChangeOldSide.Columns[1].Width = 1;
                textChangeOldSide.UpdateLayout();

                textChangeNewSide.Columns[0].Width = DataGridLength.Auto;
                textChangeNewSide.Columns[1].MinWidth = 1;
                textChangeNewSide.Columns[1].Width = 1;
                textChangeNewSide.UpdateLayout();

                var oldOffset = textChangeOldSide.NonFrozenColumnsViewportHorizontalOffset;
                var newOffset = textChangeNewSide.NonFrozenColumnsViewportHorizontalOffset;
                var minWidth = total - Math.Min(oldOffset, newOffset);

                var scroller = GetVisualChild<ScrollViewer>(textChangeNewSide);
                if (scroller.ComputedVerticalScrollBarVisibility == Visibility.Visible) minWidth -= 8;

                textChangeOldSide.Columns[1].MinWidth = minWidth;
                textChangeOldSide.Columns[1].Width = DataGridLength.Auto;
                textChangeOldSide.UpdateLayout();

                textChangeNewSide.Columns[1].MinWidth = minWidth;
                textChangeNewSide.Columns[1].Width = DataGridLength.Auto;
                textChangeNewSide.UpdateLayout();
            }
        }

        /// <summary>
        ///     Prevent default auto-scrolling when click row in DataGrid.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnLineRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) {
            e.Handled = true;
        }

        /// <summary>
        ///     Sync scroll on two sides diff.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTwoSidesScroll(object sender, ScrollChangedEventArgs e) {
            var oldSideScroller = GetVisualChild<ScrollViewer>(textChangeOldSide);
            var newSideScroller = GetVisualChild<ScrollViewer>(textChangeNewSide);

            if (e.VerticalChange != 0) {
                if (oldSideScroller.VerticalOffset != e.VerticalOffset) {
                    oldSideScroller.ScrollToVerticalOffset(e.VerticalOffset);
                }

                if (newSideScroller.VerticalOffset != e.VerticalOffset) {
                    newSideScroller.ScrollToVerticalOffset(e.VerticalOffset);
                }
            } else {
                if (oldSideScroller.HorizontalOffset != e.HorizontalOffset) {
                    oldSideScroller.ScrollToHorizontalOffset(e.HorizontalOffset);
                }

                if (newSideScroller.HorizontalOffset != e.HorizontalOffset) {
                    newSideScroller.ScrollToHorizontalOffset(e.HorizontalOffset);
                }
            }
        }

        /// <summary>
        ///     Go to next difference.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Go2Next(object sender, RoutedEventArgs e) {
            var grid = textChangeOneSide;
            if (!App.Preference.UIUseOneSideDiff) grid = textChangeNewSide;

            var scroller = GetVisualChild<ScrollViewer>(grid);
            var firstVisible = (int)scroller.VerticalOffset;
            var firstModeEnded = false;
            var first = grid.Items[firstVisible] as ChangeBlock;
            for (int i = firstVisible + 1; i < grid.Items.Count; i++) {
                var next = grid.Items[i] as ChangeBlock;
                if (next.Mode != Git.Diff.LineMode.Normal && next.Mode != Git.Diff.LineMode.Indicator) {
                    if (firstModeEnded || next.Mode != first.Mode) {
                        scroller.ScrollToVerticalOffset(i);
                        break;
                    }
                } else {
                    firstModeEnded = true;
                }
            }
        }

        /// <summary>
        ///     Go to previous difference.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Go2Prev(object sender, RoutedEventArgs e) {
            var grid = textChangeOneSide;
            if (!App.Preference.UIUseOneSideDiff) grid = textChangeNewSide;

            var scroller = GetVisualChild<ScrollViewer>(grid);
            var firstVisible = (int)scroller.VerticalOffset;
            var firstModeEnded = false;
            var first = grid.Items[firstVisible] as ChangeBlock;
            for (int i = firstVisible - 1; i >= 0; i--) {
                var next = grid.Items[i] as ChangeBlock;
                if (next.Mode != Git.Diff.LineMode.Normal && next.Mode != Git.Diff.LineMode.Indicator) {
                    if (firstModeEnded || next.Mode != first.Mode) {
                        scroller.ScrollToVerticalOffset(i);
                        break;
                    }
                } else {
                    firstModeEnded = true;
                }
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

        /// <summary>
        ///     Text change context menu opening.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTextChangeContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var grid = sender as DataGrid;
            if (grid == null) return;

            var menu = new ContextMenu();
            var copy = new MenuItem();
            copy.Header = "Copy Selected Lines";
            copy.Click += (o, ev) => {
                var items = grid.SelectedItems;
                if (items.Count == 0) return;

                var builder = new StringBuilder();
                foreach (var item in items) {
                    var block = item as ChangeBlock;
                    if (block == null) continue;
                    if (block.Mode == Git.Diff.LineMode.None || block.Mode == Git.Diff.LineMode.Indicator) continue;

                    builder.Append(block.Content);
                    builder.AppendLine();
                }

                Clipboard.SetText(builder.ToString());
            };
            menu.Items.Add(copy);
            menu.IsOpen = true;
            e.Handled = true;
        }
        #endregion
    }
}
