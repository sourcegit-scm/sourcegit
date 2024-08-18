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
                if (view != null && view.VisualLinesValid)
                {
                    var typeface = view.CreateTypeface();
                    var underlinePen = new Pen(Brushes.DarkOrange);

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

                        var time = new FormattedText(
                            info.Time,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            _editor.Foreground);
                        context.DrawText(time, new Point(x, y));
                        x += time.Width + 8;

                        var author = new FormattedText(
                            info.Author,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            _editor.Foreground);
                        context.DrawText(author, new Point(x, y));
                    }
                }
            }

            protected override Size MeasureOverride(Size availableSize)
            {
                var view = TextView;
                var maxWidth = 0.0;
                if (view != null && view.VisualLinesValid && _editor.BlameData != null)
                {
                    var typeface = view.CreateTypeface();
                    var calculated = new HashSet<string>();
                    foreach (var line in view.VisualLines)
                    {
                        var lineNumber = line.FirstDocumentLine.LineNumber;
                        if (lineNumber > _editor.BlameData.LineInfos.Count)
                            break;

                        var info = _editor.BlameData.LineInfos[lineNumber - 1];

                        if (calculated.Contains(info.CommitSHA))
                            continue;
                        calculated.Add(info.CommitSHA);

                        var x = 0.0;
                        var shaLink = new FormattedText(
                            info.CommitSHA,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            Brushes.DarkOrange);
                        x += shaLink.Width + 8;

                        var time = new FormattedText(
                            info.Time,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            _editor.Foreground);
                        x += time.Width + 8;

                        var author = new FormattedText(
                            info.Author,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            _editor.Foreground);
                        x += author.Width;

                        if (maxWidth < x)
                            maxWidth = x;
                    }
                }

                return new Size(maxWidth, 0);
            }

            protected override void OnPointerPressed(PointerPressedEventArgs e)
            {
                base.OnPointerPressed(e);

                var view = TextView;
                if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && view != null && view.VisualLinesValid)
                {
                    var pos = e.GetPosition(this);
                    var typeface = view.CreateTypeface();

                    foreach (var line in view.VisualLines)
                    {
                        if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                            continue;

                        var lineNumber = line.FirstDocumentLine.LineNumber;
                        if (lineNumber >= _editor.BlameData.LineInfos.Count)
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
                            _editor.OnCommitSHAClicked(info.CommitSHA);
                            e.Handled = true;
                            break;
                        }
                    }
                }
            }

            private readonly BlameTextEditor _editor = null;
        }

        public class VerticalSeperatorMargin : AbstractMargin
        {
            public VerticalSeperatorMargin(BlameTextEditor editor)
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

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public BlameTextEditor() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = true;
            ShowLineNumbers = false;
            WordWrap = false;

            _textMate = Models.TextMateHelper.CreateForEditor(this);

            TextArea.LeftMargins.Add(new LineNumberMargin() { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeperatorMargin(this));
            TextArea.LeftMargins.Add(new CommitInfoMargin(this) { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeperatorMargin(this));
            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
            TextArea.TextView.VisualLinesChanged += OnTextViewVisualLinesChanged;
            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.TextView.Options.EnableHyperlinks = false;
            TextArea.TextView.Options.EnableEmailHyperlinks = false;
        }

        public void OnCommitSHAClicked(string sha)
        {
            if (DataContext is ViewModels.Blame blame)
            {
                blame.NavigateToCommit(sha);
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            TextArea.LeftMargins.Clear();
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
                if (BlameData != null)
                {
                    Models.TextMateHelper.SetGrammarByFileName(_textMate, BlameData.File);
                    Text = BlameData.Content;
                }
                else
                {
                    Text = string.Empty;
                }
            }
            else if (change.Property.Name == "ActualThemeVariant" && change.NewValue != null)
            {
                Models.TextMateHelper.SetThemeByApp(_textMate);
            }
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selected = SelectedText;
            if (string.IsNullOrEmpty(selected))
                return;

            var copy = new MenuItem() { Header = App.Text("Copy") };
            copy.Click += (_, ev) =>
            {
                App.CopyText(selected);
                ev.Handled = true;
            };

            if (this.FindResource("Icons.Copy") is StreamGeometry geo)
            {
                copy.Icon = new Avalonia.Controls.Shapes.Path()
                {
                    Width = 10,
                    Height = 10,
                    Stretch = Stretch.Fill,
                    Data = geo,
                };
            }

            var menu = new ContextMenu();
            menu.Items.Add(copy);

            TextArea.TextView.OpenContextMenu(menu);
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
        }

        private TextMate.Installation _textMate = null;
    }

    public partial class Blame : ChromelessWindow
    {
        public Blame()
        {
            InitializeComponent();
        }

        private void MaximizeOrRestoreWindow(object _, TappedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;

            e.Handled = true;
        }

        private void BeginMoveWindow(object _, PointerPressedEventArgs e)
        {
            if (e.ClickCount == 1)
                BeginMoveDrag(e);

            e.Handled = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            GC.Collect();
        }
    }
}
