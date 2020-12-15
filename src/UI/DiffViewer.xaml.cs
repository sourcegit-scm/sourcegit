using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
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
        private List<DataGrid> editors = new List<DataGrid>();

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

            public bool IsContent {
                get {
                    return Mode == Git.Diff.LineMode.Added || Mode == Git.Diff.LineMode.Deleted || Mode == Git.Diff.LineMode.Normal;
                }
            }

            public bool IsDifference {
                get {
                    return Mode == Git.Diff.LineMode.Added || Mode == Git.Diff.LineMode.Deleted || Mode == Git.Diff.LineMode.None;
                }
            }
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
            lineChanges = null;
            foreach (var editor in editors) editorContainer.Children.Remove(editor);
            editors.Clear();
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
            sizeChange.Visibility = Visibility.Collapsed;
            noChange.Visibility = Visibility.Collapsed;

            foreach (var editor in editors) editorContainer.Children.Remove(editor);
            editors.Clear();

            editorLines.Children.Clear();
            editorLines.ColumnDefinitions.Clear();

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
            var lastOldLine = "";
            var lastNewLine = "";

            if (App.Preference.UIUseOneSideDiff) {
                var blocks = new List<ChangeBlock>();

                foreach (var line in lineChanges) {
                    var block = new ChangeBlock();
                    block.Content = line.Content;
                    block.Mode = line.Mode;
                    block.BG = GetLineBackground(line);
                    block.FG = block.IsContent ? fgCommon : fgIndicator;
                    block.Style = block.IsContent ? FontStyles.Normal : FontStyles.Italic;
                    block.OldLine = line.OldLine;
                    block.NewLine = line.NewLine;

                    if (line.OldLine.Length > 0) lastOldLine = line.OldLine;
                    if (line.NewLine.Length > 0) lastNewLine = line.NewLine;

                    blocks.Add(block);
                }

                Dispatcher.Invoke(() => {
                    loading.Visibility = Visibility.Collapsed;
                    textChangeOptions.Visibility = Visibility.Visible;

                    var lineNumberWidth = CalcLineNumberColWidth(lastOldLine, lastNewLine);
                    var minWidth = editorContainer.ActualWidth - lineNumberWidth * 2;
                    if (editorContainer.ActualHeight < lineChanges.Count * 16) minWidth -= 8;
                    var editor = CreateTextEditor(new string[] { "OldLine", "NewLine" });
                    editor.Columns[0].Width = new DataGridLength(lineNumberWidth, DataGridLengthUnitType.Pixel);
                    editor.Columns[1].Width = new DataGridLength(lineNumberWidth, DataGridLengthUnitType.Pixel);
                    editor.Columns[2].MinWidth = minWidth;
                    editor.ItemsSource = blocks;
                    editor.SetValue(Grid.ColumnSpanProperty, 2);
                    editorContainer.Children.Add(editor);
                    editors.Add(editor);

                    editorLines.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(lineNumberWidth) });
                    editorLines.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(lineNumberWidth) });

                    for (int i = 0; i < 2; i++) {
                        var split = new Rectangle();
                        split.Width = 1;
                        split.Fill = FindResource("Brush.Border2") as Brush;
                        split.HorizontalAlignment = HorizontalAlignment.Right;
                        Grid.SetColumn(split, i);

                        editorLines.Children.Add(split);
                    }
                });
            } else {
                var oldSideBlocks = new List<ChangeBlock>();
                var newSideBlocks = new List<ChangeBlock>();

                foreach (var line in lineChanges) {
                    var block = new ChangeBlock();
                    block.Content = line.Content;
                    block.Mode = line.Mode;
                    block.BG = GetLineBackground(line);
                    block.FG = block.IsContent ? fgCommon : fgIndicator;
                    block.Style = block.IsContent ? FontStyles.Normal : FontStyles.Italic;
                    block.OldLine = line.OldLine;
                    block.NewLine = line.NewLine;

                    if (line.OldLine.Length > 0) lastOldLine = line.OldLine;
                    if (line.NewLine.Length > 0) lastNewLine = line.NewLine;

                    switch (line.Mode) {
                    case Git.Diff.LineMode.Added:
                        newSideBlocks.Add(block);
                        break;
                    case Git.Diff.LineMode.Deleted:
                        oldSideBlocks.Add(block);
                        break;
                    default:
                        FillEmptyLines(oldSideBlocks, newSideBlocks);
                        oldSideBlocks.Add(block);
                        newSideBlocks.Add(block);
                        break;
                    }
                }

                FillEmptyLines(oldSideBlocks, newSideBlocks);

                Dispatcher.Invoke(() => {
                    loading.Visibility = Visibility.Collapsed;
                    textChangeOptions.Visibility = Visibility.Visible;

                    var lineNumberWidth = CalcLineNumberColWidth(lastOldLine, lastNewLine);
                    var minWidth = editorContainer.ActualWidth / 2 - lineNumberWidth;
                    if (editorContainer.ActualHeight < newSideBlocks.Count * 16) minWidth -= 8;

                    var oldEditor = CreateTextEditor(new string[] { "OldLine" });
                    oldEditor.SetValue(Grid.ColumnProperty, 0);
                    oldEditor.ContextMenuOpening += OnTextChangeContextMenuOpening;
                    oldEditor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnTwoSidesScroll));
                    oldEditor.Columns[0].Width = new DataGridLength(lineNumberWidth, DataGridLengthUnitType.Pixel);
                    oldEditor.Columns[1].MinWidth = minWidth;
                    oldEditor.ItemsSource = oldSideBlocks;

                    var newEditor = CreateTextEditor(new string[] { "NewLine" });
                    newEditor.SetValue(Grid.ColumnProperty, 1);
                    newEditor.ContextMenuOpening += OnTextChangeContextMenuOpening;
                    newEditor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnTwoSidesScroll));
                    newEditor.Columns[0].Width = new DataGridLength(lineNumberWidth, DataGridLengthUnitType.Pixel);
                    newEditor.Columns[1].MinWidth = minWidth;
                    newEditor.ItemsSource = newSideBlocks;

                    editorContainer.Children.Add(oldEditor);
                    editorContainer.Children.Add(newEditor);

                    editors.Add(oldEditor);
                    editors.Add(newEditor);

                    editorLines.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(lineNumberWidth) });
                    editorLines.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                    editorLines.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(lineNumberWidth) });
                    editorLines.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

                    for (int i = 0; i < 3; i++) {
                        var split = new Rectangle();
                        split.Width = 1;
                        split.Fill = FindResource("Brush.Border2") as Brush;
                        split.HorizontalAlignment = HorizontalAlignment.Right;
                        Grid.SetColumn(split, i);
                        editorLines.Children.Add(split);
                    }
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
        ///     Fill empty lines to keep same line count in both old and current.
        /// </summary>
        /// <param name="old"></param>
        /// <param name="cur"></param>
        private void FillEmptyLines(List<ChangeBlock> old, List<ChangeBlock> cur) {
            if (old.Count < cur.Count) {
                int diff = cur.Count - old.Count;

                for (int i = 0; i < diff; i++) {
                    var empty = new ChangeBlock();
                    empty.Content = "";
                    empty.Mode = Git.Diff.LineMode.None;
                    empty.BG = bgEmpty;
                    empty.FG = Brushes.Transparent;
                    empty.Style = FontStyles.Normal;
                    empty.OldLine = "";
                    empty.NewLine = "";
                    old.Add(empty);
                }
            } else if (old.Count > cur.Count) {
                int diff = old.Count - cur.Count;

                for (int i = 0; i < diff; i++) {
                    var empty = new ChangeBlock();
                    empty.Content = "";
                    empty.Mode = Git.Diff.LineMode.None;
                    empty.BG = bgEmpty;
                    empty.FG = Brushes.Transparent;
                    empty.Style = FontStyles.Normal;
                    empty.OldLine = "";
                    empty.NewLine = "";
                    cur.Add(empty);
                }
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

        /// <summary>
        ///     Create text editor.
        /// </summary>
        /// <param name="lineNumbers"></param>
        /// <returns></returns>
        private DataGrid CreateTextEditor(string[] lineNumbers) {
            var grid = new DataGrid();
            grid.SetValue(Grid.RowProperty, 1);
            grid.RowHeight = 16.0;
            grid.FrozenColumnCount = lineNumbers.Length;
            grid.ContextMenuOpening += OnTextChangeContextMenuOpening;
            grid.RowStyle = FindResource("Style.DataGridRow.NoBringIntoView") as Style;

            foreach (var number in lineNumbers) {
                var colLineNumber = new DataGridTextColumn();
                colLineNumber.IsReadOnly = true;
                colLineNumber.Binding = new Binding(number);
                colLineNumber.ElementStyle = FindResource("Style.DataGridText.LineNumber") as Style;
                grid.Columns.Add(colLineNumber);
            }

            var borderContent = new FrameworkElementFactory(typeof(Border));
            borderContent.SetBinding(Border.BackgroundProperty, new Binding("BG"));

            var textContent = new FrameworkElementFactory(typeof(TextBlock));
            textContent.SetBinding(TextBlock.TextProperty, new Binding("Content"));
            textContent.SetBinding(TextBlock.ForegroundProperty, new Binding("FG"));
            textContent.SetBinding(TextBlock.FontStyleProperty, new Binding("Style"));
            textContent.SetValue(TextBlock.BackgroundProperty, Brushes.Transparent);
            textContent.SetValue(TextBlock.FontSizeProperty, 12.0);
            textContent.SetValue(TextBlock.MarginProperty, new Thickness(0));
            textContent.SetValue(TextBlock.PaddingProperty, new Thickness(4,0,0,0));

            var visualTree = new FrameworkElementFactory(typeof(Grid));
            visualTree.AppendChild(borderContent);
            visualTree.AppendChild(textContent);

            var colContent = new DataGridTemplateColumn();
            colContent.CellTemplate = new DataTemplate();
            colContent.CellTemplate.VisualTree = visualTree;
            colContent.Width = DataGridLength.SizeToCells;
            grid.Columns.Add(colContent);

            return grid;
        }

        /// <summary>
        ///     Calculate max width for line number column.
        /// </summary>
        /// <param name="oldLine"></param>
        /// <param name="newLine"></param>
        /// <returns></returns>
        private double CalcLineNumberColWidth(string oldLine, string newLine) {
            var number = oldLine;
            if (newLine.Length > oldLine.Length) number = newLine;

            var formatted = new FormattedText(
                number,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                12.0,
                Brushes.Black,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            return formatted.Width + 16;
        }
        #endregion

        #region EVENTS

        /// <summary>
        ///     Auto fit text change diff size.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            if (editors.Count == 0) return;

            var total = editorContainer.ActualWidth;
            if (App.Preference.UIUseOneSideDiff) {
                var editor = editors[0];
                var minWidth = total - editor.NonFrozenColumnsViewportHorizontalOffset;
                var scroller = GetVisualChild<ScrollViewer>(editor);
                if (scroller != null && scroller.ComputedVerticalScrollBarVisibility == Visibility.Visible) minWidth -= 8;
                editor.Columns[2].MinWidth = minWidth;
                editor.Columns[2].Width = DataGridLength.SizeToCells;
                editor.UpdateLayout();
            } else {
                var offOld = editors[0].NonFrozenColumnsViewportHorizontalOffset;
                var offNew = editors[1].NonFrozenColumnsViewportHorizontalOffset;

                var minWidth = total / 2 - Math.Min(offOld, offNew);
                var scroller = GetVisualChild<ScrollViewer>(editors[0]);
                if (scroller != null && scroller.ComputedVerticalScrollBarVisibility == Visibility.Visible) minWidth -= 8;
                editors[0].Columns[1].MinWidth = minWidth;
                editors[0].Columns[1].Width = DataGridLength.SizeToCells;
                editors[1].Columns[1].MinWidth = minWidth;
                editors[1].Columns[1].Width = DataGridLength.SizeToCells;
                editors[0].UpdateLayout();
                editors[1].UpdateLayout();
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
            if (e.VerticalChange != 0) {
                foreach (var editor in editors) {
                    var scroller = GetVisualChild<ScrollViewer>(editor);
                    if (scroller != null && scroller.VerticalOffset != e.VerticalOffset) {
                        scroller.ScrollToVerticalOffset(e.VerticalOffset);
                    }
                }
            } else {
                foreach (var editor in editors) {
                    var scroller = GetVisualChild<ScrollViewer>(editor);
                    if (scroller != null && scroller.HorizontalOffset != e.HorizontalOffset) {
                        scroller.ScrollToHorizontalOffset(e.HorizontalOffset);
                    }
                }
            }
        }

        /// <summary>
        ///     Go to next difference.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Go2Next(object sender, RoutedEventArgs e) {
            if (editors.Count == 0) return;

            var grid = editors[0];
            var scroller = GetVisualChild<ScrollViewer>(grid);
            if (scroller == null) return;

            var firstVisible = (int)scroller.VerticalOffset;
            var firstModeEnded = false;
            var first = grid.Items[firstVisible] as ChangeBlock;
            for (int i = firstVisible + 1; i < grid.Items.Count; i++) {
                var next = grid.Items[i] as ChangeBlock;
                if (next.IsDifference) {
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
            if (editors.Count == 0) return;

            var grid = editors[0];
            var scroller = GetVisualChild<ScrollViewer>(grid);
            if (scroller == null) return;

            var firstVisible = (int)scroller.VerticalOffset;
            var firstModeEnded = false;
            var first = grid.Items[firstVisible] as ChangeBlock;
            for (int i = firstVisible - 1; i >= 0; i--) {
                var next = grid.Items[i] as ChangeBlock;
                if (next.IsDifference) {
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
            foreach (var editor in editors) editorContainer.Children.Remove(editor);
            editors.Clear();

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
                    if (!block.IsContent) continue;

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
