using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using AvaloniaEdit.Utils;
using System;
using System.Globalization;
using System.IO;
using TextMateSharp.Grammars;

namespace SourceGit.Views {
    public class BlameTextEditor : TextEditor {
        public class CommitInfoMargin : AbstractMargin {
            public CommitInfoMargin(BlameTextEditor editor) {
                _editor = editor;
                ClipToBounds = true;
            }

            public override void Render(DrawingContext context) {
                if (_editor.BlameData == null) return;

                var view = TextView;
                if (view != null && view.VisualLinesValid) {
                    var typeface = view.CreateTypeface();
                    var underlinePen = new Pen(Brushes.DarkOrange, 1);

                    foreach (var line in view.VisualLines) {
                        var lineNumber = line.FirstDocumentLine.LineNumber;
                        if (lineNumber > _editor.BlameData.LineInfos.Count) break;

                        var info = _editor.BlameData.LineInfos[lineNumber - 1];
                        if (!info.IsFirstInGroup) continue;

                        var x = 0.0;
                        var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - view.VerticalOffset;

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

            protected override Size MeasureOverride(Size availableSize) {
                return new Size(250, 0);
            }

            protected override void OnPointerPressed(PointerPressedEventArgs e) {
                base.OnPointerPressed(e);

                var view = TextView;
                if (!e.Handled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && view != null && view.VisualLinesValid) {
                    var pos = e.GetPosition(this);
                    var typeface = view.CreateTypeface();

                    foreach (var line in view.VisualLines) {
                        var lineNumber = line.FirstDocumentLine.LineNumber;
                        if (lineNumber >= _editor.BlameData.LineInfos.Count) break;

                        var info = _editor.BlameData.LineInfos[lineNumber - 1];
                        if (!info.IsFirstInGroup) continue;

                        var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - view.VerticalOffset;
                        var shaLink = new FormattedText(
                            info.CommitSHA,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            _editor.FontSize,
                            Brushes.DarkOrange);

                        var rect = new Rect(0, y, shaLink.Width, shaLink.Height);
                        if (rect.Contains(pos)) {
                            _editor.OnCommitSHAClicked(info.CommitSHA);
                            e.Handled = true;
                            break;
                        }
                    }
                }
            }

            private BlameTextEditor _editor = null;
        }

        public class VerticalSeperatorMargin : AbstractMargin {
            public VerticalSeperatorMargin(BlameTextEditor editor) {
                _editor = editor;
            }

            public override void Render(DrawingContext context) {
                var pen = new Pen(_editor.BorderBrush, 1);
                context.DrawLine(pen, new Point(0, 0), new Point(0, Bounds.Height));
            }

            protected override Size MeasureOverride(Size availableSize) {
                return new Size(1, 0);
            }

            private BlameTextEditor _editor = null;
        }

        public static readonly StyledProperty<Models.BlameData> BlameDataProperty =
            AvaloniaProperty.Register<BlameTextEditor, Models.BlameData>(nameof(BlameData));

        public Models.BlameData BlameData {
            get => GetValue(BlameDataProperty);
            set => SetValue(BlameDataProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public BlameTextEditor() : base(new TextArea(), new TextDocument()) {
            IsReadOnly = true;
            ShowLineNumbers = false;
            WordWrap = false;
        }

        public void OnCommitSHAClicked(string sha) {
            if (DataContext is ViewModels.Blame blame) {
                blame.NavigateToCommit(sha);
            }
        }

        protected override void OnLoaded(RoutedEventArgs e) {
            base.OnLoaded(e);

            TextArea.LeftMargins.Add(new LineNumberMargin() { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeperatorMargin(this));
            TextArea.LeftMargins.Add(new CommitInfoMargin(this) { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeperatorMargin(this));
            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
            TextArea.TextView.Margin = new Thickness(4, 0);

            if (App.Current?.ActualThemeVariant == ThemeVariant.Dark) {
                _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            } else {
                _registryOptions = new RegistryOptions(ThemeName.LightPlus);
            }

            _textMate = this.InstallTextMate(_registryOptions);

            if (BlameData != null) {
                _textMate.SetGrammar(_registryOptions.GetScopeByExtension(Path.GetExtension(BlameData.File)));
            }
        }

        protected override void OnUnloaded(RoutedEventArgs e) {
            base.OnUnloaded(e);

            TextArea.LeftMargins.Clear();
            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;

            _registryOptions = null;
            _textMate.Dispose();
            _textMate = null;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);

            if (change.Property == BlameDataProperty) {
                if (BlameData != null) {
                    Text = BlameData.Content;
                    if (_textMate != null) _textMate.SetGrammar(_registryOptions.GetScopeByExtension(Path.GetExtension(BlameData.File)));
                } else {
                    Text = string.Empty;
                }
            } else if (change.Property.Name == "ActualThemeVariant" && change.NewValue != null && _textMate != null) {
                if (App.Current?.ActualThemeVariant == ThemeVariant.Dark) {
                    _textMate.SetTheme(_registryOptions.LoadTheme(ThemeName.DarkPlus));
                } else {
                    _textMate.SetTheme(_registryOptions.LoadTheme(ThemeName.LightPlus));
                }
            }
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e) {
            var selected = SelectedText;
            if (string.IsNullOrEmpty(selected)) return;

            var icon = new Avalonia.Controls.Shapes.Path();
            icon.Width = 10;
            icon.Height = 10;
            icon.Stretch = Stretch.Uniform;
            icon.Data = App.Current?.FindResource("Icons.Copy") as StreamGeometry;

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = icon;
            copy.Click += (o, ev) => {
                App.CopyText(selected);
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);
            e.Handled = true;
        }

        private RegistryOptions _registryOptions = null;
        private TextMate.Installation _textMate = null;
    }

    public partial class Blame : Window {
        public Blame() {
            if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                Owner = desktop.MainWindow;
            }

            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            GC.Collect();
        }

        private void OnCommitSHAPointerPressed(object sender, PointerPressedEventArgs e) {
            if (DataContext is ViewModels.Blame blame) {
                var txt = sender as TextBlock;
                blame.NavigateToCommit(txt.Text);
            }
            e.Handled = true;
        }
    }
}
