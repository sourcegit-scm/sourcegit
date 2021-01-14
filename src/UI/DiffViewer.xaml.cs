using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SourceGit.UI {

    /// <summary>
    ///     Viewer for git diff
    /// </summary>
    public partial class DiffViewer : UserControl {
        public static readonly Brush BG_EMPTY = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
        public static readonly Brush BG_ADDED = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));
        public static readonly Brush BG_DELETED = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0));
        public static readonly Brush BG_NORMAL = Brushes.Transparent;

        private Git.Repository lastRepo = null;
        private Option lastOpts = null;
        private List<Git.Diff.LineChange> lastChanges = null;
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
            loading.Visibility = Visibility.Collapsed;
            mask.Visibility = Visibility.Visible;
            sizeChange.Visibility = Visibility.Collapsed;
            noChange.Visibility = Visibility.Collapsed;
            titleBar.Visibility = Visibility.Collapsed;

            ClearCache();
            ClearEditor();
        }

        /// <summary>
        ///     Diff with options.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="opts"></param>
        public void Diff(Git.Repository repo, Option opts) {
            loading.Visibility = Visibility.Visible;
            mask.Visibility = Visibility.Collapsed;
            sizeChange.Visibility = Visibility.Collapsed;
            noChange.Visibility = Visibility.Collapsed;
            titleBar.Visibility = Visibility.Collapsed;

            SetTitle(opts.Path, opts.OrgPath);
            ClearEditor();
            ClearCache();

            lastRepo = repo;
            lastOpts = opts;

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
                    lastChanges = rs.Lines;
                    SetTextChange();
                } else {
                    SetSame();
                }
            });
        }

        /// <summary>
        ///     Reload diff content with last repository and options.
        /// </summary>
        public void Reload() {
            if (lastRepo == null) {
                Reset();
                return;
            }

            loading.Visibility = Visibility.Visible;
            mask.Visibility = Visibility.Collapsed;
            sizeChange.Visibility = Visibility.Collapsed;
            noChange.Visibility = Visibility.Collapsed;

            var repo = lastRepo;
            var opts = lastOpts;

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
                    lastChanges = rs.Lines;
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
            titleBar.Visibility = Visibility.Visible;
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
            if (lastChanges == null) return;

            var fgCommon = FindResource("Brush.FG1") as Brush;
            var fgIndicator = FindResource("Brush.FG2") as Brush;
            var lastOldLine = "";
            var lastNewLine = "";

            if (App.Setting.UI.UseCombinedDiff) {
                var blocks = new List<ChangeBlock>();

                foreach (var line in lastChanges) {
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

                    var createEditor = editors.Count == 0;
                    var lineNumberWidth = CalcLineNumberColWidth(lastOldLine, lastNewLine);
                    var minWidth = editorContainer.ActualWidth - lineNumberWidth * 2;
                    if (editorContainer.ActualHeight < lastChanges.Count * 16) minWidth -= 8;

                    DataGrid editor;
                    if (createEditor) {
                        editor = CreateTextEditor(new string[] { "OldLine", "NewLine" });
                        editor.SetValue(Grid.ColumnSpanProperty, 2);

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
                    } else {
                        editor = editors[0] as DataGrid;
                        editorLines.ColumnDefinitions[0].Width = new GridLength(lineNumberWidth);
                        editorLines.ColumnDefinitions[0].Width = new GridLength(lineNumberWidth);
                    }

                    editor.Columns[0].Width = new DataGridLength(lineNumberWidth, DataGridLengthUnitType.Pixel);
                    editor.Columns[1].Width = new DataGridLength(lineNumberWidth, DataGridLengthUnitType.Pixel);
                    editor.Columns[2].MinWidth = minWidth;
                    editor.ItemsSource = blocks;

                    if (createEditor) {
                        editorContainer.Children.Add(editor);
                        editors.Add(editor);
                    } else {
                        editor.UpdateLayout();
                        editorLines.UpdateLayout();
                    }
                });
            } else {
                var oldSideBlocks = new List<ChangeBlock>();
                var newSideBlocks = new List<ChangeBlock>();

                foreach (var line in lastChanges) {
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

                    var createEditor = editors.Count == 0;
                    var lineNumberWidth = CalcLineNumberColWidth(lastOldLine, lastNewLine);
                    var minWidth = editorContainer.ActualWidth / 2 - lineNumberWidth;
                    if (editorContainer.ActualHeight < newSideBlocks.Count * 16) minWidth -= 8;

                    DataGrid oldEditor = null;
                    DataGrid newEditor = null;

                    if (createEditor) {
                        oldEditor = CreateTextEditor(new string[] { "OldLine" });
                        oldEditor.SetValue(Grid.ColumnProperty, 0);
                        oldEditor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnTwoSidesScroll));

                        newEditor = CreateTextEditor(new string[] { "NewLine" });
                        newEditor.SetValue(Grid.ColumnProperty, 1);
                        newEditor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnTwoSidesScroll));

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
                    } else {
                        oldEditor = editors[0];
                        newEditor = editors[1];

                        editorLines.ColumnDefinitions[0].Width = new GridLength(lineNumberWidth);
                        editorLines.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                        editorLines.ColumnDefinitions[2].Width = new GridLength(lineNumberWidth);
                        editorLines.ColumnDefinitions[3].Width = new GridLength(1, GridUnitType.Star);
                    }

                    oldEditor.Columns[0].Width = new DataGridLength(lineNumberWidth, DataGridLengthUnitType.Pixel);
                    oldEditor.Columns[1].MinWidth = minWidth;
                    oldEditor.ItemsSource = oldSideBlocks;

                    newEditor.Columns[0].Width = new DataGridLength(lineNumberWidth, DataGridLengthUnitType.Pixel);
                    newEditor.Columns[1].MinWidth = minWidth;
                    newEditor.ItemsSource = newSideBlocks;

                    if (createEditor) {
                        editorContainer.Children.Add(oldEditor);
                        editorContainer.Children.Add(newEditor);
                        editors.Add(oldEditor);
                        editors.Add(newEditor);
                    } else {
                        oldEditor.UpdateLayout();
                        newEditor.UpdateLayout();
                        editorLines.UpdateLayout();
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
                txtSizeChangeTitle.Content = App.Text("Diff.Binary");
                txtNewSize.Content = App.Format("Bytes", bc.PreSize);
                txtOldSize.Content = App.Format("Bytes", bc.Size);
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
                txtSizeChangeTitle.Content = App.Text("Diff.LFS");
                txtNewSize.Content = App.Format("Bytes", newSize);
                txtOldSize.Content = App.Format("Bytes", oldSize);
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
                return BG_ADDED;
            case Git.Diff.LineMode.Deleted:
                return BG_DELETED;
            default:
                return BG_NORMAL;
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
                    empty.BG = BG_EMPTY;
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
                    empty.BG = BG_EMPTY;
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
            grid.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (o, e) => {
                var items = (o as DataGrid).SelectedItems;
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
            }));

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

        /// <summary>
        ///     Clear cached data.
        /// </summary>
        private void ClearCache() {
            lastRepo = null;
            lastOpts = null;
            lastChanges = null;
        }

        /// <summary>
        ///     Clear editor.
        /// </summary>
        private void ClearEditor() {
            editorContainer.Children.Clear();
            editorLines.Children.Clear();
            editorLines.ColumnDefinitions.Clear();
            editors.Clear();
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
            if (App.Setting.UI.UseCombinedDiff) {
                var editor = editors[0];
                var minWidth = total - editor.NonFrozenColumnsViewportHorizontalOffset;
                var scroller = GetVisualChild<ScrollViewer>(editor);
                if (scroller != null && scroller.ComputedVerticalScrollBarVisibility == Visibility.Visible) minWidth -= 8;
                editor.Columns[2].MinWidth = minWidth;
                editor.Columns[2].Width = DataGridLength.SizeToCells;
                editor.UpdateLayout();
            } else {
                var oldEditor = editors[0];
                var newEditor = editors[1];

                var offOld = oldEditor.NonFrozenColumnsViewportHorizontalOffset;
                var offNew = newEditor.NonFrozenColumnsViewportHorizontalOffset;

                var minWidth = total / 2 - Math.Min(offOld, offNew);
                var scroller = GetVisualChild<ScrollViewer>(oldEditor);
                if (scroller != null && scroller.ComputedVerticalScrollBarVisibility == Visibility.Visible) minWidth -= 8;

                oldEditor.Columns[1].MinWidth = minWidth;
                oldEditor.Columns[1].Width = DataGridLength.SizeToCells;
                oldEditor.UpdateLayout();

                newEditor.Columns[1].MinWidth = minWidth;
                newEditor.Columns[1].Width = DataGridLength.SizeToCells;
                newEditor.UpdateLayout();
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
            foreach (var editor in editors) {
                var scroller = GetVisualChild<ScrollViewer>(editor);
                if (scroller == null) continue;

                if (e.VerticalChange != 0 && scroller.VerticalOffset != e.VerticalOffset) {
                    scroller.ScrollToVerticalOffset(e.VerticalOffset);
                }

                if (e.HorizontalChange != 0 && scroller.HorizontalOffset != e.HorizontalOffset) {
                    scroller.ScrollToHorizontalOffset(e.HorizontalOffset);
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
            if (!IsLoaded) return;

            ClearEditor();
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
            copy.Header = App.Text("Diff.Copy");
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
