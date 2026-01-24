using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
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

        public static readonly StyledProperty<bool> IsResultPanelProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, bool>(nameof(IsResultPanel), false);

        public bool IsResultPanel
        {
            get => GetValue(IsResultPanelProperty);
            set => SetValue(IsResultPanelProperty, value);
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

        public static readonly StyledProperty<IBrush> IndicatorBackgroundProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, IBrush>(nameof(IndicatorBackground), new SolidColorBrush(Color.FromArgb(100, 100, 100, 100)));

        public IBrush IndicatorBackground
        {
            get => GetValue(IndicatorBackgroundProperty);
            set => SetValue(IndicatorBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> MineContentBackgroundProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, IBrush>(nameof(MineContentBackground), new SolidColorBrush(Color.FromArgb(60, 0, 120, 215)));

        public IBrush MineContentBackground
        {
            get => GetValue(MineContentBackgroundProperty);
            set => SetValue(MineContentBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> TheirsContentBackgroundProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, IBrush>(nameof(TheirsContentBackground), new SolidColorBrush(Color.FromArgb(60, 255, 140, 0)));

        public IBrush TheirsContentBackground
        {
            get => GetValue(TheirsContentBackgroundProperty);
            set => SetValue(TheirsContentBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> CurrentConflictHighlightProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, IBrush>(nameof(CurrentConflictHighlight), new SolidColorBrush(Color.FromArgb(40, 255, 255, 0)));

        public IBrush CurrentConflictHighlight
        {
            get => GetValue(CurrentConflictHighlightProperty);
            set => SetValue(CurrentConflictHighlightProperty, value);
        }

        public static readonly StyledProperty<int> CurrentConflictStartLineProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, int>(nameof(CurrentConflictStartLine), -1);

        public int CurrentConflictStartLine
        {
            get => GetValue(CurrentConflictStartLineProperty);
            set => SetValue(CurrentConflictStartLineProperty, value);
        }

        public static readonly StyledProperty<int> CurrentConflictEndLineProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, int>(nameof(CurrentConflictEndLine), -1);

        public int CurrentConflictEndLine
        {
            get => GetValue(CurrentConflictEndLineProperty);
            set => SetValue(CurrentConflictEndLineProperty, value);
        }

        public static readonly StyledProperty<List<(int Start, int End)>> ResolvedConflictRangesProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, List<(int Start, int End)>>(nameof(ResolvedConflictRanges));

        public List<(int Start, int End)> ResolvedConflictRanges
        {
            get => GetValue(ResolvedConflictRangesProperty);
            set => SetValue(ResolvedConflictRangesProperty, value);
        }

        public static readonly StyledProperty<List<(int Start, int End)>> AllConflictRangesProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, List<(int Start, int End)>>(nameof(AllConflictRanges));

        public List<(int Start, int End)> AllConflictRanges
        {
            get => GetValue(AllConflictRangesProperty);
            set => SetValue(AllConflictRangesProperty, value);
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
            TextArea.TextView.LineTransformers.Add(new MergeDiffIndicatorTransformer(this));
        }

        public ScrollViewer GetScrollViewer()
        {
            return _scrollViewer;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            _textMate = Models.TextMateHelper.CreateForEditor(this);
            if (!string.IsNullOrEmpty(FileName))
                Models.TextMateHelper.SetGrammarByFileName(_textMate, FileName);

            TextArea.TextView.ContextRequested += OnTextViewContextRequested;

            _scrollViewer = this.FindDescendantOfType<ScrollViewer>();
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnScrollViewerScrollChanged;
                _scrollViewer.Bind(ScrollViewer.OffsetProperty, new Avalonia.Data.Binding("ScrollOffset", Avalonia.Data.BindingMode.OneWay));
            }
        }

        private void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_scrollViewer == null || DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            if (vm.ScrollOffset.NearlyEquals(_scrollViewer.Offset))
                return;

            if (IsPointerOver || e.OffsetDelta.SquaredLength > 1.0f)
            {
                vm.ScrollOffset = _scrollViewer.Offset;
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged -= OnScrollViewerScrollChanged;
                _scrollViewer = null;
            }

            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;

            if (_textMate != null)
            {
                _textMate.Dispose();
                _textMate = null;
            }

            base.OnUnloaded(e);
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
            else if (change.Property == CurrentConflictStartLineProperty ||
                     change.Property == CurrentConflictEndLineProperty ||
                     change.Property == ResolvedConflictRangesProperty ||
                     change.Property == AllConflictRangesProperty)
            {
                TextArea.TextView.InvalidateVisual();
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
        private ScrollViewer _scrollViewer;
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
            var isResult = _presenter.IsResultPanel;
            var typeface = view.CreateTypeface();

            foreach (var line in view.VisualLines)
            {
                if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                    continue;

                var index = line.FirstDocumentLine.LineNumber;
                if (index > lines.Count)
                    break;

                var info = lines[index - 1];

                string lineNumber;
                if (isResult)
                    lineNumber = info.NewLine;
                else
                    lineNumber = isOld ? info.OldLine : info.NewLine;

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

    public class MergeDiffIndicatorElementGenerator : VisualLineElementGenerator
    {
        public MergeDiffIndicatorElementGenerator(MergeDiffPresenter presenter)
        {
            _presenter = presenter;
        }

        public override int GetFirstInterestedOffset(int startOffset)
        {
            return -1; // We don't generate inline elements
        }

        public override VisualLineElement ConstructElement(int offset)
        {
            return null;
        }

        private readonly MergeDiffPresenter _presenter;
    }

    public class MergeDiffIndicatorTransformer : DocumentColorizingTransformer
    {
        public MergeDiffIndicatorTransformer(MergeDiffPresenter presenter)
        {
            _presenter = presenter;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            var lines = _presenter.DiffLines;
            if (lines == null || line.LineNumber > lines.Count)
                return;

            var info = lines[line.LineNumber - 1];
            if (info.Type == Models.TextDiffLineType.Indicator)
            {
                // Make indicator lines (conflict markers) italic and gray
                ChangeLinePart(line.Offset, line.EndOffset, element =>
                {
                    element.TextRunProperties.SetTypeface(new Typeface(
                        _presenter.FontFamily,
                        FontStyle.Italic,
                        FontWeight.Normal));
                    element.TextRunProperties.SetForegroundBrush(Brushes.Gray);
                });
            }
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
            var conflictStart = _presenter.CurrentConflictStartLine;
            var conflictEnd = _presenter.CurrentConflictEndLine;
            var allConflictRanges = _presenter.AllConflictRanges;

            foreach (var line in textView.VisualLines)
            {
                if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                    continue;

                var index = line.FirstDocumentLine.LineNumber;
                if (index > lines.Count)
                    break;

                var info = lines[index - 1];
                var lineIndex = index - 1;

                var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - textView.VerticalOffset;
                var endY = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.LineBottom) - textView.VerticalOffset;

                // Check if this line is in the current conflict
                bool isCurrentConflict = conflictStart >= 0 && conflictEnd >= 0 && lineIndex >= conflictStart && lineIndex <= conflictEnd;

                // Check if this line is in any OTHER conflict (not the current one) - should be faded
                bool isInOtherConflict = false;
                if (!isCurrentConflict && allConflictRanges != null)
                {
                    foreach (var range in allConflictRanges)
                    {
                        if (lineIndex >= range.Start && lineIndex <= range.End)
                        {
                            isInOtherConflict = true;
                            break;
                        }
                    }
                }

                // No yellow highlight - just use saturation difference
                // Current conflict = full color, other conflicts = desaturated
                var bg = GetBrushByLineType(info.Type, isInOtherConflict);
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

        private IBrush GetBrushByLineType(Models.TextDiffLineType type, bool isDesaturated = false)
        {
            IBrush brush;
            if (_presenter.IsResultPanel)
            {
                brush = type switch
                {
                    Models.TextDiffLineType.None => _presenter.EmptyContentBackground,
                    Models.TextDiffLineType.Added => _presenter.TheirsContentBackground,
                    Models.TextDiffLineType.Deleted => _presenter.MineContentBackground,
                    Models.TextDiffLineType.Indicator => _presenter.IndicatorBackground,
                    _ => null,
                };
            }
            else
            {
                brush = type switch
                {
                    Models.TextDiffLineType.None => _presenter.EmptyContentBackground,
                    Models.TextDiffLineType.Added => _presenter.AddedContentBackground,
                    Models.TextDiffLineType.Deleted => _presenter.DeletedContentBackground,
                    _ => null,
                };
            }

            // Apply desaturation for resolved conflicts (reduce opacity)
            if (isDesaturated && brush is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                // Reduce opacity by 70% to make it look faded/desaturated
                var desaturatedColor = Color.FromArgb((byte)(color.A * 0.3), color.R, color.G, color.B);
                return new SolidColorBrush(desaturatedColor);
            }

            return brush;
        }

        private readonly MergeDiffPresenter _presenter;
    }

    public partial class MergeConflictEditor : ChromelessWindow
    {
        public MergeConflictEditor()
        {
            InitializeComponent();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            // Get presenter references
            _oursPresenter = this.FindControl<MergeDiffPresenter>("OursPresenter");
            _theirsPresenter = this.FindControl<MergeDiffPresenter>("TheirsPresenter");
            _resultPresenter = this.FindControl<MergeDiffPresenter>("ResultPresenter");

            if (DataContext is ViewModels.MergeConflictEditor vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.MergeConflictEditor.IsLoading))
            {
                if (DataContext is ViewModels.MergeConflictEditor vm && !vm.IsLoading)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        UpdateCurrentConflictHighlight();
                        UpdateResolvedRanges();
                        ScrollToCurrentConflict();
                    }, Avalonia.Threading.DispatcherPriority.Loaded);
                }
            }
            else if (e.PropertyName == nameof(ViewModels.MergeConflictEditor.CurrentConflictLine))
            {
                UpdateCurrentConflictHighlight();
            }
            else if (e.PropertyName == nameof(ViewModels.MergeConflictEditor.ResolvedConflictRanges) ||
                     e.PropertyName == nameof(ViewModels.MergeConflictEditor.AllConflictRanges))
            {
                UpdateResolvedRanges();
            }
        }

        private void UpdateResolvedRanges()
        {
            if (DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            var allRanges = vm.AllConflictRanges;

            if (_oursPresenter != null)
                _oursPresenter.AllConflictRanges = allRanges;
            if (_theirsPresenter != null)
                _theirsPresenter.AllConflictRanges = allRanges;
            // Note: Result panel doesn't use conflict ranges since it shows current state
        }

        private void UpdateCurrentConflictHighlight()
        {
            if (DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            var startLine = vm.CurrentConflictStartLine;
            var endLine = vm.CurrentConflictEndLine;

            if (_oursPresenter != null)
            {
                _oursPresenter.CurrentConflictStartLine = startLine;
                _oursPresenter.CurrentConflictEndLine = endLine;
            }
            if (_theirsPresenter != null)
            {
                _theirsPresenter.CurrentConflictStartLine = startLine;
                _theirsPresenter.CurrentConflictEndLine = endLine;
            }
            if (_resultPresenter != null)
            {
                _resultPresenter.CurrentConflictStartLine = startLine;
                _resultPresenter.CurrentConflictEndLine = endLine;
            }
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (_forceClose)
                return;

            if (DataContext is ViewModels.MergeConflictEditor vm && vm.HasUnsavedChanges())
            {
                e.Cancel = true;
                var result = await App.AskConfirmAsync(App.Text("Text.MergeConflictEditor.UnsavedChanges"));
                if (result)
                {
                    _forceClose = true;
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

            var vm = DataContext as ViewModels.MergeConflictEditor;
            if (vm == null)
                return;

            var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

            if (e.KeyModifiers == modifier)
            {
                if (e.Key == Key.S && vm.CanSave)
                {
                    _ = SaveAndCloseAsync();
                    e.Handled = true;
                }
                else if (e.Key == Key.Up && vm.HasPrevConflict)
                {
                    vm.GotoPrevConflict();
                    UpdateCurrentConflictHighlight();
                    ScrollToCurrentConflict();
                    e.Handled = true;
                }
                else if (e.Key == Key.Down && vm.HasNextConflict)
                {
                    vm.GotoNextConflict();
                    UpdateCurrentConflictHighlight();
                    ScrollToCurrentConflict();
                    e.Handled = true;
                }
            }
        }

        private void OnGotoPrevConflict(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MergeConflictEditor vm && vm.HasPrevConflict)
            {
                vm.GotoPrevConflict();
                UpdateCurrentConflictHighlight();
                ScrollToCurrentConflict();
            }
            e.Handled = true;
        }

        private void OnGotoNextConflict(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MergeConflictEditor vm && vm.HasNextConflict)
            {
                vm.GotoNextConflict();
                UpdateCurrentConflictHighlight();
                ScrollToCurrentConflict();
            }
            e.Handled = true;
        }

        private void OnUseCurrentMine(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MergeConflictEditor vm)
            {
                var savedOffset = SaveScrollOffset();
                vm.AcceptCurrentOurs();
                UpdateCurrentConflictHighlight();
                UpdateResolvedRanges();
                RestoreScrollOffset(savedOffset);
            }
            e.Handled = true;
        }

        private void OnUseCurrentTheirs(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MergeConflictEditor vm)
            {
                var savedOffset = SaveScrollOffset();
                vm.AcceptCurrentTheirs();
                UpdateCurrentConflictHighlight();
                UpdateResolvedRanges();
                RestoreScrollOffset(savedOffset);
            }
            e.Handled = true;
        }

        private void OnAcceptMine(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MergeConflictEditor vm)
            {
                var savedOffset = SaveScrollOffset();
                vm.AcceptOurs();
                UpdateCurrentConflictHighlight();
                UpdateResolvedRanges();
                RestoreScrollOffset(savedOffset);
            }
            e.Handled = true;
        }

        private void OnAcceptTheirs(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MergeConflictEditor vm)
            {
                var savedOffset = SaveScrollOffset();
                vm.AcceptTheirs();
                UpdateCurrentConflictHighlight();
                UpdateResolvedRanges();
                RestoreScrollOffset(savedOffset);
            }
            e.Handled = true;
        }

        private Vector SaveScrollOffset()
        {
            if (DataContext is ViewModels.MergeConflictEditor vm)
                return vm.ScrollOffset;
            return new Vector(0, 0);
        }

        private void RestoreScrollOffset(Vector offset)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is ViewModels.MergeConflictEditor vm)
                    vm.ScrollOffset = offset;
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        private async void OnSaveAndStage(object sender, RoutedEventArgs e)
        {
            await SaveAndCloseAsync();
            e.Handled = true;
        }

        private async Task SaveAndCloseAsync()
        {
            if (DataContext is ViewModels.MergeConflictEditor vm)
            {
                var success = await vm.SaveAndStageAsync();
                if (success)
                {
                    _forceClose = true;
                    Close();
                }
            }
        }

        private void ScrollToCurrentConflict()
        {
            if (DataContext is ViewModels.MergeConflictEditor vm && vm.CurrentConflictLine >= 0)
            {
                if (_oursPresenter != null)
                {
                    var lineHeight = _oursPresenter.TextArea.TextView.DefaultLineHeight;
                    var vOffset = lineHeight * vm.CurrentConflictLine;
                    var targetOffset = new Vector(0, Math.Max(0, vOffset - _oursPresenter.Bounds.Height * 0.3));

                    // Set scroll offset via ViewModel - binding will sync all presenters
                    vm.ScrollOffset = targetOffset;
                }
            }
        }

        private bool _forceClose = false;
        private MergeDiffPresenter _oursPresenter;
        private MergeDiffPresenter _theirsPresenter;
        private MergeDiffPresenter _resultPresenter;
    }
}
