using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    public class IThemedTextDiffPresenter : TextEditor
    {
        public static readonly StyledProperty<string> FileNameProperty =
            AvaloniaProperty.Register<IThemedTextDiffPresenter, string>(nameof(FileName), string.Empty);

        public string FileName
        {
            get => GetValue(FileNameProperty);
            set => SetValue(FileNameProperty, value);
        }

        public static readonly StyledProperty<IBrush> LineBrushProperty =
            AvaloniaProperty.Register<IThemedTextDiffPresenter, IBrush>(nameof(LineBrush), new SolidColorBrush(Colors.DarkGray));

        public IBrush LineBrush
        {
            get => GetValue(LineBrushProperty);
            set => SetValue(LineBrushProperty, value);
        }

        public static readonly StyledProperty<IBrush> EmptyContentBackgroundProperty =
            AvaloniaProperty.Register<IThemedTextDiffPresenter, IBrush>(nameof(EmptyContentBackground), new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)));

        public IBrush EmptyContentBackground
        {
            get => GetValue(EmptyContentBackgroundProperty);
            set => SetValue(EmptyContentBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> AddedContentBackgroundProperty =
            AvaloniaProperty.Register<IThemedTextDiffPresenter, IBrush>(nameof(AddedContentBackground), new SolidColorBrush(Color.FromArgb(60, 0, 255, 0)));

        public IBrush AddedContentBackground
        {
            get => GetValue(AddedContentBackgroundProperty);
            set => SetValue(AddedContentBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> DeletedContentBackgroundProperty =
            AvaloniaProperty.Register<IThemedTextDiffPresenter, IBrush>(nameof(DeletedContentBackground), new SolidColorBrush(Color.FromArgb(60, 255, 0, 0)));

        public IBrush DeletedContentBackground
        {
            get => GetValue(DeletedContentBackgroundProperty);
            set => SetValue(DeletedContentBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush> AddedHighlightBrushProperty =
            AvaloniaProperty.Register<IThemedTextDiffPresenter, IBrush>(nameof(AddedHighlightBrush), new SolidColorBrush(Color.FromArgb(90, 0, 255, 0)));

        public IBrush AddedHighlightBrush
        {
            get => GetValue(AddedHighlightBrushProperty);
            set => SetValue(AddedHighlightBrushProperty, value);
        }

        public static readonly StyledProperty<IBrush> DeletedHighlightBrushProperty =
            AvaloniaProperty.Register<IThemedTextDiffPresenter, IBrush>(nameof(DeletedHighlightBrush), new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)));

        public IBrush DeletedHighlightBrush
        {
            get => GetValue(DeletedHighlightBrushProperty);
            set => SetValue(DeletedHighlightBrushProperty, value);
        }

        public static readonly StyledProperty<IBrush> IndicatorForegroundProperty =
            AvaloniaProperty.Register<IThemedTextDiffPresenter, IBrush>(nameof(IndicatorForeground), Brushes.Gray);

        public IBrush IndicatorForeground
        {
            get => GetValue(IndicatorForegroundProperty);
            set => SetValue(IndicatorForegroundProperty, value);
        }

        public static readonly StyledProperty<bool> UseSyntaxHighlightingProperty =
            AvaloniaProperty.Register<IThemedTextDiffPresenter, bool>(nameof(UseSyntaxHighlighting), false);

        public bool UseSyntaxHighlighting
        {
            get => GetValue(UseSyntaxHighlightingProperty);
            set => SetValue(UseSyntaxHighlightingProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public IThemedTextDiffPresenter(TextArea area, TextDocument doc) : base(area, doc)
        {
            IsReadOnly = true;
            ShowLineNumbers = false;
            BorderThickness = new Thickness(0);

            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.TextView.Options.EnableHyperlinks = false;
            TextArea.TextView.Options.EnableEmailHyperlinks = false;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            UpdateTextMate();
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            if (_textMate != null)
            {
                _textMate.Dispose();
                _textMate = null;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == UseSyntaxHighlightingProperty)
                UpdateTextMate();
            else if (change.Property == FileNameProperty)
                Models.TextMateHelper.SetGrammarByFileName(_textMate, FileName);
            else if (change.Property.Name == "ActualThemeVariant" && change.NewValue != null)
                Models.TextMateHelper.SetThemeByApp(_textMate);
        }

        protected void UpdateTextMate()
        {
            if (UseSyntaxHighlighting)
            {
                if (_textMate == null)
                {
                    TextArea.TextView.LineTransformers.Remove(_lineStyleTransformer);
                    _textMate = Models.TextMateHelper.CreateForEditor(this);
                    TextArea.TextView.LineTransformers.Add(_lineStyleTransformer);
                    Models.TextMateHelper.SetGrammarByFileName(_textMate, FileName);
                }
            }
            else
            {
                if (_textMate != null)
                {
                    _textMate.Dispose();
                    _textMate = null;
                    GC.Collect();

                    TextArea.TextView.Redraw();
                }
            }
        }

        private TextMate.Installation _textMate = null;
        protected IVisualLineTransformer _lineStyleTransformer = null;
    }

    public class CombinedTextDiffPresenter : IThemedTextDiffPresenter
    {
        public class LineNumberMargin : AbstractMargin
        {
            public LineNumberMargin(CombinedTextDiffPresenter editor, bool isOldLine)
            {
                _editor = editor;
                _isOldLine = isOldLine;
                ClipToBounds = true;
            }

            public override void Render(DrawingContext context)
            {
                if (_editor.DiffData == null)
                    return;

                var view = TextView;
                if (view != null && view.VisualLinesValid)
                {
                    var typeface = view.CreateTypeface();
                    foreach (var line in view.VisualLines)
                    {
                        var index = line.FirstDocumentLine.LineNumber;
                        if (index > _editor.DiffData.Lines.Count)
                            break;

                        var info = _editor.DiffData.Lines[index - 1];
                        var lineNumber = _isOldLine ? info.OldLine : info.NewLine;
                        if (string.IsNullOrEmpty(lineNumber))
                            continue;

                        var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - view.VerticalOffset;
                        var txt = new FormattedText(
                            lineNumber,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            _editor.Foreground);
                        context.DrawText(txt, new Point(Bounds.Width - txt.Width, y));
                    }
                }
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                if (_editor.DiffData == null || TextView == null)
                {
                    return new Size(32, 0);
                }
                else
                {
                    var typeface = TextView.CreateTypeface();
                    var test = new FormattedText(
                            $"{_editor.DiffData.MaxLineNumber}",
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            Brushes.White);
                    return new Size(test.Width, 0);
                }
            }

            protected override void OnDataContextChanged(EventArgs e)
            {
                base.OnDataContextChanged(e);
                InvalidateMeasure();
            }

            private readonly CombinedTextDiffPresenter _editor;
            private readonly bool _isOldLine;
        }

        public class VerticalSeperatorMargin : AbstractMargin
        {
            public VerticalSeperatorMargin(CombinedTextDiffPresenter editor)
            {
                _editor = editor;
            }

            public override void Render(DrawingContext context)
            {
                var pen = new Pen(_editor.LineBrush, 1);
                context.DrawLine(pen, new Point(0, 0), new Point(0, Bounds.Height));
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                return new Size(1, 0);
            }

            private readonly CombinedTextDiffPresenter _editor = null;
        }

        public class LineBackgroundRenderer : IBackgroundRenderer
        {
            public KnownLayer Layer => KnownLayer.Background;

            public LineBackgroundRenderer(CombinedTextDiffPresenter editor)
            {
                _editor = editor;
            }

            public void Draw(TextView textView, DrawingContext drawingContext)
            {
                if (_editor.Document == null || !textView.VisualLinesValid)
                    return;

                var width = textView.Bounds.Width;
                foreach (var line in textView.VisualLines)
                {
                    if (line.FirstDocumentLine == null)
                        continue;

                    var index = line.FirstDocumentLine.LineNumber;
                    if (index > _editor.DiffData.Lines.Count)
                        break;

                    var info = _editor.DiffData.Lines[index - 1];
                    var bg = GetBrushByLineType(info.Type);
                    if (bg == null)
                        continue;

                    var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - textView.VerticalOffset;
                    drawingContext.DrawRectangle(bg, null, new Rect(0, y, width, line.Height));
                }
            }

            private IBrush GetBrushByLineType(Models.TextDiffLineType type)
            {
                switch (type)
                {
                    case Models.TextDiffLineType.None:
                        return _editor.EmptyContentBackground;
                    case Models.TextDiffLineType.Added:
                        return _editor.AddedContentBackground;
                    case Models.TextDiffLineType.Deleted:
                        return _editor.DeletedContentBackground;
                    default:
                        return null;
                }
            }

            private readonly CombinedTextDiffPresenter _editor = null;
        }

        public class LineStyleTransformer : DocumentColorizingTransformer
        {
            public LineStyleTransformer(CombinedTextDiffPresenter editor)
            {
                _editor = editor;
            }

            protected override void ColorizeLine(DocumentLine line)
            {
                var idx = line.LineNumber;
                if (idx > _editor.DiffData.Lines.Count)
                    return;

                var info = _editor.DiffData.Lines[idx - 1];
                if (info.Type == Models.TextDiffLineType.Indicator)
                {
                    ChangeLinePart(line.Offset, line.EndOffset, v =>
                    {
                        v.TextRunProperties.SetForegroundBrush(_editor.IndicatorForeground);
                        v.TextRunProperties.SetTypeface(new Typeface(_editor.FontFamily, FontStyle.Italic));
                    });

                    return;
                }

                if (info.Highlights.Count > 0)
                {
                    var bg = info.Type == Models.TextDiffLineType.Added ? _editor.AddedHighlightBrush : _editor.DeletedHighlightBrush;
                    foreach (var highlight in info.Highlights)
                    {
                        ChangeLinePart(line.Offset + highlight.Start, line.Offset + highlight.Start + highlight.Count, v =>
                        {
                            v.TextRunProperties.SetBackgroundBrush(bg);
                        });
                    }
                }
            }

            private readonly CombinedTextDiffPresenter _editor;
        }

        public Models.TextDiff DiffData => DataContext as Models.TextDiff;

        public CombinedTextDiffPresenter() : base(new TextArea(), new TextDocument())
        {
            _lineStyleTransformer = new LineStyleTransformer(this);

            TextArea.LeftMargins.Add(new LineNumberMargin(this, true) { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeperatorMargin(this));
            TextArea.LeftMargins.Add(new LineNumberMargin(this, false) { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeperatorMargin(this));

            TextArea.TextView.BackgroundRenderers.Add(new LineBackgroundRenderer(this));
            TextArea.TextView.LineTransformers.Add(_lineStyleTransformer);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            var scroller = (ScrollViewer)e.NameScope.Find("PART_ScrollViewer");
            scroller.Bind(ScrollViewer.OffsetProperty, new Binding("SyncScrollOffset", BindingMode.TwoWay));
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            var textDiff = DataContext as Models.TextDiff;
            if (textDiff != null)
            {
                var builder = new StringBuilder();
                foreach (var line in textDiff.Lines)
                    builder.AppendLine(line.Content);

                Text = builder.ToString();
            }
            else
            {
                Text = string.Empty;
            }

            GC.Collect();
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selection = TextArea.Selection;
            if (selection.IsEmpty)
                return;

            var menu = new ContextMenu();
            var parentView = this.FindAncestorOfType<TextDiffView>();
            if (parentView != null)
                parentView.FillContextMenuForWorkingCopyChange(menu, selection.StartPosition.Line, selection.EndPosition.Line, false);

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (o, ev) =>
            {
                App.CopyText(SelectedText);
                ev.Handled = true;
            };

            menu.Items.Add(copy);

            TextArea.TextView.OpenContextMenu(menu);
            e.Handled = true;
        }
    }

    public class SingleSideTextDiffPresenter : IThemedTextDiffPresenter
    {
        public class LineNumberMargin : AbstractMargin
        {
            public LineNumberMargin(SingleSideTextDiffPresenter editor)
            {
                _editor = editor;
                ClipToBounds = true;
            }

            public override void Render(DrawingContext context)
            {
                if (_editor.DiffData == null)
                    return;

                var view = TextView;
                if (view != null && view.VisualLinesValid)
                {
                    var typeface = view.CreateTypeface();
                    var infos = _editor.IsOld ? _editor.DiffData.Old : _editor.DiffData.New;
                    foreach (var line in view.VisualLines)
                    {
                        var index = line.FirstDocumentLine.LineNumber;
                        if (index > infos.Count)
                            break;

                        var info = infos[index - 1];
                        var lineNumber = _editor.IsOld ? info.OldLine : info.NewLine;
                        if (string.IsNullOrEmpty(lineNumber))
                            continue;

                        var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - view.VerticalOffset;
                        var txt = new FormattedText(
                            lineNumber,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            _editor.Foreground);
                        context.DrawText(txt, new Point(Bounds.Width - txt.Width, y));
                    }
                }
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                if (_editor.DiffData == null || TextView == null)
                {
                    return new Size(32, 0);
                }
                else
                {
                    var typeface = TextView.CreateTypeface();
                    var test = new FormattedText(
                            $"{_editor.DiffData.MaxLineNumber}",
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            Brushes.White);
                    return new Size(test.Width, 0);
                }
            }

            protected override void OnDataContextChanged(EventArgs e)
            {
                base.OnDataContextChanged(e);
                InvalidateMeasure();
            }

            private readonly SingleSideTextDiffPresenter _editor;
        }

        public class VerticalSeperatorMargin : AbstractMargin
        {
            public VerticalSeperatorMargin(SingleSideTextDiffPresenter editor)
            {
                _editor = editor;
            }

            public override void Render(DrawingContext context)
            {
                var pen = new Pen(_editor.LineBrush, 1);
                context.DrawLine(pen, new Point(0, 0), new Point(0, Bounds.Height));
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                return new Size(1, 0);
            }

            private readonly SingleSideTextDiffPresenter _editor = null;
        }

        public class LineBackgroundRenderer : IBackgroundRenderer
        {
            public KnownLayer Layer => KnownLayer.Background;

            public LineBackgroundRenderer(SingleSideTextDiffPresenter editor)
            {
                _editor = editor;
            }

            public void Draw(TextView textView, DrawingContext drawingContext)
            {
                if (_editor.Document == null || !textView.VisualLinesValid)
                    return;

                var width = textView.Bounds.Width;
                var infos = _editor.IsOld ? _editor.DiffData.Old : _editor.DiffData.New;
                foreach (var line in textView.VisualLines)
                {
                    if (line.FirstDocumentLine == null)
                        continue;

                    var index = line.FirstDocumentLine.LineNumber;
                    if (index > infos.Count)
                        break;

                    var info = infos[index - 1];
                    var bg = GetBrushByLineType(info.Type);
                    if (bg == null)
                        continue;

                    var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - textView.VerticalOffset;
                    drawingContext.DrawRectangle(bg, null, new Rect(0, y, width, line.Height));
                }
            }

            private IBrush GetBrushByLineType(Models.TextDiffLineType type)
            {
                switch (type)
                {
                    case Models.TextDiffLineType.None:
                        return _editor.EmptyContentBackground;
                    case Models.TextDiffLineType.Added:
                        return _editor.AddedContentBackground;
                    case Models.TextDiffLineType.Deleted:
                        return _editor.DeletedContentBackground;
                    default:
                        return null;
                }
            }

            private readonly SingleSideTextDiffPresenter _editor = null;
        }

        public class LineStyleTransformer : DocumentColorizingTransformer
        {
            public LineStyleTransformer(SingleSideTextDiffPresenter editor)
            {
                _editor = editor;
            }

            protected override void ColorizeLine(DocumentLine line)
            {
                var infos = _editor.IsOld ? _editor.DiffData.Old : _editor.DiffData.New;
                var idx = line.LineNumber;
                if (idx > infos.Count)
                    return;

                var info = infos[idx - 1];
                if (info.Type == Models.TextDiffLineType.Indicator)
                {
                    ChangeLinePart(line.Offset, line.EndOffset, v =>
                    {
                        v.TextRunProperties.SetForegroundBrush(_editor.IndicatorForeground);
                        v.TextRunProperties.SetTypeface(new Typeface(_editor.FontFamily, FontStyle.Italic));
                    });

                    return;
                }

                if (info.Highlights.Count > 0)
                {
                    var bg = info.Type == Models.TextDiffLineType.Added ? _editor.AddedHighlightBrush : _editor.DeletedHighlightBrush;
                    foreach (var highlight in info.Highlights)
                    {
                        ChangeLinePart(line.Offset + highlight.Start, line.Offset + highlight.Start + highlight.Count, v =>
                        {
                            v.TextRunProperties.SetBackgroundBrush(bg);
                        });
                    }
                }
            }

            private readonly SingleSideTextDiffPresenter _editor;
        }

        public static readonly StyledProperty<bool> IsOldProperty =
            AvaloniaProperty.Register<SingleSideTextDiffPresenter, bool>(nameof(IsOld));

        public bool IsOld
        {
            get => GetValue(IsOldProperty);
            set => SetValue(IsOldProperty, value);
        }

        public ViewModels.TwoSideTextDiff DiffData => DataContext as ViewModels.TwoSideTextDiff;

        public SingleSideTextDiffPresenter() : base(new TextArea(), new TextDocument())
        {
            _lineStyleTransformer = new LineStyleTransformer(this);

            TextArea.LeftMargins.Add(new LineNumberMargin(this) { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeperatorMargin(this));
            TextArea.TextView.BackgroundRenderers.Add(new LineBackgroundRenderer(this));
            TextArea.TextView.LineTransformers.Add(_lineStyleTransformer);
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            _scrollViewer = this.FindDescendantOfType<ScrollViewer>();
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnTextViewScrollChanged;
                _scrollViewer.Bind(ScrollViewer.OffsetProperty, new Binding("SyncScrollOffset", BindingMode.OneWay));
            }

            TextArea.PointerWheelChanged += OnTextAreaPointerWheelChanged;
            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged -= OnTextViewScrollChanged;
                _scrollViewer = null;
            }

            TextArea.PointerWheelChanged -= OnTextAreaPointerWheelChanged;
            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;

            GC.Collect();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is ViewModels.TwoSideTextDiff diff)
            {
                var builder = new StringBuilder();
                var lines = IsOld ? diff.Old : diff.New;
                foreach (var line in lines)
                    builder.AppendLine(line.Content);

                Text = builder.ToString();
            }
            else
            {
                Text = string.Empty;
            }
        }

        private void OnTextAreaPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (!TextArea.IsFocused)
                Focus();
        }

        private void OnTextViewScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (TextArea.IsFocused && DataContext is ViewModels.TwoSideTextDiff diff)
                diff.SyncScrollOffset = _scrollViewer.Offset;
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selection = TextArea.Selection;
            if (selection.IsEmpty)
                return;

            var menu = new ContextMenu();
            var parentView = this.FindAncestorOfType<TextDiffView>();
            if (parentView != null)
                parentView.FillContextMenuForWorkingCopyChange(menu, selection.StartPosition.Line, selection.EndPosition.Line, IsOld);

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (o, ev) =>
            {
                App.CopyText(SelectedText);
                ev.Handled = true;
            };

            menu.Items.Add(copy);

            TextArea.TextView.OpenContextMenu(menu);
            e.Handled = true;
        }

        private ScrollViewer _scrollViewer = null;
    }

    public partial class TextDiffView : UserControl
    {
        public static readonly StyledProperty<bool> UseSideBySideDiffProperty =
            AvaloniaProperty.Register<TextDiffView, bool>(nameof(UseSideBySideDiff), false);

        public bool UseSideBySideDiff
        {
            get => GetValue(UseSideBySideDiffProperty);
            set => SetValue(UseSideBySideDiffProperty, value);
        }

        static TextDiffView()
        {
            UseSideBySideDiffProperty.Changed.AddClassHandler<TextDiffView>((v, e) =>
            {
                if (v.DataContext is Models.TextDiff diff)
                {
                    diff.SyncScrollOffset = Vector.Zero;

                    if (v.UseSideBySideDiff)
                        v.Content = new ViewModels.TwoSideTextDiff(diff);
                    else
                        v.Content = diff;
                }
            });
        }

        public TextDiffView()
        {
            InitializeComponent();
        }

        public void FillContextMenuForWorkingCopyChange(ContextMenu menu, int startLine, int endLine, bool isOldSide)
        {
            var diff = DataContext as Models.TextDiff;
            if (diff == null)
                return;

            var parentView = this.FindAncestorOfType<DiffView>();
            if (parentView == null)
                return;

            var ctx = parentView.DataContext as ViewModels.DiffContext;
            if (ctx == null)
                return;

            var change = ctx.WorkingCopyChange;
            if (change == null)
                return;

            if (startLine > endLine)
            {
                var tmp = startLine;
                startLine = endLine;
                endLine = tmp;
            }

            var selection = GetUnifiedSelection(diff, startLine, endLine, isOldSide);
            if (!selection.HasChanges)
                return;

            // If all changes has been selected the use method provided by ViewModels.WorkingCopy.
            // Otherwise, use `git apply`
            if (!selection.HasLeftChanges)
            {
                var workcopyView = this.FindAncestorOfType<WorkingCopy>();
                if (workcopyView == null)
                    return;

                if (ctx.IsUnstaged)
                {
                    var stage = new MenuItem();
                    stage.Header = App.Text("FileCM.StageSelectedLines");
                    stage.Icon = App.CreateMenuIcon("Icons.File.Add");
                    stage.Click += (_, e) =>
                    {
                        var workcopy = workcopyView.DataContext as ViewModels.WorkingCopy;
                        workcopy.StageChanges(new List<Models.Change> { change });
                        e.Handled = true;
                    };

                    var discard = new MenuItem();
                    discard.Header = App.Text("FileCM.DiscardSelectedLines");
                    discard.Icon = App.CreateMenuIcon("Icons.Undo");
                    discard.Click += (_, e) =>
                    {
                        var workcopy = workcopyView.DataContext as ViewModels.WorkingCopy;
                        workcopy.Discard(new List<Models.Change> { change }, true);
                        e.Handled = true;
                    };

                    menu.Items.Add(stage);
                    menu.Items.Add(discard);
                }
                else
                {
                    var unstage = new MenuItem();
                    unstage.Header = App.Text("FileCM.UnstageSelectedLines");
                    unstage.Icon = App.CreateMenuIcon("Icons.File.Remove");
                    unstage.Click += (_, e) =>
                    {
                        var workcopy = workcopyView.DataContext as ViewModels.WorkingCopy;
                        workcopy.UnstageChanges(new List<Models.Change> { change });
                        e.Handled = true;
                    };

                    var discard = new MenuItem();
                    discard.Header = App.Text("FileCM.DiscardSelectedLines");
                    discard.Icon = App.CreateMenuIcon("Icons.Undo");
                    discard.Click += (_, e) =>
                    {
                        var workcopy = workcopyView.DataContext as ViewModels.WorkingCopy;
                        workcopy.Discard(new List<Models.Change> { change }, false);
                        e.Handled = true;
                    };

                    menu.Items.Add(unstage);
                    menu.Items.Add(discard);
                }
            }
            else
            {
                var repoView = this.FindAncestorOfType<Repository>();
                if (repoView == null)
                    return;

                if (ctx.IsUnstaged)
                {
                    var stage = new MenuItem();
                    stage.Header = App.Text("FileCM.StageSelectedLines");
                    stage.Icon = App.CreateMenuIcon("Icons.File.Add");
                    stage.Click += (_, e) =>
                    {
                        var repo = repoView.DataContext as ViewModels.Repository;
                        repo.SetWatcherEnabled(false);

                        var tmpFile = Path.GetTempFileName();
                        if (change.WorkTree == Models.ChangeState.Untracked)
                        {
                            diff.GenerateNewPatchFromSelection(change, null, selection, false, tmpFile);
                        }
                        else if (!UseSideBySideDiff)
                        {
                            var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                            diff.GeneratePatchFromSelection(change, treeGuid, selection, false, tmpFile);
                        }
                        else
                        {
                            var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                            diff.GeneratePatchFromSelectionSingleSide(change, treeGuid, selection, false, isOldSide, tmpFile);
                        }

                        new Commands.Apply(ctx.RepositoryPath, tmpFile, true, "nowarn", "--cache --index").Exec();
                        File.Delete(tmpFile);

                        repo.MarkWorkingCopyDirtyManually();
                        repo.SetWatcherEnabled(true);
                        e.Handled = true;
                    };

                    var discard = new MenuItem();
                    discard.Header = App.Text("FileCM.DiscardSelectedLines");
                    discard.Icon = App.CreateMenuIcon("Icons.Undo");
                    discard.Click += (_, e) =>
                    {
                        var repo = repoView.DataContext as ViewModels.Repository;
                        repo.SetWatcherEnabled(false);

                        var tmpFile = Path.GetTempFileName();
                        if (change.WorkTree == Models.ChangeState.Untracked)
                        {
                            diff.GenerateNewPatchFromSelection(change, null, selection, true, tmpFile);
                        }
                        else if (!UseSideBySideDiff)
                        {
                            var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                            diff.GeneratePatchFromSelection(change, treeGuid, selection, true, tmpFile);
                        }
                        else
                        {
                            var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                            diff.GeneratePatchFromSelectionSingleSide(change, treeGuid, selection, true, isOldSide, tmpFile);
                        }

                        new Commands.Apply(ctx.RepositoryPath, tmpFile, true, "nowarn", "--reverse").Exec();
                        File.Delete(tmpFile);

                        repo.MarkWorkingCopyDirtyManually();
                        repo.SetWatcherEnabled(true);
                        e.Handled = true;
                    };

                    menu.Items.Add(stage);
                    menu.Items.Add(discard);
                }
                else
                {
                    var unstage = new MenuItem();
                    unstage.Header = App.Text("FileCM.UnstageSelectedLines");
                    unstage.Icon = App.CreateMenuIcon("Icons.File.Remove");
                    unstage.Click += (_, e) =>
                    {
                        var repo = repoView.DataContext as ViewModels.Repository;
                        repo.SetWatcherEnabled(false);

                        var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                        var tmpFile = Path.GetTempFileName();
                        if (change.Index == Models.ChangeState.Added)
                        {
                            diff.GenerateNewPatchFromSelection(change, treeGuid, selection, true, tmpFile);
                        }
                        else if (!UseSideBySideDiff)
                        {
                            diff.GeneratePatchFromSelection(change, treeGuid, selection, true, tmpFile);
                        }
                        else
                        {
                            diff.GeneratePatchFromSelectionSingleSide(change, treeGuid, selection, true, isOldSide, tmpFile);
                        }

                        new Commands.Apply(ctx.RepositoryPath, tmpFile, true, "nowarn", "--cache --index --reverse").Exec();
                        File.Delete(tmpFile);

                        repo.MarkWorkingCopyDirtyManually();
                        repo.SetWatcherEnabled(true);
                        e.Handled = true;
                    };

                    var discard = new MenuItem();
                    discard.Header = App.Text("FileCM.DiscardSelectedLines");
                    discard.Icon = App.CreateMenuIcon("Icons.Undo");
                    discard.Click += (_, e) =>
                    {
                        var repo = repoView.DataContext as ViewModels.Repository;
                        repo.SetWatcherEnabled(false);

                        var tmpFile = Path.GetTempFileName();
                        if (change.WorkTree == Models.ChangeState.Untracked)
                        {
                            diff.GenerateNewPatchFromSelection(change, null, selection, true, tmpFile);
                        }
                        else if (!UseSideBySideDiff)
                        {
                            var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                            diff.GeneratePatchFromSelection(change, treeGuid, selection, true, tmpFile);
                        }
                        else
                        {
                            var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                            diff.GeneratePatchFromSelectionSingleSide(change, treeGuid, selection, true, isOldSide, tmpFile);
                        }

                        new Commands.Apply(ctx.RepositoryPath, tmpFile, true, "nowarn", "--index --reverse").Exec();
                        File.Delete(tmpFile);

                        repo.MarkWorkingCopyDirtyManually();
                        repo.SetWatcherEnabled(true);
                        e.Handled = true;
                    };

                    menu.Items.Add(unstage);
                    menu.Items.Add(discard);
                }
            }

            menu.Items.Add(new MenuItem() { Header = "-" });
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            var diff = DataContext as Models.TextDiff;
            if (diff == null)
            {
                Content = null;
                GC.Collect();
                return;
            }

            if (UseSideBySideDiff)
                Content = new ViewModels.TwoSideTextDiff(diff, Content as ViewModels.TwoSideTextDiff);
            else
                Content = diff;
        }

        private Models.TextDiffSelection GetUnifiedSelection(Models.TextDiff diff, int startLine, int endLine, bool isOldSide)
        {
            var rs = new Models.TextDiffSelection();

            endLine = Math.Min(endLine, diff.Lines.Count);
            if (Content is ViewModels.TwoSideTextDiff twoSides)
            {
                var target = isOldSide ? twoSides.Old : twoSides.New;
                var firstContentLine = -1;
                for (int i = startLine - 1; i < endLine; i++)
                {
                    var line = target[i];
                    if (line.Type != Models.TextDiffLineType.None)
                    {
                        firstContentLine = i;
                        break;
                    }
                }

                if (firstContentLine < 0)
                    return rs;

                var endContentLine = -1;
                for (int i = Math.Min(endLine - 1, target.Count - 1); i >= startLine - 1; i--)
                {
                    var line = target[i];
                    if (line.Type != Models.TextDiffLineType.None)
                    {
                        endContentLine = i;
                        break;
                    }
                }

                if (endContentLine < 0)
                    return rs;

                var firstContent = target[firstContentLine];
                var endContent = target[endContentLine];
                startLine = diff.Lines.IndexOf(firstContent) + 1;
                endLine = diff.Lines.IndexOf(endContent) + 1;
            }

            rs.StartLine = startLine;
            rs.EndLine = endLine;

            for (int i = 0; i < startLine - 1; i++)
            {
                var line = diff.Lines[i];
                if (line.Type == Models.TextDiffLineType.Added)
                {
                    rs.HasLeftChanges = true;
                    rs.IgnoredAdds++;
                }
                else if (line.Type == Models.TextDiffLineType.Deleted)
                {
                    rs.HasLeftChanges = true;
                    rs.IgnoredDeletes++;
                }
            }

            for (int i = startLine - 1; i < endLine; i++)
            {
                var line = diff.Lines[i];
                if (line.Type == Models.TextDiffLineType.Added)
                {
                    if (!UseSideBySideDiff)
                    {
                        rs.HasChanges = true;
                        break;
                    }
                    else if (isOldSide)
                    {
                        rs.HasLeftChanges = true;
                    }
                    else
                    {
                        rs.HasChanges = true;
                    }
                }
                else if (line.Type == Models.TextDiffLineType.Deleted)
                {
                    if (!UseSideBySideDiff)
                    {
                        rs.HasChanges = true;
                        break;
                    }
                    else if (isOldSide)
                    {
                        rs.HasChanges = true;
                    }
                    else
                    {
                        rs.HasLeftChanges = true;
                    }
                }
            }

            if (!rs.HasLeftChanges)
            {
                for (int i = endLine; i < diff.Lines.Count; i++)
                {
                    var line = diff.Lines[i];
                    if (line.Type == Models.TextDiffLineType.Added || line.Type == Models.TextDiffLineType.Deleted)
                    {
                        rs.HasLeftChanges = true;
                        break;
                    }
                }
            }

            return rs;
        }
    }
}
