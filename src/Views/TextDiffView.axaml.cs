using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    public class CombinedTextDiffPresenter : TextEditor
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
                var pen = new Pen(_editor.BorderBrush, 1);
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
            private static readonly Brush BG_EMPTY = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
            private static readonly Brush BG_ADDED = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));
            private static readonly Brush BG_DELETED = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0));

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
                        return BG_EMPTY;
                    case Models.TextDiffLineType.Added:
                        return BG_ADDED;
                    case Models.TextDiffLineType.Deleted:
                        return BG_DELETED;
                    default:
                        return null;
                }
            }

            private readonly CombinedTextDiffPresenter _editor = null;
        }

        public class LineStyleTransformer : DocumentColorizingTransformer
        {
            private static readonly Brush HL_ADDED = new SolidColorBrush(Color.FromArgb(90, 0, 255, 0));
            private static readonly Brush HL_DELETED = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0));

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
                        v.TextRunProperties.SetForegroundBrush(_editor.SecondaryFG);
                        v.TextRunProperties.SetTypeface(new Typeface(_editor.FontFamily, FontStyle.Italic));
                    });

                    return;
                }

                if (info.Highlights.Count > 0)
                {
                    var bg = info.Type == Models.TextDiffLineType.Added ? HL_ADDED : HL_DELETED;
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

        public static readonly StyledProperty<Models.TextDiff> DiffDataProperty =
            AvaloniaProperty.Register<CombinedTextDiffPresenter, Models.TextDiff>(nameof(DiffData));

        public Models.TextDiff DiffData
        {
            get => GetValue(DiffDataProperty);
            set => SetValue(DiffDataProperty, value);
        }

        public static readonly StyledProperty<IBrush> SecondaryFGProperty =
            AvaloniaProperty.Register<CombinedTextDiffPresenter, IBrush>(nameof(SecondaryFG), Brushes.Gray);

        public IBrush SecondaryFG
        {
            get => GetValue(SecondaryFGProperty);
            set => SetValue(SecondaryFGProperty, value);
        }

        public static readonly StyledProperty<Vector> SyncScrollOffsetProperty =
            AvaloniaProperty.Register<CombinedTextDiffPresenter, Vector>(nameof(SyncScrollOffset));

        public Vector SyncScrollOffset
        {
            get => GetValue(SyncScrollOffsetProperty);
            set => SetValue(SyncScrollOffsetProperty, value);
        }

        public static readonly StyledProperty<bool> UseSyntaxHighlightingProperty =
            AvaloniaProperty.Register<CombinedTextDiffPresenter, bool>(nameof(UseSyntaxHighlighting), false);

        public bool UseSyntaxHighlighting
        {
            get => GetValue(UseSyntaxHighlightingProperty);
            set => SetValue(UseSyntaxHighlightingProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public CombinedTextDiffPresenter() : base(new TextArea(), new TextDocument())
        {
            _lineStyleTransformer = new LineStyleTransformer(this);

            IsReadOnly = true;
            ShowLineNumbers = false;
            WordWrap = false;

            TextArea.LeftMargins.Add(new LineNumberMargin(this, true) { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeperatorMargin(this));
            TextArea.LeftMargins.Add(new LineNumberMargin(this, false) { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeperatorMargin(this));

            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.TextView.BackgroundRenderers.Add(new LineBackgroundRenderer(this));
            TextArea.TextView.LineTransformers.Add(_lineStyleTransformer);
            TextArea.TextView.Options.EnableHyperlinks = false;
            TextArea.TextView.Options.EnableEmailHyperlinks = false;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            UpdateTextMate();

            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
            TextArea.TextView.ScrollOffsetChanged += OnTextViewScrollOffsetChanged;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;
            TextArea.TextView.ScrollOffsetChanged -= OnTextViewScrollOffsetChanged;

            if (_textMate != null)
            {
                _textMate.Dispose();
                _textMate = null;
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
            {
                parentView.FillContextMenuForWorkingCopyChange(menu, selection.StartPosition.Line, selection.EndPosition.Line, false);
            }

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (o, ev) =>
            {
                App.CopyText(SelectedText);
                ev.Handled = true;
            };

            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);
            e.Handled = true;
        }

        private void OnTextViewScrollOffsetChanged(object sender, EventArgs e)
        {
            SetCurrentValue(SyncScrollOffsetProperty, TextArea.TextView.ScrollOffset);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == DiffDataProperty)
            {
                if (DiffData != null)
                {
                    var builder = new StringBuilder();
                    foreach (var line in DiffData.Lines)
                    {
                        builder.AppendLine(line.Content);
                    }

                    Text = builder.ToString();
                    Models.TextMateHelper.SetGrammarByFileName(_textMate, DiffData.File);
                }
                else
                {
                    Text = string.Empty;
                }
            }
            else if (change.Property == SyncScrollOffsetProperty)
            {
                if (TextArea.TextView.ScrollOffset != SyncScrollOffset)
                {
                    IScrollable scrollable = TextArea.TextView;
                    scrollable.Offset = SyncScrollOffset;
                }
            }
            else if (change.Property == UseSyntaxHighlightingProperty)
            {
                UpdateTextMate();
            }
            else if (change.Property.Name == "ActualThemeVariant" && change.NewValue != null)
            {
                Models.TextMateHelper.SetThemeByApp(_textMate);
            }
        }

        private void UpdateTextMate()
        {
            if (UseSyntaxHighlighting)
            {
                if (_textMate == null)
                {
                    TextArea.TextView.LineTransformers.Remove(_lineStyleTransformer);
                    _textMate = Models.TextMateHelper.CreateForEditor(this);
                    TextArea.TextView.LineTransformers.Add(_lineStyleTransformer);

                    if (DiffData != null)
                        Models.TextMateHelper.SetGrammarByFileName(_textMate, DiffData.File);
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

        private TextMate.Installation _textMate;
        private readonly LineStyleTransformer _lineStyleTransformer = null;
    }

    public class SingleSideTextDiffPresenter : TextEditor
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
                var pen = new Pen(_editor.BorderBrush, 1);
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
            private static readonly Brush BG_EMPTY = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
            private static readonly Brush BG_ADDED = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));
            private static readonly Brush BG_DELETED = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0));

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
                        return BG_EMPTY;
                    case Models.TextDiffLineType.Added:
                        return BG_ADDED;
                    case Models.TextDiffLineType.Deleted:
                        return BG_DELETED;
                    default:
                        return null;
                }
            }

            private readonly SingleSideTextDiffPresenter _editor = null;
        }

        public class LineStyleTransformer : DocumentColorizingTransformer
        {
            private static readonly Brush HL_ADDED = new SolidColorBrush(Color.FromArgb(90, 0, 255, 0));
            private static readonly Brush HL_DELETED = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0));

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
                        v.TextRunProperties.SetForegroundBrush(_editor.SecondaryFG);
                        v.TextRunProperties.SetTypeface(new Typeface(_editor.FontFamily, FontStyle.Italic));
                    });

                    return;
                }

                if (info.Highlights.Count > 0)
                {
                    var bg = info.Type == Models.TextDiffLineType.Added ? HL_ADDED : HL_DELETED;
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

        public static readonly StyledProperty<ViewModels.TwoSideTextDiff> DiffDataProperty =
            AvaloniaProperty.Register<SingleSideTextDiffPresenter, ViewModels.TwoSideTextDiff>(nameof(DiffData));

        public ViewModels.TwoSideTextDiff DiffData
        {
            get => GetValue(DiffDataProperty);
            set => SetValue(DiffDataProperty, value);
        }

        public static readonly StyledProperty<IBrush> SecondaryFGProperty =
            AvaloniaProperty.Register<SingleSideTextDiffPresenter, IBrush>(nameof(SecondaryFG), Brushes.Gray);

        public IBrush SecondaryFG
        {
            get => GetValue(SecondaryFGProperty);
            set => SetValue(SecondaryFGProperty, value);
        }

        public static readonly StyledProperty<Vector> SyncScrollOffsetProperty =
            AvaloniaProperty.Register<SingleSideTextDiffPresenter, Vector>(nameof(SyncScrollOffset), Vector.Zero);

        public Vector SyncScrollOffset
        {
            get => GetValue(SyncScrollOffsetProperty);
            set => SetValue(SyncScrollOffsetProperty, value);
        }

        public static readonly StyledProperty<bool> UseSyntaxHighlightingProperty =
            AvaloniaProperty.Register<SingleSideTextDiffPresenter, bool>(nameof(UseSyntaxHighlighting), false);

        public bool UseSyntaxHighlighting
        {
            get => GetValue(UseSyntaxHighlightingProperty);
            set => SetValue(UseSyntaxHighlightingProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public SingleSideTextDiffPresenter() : base(new TextArea(), new TextDocument())
        {
            _lineStyleTransformer = new LineStyleTransformer(this);

            IsReadOnly = true;
            ShowLineNumbers = false;
            WordWrap = false;

            TextArea.LeftMargins.Add(new LineNumberMargin(this) { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeperatorMargin(this));
            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.TextView.BackgroundRenderers.Add(new LineBackgroundRenderer(this));
            TextArea.TextView.LineTransformers.Add(_lineStyleTransformer);
            TextArea.TextView.Options.EnableHyperlinks = false;
            TextArea.TextView.Options.EnableEmailHyperlinks = false;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            _scrollViewer = this.FindDescendantOfType<ScrollViewer>();
            if (_scrollViewer != null)
            {
                _scrollViewer.Offset = SyncScrollOffset;
                _scrollViewer.ScrollChanged += OnTextViewScrollChanged;
            }

            UpdateTextMate();

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

            if (_textMate != null)
            {
                _textMate.Dispose();
                _textMate = null;
            }

            TextArea.PointerWheelChanged -= OnTextAreaPointerWheelChanged;
            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;

            GC.Collect();
        }

        private void OnTextAreaPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (!TextArea.IsFocused)
                Focus();
        }

        private void OnTextViewScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (TextArea.IsFocused)
                SetCurrentValue(SyncScrollOffsetProperty, _scrollViewer.Offset);
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selection = TextArea.Selection;
            if (selection.IsEmpty)
                return;

            var menu = new ContextMenu();
            var parentView = this.FindAncestorOfType<TextDiffView>();
            if (parentView != null)
            {
                parentView.FillContextMenuForWorkingCopyChange(menu, selection.StartPosition.Line, selection.EndPosition.Line, IsOld);
            }

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (o, ev) =>
            {
                App.CopyText(SelectedText);
                ev.Handled = true;
            };

            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);
            e.Handled = true;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == DiffDataProperty)
            {
                if (DiffData != null)
                {
                    var builder = new StringBuilder();
                    if (IsOld)
                    {
                        foreach (var line in DiffData.Old)
                        {
                            builder.AppendLine(line.Content);
                        }
                    }
                    else
                    {
                        foreach (var line in DiffData.New)
                        {
                            builder.AppendLine(line.Content);
                        }
                    }

                    Text = builder.ToString();
                    Models.TextMateHelper.SetGrammarByFileName(_textMate, DiffData.File);
                }
                else
                {
                    Text = string.Empty;
                }
            }
            else if (change.Property == SyncScrollOffsetProperty)
            {
                if (!TextArea.IsFocused && _scrollViewer != null)
                    _scrollViewer.Offset = SyncScrollOffset;
            }
            else if (change.Property == UseSyntaxHighlightingProperty)
            {
                UpdateTextMate();
            }
            else if (change.Property.Name == "ActualThemeVariant" && change.NewValue != null)
            {
                Models.TextMateHelper.SetThemeByApp(_textMate);
            }
        }

        private void UpdateTextMate()
        {
            if (UseSyntaxHighlighting)
            {
                if (_textMate == null)
                {
                    TextArea.TextView.LineTransformers.Remove(_lineStyleTransformer);
                    _textMate = Models.TextMateHelper.CreateForEditor(this);
                    TextArea.TextView.LineTransformers.Add(_lineStyleTransformer);

                    if (DiffData != null)
                        Models.TextMateHelper.SetGrammarByFileName(_textMate, DiffData.File);
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

        private TextMate.Installation _textMate;
        private readonly LineStyleTransformer _lineStyleTransformer = null;
        private ScrollViewer _scrollViewer = null;
    }

    public partial class TextDiffView : UserControl
    {
        public static readonly StyledProperty<Models.TextDiff> TextDiffProperty =
            AvaloniaProperty.Register<TextDiffView, Models.TextDiff>(nameof(TextDiff), null);

        public Models.TextDiff TextDiff
        {
            get => GetValue(TextDiffProperty);
            set => SetValue(TextDiffProperty, value);
        }

        public static readonly StyledProperty<bool> UseSideBySideDiffProperty =
            AvaloniaProperty.Register<TextDiffView, bool>(nameof(UseSideBySideDiff), false);

        public bool UseSideBySideDiff
        {
            get => GetValue(UseSideBySideDiffProperty);
            set => SetValue(UseSideBySideDiffProperty, value);
        }

        public TextDiffView()
        {
            InitializeComponent();
        }

        public void FillContextMenuForWorkingCopyChange(ContextMenu menu, int startLine, int endLine, bool isOldSide)
        {
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

            var selection = GetUnifiedSelection(startLine, endLine, isOldSide);
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
                            TextDiff.GenerateNewPatchFromSelection(change, null, selection, false, tmpFile);
                        }
                        else if (!UseSideBySideDiff)
                        {
                            var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                            TextDiff.GeneratePatchFromSelection(change, treeGuid, selection, false, tmpFile);
                        }
                        else
                        {
                            var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                            TextDiff.GeneratePatchFromSelectionSingleSide(change, treeGuid, selection, false, isOldSide, tmpFile);
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
                            TextDiff.GenerateNewPatchFromSelection(change, null, selection, true, tmpFile);
                        }
                        else if (!UseSideBySideDiff)
                        {
                            var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                            TextDiff.GeneratePatchFromSelection(change, treeGuid, selection, true, tmpFile);
                        }
                        else
                        {
                            var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                            TextDiff.GeneratePatchFromSelectionSingleSide(change, treeGuid, selection, true, isOldSide, tmpFile);
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
                            TextDiff.GenerateNewPatchFromSelection(change, treeGuid, selection, true, tmpFile);
                        }
                        else if (!UseSideBySideDiff)
                        {
                            TextDiff.GeneratePatchFromSelection(change, treeGuid, selection, true, tmpFile);
                        }
                        else
                        {
                            TextDiff.GeneratePatchFromSelectionSingleSide(change, treeGuid, selection, true, isOldSide, tmpFile);
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
                            TextDiff.GenerateNewPatchFromSelection(change, null, selection, true, tmpFile);
                        }
                        else if (!UseSideBySideDiff)
                        {
                            var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                            TextDiff.GeneratePatchFromSelection(change, treeGuid, selection, true, tmpFile);
                        }
                        else
                        {
                            var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                            TextDiff.GeneratePatchFromSelectionSingleSide(change, treeGuid, selection, true, isOldSide, tmpFile);
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

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextDiffProperty || change.Property == UseSideBySideDiffProperty)
            {
                if (TextDiff == null)
                {
                    Content = null;
                }
                else if (UseSideBySideDiff)
                {
                    Content = new ViewModels.TwoSideTextDiff(TextDiff);
                }
                else
                {
                    Content = TextDiff;
                }
            }
        }

        private Models.TextDiffSelection GetUnifiedSelection(int startLine, int endLine, bool isOldSide)
        {
            var rs = new Models.TextDiffSelection();
            var diff = TextDiff;

            endLine = Math.Min(endLine, TextDiff.Lines.Count);
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
                startLine = TextDiff.Lines.IndexOf(firstContent) + 1;
                endLine = TextDiff.Lines.IndexOf(endContent) + 1;
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
