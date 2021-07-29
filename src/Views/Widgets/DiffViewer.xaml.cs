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

namespace SourceGit.Views.Widgets {
    /// <summary>
    ///     变更对比视图
    /// </summary>
    public partial class DiffViewer : UserControl {
        private static readonly Brush BG_EMPTY = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
        private static readonly Brush BG_ADDED = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));
        private static readonly Brush BG_DELETED = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0));
        private static readonly Brush BG_NORMAL = Brushes.Transparent;
        private static readonly Brush HL_ADDED = new SolidColorBrush(Color.FromArgb(100, 0, 255, 0));
        private static readonly Brush HL_DELETED = new SolidColorBrush(Color.FromArgb(100, 255, 0, 0));
        private static readonly Brush HL_NORMAL = Brushes.Transparent;

        public class Option {
            public string[] RevisionRange = new string[] { };
            public string Path = "";
            public string OrgPath = null;
            public string ExtraArgs = "";
            public bool UseLFS = false;
        }

        public class Block {
            public Models.TextChanges.LineMode Mode { get; set; }
            public Brush BG { get; set; }
            public Brush FG { get; set; }
            public FontStyle Style { get; set; }
            public string OldLine { get; set; }
            public string NewLine { get; set; }
            public Controls.HighlightableTextBlock.Data Data { get; set; } = new Controls.HighlightableTextBlock.Data();

            public bool IsContent {
                get {
                    return Mode == Models.TextChanges.LineMode.Added 
                        || Mode == Models.TextChanges.LineMode.Deleted 
                        || Mode == Models.TextChanges.LineMode.Normal;
                }
            }

            public bool IsDifference {
                get {
                    return Mode == Models.TextChanges.LineMode.Added 
                        || Mode == Models.TextChanges.LineMode.Deleted 
                        || Mode == Models.TextChanges.LineMode.None;
                }
            }
        }

        private ulong seq = 0;
        private string repo = null;
        private Option opt = null;
        private List<Models.TextChanges.Line> cachedTextChanges = null;
        private List<DataGrid> editors = new List<DataGrid>();
        private List<Rectangle> splitters = new List<Rectangle>();

        public DiffViewer() {
            Models.Theme.AddListener(this, Reload);
            InitializeComponent();
            Reset();
        }

        public void Reload() {
            if (repo == null || opt == null) {
                Reset();
            } else {
                Diff(repo, opt);
            }
        }

        public void Reset() {
            seq++;
            mask.Visibility = Visibility.Visible;
            toolbar.Visibility = Visibility.Collapsed;
            noChange.Visibility = Visibility.Collapsed;
            sizeChange.Visibility = Visibility.Collapsed;
            ClearCache();

            foreach (var e in editors) e.ItemsSource = null;
            foreach (var s in splitters) s.Visibility = Visibility.Hidden;
        }

        public void Diff(string repo, Option opt) {
            seq++;

            mask.Visibility = Visibility.Collapsed;
            noChange.Visibility = Visibility.Collapsed;
            sizeChange.Visibility = Visibility.Collapsed;
            toolbar.Visibility = Visibility.Visible;
            loading.Visibility = Visibility.Visible;
            loading.IsAnimating = true;

            SetTitle(opt.Path, opt.OrgPath);
            ClearCache();

            this.repo = repo;
            this.opt = opt;

            var dummy = seq;
            Task.Run(() => {
                var args = $"{opt.ExtraArgs} ";
                if (opt.RevisionRange.Length > 0) args += $"{opt.RevisionRange[0]} ";
                if (opt.RevisionRange.Length > 1) args += $"{opt.RevisionRange[1]} ";

                args += "-- ";

                if (!string.IsNullOrEmpty(opt.OrgPath)) args += $"\"{opt.OrgPath}\" ";
                args += $"\"{opt.Path}\"";

                if (opt.UseLFS) {
                    var isLFSObject = new Commands.LFS(repo).IsFiltered(opt.Path);
                    if (isLFSObject) {
                        var lc = new Commands.QueryLFSObjectChange(repo, args).Result();
                        if (lc.IsValid) {
                            SetLFSChange(lc, dummy);
                        } else {
                            SetSame(dummy);
                        }
                        return;
                    }
                }

                var rs = new Commands.Diff(repo, args).Result();
                if (rs.IsBinary) {
                    var fsc = new Commands.QueryFileSizeChange(repo, opt.RevisionRange, opt.Path, opt.OrgPath).Result();
                    SetSizeChange(fsc, dummy);
                } else if (rs.Lines.Count > 0) {
                    cachedTextChanges = rs.Lines;
                    SetTextChange(dummy);
                } else {
                    SetSame(dummy);
                }
            });
        }

        #region LAYOUT_DATA
        private void SetTitle(string file, string orgFile) {
            txtFileName.Text = file;
            if (!string.IsNullOrEmpty(orgFile) && orgFile != "/dev/null") {
                orgFileNamePanel.Visibility = Visibility.Visible;
                txtOrgFileName.Text = orgFile;
            } else {
                orgFileNamePanel.Visibility = Visibility.Collapsed;
            }
        }

        private void SetTextChange(ulong dummy) {
            if (cachedTextChanges == null) return;

            if (Models.Preference.Instance.Window.UseCombinedDiff) {
                MakeCombinedViewer(dummy);
            } else {
                MakeSideBySideViewer(dummy);
            }
        }

        private void SetSizeChange(Models.FileSizeChange fsc, ulong dummy) {
            Dispatcher.Invoke(() => {
                if (dummy != seq) return;

                loading.Visibility = Visibility.Collapsed;
                mask.Visibility = Visibility.Collapsed;
                toolbarOptions.Visibility = Visibility.Collapsed;
                sizeChange.Visibility = Visibility.Visible;

                txtSizeChangeTitle.Text = App.Text("Diff.Binary");
                iconSizeChange.Data = FindResource("Icon.Binary") as Geometry;
                txtOldSize.Text = App.Text("Bytes", fsc.OldSize);
                txtNewSize.Text = App.Text("Bytes", fsc.NewSize);
            });
        }

        private void SetLFSChange(Models.LFSChange lc, ulong dummy) {
            Dispatcher.Invoke(() => {
                if (dummy != seq) return;

                var oldSize = lc.Old == null ? 0 : lc.Old.Size;
                var newSize = lc.New == null ? 0 : lc.New.Size;

                loading.Visibility = Visibility.Collapsed;
                mask.Visibility = Visibility.Collapsed;
                toolbarOptions.Visibility = Visibility.Collapsed;
                sizeChange.Visibility = Visibility.Visible;

                txtSizeChangeTitle.Text = App.Text("Diff.LFS");
                iconSizeChange.Data = FindResource("Icon.LFS") as Geometry;
                txtNewSize.Text = App.Text("Bytes", newSize);
                txtOldSize.Text = App.Text("Bytes", oldSize);
            });
        }

        private void SetSame(ulong dummy) {
            Dispatcher.Invoke(() => {
                if (dummy != seq) return;

                loading.Visibility = Visibility.Collapsed;
                mask.Visibility = Visibility.Collapsed; 
                toolbarOptions.Visibility = Visibility.Collapsed;
                noChange.Visibility = Visibility.Visible;
            });
        }

        private void MakeCombinedViewer(ulong dummy) {
            var fgCommon = FindResource("Brush.FG1") as Brush;
            var fgIndicator = FindResource("Brush.FG2") as Brush;
            var lastOldLine = "";
            var lastNewLine = "";
            var blocks = new List<Block>();

            foreach (var line in cachedTextChanges) {
                var block = new Block();
                block.Mode = line.Mode;
                block.BG = GetLineBackground(line);
                block.FG = block.IsContent ? fgCommon : fgIndicator;
                block.Style = block.IsContent ? FontStyles.Normal : FontStyles.Italic;
                block.OldLine = line.OldLine;
                block.NewLine = line.NewLine;
                block.Data.Text = line.Content;
                block.Data.HighlightBrush = GetLineHighlight(line);
                block.Data.Highlights.AddRange(line.Highlights);

                if (line.OldLine.Length > 0) lastOldLine = line.OldLine;
                if (line.NewLine.Length > 0) lastNewLine = line.NewLine;

                blocks.Add(block);
            }

            Dispatcher.Invoke(() => {
                if (dummy != seq) return;

                loading.Visibility = Visibility.Collapsed;
                mask.Visibility = Visibility.Collapsed;
                toolbarOptions.Visibility = Visibility.Visible;

                var createEditor = editors.Count == 0;
                var lineNumberWidth = CalcLineNumberColWidth(lastOldLine, lastNewLine);
                var minWidth = textDiff.ActualWidth - lineNumberWidth * 2;
                if (textDiff.ActualHeight < cachedTextChanges.Count * 16) minWidth -= 8;

                DataGrid editor;
                if (createEditor) {
                    editor = CreateTextEditor(new string[] { "OldLine", "NewLine" });
                    editor.SetValue(Grid.ColumnProperty, 0);
                    editor.SetValue(Grid.ColumnSpanProperty, 2);
                    editors.Add(editor);
                    textDiff.Children.Add(editor);

                    AddSplitter(0, Math.Floor(lineNumberWidth));
                    AddSplitter(0, Math.Floor(lineNumberWidth) * 2);
                } else {
                    editor = editors[0];
                    splitters[0].Margin = new Thickness(Math.Floor(lineNumberWidth), 0, 0, 0);
                    splitters[1].Margin = new Thickness(Math.Floor(lineNumberWidth) * 2, 0, 0, 0);
                }

                foreach (var s in splitters) s.Visibility = Visibility.Visible;
                
                editor.Columns[0].Width = new DataGridLength(lineNumberWidth, DataGridLengthUnitType.Pixel);
                editor.Columns[1].Width = new DataGridLength(lineNumberWidth, DataGridLengthUnitType.Pixel);
                editor.Columns[2].MinWidth = minWidth;
                editor.SetBinding(DataGrid.ItemsSourceProperty, new Binding() { Source = blocks, IsAsync = true });
            });
        }

        private void MakeSideBySideViewer(ulong dummy) {
            var fgCommon = FindResource("Brush.FG1") as Brush;
            var fgIndicator = FindResource("Brush.FG2") as Brush;
            var lastOldLine = "";
            var lastNewLine = "";
            var oldSideBlocks = new List<Block>();
            var newSideBlocks = new List<Block>();

            foreach (var line in cachedTextChanges) {
                var block = new Block();
                block.Mode = line.Mode;
                block.BG = GetLineBackground(line);
                block.FG = block.IsContent ? fgCommon : fgIndicator;
                block.Style = block.IsContent ? FontStyles.Normal : FontStyles.Italic;
                block.OldLine = line.OldLine;
                block.NewLine = line.NewLine;
                block.Data.Text = line.Content;
                block.Data.HighlightBrush = GetLineHighlight(line);
                block.Data.Highlights.AddRange(line.Highlights);

                if (line.OldLine.Length > 0) lastOldLine = line.OldLine;
                if (line.NewLine.Length > 0) lastNewLine = line.NewLine;

                switch (line.Mode) {
                case Models.TextChanges.LineMode.Added:
                    newSideBlocks.Add(block);
                    break;
                case Models.TextChanges.LineMode.Deleted:
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
                if (dummy != seq) return;

                loading.Visibility = Visibility.Collapsed;
                mask.Visibility = Visibility.Collapsed;
                toolbarOptions.Visibility = Visibility.Visible;

                var createEditor = editors.Count == 0;
                var lineNumberWidth = CalcLineNumberColWidth(lastOldLine, lastNewLine);
                var minWidth = textDiff.ActualWidth / 2 - lineNumberWidth;
                if (textDiff.ActualHeight < newSideBlocks.Count * 16) minWidth -= 8;

                DataGrid oldEditor, newEditor;
                if (createEditor) {
                    oldEditor = CreateTextEditor(new string[] { "OldLine" });
                    oldEditor.SetValue(Grid.ColumnProperty, 0);
                    oldEditor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnTextDiffSyncScroll));
                    oldEditor.SelectionChanged += new SelectionChangedEventHandler(OnTextDiffSyncSelected);

                    newEditor = CreateTextEditor(new string[] { "NewLine" });
                    newEditor.SetValue(Grid.ColumnProperty, 1);
                    newEditor.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnTextDiffSyncScroll));
                    newEditor.SelectionChanged += new SelectionChangedEventHandler(OnTextDiffSyncSelected);

                    editors.Add(oldEditor);
                    editors.Add(newEditor);
                    textDiff.Children.Add(oldEditor);
                    textDiff.Children.Add(newEditor);

                    AddSplitter(0, Math.Floor(lineNumberWidth));
                    AddSplitter(1, 0);
                    AddSplitter(1, Math.Floor(lineNumberWidth));
                } else {
                    oldEditor = editors[0];
                    newEditor = editors[1];

                    splitters[0].Margin = new Thickness(Math.Floor(lineNumberWidth), 0, 0, 0);
                    splitters[2].Margin = new Thickness(Math.Floor(lineNumberWidth), 0, 0, 0);
                }

                foreach (var s in splitters) s.Visibility = Visibility.Visible;

                oldEditor.Columns[0].Width = new DataGridLength(lineNumberWidth, DataGridLengthUnitType.Pixel);
                oldEditor.Columns[1].MinWidth = minWidth;
                oldEditor.SetBinding(DataGrid.ItemsSourceProperty, new Binding() { Source = oldSideBlocks, IsAsync = true });

                newEditor.Columns[0].Width = new DataGridLength(lineNumberWidth, DataGridLengthUnitType.Pixel);
                newEditor.Columns[1].MinWidth = minWidth;
                newEditor.SetBinding(DataGrid.ItemsSourceProperty, new Binding() { Source = newSideBlocks, IsAsync = true });
            });
        }

        private Brush GetLineBackground(Models.TextChanges.Line line) {
            switch (line.Mode) {
            case Models.TextChanges.LineMode.Added:
                return BG_ADDED;
            case Models.TextChanges.LineMode.Deleted:
                return BG_DELETED;
            default:
                return BG_NORMAL;
            }
        }

        private Brush GetLineHighlight(Models.TextChanges.Line line) {
            switch (line.Mode) {
            case Models.TextChanges.LineMode.Added:
                return HL_ADDED;
            case Models.TextChanges.LineMode.Deleted:
                return HL_DELETED;
            default:
                return HL_NORMAL;
            }
        }

        private void FillEmptyLines(List<Block> old, List<Block> cur) {
            if (old.Count < cur.Count) {
                int diff = cur.Count - old.Count;

                for (int i = 0; i < diff; i++) {
                    var empty = new Block();
                    empty.Mode = Models.TextChanges.LineMode.None;
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
                    var empty = new Block();
                    empty.Mode = Models.TextChanges.LineMode.None;
                    empty.BG = BG_EMPTY;
                    empty.FG = Brushes.Transparent;
                    empty.Style = FontStyles.Normal;
                    empty.OldLine = "";
                    empty.NewLine = "";
                    cur.Add(empty);
                }
            }
        }

        private void AddSplitter(int column, double offset) {
            var split = new Rectangle();
            split.Width = 1;
            split.HorizontalAlignment = HorizontalAlignment.Left;
            split.Margin = new Thickness(offset, 0, 0, 0);
            split.SetValue(Grid.ColumnProperty, column);
            split.SetResourceReference(Rectangle.FillProperty, "Brush.Border2");

            textDiff.Children.Add(split);
            splitters.Add(split);
        }

        private DataGrid CreateTextEditor(string[] lineNumbers) {
            var grid = new DataGrid();
            grid.EnableRowVirtualization = true;
            grid.EnableColumnVirtualization = true;
            grid.RowHeight = 16.0;
            grid.FrozenColumnCount = lineNumbers.Length;
            grid.RowStyle = FindResource("Style.DataGridRow.DiffViewer") as Style;
            grid.ContextMenuOpening += OnTextDiffContextMenuOpening;
            grid.PreviewMouseWheel += OnTextDiffPreviewMouseWheel;
            grid.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (o, e) => {
                var items = (o as DataGrid).SelectedItems;
                if (items.Count == 0) return;

                var builder = new StringBuilder();
                foreach (var item in items) {
                    var block = item as Block;
                    if (block == null) continue;
                    if (!block.IsContent) continue;

                    builder.Append(block.Data.Text);
                    builder.AppendLine();
                }

                Clipboard.SetText(builder.ToString());
            }));

            foreach (var number in lineNumbers) {
                var colLineNumber = new DataGridTextColumn();
                colLineNumber.IsReadOnly = true;
                colLineNumber.Binding = new Binding(number);
                colLineNumber.ElementStyle = FindResource("Style.TextBlock.LineNumber") as Style;
                grid.Columns.Add(colLineNumber);
            }

            var line = new FrameworkElementFactory(typeof(Controls.HighlightableTextBlock));
            line.SetBinding(Controls.HighlightableTextBlock.ContentProperty, new Binding("Data"));
            line.SetBinding(TextBlock.BackgroundProperty, new Binding("BG"));
            line.SetBinding(TextBlock.ForegroundProperty, new Binding("FG"));
            line.SetBinding(TextBlock.FontStyleProperty, new Binding("Style"));
            line.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Consolas,Microsoft YaHei UI"));
            line.SetValue(TextBlock.FontSizeProperty, new FontSizeConverter().ConvertFrom("9pt"));
            line.SetValue(TextBlock.MarginProperty, new Thickness(0));
            line.SetValue(TextBlock.PaddingProperty, new Thickness(4, 0, 0, 0));
            line.SetValue(TextBlock.LineHeightProperty, 16.0);
            line.SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Display);
            line.SetValue(TextOptions.TextRenderingModeProperty, TextRenderingMode.ClearType);

            var colContent = new DataGridTemplateColumn();
            colContent.CellTemplate = new DataTemplate();
            colContent.CellTemplate.VisualTree = line;
            colContent.Width = DataGridLength.SizeToCells;
            grid.Columns.Add(colContent);

            return grid;
        }

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

        private void ClearCache() {
            repo = null;
            opt = null;
            cachedTextChanges = null;
        }

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
        #endregion

        #region EVENTS
        private void OnDiffViewModeChanged(object sender, RoutedEventArgs e) {
            if (editors.Count > 0) {
                editors.Clear();
                splitters.Clear();
                textDiff.Children.Clear();

                SetTextChange(seq);
            }
        }

        private void OnTextDiffSizeChanged(object sender, SizeChangedEventArgs e) {
            if (editors.Count == 0) return;

            var total = textDiff.ActualWidth / editors.Count;
            for (int i = 0; i < editors.Count; i++) {
                var editor = editors[i];
                var minWidth = total - editor.NonFrozenColumnsViewportHorizontalOffset;
                if (editor.Items.Count * 16 > textDiff.ActualHeight) minWidth -= 8;

                var lastColumn = editor.Columns.Count - 1;
                editor.Columns[lastColumn].MinWidth = minWidth;
                editor.Columns[lastColumn].Width = DataGridLength.SizeToCells;
                editor.UpdateLayout();
            }
        }

        private void OnTextDiffContextMenuOpening(object sender, ContextMenuEventArgs e) {
            var grid = sender as DataGrid;
            if (grid == null) return;

            var menu = new ContextMenu();

            var copyIcon = new Path();
            copyIcon.Data = FindResource("Icon.Copy") as Geometry;
            copyIcon.Width = 10;

            var copy = new MenuItem();
            copy.Header = App.Text("Diff.Copy");
            copy.Icon = copyIcon;
            copy.Click += (o, ev) => {
                var items = grid.SelectedItems;
                if (items.Count == 0) return;

                var builder = new StringBuilder();
                foreach (var item in items) {
                    var block = item as Block;
                    if (block == null) continue;
                    if (!block.IsContent) continue;

                    builder.Append(block.Data.Text);
                    builder.AppendLine();
                }

                Clipboard.SetText(builder.ToString());
            };
            menu.Items.Add(copy);
            menu.IsOpen = true;
            e.Handled = true;
        }

        private void OnTextDiffPreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) {
                var editor = sender as DataGrid;
                if (editor == null) return;

                var scroller = GetVisualChild<ScrollViewer>(editor);
                if (scroller == null || scroller.ComputedHorizontalScrollBarVisibility != Visibility.Visible) return;

                if (e.Delta > 0) scroller.LineLeft();
                else scroller.LineRight();

                e.Handled = true;
            }
        }

        private void OnTextDiffSyncScroll(object sender, ScrollChangedEventArgs e) {
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


        private void OnTextDiffSyncSelected(object sender, SelectionChangedEventArgs e) {
            DataGrid dG = sender as DataGrid;
            int Index = dG.SelectedIndex;

            foreach (var editor in editors) {
                if (editor == sender || editor.SelectedIndex == Index) {    /// 对于事件发起者不用处理
                    continue;
                }

                editor.SelectedIndex = Index;       ///* 这里更改后，会导致再次进入改接口
            }
        }

        private void OnTextDiffBringIntoView(object sender, RequestBringIntoViewEventArgs e) {
            e.Handled = true;
        }

        private void GotoPrevChange(object sender, RoutedEventArgs e) {
            if (editors.Count == 0) return;

            var grid = editors[0];
            var scroller = GetVisualChild<ScrollViewer>(grid);
            if (scroller == null) return;

            var firstVisible = (int)scroller.VerticalOffset;
            var firstModeEnded = false;
            var first = grid.Items[firstVisible] as Block;
            for (int i = firstVisible - 1; i >= 0; i--) {
                var next = grid.Items[i] as Block;
                if (next.IsDifference) {
                    if (firstModeEnded || next.Mode != first.Mode) {
                        scroller.ScrollToVerticalOffset(i);
                        grid.SelectedIndex = i;
                        break;
                    }
                } else {
                    firstModeEnded = true;
                }
            }
        }

        private void GotoNextChange(object sender, RoutedEventArgs e) {
            if (editors.Count == 0) return;

            var grid = editors[0];
            var scroller = GetVisualChild<ScrollViewer>(grid);
            if (scroller == null) return;

            var firstVisible = (int)scroller.VerticalOffset;
            var firstModeEnded = false;
            var first = grid.Items[firstVisible] as Block;
            for (int i = firstVisible + 1; i < grid.Items.Count; i++) {
                var next = grid.Items[i] as Block;
                if (next.IsDifference) {
                    if (firstModeEnded || next.Mode != first.Mode) {
                        scroller.ScrollToVerticalOffset(i);
                        grid.SelectedIndex = i;
                        break;
                    }
                } else {
                    firstModeEnded = true;
                }
            }
        }

        private async void OpenWithMerger(object sender, RoutedEventArgs e) {
            var mergeType = Models.Preference.Instance.MergeTool.Type;
            var mergeExe = Models.Preference.Instance.MergeTool.Path;

            var merger = Models.MergeTool.Supported.Find(x => x.Type == mergeType);
            if (merger == null || merger.Type == 0 || !System.IO.File.Exists(mergeExe)) {
                Models.Exception.Raise("Invalid merge tool in preference setting!");
                return;
            }

            var args = $"{opt.ExtraArgs} ";
            if (opt.RevisionRange.Length > 0) args += $"{opt.RevisionRange[0]} ";
            if (opt.RevisionRange.Length > 1) args += $"{opt.RevisionRange[1]} ";

            args += "-- ";

            if (!string.IsNullOrEmpty(opt.OrgPath)) args += $"\"{opt.OrgPath}\" ";
            args += $"\"{opt.Path}\"";

            var cmd = new Commands.Command();
            cmd.Cwd = repo;
            cmd.DontRaiseError = true;
            cmd.Args = $"-c difftool.sourcegit.cmd=\"\\\"{mergeExe}\\\" {merger.DiffCmd}\" ";
            cmd.Args += $"difftool --tool=sourcegit --no-prompt {args}";

            await Task.Run(() => cmd.Exec());
            e.Handled = true;
        }
        #endregion
    }
}
