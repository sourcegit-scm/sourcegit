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

            public override void Render(DrawingContext context)
            {
                if (_editor.BlameData == null)
                    return;

                var view = TextView;
                if (view is { VisualLinesValid: true })
                {
                    var typeface = view.CreateTypeface();
                    var underlinePen = new Pen(Brushes.DarkOrange);
                    var width = Bounds.Width;

                    foreach (var line in view.VisualLines)
                    {
                        if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                            continue;

                        var lineNumber = line.FirstDocumentLine.LineNumber;
                        if (lineNumber > _editor.BlameData.LineInfos.Count)
                            break;

                        var info = _editor.BlameData.LineInfos[lineNumber - 1];
                        var x = 0.0;
                        var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - view.VerticalOffset;
                        if (!info.IsFirstInGroup && y > view.DefaultLineHeight * 0.6)
                            continue;

                        var shaLink = new FormattedText(
                            info.CommitSHA,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            Brushes.DarkOrange);
                        context.DrawText(shaLink, new Point(x, y));
                        context.DrawLine(underlinePen, new Point(x, y + shaLink.Baseline + 2), new Point(x + shaLink.Width, y + shaLink.Baseline + 2));
                        x += shaLink.Width + 8;

                        var author = new FormattedText(
                            info.Author,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            _editor.Foreground);
                        context.DrawText(author, new Point(x, y));

                        var time = new FormattedText(
                            info.Time,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            _editor.Foreground);
                        context.DrawText(time, new Point(width - time.Width, y));
                    }
                }
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                var view = TextView;
                var maxWidth = 0.0;
                if (view is { VisualLinesValid: true } && _editor.BlameData != null)
                {
                    var typeface = view.CreateTypeface();
                    var calculated = new HashSet<string>();
                    foreach (var line in view.VisualLines)
                    {
                        var lineNumber = line.FirstDocumentLine.LineNumber;
                        if (lineNumber > _editor.BlameData.LineInfos.Count)
                            break;

                        var info = _editor.BlameData.LineInfos[lineNumber - 1];

                        if (!calculated.Add(info.CommitSHA))
                            continue;

                        var x = 0.0;
                        var shaLink = new FormattedText(
                            info.CommitSHA,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            Brushes.DarkOrange);
                        x += shaLink.Width + 8;

                        var author = new FormattedText(
                            info.Author,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            _editor.Foreground);
                        x += author.Width + 8;

                        var time = new FormattedText(
                            info.Time,
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
                if (!e.Handled && view is { VisualLinesValid: true })
                {
                    var pos = e.GetPosition(this);
                    var typeface = view.CreateTypeface();

                    foreach (var line in view.VisualLines)
                    {
                        if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                            continue;

                        var lineNumber = line.FirstDocumentLine.LineNumber;
                        if (lineNumber > _editor.BlameData.LineInfos.Count)
                            break;

                        var info = _editor.BlameData.LineInfos[lineNumber - 1];
                        var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - view.VerticalOffset;
                        var shaLink = new FormattedText(
                            info.CommitSHA,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            Brushes.DarkOrange);

                        var rect = new Rect(0, y, shaLink.Width, shaLink.Height);
                        if (rect.Contains(pos))
                        {
                            Cursor = Cursor.Parse("Hand");

                            if (DataContext is ViewModels.Blame blame)
                            {
                                var msg = blame.GetCommitMessage(info.CommitSHA);
                                ToolTip.SetTip(this, msg);
                            }

                            return;
                        }
                    }

                    Cursor = Cursor.Default;
                    ToolTip.SetTip(this, null);
                }
            }

            protected override void OnPointerPressed(PointerPressedEventArgs e)
            {
                base.OnPointerPressed(e);

                var view = TextView;
                if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && view is { VisualLinesValid: true })
                {
                    var pos = e.GetPosition(this);
                    var typeface = view.CreateTypeface();

                    foreach (var line in view.VisualLines)
                    {
                        if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                            continue;

                        var lineNumber = line.FirstDocumentLine.LineNumber;
                        if (lineNumber > _editor.BlameData.LineInfos.Count)
                            break;

                        var info = _editor.BlameData.LineInfos[lineNumber - 1];
                        var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - view.VerticalOffset;
                        var shaLink = new FormattedText(
                            info.CommitSHA,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            Brushes.DarkOrange);

                        var rect = new Rect(0, y, shaLink.Width, shaLink.Height);
                        if (rect.Contains(pos))
                        {
                            if (DataContext is ViewModels.Blame blame)
                                blame.NavigateToCommit(info.CommitSHA, false);

                            e.Handled = true;
                            break;
                        }
                    }
                }
            }

            private readonly BlameTextEditor _editor = null;
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
                context.DrawLine(pen, new Point(0, 0), new Point(0, Bounds.Height));
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                return new Size(1, 0);
            }

            private readonly BlameTextEditor _editor = null;
        }

        public static readonly StyledProperty<Models.BlameData> BlameDataProperty =
            AvaloniaProperty.Register<BlameTextEditor, Models.BlameData>(nameof(BlameData));

        public Models.BlameData BlameData
        {
            get => GetValue(BlameDataProperty);
            set => SetValue(BlameDataProperty, value);
        }

        public static readonly StyledProperty<int> TabWidthProperty =
            AvaloniaProperty.Register<BlameTextEditor, int>(nameof(TabWidth), 4);

        public int TabWidth
        {
            get => GetValue(TabWidthProperty);
            set => SetValue(TabWidthProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public BlameTextEditor() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = true;
            ShowLineNumbers = false;
            WordWrap = false;

            Options.IndentationSize = TabWidth;
            Options.EnableHyperlinks = false;
            Options.EnableEmailHyperlinks = false;

            _textMate = Models.TextMateHelper.CreateForEditor(this);

            TextArea.LeftMargins.Add(new CommitInfoMargin(this) { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeparatorMargin(this));
            TextArea.LeftMargins.Add(new LineNumberMargin() { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeparatorMargin(this));
            TextArea.Caret.PositionChanged += OnTextAreaCaretPositionChanged;
            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
            TextArea.TextView.VisualLinesChanged += OnTextViewVisualLinesChanged;
            TextArea.TextView.Margin = new Thickness(4, 0);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (string.IsNullOrEmpty(_highlight))
                return;

            var view = TextArea.TextView;
            if (view is not { VisualLinesValid: true })
                return;

            var color = (Color)this.FindResource("SystemAccentColor")!;
            var brush = new SolidColorBrush(color, 0.4);
            foreach (var line in view.VisualLines)
            {
                if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                    continue;

                var lineNumber = line.FirstDocumentLine.LineNumber;
                if (lineNumber > BlameData.LineInfos.Count)
                    break;

                var info = BlameData.LineInfos[lineNumber - 1];
                if (info.CommitSHA != _highlight)
                    continue;

                var startY = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.LineTop) - view.VerticalOffset;
                var endY = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.LineBottom) - view.VerticalOffset;
                context.FillRectangle(brush, new Rect(0, startY, Bounds.Width, endY - startY));
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            TextArea.LeftMargins.Clear();
            TextArea.Caret.PositionChanged -= OnTextAreaCaretPositionChanged;
            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;
            TextArea.TextView.VisualLinesChanged -= OnTextViewVisualLinesChanged;

            if (_textMate != null)
            {
                _textMate.Dispose();
                _textMate = null;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BlameDataProperty)
            {
                if (BlameData is { IsBinary: false } blame)
                {
                    Models.TextMateHelper.SetGrammarByFileName(_textMate, blame.File);
                    Text = blame.Content;
                }
                else
                {
                    Text = string.Empty;
                }
            }
            else if (change.Property == TabWidthProperty)
            {
                Options.IndentationSize = TabWidth;
            }
            else if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue != null)
            {
                Models.TextMateHelper.SetThemeByApp(_textMate);
            }
        }

        private void OnTextAreaCaretPositionChanged(object sender, EventArgs e)
        {
            if (!TextArea.IsFocused)
                return;

            var caret = TextArea.Caret;
            if (caret == null || caret.Line > BlameData.LineInfos.Count)
                return;

            _highlight = BlameData.LineInfos[caret.Line - 1].CommitSHA;
            InvalidateVisual();
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

        private void OnTextViewVisualLinesChanged(object sender, EventArgs e)
        {
            foreach (var margin in TextArea.LeftMargins)
            {
                if (margin is CommitInfoMargin commitInfo)
                {
                    commitInfo.InvalidateMeasure();
                    break;
                }
            }

            InvalidateVisual();
        }

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
