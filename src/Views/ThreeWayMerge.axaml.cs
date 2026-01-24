using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using AvaloniaEdit.Utils;

namespace SourceGit.Views
{
    /// <summary>
    /// Presenter for displaying diff lines in the merge conflict view (MINE/THEIRS panels)
    /// </summary>
    public class MergeDiffPresenter : TextEditor
    {
        public static readonly StyledProperty<string> FileNameProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, string>(nameof(FileName), string.Empty);

        public string FileName
        {
            get => GetValue(FileNameProperty);
            set => SetValue(FileNameProperty, value);
        }

        public static readonly StyledProperty<List<Models.TextDiffLine>> DiffLinesProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, List<Models.TextDiffLine>>(nameof(DiffLines));

        public List<Models.TextDiffLine> DiffLines
        {
            get => GetValue(DiffLinesProperty);
            set => SetValue(DiffLinesProperty, value);
        }

        public static readonly StyledProperty<int> MaxLineNumberProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, int>(nameof(MaxLineNumber));

        public int MaxLineNumber
        {
            get => GetValue(MaxLineNumberProperty);
            set => SetValue(MaxLineNumberProperty, value);
        }

        public static readonly StyledProperty<bool> IsOldSideProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, bool>(nameof(IsOldSide));

        public bool IsOldSide
        {
            get => GetValue(IsOldSideProperty);
            set => SetValue(IsOldSideProperty, value);
        }

        public static readonly StyledProperty<IBrush> EmptyContentBackgroundProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, IBrush>(nameof(EmptyContentBackground), new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)));

        public IBrush EmptyContentBackground
        {
            get => GetValue(EmptyContentBackgroundProperty);
            set => SetValue(EmptyContentBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> AddedContentBackgroundProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, IBrush>(nameof(AddedContentBackground), new SolidColorBrush(Color.FromArgb(60, 0, 255, 0)));

        public IBrush AddedContentBackground
        {
            get => GetValue(AddedContentBackgroundProperty);
            set => SetValue(AddedContentBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> DeletedContentBackgroundProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, IBrush>(nameof(DeletedContentBackground), new SolidColorBrush(Color.FromArgb(60, 255, 0, 0)));

        public IBrush DeletedContentBackground
        {
            get => GetValue(DeletedContentBackgroundProperty);
            set => SetValue(DeletedContentBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> AddedHighlightBrushProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, IBrush>(nameof(AddedHighlightBrush), new SolidColorBrush(Color.FromArgb(90, 0, 255, 0)));

        public IBrush AddedHighlightBrush
        {
            get => GetValue(AddedHighlightBrushProperty);
            set => SetValue(AddedHighlightBrushProperty, value);
        }

        public static readonly StyledProperty<IBrush> DeletedHighlightBrushProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, IBrush>(nameof(DeletedHighlightBrush), new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)));

        public IBrush DeletedHighlightBrush
        {
            get => GetValue(DeletedHighlightBrushProperty);
            set => SetValue(DeletedHighlightBrushProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public MergeDiffPresenter() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = true;
            ShowLineNumbers = false;
            BorderThickness = new Thickness(0);

            Options.EnableHyperlinks = false;
            Options.EnableEmailHyperlinks = false;
            Options.AllowScrollBelowDocument = false;

            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.LeftMargins.Add(new MergeDiffLineNumberMargin(this));
            TextArea.LeftMargins.Add(new MergeDiffVerticalSeparatorMargin(this));
            TextArea.TextView.BackgroundRenderers.Add(new MergeDiffLineBackgroundRenderer(this));
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            _textMate = Models.TextMateHelper.CreateForEditor(this);
            if (!string.IsNullOrEmpty(FileName))
                Models.TextMateHelper.SetGrammarByFileName(_textMate, FileName);

            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;

            if (_textMate != null)
            {
                _textMate.Dispose();
                _textMate = null;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == DiffLinesProperty)
            {
                UpdateContent();
            }
            else if (change.Property == FileNameProperty)
            {
                if (_textMate != null && !string.IsNullOrEmpty(FileName))
                    Models.TextMateHelper.SetGrammarByFileName(_textMate, FileName);
            }
            else if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue != null)
            {
                Models.TextMateHelper.SetThemeByApp(_textMate);
            }
        }

        private void UpdateContent()
        {
            var lines = DiffLines;
            if (lines == null || lines.Count == 0)
            {
                Text = string.Empty;
                return;
            }

            var builder = new StringBuilder();
            foreach (var line in lines)
            {
                if (line.Content.Length > 1000)
                {
                    builder.Append(line.Content.AsSpan(0, 1000));
                    builder.Append($"...({line.Content.Length - 1000} characters trimmed)");
                }
                else
                {
                    builder.Append(line.Content);
                }

                builder.Append('\n');
            }

            Text = builder.ToString();
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selected = SelectedText;
            if (string.IsNullOrEmpty(selected))
                return;

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, ev) =>
            {
                await App.CopyTextAsync(selected);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);

            e.Handled = true;
        }

        private TextMate.Installation _textMate;
    }

    public class MergeDiffLineNumberMargin : AbstractMargin
    {
        public MergeDiffLineNumberMargin(MergeDiffPresenter presenter)
        {
            _presenter = presenter;
            Margin = new Thickness(8, 0);
            ClipToBounds = true;
        }

        public override void Render(DrawingContext context)
        {
            var lines = _presenter.DiffLines;
            if (lines == null)
                return;

            var view = TextView;
            if (view is not { VisualLinesValid: true })
                return;

            var isOld = _presenter.IsOldSide;
            var typeface = view.CreateTypeface();

            foreach (var line in view.VisualLines)
            {
                if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                    continue;

                var index = line.FirstDocumentLine.LineNumber;
                if (index > lines.Count)
                    break;

                var info = lines[index - 1];
                var lineNumber = isOld ? info.OldLine : info.NewLine;
                if (string.IsNullOrEmpty(lineNumber))
                    continue;

                var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineMiddle) - view.VerticalOffset;
                var txt = new FormattedText(
                    lineNumber,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    _presenter.FontSize,
                    _presenter.Foreground);
                context.DrawText(txt, new Point(Bounds.Width - txt.Width, y - (txt.Height * 0.5)));
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var maxLine = _presenter.MaxLineNumber;
            if (maxLine == 0)
                return new Size(32, 0);

            var typeface = TextView.CreateTypeface();
            var test = new FormattedText(
                $"{maxLine}",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                _presenter.FontSize,
                Brushes.White);
            return new Size(test.Width, 0);
        }

        private readonly MergeDiffPresenter _presenter;
    }

    public class MergeDiffVerticalSeparatorMargin : AbstractMargin
    {
        public MergeDiffVerticalSeparatorMargin(MergeDiffPresenter presenter)
        {
            _presenter = presenter;
        }

        public override void Render(DrawingContext context)
        {
            var pen = new Pen(Brushes.DarkGray);
            context.DrawLine(pen, new Point(0, 0), new Point(0, Bounds.Height));
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(1, 0);
        }

        private readonly MergeDiffPresenter _presenter;
    }

    public class MergeDiffLineBackgroundRenderer : IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Background;

        public MergeDiffLineBackgroundRenderer(MergeDiffPresenter presenter)
        {
            _presenter = presenter;
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            var lines = _presenter.DiffLines;
            if (lines == null || _presenter.Document == null || !textView.VisualLinesValid)
                return;

            var width = textView.Bounds.Width;
            foreach (var line in textView.VisualLines)
            {
                if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                    continue;

                var index = line.FirstDocumentLine.LineNumber;
                if (index > lines.Count)
                    break;

                var info = lines[index - 1];

                var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - textView.VerticalOffset;
                var endY = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.LineBottom) - textView.VerticalOffset;

                var bg = GetBrushByLineType(info.Type);
                if (bg != null)
                {
                    drawingContext.DrawRectangle(bg, null, new Rect(0, startY, width, endY - startY));

                    // Draw inline highlights
                    if (info.Highlights.Count > 0)
                    {
                        var highlightBG = info.Type == Models.TextDiffLineType.Added
                            ? _presenter.AddedHighlightBrush
                            : _presenter.DeletedHighlightBrush;

                        var processingIdxStart = 0;
                        var processingIdxEnd = 0;
                        var nextHighlight = 0;

                        foreach (var tl in line.TextLines)
                        {
                            processingIdxEnd += tl.Length;

                            var y = line.GetTextLineVisualYPosition(tl, VisualYPosition.LineTop) - textView.VerticalOffset;
                            var h = line.GetTextLineVisualYPosition(tl, VisualYPosition.LineBottom) - textView.VerticalOffset - y;

                            while (nextHighlight < info.Highlights.Count)
                            {
                                var highlight = info.Highlights[nextHighlight];
                                if (highlight.Start >= processingIdxEnd)
                                    break;

                                var start = line.GetVisualColumn(highlight.Start < processingIdxStart ? processingIdxStart : highlight.Start);
                                var end = line.GetVisualColumn(highlight.End >= processingIdxEnd ? processingIdxEnd : highlight.End + 1);

                                var x = line.GetTextLineVisualXPosition(tl, start) - textView.HorizontalOffset;
                                var w = line.GetTextLineVisualXPosition(tl, end) - textView.HorizontalOffset - x;
                                var rect = new Rect(x, y, w, h);
                                drawingContext.DrawRectangle(highlightBG, null, rect);

                                if (highlight.End >= processingIdxEnd)
                                    break;

                                nextHighlight++;
                            }

                            processingIdxStart = processingIdxEnd;
                        }
                    }
                }
            }
        }

        private IBrush GetBrushByLineType(Models.TextDiffLineType type)
        {
            return type switch
            {
                Models.TextDiffLineType.None => _presenter.EmptyContentBackground,
                Models.TextDiffLineType.Added => _presenter.AddedContentBackground,
                Models.TextDiffLineType.Deleted => _presenter.DeletedContentBackground,
                _ => null,
            };
        }

        private readonly MergeDiffPresenter _presenter;
    }

    public class MergeConflictBackgroundRenderer : IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Background;

        private enum ConflictSection
        {
            None,
            MineMarker,     // <<<<<<< line
            MineContent,    // Content between <<<<<<< and ||||||| or =======
            BaseMarker,     // ||||||| line (diff3)
            BaseContent,    // Content between ||||||| and =======
            Separator,      // ======= line
            TheirsContent,  // Content between ======= and >>>>>>>
            TheirsMarker    // >>>>>>> line
        }

        public MergeConflictBackgroundRenderer(MergeTextEditor editor)
        {
            _editor = editor;
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (!_editor.HighlightConflictMarkers || _editor.Document == null || !textView.VisualLinesValid)
                return;

            // Build a map of line numbers to their conflict sections
            var lineSections = new Dictionary<int, ConflictSection>();
            var currentSection = ConflictSection.None;

            for (int i = 1; i <= _editor.Document.LineCount; i++)
            {
                var docLine = _editor.Document.GetLineByNumber(i);
                var lineText = _editor.Document.GetText(docLine.Offset, docLine.Length);

                if (lineText.StartsWith("<<<<<<<", StringComparison.Ordinal))
                {
                    currentSection = ConflictSection.MineMarker;
                    lineSections[i] = currentSection;
                    currentSection = ConflictSection.MineContent;
                }
                else if (lineText.StartsWith("|||||||", StringComparison.Ordinal))
                {
                    lineSections[i] = ConflictSection.BaseMarker;
                    currentSection = ConflictSection.BaseContent;
                }
                else if (lineText.StartsWith("=======", StringComparison.Ordinal))
                {
                    lineSections[i] = ConflictSection.Separator;
                    currentSection = ConflictSection.TheirsContent;
                }
                else if (lineText.StartsWith(">>>>>>>", StringComparison.Ordinal))
                {
                    lineSections[i] = ConflictSection.TheirsMarker;
                    currentSection = ConflictSection.None;
                }
                else if (currentSection != ConflictSection.None)
                {
                    lineSections[i] = currentSection;
                }
            }

            // Draw backgrounds for visible lines
            var width = textView.Bounds.Width;
            foreach (var line in textView.VisualLines)
            {
                if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                    continue;

                var lineNumber = line.FirstDocumentLine.LineNumber;
                if (!lineSections.TryGetValue(lineNumber, out var section))
                    continue;

                var brush = GetBrushForSection(section);
                if (brush != null)
                {
                    var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - textView.VerticalOffset;
                    var endY = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.LineBottom) - textView.VerticalOffset;
                    drawingContext.DrawRectangle(brush, null, new Rect(0, startY, width, endY - startY));
                }
            }
        }

        private IBrush GetBrushForSection(ConflictSection section)
        {
            return section switch
            {
                ConflictSection.MineMarker => _editor.ConflictMineMarkerBrush,
                ConflictSection.MineContent => _editor.ConflictMineContentBrush,
                ConflictSection.BaseMarker => _editor.ConflictBaseMarkerBrush,
                ConflictSection.BaseContent => _editor.ConflictBaseContentBrush,
                ConflictSection.Separator => _editor.ConflictSeparatorBrush,
                ConflictSection.TheirsContent => _editor.ConflictTheirsContentBrush,
                ConflictSection.TheirsMarker => _editor.ConflictTheirsMarkerBrush,
                _ => null
            };
        }

        private readonly MergeTextEditor _editor;
    }

    public class MergeTextEditor : TextEditor
    {
        public static readonly StyledProperty<string> FilePathProperty =
            AvaloniaProperty.Register<MergeTextEditor, string>(nameof(FilePath));

        public string FilePath
        {
            get => GetValue(FilePathProperty);
            set => SetValue(FilePathProperty, value);
        }

        public static readonly StyledProperty<bool> HighlightConflictMarkersProperty =
            AvaloniaProperty.Register<MergeTextEditor, bool>(nameof(HighlightConflictMarkers), false);

        public bool HighlightConflictMarkers
        {
            get => GetValue(HighlightConflictMarkersProperty);
            set => SetValue(HighlightConflictMarkersProperty, value);
        }

        public static readonly StyledProperty<string> ContentProperty =
            AvaloniaProperty.Register<MergeTextEditor, string>(nameof(Content), string.Empty);

        public string Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        // Brush for <<<<<<< marker line
        public static readonly StyledProperty<IBrush> ConflictMineMarkerBrushProperty =
            AvaloniaProperty.Register<MergeTextEditor, IBrush>(nameof(ConflictMineMarkerBrush), new SolidColorBrush(Color.FromArgb(120, 0, 120, 215)));

        public IBrush ConflictMineMarkerBrush
        {
            get => GetValue(ConflictMineMarkerBrushProperty);
            set => SetValue(ConflictMineMarkerBrushProperty, value);
        }

        // Brush for content between <<<<<<< and ======= (mine/ours content)
        public static readonly StyledProperty<IBrush> ConflictMineContentBrushProperty =
            AvaloniaProperty.Register<MergeTextEditor, IBrush>(nameof(ConflictMineContentBrush), new SolidColorBrush(Color.FromArgb(60, 0, 120, 215)));

        public IBrush ConflictMineContentBrush
        {
            get => GetValue(ConflictMineContentBrushProperty);
            set => SetValue(ConflictMineContentBrushProperty, value);
        }

        // Brush for ||||||| marker line (diff3 base)
        public static readonly StyledProperty<IBrush> ConflictBaseMarkerBrushProperty =
            AvaloniaProperty.Register<MergeTextEditor, IBrush>(nameof(ConflictBaseMarkerBrush), new SolidColorBrush(Color.FromArgb(120, 128, 128, 128)));

        public IBrush ConflictBaseMarkerBrush
        {
            get => GetValue(ConflictBaseMarkerBrushProperty);
            set => SetValue(ConflictBaseMarkerBrushProperty, value);
        }

        // Brush for content between ||||||| and ======= (base content in diff3)
        public static readonly StyledProperty<IBrush> ConflictBaseContentBrushProperty =
            AvaloniaProperty.Register<MergeTextEditor, IBrush>(nameof(ConflictBaseContentBrush), new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)));

        public IBrush ConflictBaseContentBrush
        {
            get => GetValue(ConflictBaseContentBrushProperty);
            set => SetValue(ConflictBaseContentBrushProperty, value);
        }

        // Brush for ======= separator line
        public static readonly StyledProperty<IBrush> ConflictSeparatorBrushProperty =
            AvaloniaProperty.Register<MergeTextEditor, IBrush>(nameof(ConflictSeparatorBrush), new SolidColorBrush(Color.FromArgb(100, 128, 128, 128)));

        public IBrush ConflictSeparatorBrush
        {
            get => GetValue(ConflictSeparatorBrushProperty);
            set => SetValue(ConflictSeparatorBrushProperty, value);
        }

        // Brush for content between ======= and >>>>>>> (theirs content)
        public static readonly StyledProperty<IBrush> ConflictTheirsContentBrushProperty =
            AvaloniaProperty.Register<MergeTextEditor, IBrush>(nameof(ConflictTheirsContentBrush), new SolidColorBrush(Color.FromArgb(60, 215, 120, 0)));

        public IBrush ConflictTheirsContentBrush
        {
            get => GetValue(ConflictTheirsContentBrushProperty);
            set => SetValue(ConflictTheirsContentBrushProperty, value);
        }

        // Brush for >>>>>>> marker line
        public static readonly StyledProperty<IBrush> ConflictTheirsMarkerBrushProperty =
            AvaloniaProperty.Register<MergeTextEditor, IBrush>(nameof(ConflictTheirsMarkerBrush), new SolidColorBrush(Color.FromArgb(120, 215, 120, 0)));

        public IBrush ConflictTheirsMarkerBrush
        {
            get => GetValue(ConflictTheirsMarkerBrushProperty);
            set => SetValue(ConflictTheirsMarkerBrushProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public MergeTextEditor() : base(new TextArea(), new TextDocument())
        {
            ShowLineNumbers = true;
            WordWrap = false;
            Options.EnableHyperlinks = false;
            Options.EnableEmailHyperlinks = false;

            _textMate = Models.TextMateHelper.CreateForEditor(this);

            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
            TextArea.TextView.BackgroundRenderers.Add(new MergeConflictBackgroundRenderer(this));
        }

        public void ScrollToConflictLine(int lineNumber)
        {
            if (lineNumber > 0 && lineNumber <= Document.LineCount)
            {
                var line = Document.GetLineByNumber(lineNumber);
                ScrollTo(lineNumber, 0);
                TextArea.Caret.Offset = line.Offset;
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;

            if (_textMate != null)
            {
                _textMate.Dispose();
                _textMate = null;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == FilePathProperty)
            {
                if (FilePath is { Length: > 0 })
                    Models.TextMateHelper.SetGrammarByFileName(_textMate, FilePath);
            }
            else if (change.Property == ContentProperty)
            {
                Text = Content ?? string.Empty;
            }
            else if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue != null)
            {
                Models.TextMateHelper.SetThemeByApp(_textMate);
            }
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selected = SelectedText;
            if (string.IsNullOrEmpty(selected))
                return;

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, ev) =>
            {
                await App.CopyTextAsync(selected);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);

            e.Handled = true;
        }

        private TextMate.Installation _textMate = null;
    }

    public partial class ThreeWayMerge : ChromelessWindow
    {
        public ThreeWayMerge()
        {
            InitializeComponent();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            // Set up scroll synchronization for side-by-side editors
            SetupScrollSync();

            // Set up text change tracking for result editor
            SetupResultEditorBinding();

            // Watch for content loaded to scroll to first conflict
            if (DataContext is ViewModels.ThreeWayMerge vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.ThreeWayMerge.IsLoading))
            {
                if (DataContext is ViewModels.ThreeWayMerge vm && !vm.IsLoading)
                {
                    // Content loaded, scroll to first conflict after a short delay to let UI render
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        ScrollToCurrentConflict();
                    }, Avalonia.Threading.DispatcherPriority.Loaded);
                }
            }
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (DataContext is ViewModels.ThreeWayMerge vm && vm.HasUnsavedChanges())
            {
                e.Cancel = true;
                var result = await App.AskConfirmAsync(App.Text("ThreeWayMerge.UnsavedChanges"));
                if (result)
                {
                    Close();
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            GC.Collect();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Handled)
                return;

            var vm = DataContext as ViewModels.ThreeWayMerge;
            if (vm == null)
                return;

            var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

            if (e.KeyModifiers == modifier)
            {
                // Ctrl+S to save
                if (e.Key == Key.S && vm.CanSave)
                {
                    _ = SaveAndCloseAsync();
                    e.Handled = true;
                }
                // Ctrl+Up to go to previous conflict
                else if (e.Key == Key.Up && vm.HasPrevConflict)
                {
                    vm.GotoPrevConflict();
                    ScrollToCurrentConflict();
                    e.Handled = true;
                }
                // Ctrl+Down to go to next conflict
                else if (e.Key == Key.Down && vm.HasNextConflict)
                {
                    vm.GotoNextConflict();
                    ScrollToCurrentConflict();
                    e.Handled = true;
                }
            }
        }

        private void OnUseCurrentMine(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ThreeWayMerge vm)
            {
                vm.AcceptCurrentOurs();
                ScrollToCurrentConflict();
            }
            e.Handled = true;
        }

        private void OnUseCurrentTheirs(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ThreeWayMerge vm)
            {
                vm.AcceptCurrentTheirs();
                ScrollToCurrentConflict();
            }
            e.Handled = true;
        }

        private void OnAcceptMine(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ThreeWayMerge vm)
            {
                vm.AcceptOurs();
            }
            e.Handled = true;
        }

        private void OnAcceptTheirs(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ThreeWayMerge vm)
            {
                vm.AcceptTheirs();
            }
            e.Handled = true;
        }

        private void OnGotoPrevConflict(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ThreeWayMerge vm && vm.HasPrevConflict)
            {
                vm.GotoPrevConflict();
                ScrollToCurrentConflict();
            }
            e.Handled = true;
        }

        private void OnGotoNextConflict(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ThreeWayMerge vm && vm.HasNextConflict)
            {
                vm.GotoNextConflict();
                ScrollToCurrentConflict();
            }
            e.Handled = true;
        }

        private async void OnSaveAndStage(object sender, RoutedEventArgs e)
        {
            await SaveAndCloseAsync();
            e.Handled = true;
        }

        private async Task SaveAndCloseAsync()
        {
            if (DataContext is ViewModels.ThreeWayMerge vm)
            {
                var success = await vm.SaveAndStageAsync();
                if (success)
                {
                    Close();
                }
            }
        }

        private void SetupScrollSync()
        {
            // Sync scrolling only between Mine and Theirs diff presenters (they have aligned content)
            var oursPresenter = this.FindControl<MergeDiffPresenter>("OursPresenter");
            var theirsPresenter = this.FindControl<MergeDiffPresenter>("TheirsPresenter");

            if (oursPresenter != null)
                _oursScrollViewer = oursPresenter.FindDescendantOfType<ScrollViewer>();
            if (theirsPresenter != null)
                _theirsScrollViewer = theirsPresenter.FindDescendantOfType<ScrollViewer>();

            // Set up scroll sync only for MINE/THEIRS (they are properly aligned)
            if (_oursScrollViewer != null)
                _oursScrollViewer.ScrollChanged += OnOursTheirsScrollChanged;
            if (_theirsScrollViewer != null)
                _theirsScrollViewer.ScrollChanged += OnOursTheirsScrollChanged;

            // Note: BASE and RESULT have different line counts and content structure,
            // so scroll syncing with them doesn't make sense without proper line alignment.
        }

        private void OnOursTheirsScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncingScroll)
                return;

            _isSyncingScroll = true;

            var sourceScrollViewer = sender as ScrollViewer;
            if (sourceScrollViewer == null)
            {
                _isSyncingScroll = false;
                return;
            }

            var offset = sourceScrollViewer.Offset;

            // Sync MINE and THEIRS (side-by-side, aligned content)
            if (_oursScrollViewer != null && _oursScrollViewer != sourceScrollViewer)
                _oursScrollViewer.Offset = offset;
            if (_theirsScrollViewer != null && _theirsScrollViewer != sourceScrollViewer)
                _theirsScrollViewer.Offset = offset;

            _isSyncingScroll = false;
        }

        private void SetupResultEditorBinding()
        {
            var resultEditor = this.FindControl<MergeTextEditor>("ResultEditor");
            if (resultEditor != null)
            {
                resultEditor.TextChanged += (s, e) =>
                {
                    if (DataContext is ViewModels.ThreeWayMerge vm && !_isUpdatingContent)
                    {
                        _isUpdatingContent = true;
                        vm.ResultContent = resultEditor.Text;
                        _isUpdatingContent = false;
                    }
                };
            }
        }

        private void ScrollToCurrentConflict()
        {
            if (DataContext is ViewModels.ThreeWayMerge vm && vm.CurrentConflictLine >= 0)
            {
                // Disable scroll sync during programmatic scrolling
                _isSyncingScroll = true;

                var resultEditor = this.FindControl<MergeTextEditor>("ResultEditor");
                resultEditor?.ScrollToConflictLine(vm.CurrentConflictLine + 1);

                // Re-enable scroll sync after a short delay to let the scroll complete
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _isSyncingScroll = false;
                }, Avalonia.Threading.DispatcherPriority.Background);
            }
        }

        private bool _isSyncingScroll = false;
        private bool _isUpdatingContent = false;
        private ScrollViewer _oursScrollViewer;
        private ScrollViewer _theirsScrollViewer;
    }
}
