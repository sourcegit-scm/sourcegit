using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using AvaloniaEdit.Utils;

namespace SourceGit.Views
{
    public class BlameTextEditor : TextEditor
    {
        public class CommitInfoMargin : AbstractMargin
        {
            public CommitInfoMargin(BlameTextEditor editor)
            {
                _editor = editor;
                ClipToBounds = true;
            }

            private sealed class CommitInfoLink
            {
                public CommitInfoLink(Models.BlameLineInfo info, FormattedText shaLink, double lineCenter)
                {
                    Info = info;
                    ShaLink = shaLink;
                    LineCenter = lineCenter;
                }

                public Models.BlameLineInfo Info { get; }
                public FormattedText ShaLink { get; }
                public double LineCenter { get; }
                public double Top { get; set; }
                public bool KeepFirstVisible { get; set; }
            }

            public override void Render(DrawingContext context)
            {
                if (_editor.BlameData == null)
                    return;

                var view = TextView;
                if (view is not { VisualLinesValid: true })
                    return;

                var underlinePen = new Pen(Brushes.DarkOrange);
                var width = Bounds.Width;
                var pixelHeight = PixelSnapHelpers.GetPixelSize(view).Height;
                var typeface = view.CreateTypeface();

                foreach (var link in GetCommitInfoLinks(view, typeface))
                {
                    var shaLink = link.ShaLink;
                    var shaLinkTop = link.Top;
                    context.DrawText(shaLink, new Point(0, shaLinkTop));
                    var underlineY = PixelSnapHelpers.PixelAlign(shaLinkTop + shaLink.Height + 0.5, pixelHeight);
                    context.DrawLine(underlinePen, new Point(0, underlineY), new Point(shaLink.Width, underlineY));

                    var author = new FormattedText(
                        link.Info.Author,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        _editor.FontSize,
                        _editor.Foreground);
                    var authorTop = GetTextTop(link.LineCenter, author.Height, link.KeepFirstVisible);
                    context.DrawText(author, new Point(shaLink.Width + 8, authorTop));

                    var timeStr = Models.DateTimeFormat.Format(link.Info.Timestamp, true);
                    var time = new FormattedText(
                        timeStr,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        _editor.FontSize,
                        _editor.Foreground);
                    var timeTop = GetTextTop(link.LineCenter, time.Height, link.KeepFirstVisible);
                    context.DrawText(time, new Point(width - time.Width, timeTop));
                }
            }

            private List<CommitInfoLink> GetCommitInfoLinks(TextView view, Typeface typeface)
            {
                var lineHeight = view.DefaultLineHeight;
                var visualLineCount = view.VisualLines.Count;
                if (_commitInfoLinks != null &&
                    ReferenceEquals(_layoutBlameData, _editor.BlameData) &&
                    _layoutVerticalOffset == view.VerticalOffset &&
                    _layoutWidth == Bounds.Width &&
                    _layoutHeight == Bounds.Height &&
                    _layoutLineHeight == lineHeight &&
                    _layoutVisualLineCount == visualLineCount)
                {
                    return _commitInfoLinks;
                }

                var links = new List<CommitInfoLink>();
                var renderedGroup = string.Empty;
                var firstGroup = string.Empty;
                var firstLineCenter = 0.0;
                var firstLabel = (FormattedText)null;
                var hasFirstGroup = false;
                var hasKeepFirstResult = false;
                var keepFirstGroupVisible = false;

                foreach (var line in view.VisualLines)
                {
                    if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                        continue;

                    var lineNumber = line.FirstDocumentLine.LineNumber;
                    if (lineNumber > _editor.BlameData.LineInfos.Count)
                        break;

                    var info = _editor.BlameData.LineInfos[lineNumber - 1];
                    if (!TryGetLineCenter(view, line, out var lineCenter))
                        continue;

                    if (!hasFirstGroup)
                    {
                        firstGroup = info.CommitSHA;
                        firstLineCenter = lineCenter;
                        firstLabel = CreateShaLink(typeface, info.CommitSHA);
                        hasFirstGroup = true;
                    }
                    else if (!hasKeepFirstResult && !string.Equals(firstGroup, info.CommitSHA, StringComparison.Ordinal))
                    {
                        if (info.IsFirstInGroup || lineCenter > lineHeight)
                        {
                            var nextLabelTop = lineCenter - firstLabel.Height * 0.5;
                            keepFirstGroupVisible = firstLineCenter - firstLabel.Height * 0.5 < 0 && nextLabelTop >= firstLabel.Height;
                            hasKeepFirstResult = true;
                        }
                    }

                    if (links.Count > 0 && string.Equals(renderedGroup, info.CommitSHA, StringComparison.Ordinal))
                        continue;

                    if (!info.IsFirstInGroup && lineCenter > lineHeight)
                        continue;

                    var shaLink = string.Equals(firstGroup, info.CommitSHA, StringComparison.Ordinal)
                        ? firstLabel
                        : CreateShaLink(typeface, info.CommitSHA);
                    links.Add(new CommitInfoLink(info, shaLink, lineCenter));
                    renderedGroup = info.CommitSHA;
                }

                if (!hasKeepFirstResult)
                    keepFirstGroupVisible = hasFirstGroup && firstLineCenter - firstLabel.Height * 0.5 < 0;

                foreach (var link in links)
                {
                    link.KeepFirstVisible = keepFirstGroupVisible;
                    link.Top = GetTextTop(link.LineCenter, link.ShaLink.Height, keepFirstGroupVisible);
                }

                _commitInfoLinks = links;
                _layoutBlameData = _editor.BlameData;
                _layoutVerticalOffset = view.VerticalOffset;
                _layoutWidth = Bounds.Width;
                _layoutHeight = Bounds.Height;
                _layoutLineHeight = lineHeight;
                _layoutVisualLineCount = visualLineCount;
                return links;
            }

            private FormattedText CreateShaLink(Typeface typeface, string commitSHA)
            {
                return new FormattedText(
                    commitSHA,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    _editor.FontSize,
                    Brushes.DarkOrange);
            }

            internal void InvalidateLayoutCache()
            {
                _commitInfoLinks = null;
                _layoutBlameData = null;
            }

            private bool TryGetLineCenter(TextView view, VisualLine line, out double lineCenter)
            {
                if (line.TextLines.Count == 0)
                {
                    lineCenter = 0;
                    return false;
                }

                lineCenter = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineMiddle) - view.VerticalOffset;

                var lineTop = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - view.VerticalOffset;
                var lineBottom = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.LineBottom) - view.VerticalOffset;
                return lineBottom > 0 && lineTop < Bounds.Height;
            }

            private double GetTextTop(double lineCenter, double textHeight, bool keepFirstVisible)
            {
                var textTop = lineCenter - textHeight * 0.5;
                return keepFirstVisible ? Math.Max(0, textTop) : textTop;
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                var view = TextView;
                var maxWidth = 0.0;
                if (view != null && _editor.BlameData != null)
                {
                    var typeface = view.CreateTypeface();
                    var calculated = new HashSet<string>();
                    foreach (var info in _editor.BlameData.LineInfos)
                    {
                        if (!calculated.Add(info.CommitSHA))
                            continue;

                        var x = 0.0;
                        var shaLink = CreateShaLink(typeface, info.CommitSHA);
                        x += shaLink.Width + 8;

                        var author = new FormattedText(
                            info.Author,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            _editor.Foreground);
                        x += author.Width + 8;

                        var timeStr = Models.DateTimeFormat.Format(info.Timestamp, true);
                        var time = new FormattedText(
                            timeStr,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            _editor.Foreground);
                        x += time.Width;

                        if (maxWidth < x)
                            maxWidth = x;
                    }
                }

                return new Size(maxWidth, 0);
            }

            protected override void OnPointerMoved(PointerEventArgs e)
            {
                base.OnPointerMoved(e);

                var view = TextView;
                if (e.Handled)
                    return;

                if (view is not { VisualLinesValid: true } || _editor.BlameData == null)
                {
                    Cursor = Cursor.Default;
                    ToolTip.SetTip(this, null);
                    return;
                }

                var pos = e.GetPosition(this);
                foreach (var link in GetCommitInfoLinks(view, view.CreateTypeface()))
                {
                    var rect = new Rect(0, link.Top, link.ShaLink.Width, link.ShaLink.Height);
                    if (!rect.Contains(pos))
                        continue;

                    Cursor = Cursor.Parse("Hand");

                    if (DataContext is ViewModels.Blame blame)
                    {
                        var msg = blame.GetCommitMessage(link.Info.CommitSHA);
                        ToolTip.SetTip(this, msg);
                    }

                    return;
                }

                Cursor = Cursor.Default;
                ToolTip.SetTip(this, null);
            }

            protected override void OnPointerPressed(PointerPressedEventArgs e)
            {
                base.OnPointerPressed(e);

                var view = TextView;
                if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && view is { VisualLinesValid: true } && _editor.BlameData != null)
                {
                    var pos = e.GetPosition(this);
                    foreach (var link in GetCommitInfoLinks(view, view.CreateTypeface()))
                    {
                        var rect = new Rect(0, link.Top, link.ShaLink.Width, link.ShaLink.Height);
                        if (!rect.Contains(pos))
                            continue;

                        if (DataContext is ViewModels.Blame blame)
                            blame.NavigateToCommit(link.Info.File, link.Info.CommitSHA);

                        e.Handled = true;
                        break;
                    }
                }
            }

            private readonly BlameTextEditor _editor = null;
            private List<CommitInfoLink> _commitInfoLinks = null;
            private Models.BlameData _layoutBlameData = null;
            private double _layoutVerticalOffset = double.NaN;
            private double _layoutWidth = double.NaN;
            private double _layoutHeight = double.NaN;
            private double _layoutLineHeight = double.NaN;
            private int _layoutVisualLineCount = -1;
        }

        public class VerticalSeparatorMargin : AbstractMargin
        {
            public VerticalSeparatorMargin(BlameTextEditor editor)
            {
                _editor = editor;
            }

            public override void Render(DrawingContext context)
            {
                var pen = new Pen(_editor.BorderBrush);
                context.DrawLine(pen, new Point(0.5, 0), new Point(0.5, Bounds.Height));
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                return new Size(1, 0);
            }

            private readonly BlameTextEditor _editor = null;
        }

        public class LineBackgroundRenderer : IBackgroundRenderer
        {
            public KnownLayer Layer => KnownLayer.Background;

            public LineBackgroundRenderer(BlameTextEditor owner)
            {
                _owner = owner;
            }

            public void Draw(TextView textView, DrawingContext drawingContext)
            {
                if (!textView.VisualLinesValid)
                    return;

                var w = textView.Bounds.Width;
                if (double.IsNaN(w) || double.IsInfinity(w) || w <= 0)
                    return;

                var highlight = _owner._highlight;
                if (string.IsNullOrEmpty(highlight) || _owner.BlameData == null)
                    return;

                var color = (Color)_owner.FindResource("SystemAccentColor")!;
                var brush = new SolidColorBrush(color, 0.2);
                var lines = _owner.BlameData.LineInfos;

                foreach (var line in textView.VisualLines)
                {
                    if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted || line.TextLines.Count == 0)
                        continue;

                    var lineNumber = line.FirstDocumentLine.LineNumber;
                    if (lineNumber > lines.Count)
                        break;

                    var info = lines[lineNumber - 1];
                    if (!info.CommitSHA.Equals(highlight, StringComparison.Ordinal))
                        continue;

                    var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - textView.VerticalOffset;
                    var endY = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.LineBottom) - textView.VerticalOffset;
                    drawingContext.FillRectangle(brush, new Rect(0, startY, w, endY - startY));
                }
            }

            private readonly BlameTextEditor _owner;
        }

        public static readonly DirectProperty<BlameTextEditor, string> FileProperty =
            AvaloniaProperty.RegisterDirect<BlameTextEditor, string>(
                nameof(File),
                static o => o.File,
                static (o, v) => o.File = v);

        public string File
        {
            get => _file;
            set => SetAndRaise(FileProperty, ref _file, value);
        }

        public static readonly DirectProperty<BlameTextEditor, Models.BlameData> BlameDataProperty =
            AvaloniaProperty.RegisterDirect<BlameTextEditor, Models.BlameData>(
                nameof(BlameData),
                static o => o.BlameData,
                static (o, v) => o.BlameData = v);

        public Models.BlameData BlameData
        {
            get => _blameData;
            set => SetAndRaise(BlameDataProperty, ref _blameData, value);
        }

        public static readonly DirectProperty<BlameTextEditor, int> TabWidthProperty =
            AvaloniaProperty.RegisterDirect<BlameTextEditor, int>(
                nameof(TabWidth),
                static o => o.TabWidth,
                static (o, v) => o.TabWidth = v);

        public int TabWidth
        {
            get => _tabWidth;
            set => SetAndRaise(TabWidthProperty, ref _tabWidth, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public BlameTextEditor() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = true;
            ShowLineNumbers = false;
            WordWrap = false;

            Options.IndentationSize = _tabWidth;
            Options.EnableHyperlinks = false;
            Options.EnableEmailHyperlinks = false;

            _textMate = Models.TextMateHelper.CreateForEditor(this);

            TextArea.LeftMargins.Add(new CommitInfoMargin(this) { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeparatorMargin(this));
            TextArea.LeftMargins.Add(new LineNumberMargin() { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeparatorMargin(this));
            TextArea.Caret.PositionChanged += OnTextAreaCaretPositionChanged;
            TextArea.TextView.BackgroundRenderers.Add(new LineBackgroundRenderer(this));
            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
            TextArea.TextView.Margin = new Thickness(4, 0);
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            TextArea.LeftMargins.Clear();
            TextArea.Caret.PositionChanged -= OnTextAreaCaretPositionChanged;
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

            if (change.Property == FileProperty)
            {
                if (_file is { Length: > 0 })
                    Models.TextMateHelper.SetGrammarByFileName(_textMate, _file);
            }
            else if (change.Property == BlameDataProperty)
            {
                _highlight = string.Empty;
                if (_blameData is { IsBinary: false } blame)
                    Text = blame.Content;
                else
                    Text = string.Empty;

                InvalidateCommitInfoMarginMeasure();
            }
            else if (change.Property == TabWidthProperty)
            {
                Options.IndentationSize = _tabWidth;
            }
            else if (change.Property.Name is nameof(FontFamily) or nameof(FontSize) or nameof(FontStyle) or nameof(FontWeight))
            {
                InvalidateCommitInfoMarginMeasure();
            }
            else if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue != null)
            {
                Models.TextMateHelper.SetThemeByApp(_textMate);
            }
        }

        private void OnTextAreaCaretPositionChanged(object sender, EventArgs e)
        {
            if (!TextArea.IsFocused || _blameData == null)
                return;

            var caret = TextArea.Caret;
            if (caret == null || caret.Line > _blameData.LineInfos.Count)
                return;

            _highlight = _blameData.LineInfos[caret.Line - 1].CommitSHA;
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selected = SelectedText;
            if (string.IsNullOrEmpty(selected))
                return;

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = this.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, ev) =>
            {
                await this.CopyTextAsync(selected);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);

            e.Handled = true;
        }

        private void InvalidateCommitInfoMarginMeasure()
        {
            foreach (var margin in TextArea.LeftMargins)
            {
                if (margin is CommitInfoMargin commitInfo)
                {
                    commitInfo.InvalidateLayoutCache();
                    commitInfo.InvalidateMeasure();
                    break;
                }
            }
        }

        private string _file = null;
        private Models.BlameData _blameData = null;
        private int _tabWidth = 4;
        private TextMate.Installation _textMate = null;
        private string _highlight = string.Empty;
    }

    public partial class Blame : ChromelessWindow
    {
        public Blame()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            GC.Collect();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (!e.Handled && DataContext is ViewModels.Blame blame)
            {
                if (e.InitialPressMouseButton == MouseButton.XButton1)
                {
                    blame.Back();
                    e.Handled = true;
                }
                else if (e.InitialPressMouseButton == MouseButton.XButton2)
                {
                    blame.Forward();
                    e.Handled = true;
                }
            }
        }
    }
}
