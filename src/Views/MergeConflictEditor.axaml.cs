using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

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

        public static readonly StyledProperty<Models.ConflictPanelType> PanelTypeProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, Models.ConflictPanelType>(nameof(PanelType));

        public Models.ConflictPanelType PanelType
        {
            get => GetValue(PanelTypeProperty);
            set => SetValue(PanelTypeProperty, value);
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

        public static readonly StyledProperty<IBrush> IndicatorBackgroundProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, IBrush>(nameof(IndicatorBackground), new SolidColorBrush(Color.FromArgb(100, 100, 100, 100)));

        public IBrush IndicatorBackground
        {
            get => GetValue(IndicatorBackgroundProperty);
            set => SetValue(IndicatorBackgroundProperty, value);
        }

        public static readonly StyledProperty<Models.ConflictSelectedChunk> SelectedChunkProperty =
            AvaloniaProperty.Register<MergeDiffPresenter, Models.ConflictSelectedChunk>(nameof(SelectedChunk));

        public Models.ConflictSelectedChunk SelectedChunk
        {
            get => GetValue(SelectedChunkProperty);
            set => SetValue(SelectedChunkProperty, value);
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
            TextArea.LeftMargins.Add(new MergeDiffVerticalSeparatorMargin());
            TextArea.TextView.BackgroundRenderers.Add(new MergeDiffLineBackgroundRenderer(this));
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnTextViewScrollChanged;
                _scrollViewer.Bind(ScrollViewer.OffsetProperty, new Binding("ScrollOffset", BindingMode.OneWay));
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            _textMate = Models.TextMateHelper.CreateForEditor(this);
            if (!string.IsNullOrEmpty(FileName))
                Models.TextMateHelper.SetGrammarByFileName(_textMate, FileName);

            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
            TextArea.TextView.PointerEntered += OnTextViewPointerChanged;
            TextArea.TextView.PointerMoved += OnTextViewPointerChanged;
            TextArea.TextView.PointerWheelChanged += OnTextViewPointerWheelChanged;
            TextArea.TextView.LineTransformers.Add(new MergeDiffIndicatorTransformer(this));
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;
            TextArea.TextView.PointerEntered -= OnTextViewPointerChanged;
            TextArea.TextView.PointerMoved -= OnTextViewPointerChanged;
            TextArea.TextView.PointerWheelChanged -= OnTextViewPointerWheelChanged;

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
                UpdateContent();
            else if (change.Property == FileNameProperty)
                Models.TextMateHelper.SetGrammarByFileName(_textMate, FileName);
            else if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue != null)
                Models.TextMateHelper.SetThemeByApp(_textMate);
            else if (change.Property == SelectedChunkProperty)
                TextArea.TextView.InvalidateVisual();
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

        private void OnTextViewPointerChanged(object sender, PointerEventArgs e)
        {
            if (DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            if (sender is not TextView view)
                return;

            UpdateSelectedChunkPosition(vm, e.GetPosition(view).Y + view.VerticalOffset);
        }

        private void OnTextViewPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            if (sender is not TextView view)
                return;

            var y = e.GetPosition(view).Y + view.VerticalOffset;
            Dispatcher.UIThread.Post(() => UpdateSelectedChunkPosition(vm, y));
        }

        private void OnTextViewScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_scrollViewer == null || DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            if (vm.ScrollOffset.NearlyEquals(_scrollViewer.Offset))
                return;

            if (IsPointerOver || e.OffsetDelta.SquaredLength > 1.0f)
            {
                vm.ScrollOffset = _scrollViewer.Offset;

                if (!TextArea.TextView.IsPointerOver)
                    vm.SelectedChunk = null;
            }
        }

        private void UpdateSelectedChunkPosition(ViewModels.MergeConflictEditor vm, double y)
        {
            var lines = DiffLines;
            var panel = PanelType;
            var view = TextArea.TextView;
            var lineIdx = -1;
            foreach (var line in view.VisualLines)
            {
                if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                    continue;

                var index = line.FirstDocumentLine.LineNumber;
                if (index > lines.Count)
                    break;

                var endY = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.TextBottom);
                if (endY > y)
                {
                    lineIdx = index - 1;
                    break;
                }
            }

            if (lineIdx == -1)
            {
                vm.SelectedChunk = null;
                return;
            }

            for (var i = 0; i < vm.ConflictRegions.Count; i++)
            {
                var r = vm.ConflictRegions[i];
                if (r.StartLineInOriginal <= lineIdx && r.EndLineInOriginal >= lineIdx)
                {
                    if (r.IsResolved && panel != Models.ConflictPanelType.Result)
                    {
                        vm.SelectedChunk = null;
                        return;
                    }

                    var startLine = r.StartLineInOriginal + 1;
                    var endLine = r.EndLineInOriginal + 1;
                    if (startLine > Document.LineCount || endLine > Document.LineCount)
                    {
                        vm.SelectedChunk = null;
                        return;
                    }

                    var vOffset = view.VerticalOffset;
                    var startVisualLine = view.GetVisualLine(startLine);
                    var endVisualLine = view.GetVisualLine(endLine);
                    var topY = startVisualLine?.GetTextLineVisualYPosition(startVisualLine.TextLines[0], VisualYPosition.LineTop) ?? vOffset;
                    var bottomY = endVisualLine?.GetTextLineVisualYPosition(endVisualLine.TextLines[^1], VisualYPosition.LineBottom) ?? (view.Bounds.Height + vOffset);
                    vm.SelectedChunk = new Models.ConflictSelectedChunk(topY - vOffset, bottomY - topY, i, panel, r.IsResolved);
                    return;
                }
            }

            vm.SelectedChunk = null;
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

            var panel = _presenter.PanelType;
            var typeface = view.CreateTypeface();

            foreach (var line in view.VisualLines)
            {
                if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                    continue;

                var index = line.FirstDocumentLine.LineNumber;
                if (index > lines.Count)
                    break;

                var info = lines[index - 1];

                string lineNumber = panel switch
                {
                    Models.ConflictPanelType.Mine => info.OldLine,
                    _ => info.NewLine,
                };

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
        public override void Render(DrawingContext context)
        {
            var pen = new Pen(Brushes.DarkGray);
            context.DrawLine(pen, new Point(0, 0), new Point(0, Bounds.Height));
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(1, 0);
        }
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

            if (_presenter.DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            var width = textView.Bounds.Width;
            foreach (var line in textView.VisualLines)
            {
                if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                    continue;

                var index = line.FirstDocumentLine.LineNumber;
                if (index > lines.Count)
                    break;

                var lineIndex = index - 1;
                var info = lines[lineIndex];
                var lineState = vm.GetLineState(lineIndex);

                var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - textView.VerticalOffset;
                var endY = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.LineBottom) - textView.VerticalOffset;
                var rect = new Rect(0, startY, width, endY - startY);

                if (lineState == Models.ConflictLineState.ConflictBlockStart)
                    drawingContext.DrawLine(new Pen(new SolidColorBrush(Colors.Red, 0.6)), new Point(0, startY + 0.5), new Point(width, startY + 0.5));
                else if (lineState == Models.ConflictLineState.ConflictBlockEnd)
                    drawingContext.DrawLine(new Pen(new SolidColorBrush(Colors.Red, 0.6)), new Point(0, endY - 0.5), new Point(width, endY - 0.5));
                else if (lineState == Models.ConflictLineState.ResolvedBlockStart)
                    drawingContext.DrawLine(new Pen(new SolidColorBrush(Colors.Green, 0.6)), new Point(0, startY + 0.5), new Point(width, startY + 0.5));
                else if (lineState == Models.ConflictLineState.ResolvedBlockEnd)
                    drawingContext.DrawLine(new Pen(new SolidColorBrush(Colors.Green, 0.6)), new Point(0, endY - 0.5), new Point(width, endY - 0.5));

                if (lineState >= Models.ConflictLineState.ResolvedBlockStart)
                    drawingContext.DrawRectangle(new SolidColorBrush(Colors.Green, 0.1), null, rect);
                else if (lineState >= Models.ConflictLineState.ConflictBlockStart)
                    drawingContext.DrawRectangle(new SolidColorBrush(Colors.Red, 0.1), null, rect);

                var bg = GetBrushByLineType(info.Type);
                if (bg != null)
                    drawingContext.DrawRectangle(bg, null, rect);
            }
        }

        private IBrush GetBrushByLineType(Models.TextDiffLineType type)
        {
            return type switch
            {
                Models.TextDiffLineType.None => _presenter.EmptyContentBackground,
                Models.TextDiffLineType.Added => _presenter.AddedContentBackground,
                Models.TextDiffLineType.Deleted => _presenter.DeletedContentBackground,
                Models.TextDiffLineType.Indicator => _presenter.IndicatorBackground,
                _ => null,
            };
        }

        private readonly MergeDiffPresenter _presenter;
    }

    public partial class MergeConflictEditor : ChromelessWindow
    {
        public MergeConflictEditor()
        {
            InitializeComponent();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is ViewModels.MergeConflictEditor vm)
                vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            if (_forceClose || !vm.HasUnsavedChanges)
            {
                vm.PropertyChanged -= OnViewModelPropertyChanged;
                return;
            }

            e.Cancel = true;

            var confirm = new Confirm();
            confirm.SetData(App.Text("MergeConflictEditor.UnsavedChanges"), Models.ConfirmButtonType.OkCancel);

            var result = await confirm.ShowDialog<bool>(this);
            if (result)
            {
                _forceClose = true;
                Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            GC.Collect();
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.MergeConflictEditor.SelectedChunk))
                UpdatePopupVisibility();
        }

        private void OnGotoPrevConflict(object sender, RoutedEventArgs e)
        {
            if (IsLoaded && DataContext is ViewModels.MergeConflictEditor vm && vm.HasUnresolvedConflicts)
            {
                var view = OursPresenter.TextArea?.TextView;
                var lines = vm.OursDiffLines;
                var minY = double.MaxValue;
                var minLineIdx = lines.Count;
                if (view is { VisualLinesValid: true })
                {
                    foreach (var line in view.VisualLines)
                    {
                        if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                            continue;

                        var index = line.FirstDocumentLine.LineNumber;
                        if (index > lines.Count)
                            break;

                        var lineIndex = index - 1;
                        var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - view.VerticalOffset;
                        if (startY < minY)
                        {
                            minY = startY;
                            minLineIdx = lineIndex;
                        }
                    }

                    for (var i = vm.ConflictRegions.Count - 1; i >= 0; i--)
                    {
                        var r = vm.ConflictRegions[i];
                        if (r.StartLineInOriginal < minLineIdx && !r.IsResolved)
                        {
                            OursPresenter.ScrollToLine(r.StartLineInOriginal + 1);
                            break;
                        }
                    }
                }
            }

            e.Handled = true;
        }

        private void OnGotoNextConflict(object sender, RoutedEventArgs e)
        {
            if (IsLoaded && DataContext is ViewModels.MergeConflictEditor vm && vm.HasUnresolvedConflicts)
            {
                var view = OursPresenter.TextArea?.TextView;
                var lines = vm.OursDiffLines;
                var maxY = 0.0;
                var maxLineIdx = 0;
                if (view is { VisualLinesValid: true })
                {
                    foreach (var line in view.VisualLines)
                    {
                        if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                            continue;

                        var index = line.FirstDocumentLine.LineNumber;
                        if (index > lines.Count)
                            break;

                        var lineIndex = index - 1;
                        var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - view.VerticalOffset;
                        if (startY > maxY)
                        {
                            maxY = startY;
                            maxLineIdx = lineIndex;
                        }
                    }

                    for (var i = 0; i < vm.ConflictRegions.Count; i++)
                    {
                        var r = vm.ConflictRegions[i];
                        if (r.StartLineInOriginal > maxLineIdx && !r.IsResolved)
                        {
                            OursPresenter.ScrollToLine(r.StartLineInOriginal + 1);
                            break;
                        }
                    }
                }
            }

            e.Handled = true;
        }

        private async void OnSaveAndStage(object sender, RoutedEventArgs e)
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

        private void UpdatePopupVisibility()
        {
            // Hide all popups first
            MinePopup.IsVisible = false;
            TheirsPopup.IsVisible = false;
            ResultPopup.IsVisible = false;
            ResultUndoPopup.IsVisible = false;

            if (DataContext is not ViewModels.MergeConflictEditor vm)
                return;

            var chunk = vm.SelectedChunk;
            if (chunk == null)
                return;

            // Get the presenter for bounds checking
            MergeDiffPresenter presenter = chunk.Panel switch
            {
                Models.ConflictPanelType.Mine => OursPresenter,
                Models.ConflictPanelType.Theirs => TheirsPresenter,
                Models.ConflictPanelType.Result => ResultPresenter,
                _ => null
            };

            // Show the appropriate popup based on panel type and resolved state
            Border popup = chunk.Panel switch
            {
                Models.ConflictPanelType.Mine => MinePopup,
                Models.ConflictPanelType.Theirs => TheirsPopup,
                Models.ConflictPanelType.Result => chunk.IsResolved ? ResultUndoPopup : ResultPopup,
                _ => null
            };

            if (popup != null && presenter != null)
            {
                // Position popup - clamp to visible area
                var top = chunk.Y + (chunk.Height >= 36 ? 8 : 2);

                // Clamp top to ensure popup is visible
                var popupHeight = popup.Bounds.Height > 0 ? popup.Bounds.Height : 32;
                var presenterHeight = presenter.Bounds.Height;
                top = Math.Max(4, Math.Min(top, presenterHeight - popupHeight - 4));

                popup.Margin = new Thickness(0, top, 24, 0);
                popup.IsVisible = true;
            }
        }

        private bool _forceClose = false;
    }
}
