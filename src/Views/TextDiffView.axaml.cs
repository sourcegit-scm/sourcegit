using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using AvaloniaEdit.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TextMateSharp.Grammars;

namespace SourceGit.Views {
    public class TextDiffUnifiedSelection {
        public int StartLine { get; set; } = 0;
        public int EndLine { get; set; } = 0;
        public bool HasChanges { get; set; } = false;
        public bool HasLeftChanges { get; set; } = false;
        public int IgnoredAdds { get; set; } = 0;
        public int IgnoredDeletes { get; set; } = 0;

        public bool IsInRange(int idx) {
            return idx >= StartLine - 1 && idx < EndLine;
        }
    }

    public class CombinedTextDiffPresenter : TextEditor {
        public class LineNumberMargin : AbstractMargin {
            public LineNumberMargin(CombinedTextDiffPresenter editor, bool isOldLine) {
                _editor = editor;
                _isOldLine = isOldLine;
                ClipToBounds = true;
            }

            public override void Render(DrawingContext context) {
                if (_editor.DiffData == null) return;

                var view = TextView;
                if (view != null && view.VisualLinesValid) {
                    var typeface = view.CreateTypeface();
                    foreach (var line in view.VisualLines) {
                        var index = line.FirstDocumentLine.LineNumber;
                        if (index > _editor.DiffData.Lines.Count) break;

                        var info = _editor.DiffData.Lines[index - 1];
                        var lineNumber = _isOldLine ? info.OldLine : info.NewLine;
                        if (string.IsNullOrEmpty(lineNumber)) continue;

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

            protected override Size MeasureOverride(Size availableSize) {
                if (_editor.DiffData == null || TextView == null) {
                    return new Size(32, 0);
                } else {
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

            private CombinedTextDiffPresenter _editor;
            private bool _isOldLine;
        }

        public class VerticalSeperatorMargin : AbstractMargin {
            public VerticalSeperatorMargin(CombinedTextDiffPresenter editor) {
                _editor = editor;
            }

            public override void Render(DrawingContext context) {
                var pen = new Pen(_editor.BorderBrush, 1);
                context.DrawLine(pen, new Point(0, 0), new Point(0, Bounds.Height));
            }

            protected override Size MeasureOverride(Size availableSize) {
                return new Size(1, 0);
            }

            private CombinedTextDiffPresenter _editor = null;
        }

        public class LineBackgroundRenderer : IBackgroundRenderer {
            private static readonly Brush BG_EMPTY = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
            private static readonly Brush BG_ADDED = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));
            private static readonly Brush BG_DELETED = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0));

            public KnownLayer Layer => KnownLayer.Background;

            public LineBackgroundRenderer(CombinedTextDiffPresenter editor) {
                _editor = editor;
            }

            public void Draw(TextView textView, DrawingContext drawingContext) {
                if (_editor.Document == null || !textView.VisualLinesValid) return;

                var width = textView.Bounds.Width;
                foreach (var line in textView.VisualLines) {
                    var index = line.FirstDocumentLine.LineNumber;
                    if (index > _editor.DiffData.Lines.Count) break;

                    var info = _editor.DiffData.Lines[index - 1];
                    var bg = GetBrushByLineType(info.Type);
                    if (bg == null) continue;

                    var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - textView.VerticalOffset;
                    drawingContext.DrawRectangle(bg, null, new Rect(0, y, width, line.Height));
                }
            }

            private IBrush GetBrushByLineType(Models.TextDiffLineType type) {
                switch (type) {
                case Models.TextDiffLineType.None: return BG_EMPTY;
                case Models.TextDiffLineType.Added: return BG_ADDED;
                case Models.TextDiffLineType.Deleted: return BG_DELETED;
                default: return null;
                }
            }

            private CombinedTextDiffPresenter _editor = null;
        }

        public class LineStyleTransformer : DocumentColorizingTransformer {
            private static readonly Brush HL_ADDED = new SolidColorBrush(Color.FromArgb(90, 0, 255, 0));
            private static readonly Brush HL_DELETED = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0));

            public LineStyleTransformer(CombinedTextDiffPresenter editor, IBrush indicatorFG) {
                _editor = editor;
                _indicatorFG = indicatorFG;

                var font = App.Current.FindResource("JetBrainsMonoItalic") as FontFamily;
                _indicatorTypeface = new Typeface(font, FontStyle.Italic, FontWeight.Regular);
            }

            protected override void ColorizeLine(DocumentLine line) {
                var idx = line.LineNumber;
                if (idx > _editor.DiffData.Lines.Count) return;

                var info = _editor.DiffData.Lines[idx - 1];
                if (info.Type == Models.TextDiffLineType.Indicator) {
                    ChangeLinePart(line.Offset, line.EndOffset, v => {
                        v.TextRunProperties.SetForegroundBrush(_indicatorFG);
                        v.TextRunProperties.SetTypeface(_indicatorTypeface);
                    });

                    return;
                }

                if (info.Highlights.Count > 0) {
                    var bg = info.Type == Models.TextDiffLineType.Added ? HL_ADDED : HL_DELETED;
                    foreach (var highlight in info.Highlights) {
                        ChangeLinePart(line.Offset + highlight.Start, line.Offset + highlight.Start + highlight.Count, v => {
                            v.TextRunProperties.SetBackgroundBrush(bg);
                        });
                    }
                }
            }

            private CombinedTextDiffPresenter _editor;
            private IBrush _indicatorFG = Brushes.DarkGray;
            private Typeface _indicatorTypeface = Typeface.Default;
        }

        public static readonly StyledProperty<Models.TextDiff> DiffDataProperty =
            AvaloniaProperty.Register<CombinedTextDiffPresenter, Models.TextDiff>(nameof(DiffData));

        public Models.TextDiff DiffData {
            get => GetValue(DiffDataProperty);
            set => SetValue(DiffDataProperty, value);
        }

        public static readonly StyledProperty<IBrush> SecondaryFGProperty =
            AvaloniaProperty.Register<CombinedTextDiffPresenter, IBrush>(nameof(SecondaryFG), Brushes.Gray);

        public IBrush SecondaryFG {
            get => GetValue(SecondaryFGProperty);
            set => SetValue(SecondaryFGProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public CombinedTextDiffPresenter() : base(new TextArea(), new TextDocument()) {
            IsReadOnly = true;
            ShowLineNumbers = false;
            WordWrap = false;
        }

        protected override void OnLoaded(RoutedEventArgs e) {
            base.OnLoaded(e);

            TextArea.LeftMargins.Add(new LineNumberMargin(this, true) { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeperatorMargin(this));
            TextArea.LeftMargins.Add(new LineNumberMargin(this, false) { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeperatorMargin(this));

            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.TextView.BackgroundRenderers.Add(new LineBackgroundRenderer(this));
            TextArea.TextView.LineTransformers.Add(new LineStyleTransformer(this, SecondaryFG));
            TextArea.TextView.ContextRequested += OnTextViewContextRequested;

            if (App.Current?.ActualThemeVariant == ThemeVariant.Dark) {
                _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            } else {
                _registryOptions = new RegistryOptions(ThemeName.LightPlus);
            }

            _textMate = this.InstallTextMate(_registryOptions);
            UpdateGrammar();
        }

        protected override void OnUnloaded(RoutedEventArgs e) {
            base.OnUnloaded(e);

            TextArea.LeftMargins.Clear();
            TextArea.TextView.BackgroundRenderers.Clear();
            TextArea.TextView.LineTransformers.Clear();
            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;
            _registryOptions = null;
            _textMate.Dispose();
            _textMate = null;
            GC.Collect();
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e) {
            var selection = TextArea.Selection;
            if (selection.IsEmpty) return;

            var menu = new ContextMenu();
            var parentView = this.FindAncestorOfType<TextDiffView>();
            if (parentView != null) {
                parentView.FillContextMenuForWorkingCopyChange(menu, selection.StartPosition.Line, selection.EndPosition.Line, false);
            }

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (o, ev) => {
                App.CopyText(SelectedText);
                ev.Handled = true;
            };

            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);
            e.Handled = true;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);

            if (change.Property == DiffDataProperty) {
                if (DiffData != null) {
                    var builder = new StringBuilder();
                    foreach (var line in DiffData.Lines) {
                        builder.AppendLine(line.Content);
                    }

                    UpdateGrammar();
                    Text = builder.ToString();
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

        private void UpdateGrammar() {
            if (_textMate == null || DiffData == null) return;

            var ext = Path.GetExtension(DiffData.File);
            if (ext == ".h") {
                _textMate.SetGrammar(_registryOptions.GetScopeByLanguageId("cpp"));
            } else {
                _textMate.SetGrammar(_registryOptions.GetScopeByExtension(ext));
            }
        }

        private RegistryOptions _registryOptions;
        private TextMate.Installation _textMate;
    }

    public class SingleSideTextDiffPresenter : TextEditor {
        public class LineNumberMargin : AbstractMargin {
            public LineNumberMargin(SingleSideTextDiffPresenter editor) {
                _editor = editor;
                ClipToBounds = true;
            }

            public override void Render(DrawingContext context) {
                if (_editor.DiffData == null) return;

                var view = TextView;
                if (view != null && view.VisualLinesValid) {
                    var typeface = view.CreateTypeface();
                    var infos = _editor.IsOld ? _editor.DiffData.Old : _editor.DiffData.New;
                    foreach (var line in view.VisualLines) {
                        var index = line.FirstDocumentLine.LineNumber;
                        if (index > infos.Count) break;

                        var info = infos[index - 1];
                        var lineNumber = _editor.IsOld ? info.OldLine : info.NewLine;
                        if (string.IsNullOrEmpty(lineNumber)) continue;

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

            protected override Size MeasureOverride(Size availableSize) {
                if (_editor.DiffData == null || TextView == null) {
                    return new Size(32, 0);
                } else {
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

            private SingleSideTextDiffPresenter _editor;
        }

        public class VerticalSeperatorMargin : AbstractMargin {
            public VerticalSeperatorMargin(SingleSideTextDiffPresenter editor) {
                _editor = editor;
            }

            public override void Render(DrawingContext context) {
                var pen = new Pen(_editor.BorderBrush, 1);
                context.DrawLine(pen, new Point(0, 0), new Point(0, Bounds.Height));
            }

            protected override Size MeasureOverride(Size availableSize) {
                return new Size(1, 0);
            }

            private SingleSideTextDiffPresenter _editor = null;
        }

        public class LineBackgroundRenderer : IBackgroundRenderer {
            private static readonly Brush BG_EMPTY = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
            private static readonly Brush BG_ADDED = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));
            private static readonly Brush BG_DELETED = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0));

            public KnownLayer Layer => KnownLayer.Background;

            public LineBackgroundRenderer(SingleSideTextDiffPresenter editor) {
                _editor = editor;
            }

            public void Draw(TextView textView, DrawingContext drawingContext) {
                if (_editor.Document == null || !textView.VisualLinesValid) return;

                var width = textView.Bounds.Width;
                var infos = _editor.IsOld ? _editor.DiffData.Old : _editor.DiffData.New;
                foreach (var line in textView.VisualLines) {
                    var index = line.FirstDocumentLine.LineNumber;
                    if (index > infos.Count) break;

                    var info = infos[index - 1];
                    var bg = GetBrushByLineType(info.Type);
                    if (bg == null) continue;

                    var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - textView.VerticalOffset;
                    drawingContext.DrawRectangle(bg, null, new Rect(0, y, width, line.Height));
                }
            }

            private IBrush GetBrushByLineType(Models.TextDiffLineType type) {
                switch (type) {
                case Models.TextDiffLineType.None: return BG_EMPTY;
                case Models.TextDiffLineType.Added: return BG_ADDED;
                case Models.TextDiffLineType.Deleted: return BG_DELETED;
                default: return null;
                }
            }

            private SingleSideTextDiffPresenter _editor = null;
        }

        public class LineStyleTransformer : DocumentColorizingTransformer {
            private static readonly Brush HL_ADDED = new SolidColorBrush(Color.FromArgb(90, 0, 255, 0));
            private static readonly Brush HL_DELETED = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0));

            public LineStyleTransformer(SingleSideTextDiffPresenter editor, IBrush indicatorFG) {
                _editor = editor;
                _indicatorFG = indicatorFG;

                var font = App.Current.FindResource("JetBrainsMonoItalic") as FontFamily;
                _indicatorTypeface = new Typeface(font, FontStyle.Italic, FontWeight.Regular);
            }

            protected override void ColorizeLine(DocumentLine line) {
                var infos = _editor.IsOld ? _editor.DiffData.Old : _editor.DiffData.New;
                var idx = line.LineNumber;
                if (idx > infos.Count) return;

                var info = infos[idx - 1];
                if (info.Type == Models.TextDiffLineType.Indicator) {
                    ChangeLinePart(line.Offset, line.EndOffset, v => {
                        v.TextRunProperties.SetForegroundBrush(_indicatorFG);
                        v.TextRunProperties.SetTypeface(_indicatorTypeface);
                    });

                    return;
                }

                if (info.Highlights.Count > 0) {
                    var bg = info.Type == Models.TextDiffLineType.Added ? HL_ADDED : HL_DELETED;
                    foreach (var highlight in info.Highlights) {
                        ChangeLinePart(line.Offset + highlight.Start, line.Offset + highlight.Start + highlight.Count, v => {
                            v.TextRunProperties.SetBackgroundBrush(bg);
                        });
                    }
                }
            }

            private SingleSideTextDiffPresenter _editor;
            private IBrush _indicatorFG = Brushes.DarkGray;
            private Typeface _indicatorTypeface = Typeface.Default;
        }

        public static readonly StyledProperty<bool> IsOldProperty =
            AvaloniaProperty.Register<SingleSideTextDiffPresenter, bool>(nameof(IsOld));

        public bool IsOld {
            get => GetValue(IsOldProperty);
            set => SetValue(IsOldProperty, value);
        }

        public static readonly StyledProperty<ViewModels.TwoSideTextDiff> DiffDataProperty =
            AvaloniaProperty.Register<SingleSideTextDiffPresenter, ViewModels.TwoSideTextDiff>(nameof(DiffData));

        public ViewModels.TwoSideTextDiff DiffData {
            get => GetValue(DiffDataProperty);
            set => SetValue(DiffDataProperty, value);
        }

        public static readonly StyledProperty<IBrush> SecondaryFGProperty =
            AvaloniaProperty.Register<SingleSideTextDiffPresenter, IBrush>(nameof(SecondaryFG), Brushes.Gray);

        public IBrush SecondaryFG {
            get => GetValue(SecondaryFGProperty);
            set => SetValue(SecondaryFGProperty, value);
        }

        public static readonly StyledProperty<Vector> SyncScrollOffsetProperty =
            AvaloniaProperty.Register<SingleSideTextDiffPresenter, Vector>(nameof(SyncScrollOffset));

        public Vector SyncScrollOffset {
            get => GetValue(SyncScrollOffsetProperty);
            set => SetValue(SyncScrollOffsetProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public SingleSideTextDiffPresenter() : base(new TextArea(), new TextDocument()) {
            IsReadOnly = true;
            ShowLineNumbers = false;
            WordWrap = false;
        }

        protected override void OnLoaded(RoutedEventArgs e) {
            base.OnLoaded(e);

            TextArea.LeftMargins.Add(new LineNumberMargin(this) { Margin = new Thickness(8, 0) });
            TextArea.LeftMargins.Add(new VerticalSeperatorMargin(this));
            TextArea.TextView.Margin = new Thickness(4, 0);
            TextArea.TextView.BackgroundRenderers.Add(new LineBackgroundRenderer(this));
            TextArea.TextView.LineTransformers.Add(new LineStyleTransformer(this, SecondaryFG));
            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
            TextArea.TextView.ScrollOffsetChanged += OnTextViewScrollOffsetChanged;

            if (App.Current?.ActualThemeVariant == ThemeVariant.Dark) {
                _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
            } else {
                _registryOptions = new RegistryOptions(ThemeName.LightPlus);
            }

            _textMate = this.InstallTextMate(_registryOptions);
            UpdateGrammar();
        }

        protected override void OnUnloaded(RoutedEventArgs e) {
            base.OnUnloaded(e);

            TextArea.LeftMargins.Clear();
            TextArea.TextView.BackgroundRenderers.Clear();
            TextArea.TextView.LineTransformers.Clear();
            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;
            TextArea.TextView.ScrollOffsetChanged -= OnTextViewScrollOffsetChanged;
            _registryOptions = null;
            _textMate.Dispose();
            _textMate = null;
            GC.Collect();
        }

        private void OnTextViewScrollOffsetChanged(object sender, EventArgs e) {
            SyncScrollOffset = TextArea.TextView.ScrollOffset;
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e) {
            var selection = TextArea.Selection;
            if (selection.IsEmpty) return;

            var menu = new ContextMenu();
            var parentView = this.FindAncestorOfType<TextDiffView>();
            if (parentView != null) {
                parentView.FillContextMenuForWorkingCopyChange(menu, selection.StartPosition.Line, selection.EndPosition.Line, IsOld);
            }

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.Click += (o, ev) => {
                App.CopyText(SelectedText);
                ev.Handled = true;
            };

            menu.Items.Add(copy);
            menu.Open(TextArea.TextView);
            e.Handled = true;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);

            if (change.Property == DiffDataProperty) {
                if (DiffData != null) {
                    var builder = new StringBuilder();
                    if (IsOld) {
                        foreach (var line in DiffData.Old) {
                            builder.AppendLine(line.Content);
                        }
                    } else {
                        foreach (var line in DiffData.New) {
                            builder.AppendLine(line.Content);
                        }
                    }

                    UpdateGrammar();
                    Text = builder.ToString();
                } else {
                    Text = string.Empty;
                }
            } else if (change.Property == SyncScrollOffsetProperty) {
                if (TextArea.TextView.ScrollOffset != SyncScrollOffset) {
                    IScrollable scrollable = TextArea.TextView;
                    scrollable.Offset = SyncScrollOffset;
                }
            } else if (change.Property.Name == "ActualThemeVariant" && change.NewValue != null && _textMate != null) {
                if (App.Current?.ActualThemeVariant == ThemeVariant.Dark) {
                    _textMate.SetTheme(_registryOptions.LoadTheme(ThemeName.DarkPlus));
                } else {
                    _textMate.SetTheme(_registryOptions.LoadTheme(ThemeName.LightPlus));
                }
            }
        }

        private void UpdateGrammar() {
            if (_textMate == null || DiffData == null) return;

            var ext = Path.GetExtension(DiffData.File);
            if (ext == ".h") {
                _textMate.SetGrammar(_registryOptions.GetScopeByLanguageId("cpp"));
            } else {
                _textMate.SetGrammar(_registryOptions.GetScopeByExtension(ext));
            }
        }

        private RegistryOptions _registryOptions;
        private TextMate.Installation _textMate;
    }

    public partial class TextDiffView : UserControl {
        public static readonly StyledProperty<Models.TextDiff> TextDiffProperty =
            AvaloniaProperty.Register<TextDiffView, Models.TextDiff>(nameof(TextDiff), null);

        public Models.TextDiff TextDiff {
            get => GetValue(TextDiffProperty);
            set => SetValue(TextDiffProperty, value);
        }

        public static readonly StyledProperty<bool> UseCombinedProperty =
            AvaloniaProperty.Register<TextDiffView, bool>(nameof(UseCombined), false);

        public bool UseCombined {
            get => GetValue(UseCombinedProperty);
            set => SetValue(UseCombinedProperty, value);
        }

        public TextDiffView() {
            InitializeComponent();
        }

        public void FillContextMenuForWorkingCopyChange(ContextMenu menu, int startLine, int endLine, bool isOldSide) {
            var parentView = this.FindAncestorOfType<DiffView>();
            if (parentView == null) return;

            var ctx = parentView.DataContext as ViewModels.DiffContext;
            if (ctx == null) return;

            var change = ctx.WorkingCopyChange;
            if (change == null) return;

            if (startLine > endLine) {
                var tmp = startLine;
                startLine = endLine;
                endLine = tmp;
            }

            var selection = GetUnifiedSelection(startLine, endLine, isOldSide);
            if (!selection.HasChanges) return;

            // If all changes has been selected the use method provided by ViewModels.WorkingCopy.
            // Otherwise, use `git apply`
            if (!selection.HasLeftChanges) {
                var workcopyView = this.FindAncestorOfType<WorkingCopy>();
                if (workcopyView == null) return;

                if (ctx.IsUnstaged) {
                    var stage = new MenuItem();
                    stage.Header = App.Text("FileCM.StageSelectedLines");
                    stage.Icon = App.CreateMenuIcon("Icons.File.Add");
                    stage.Click += (_, e) => {
                        var workcopy = workcopyView.DataContext as ViewModels.WorkingCopy;
                        workcopy.StageChanges(new List<Models.Change> { change });
                        e.Handled = true;
                    };

                    var discard = new MenuItem();
                    discard.Header = App.Text("FileCM.DiscardSelectedLines");
                    discard.Icon = App.CreateMenuIcon("Icons.Undo");
                    discard.Click += (_, e) => {
                        var workcopy = workcopyView.DataContext as ViewModels.WorkingCopy;
                        workcopy.Discard(new List<Models.Change> { change });
                        e.Handled = true;
                    };

                    menu.Items.Add(stage);
                    menu.Items.Add(discard);
                } else {
                    var unstage = new MenuItem();
                    unstage.Header = App.Text("FileCM.UnstageSelectedLines");
                    unstage.Icon = App.CreateMenuIcon("Icons.File.Remove");
                    unstage.Click += (_, e) => {
                        var workcopy = workcopyView.DataContext as ViewModels.WorkingCopy;
                        workcopy.UnstageChanges(new List<Models.Change> { change });
                        e.Handled = true;
                    };
                    menu.Items.Add(unstage);
                }
            } else {
                var repoView = this.FindAncestorOfType<Repository>();
                if (repoView == null) return;

                if (ctx.IsUnstaged) {
                    var stage = new MenuItem();
                    stage.Header = App.Text("FileCM.StageSelectedLines");
                    stage.Icon = App.CreateMenuIcon("Icons.File.Add");
                    stage.Click += (_, e) => {
                        var repo = repoView.DataContext as ViewModels.Repository;
                        repo.SetWatcherEnabled(false);

                        var tmpFile = Path.GetTempFileName();
                        if (change.WorkTree == Models.ChangeState.Untracked) {
                            GenerateNewPatchFromSelection(change, null, selection, false, tmpFile);
                        } else if (UseCombined) {
                            var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                            GenerateCombinedPatchFromSelection(change, treeGuid, selection, false, tmpFile);
                        }

                        new Commands.Apply(ctx.RepositoryPath, tmpFile, true, "nowarn", "--cache --index").Exec();
                        File.Delete(tmpFile);

                        repo.RefreshWorkingCopyChanges();
                        repo.SetWatcherEnabled(true);
                        e.Handled = true;
                    };

                    var discard = new MenuItem();
                    discard.Header = App.Text("FileCM.DiscardSelectedLines");
                    discard.Icon = App.CreateMenuIcon("Icons.Undo");
                    discard.Click += (_, e) => {
                        var repo = repoView.DataContext as ViewModels.Repository;
                        repo.SetWatcherEnabled(false);

                        var tmpFile = Path.GetTempFileName();
                        if (change.WorkTree == Models.ChangeState.Untracked) {
                            GenerateNewPatchFromSelection(change, null, selection, true, tmpFile);
                        } else if (UseCombined) {
                            var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                            GenerateCombinedPatchFromSelection(change, treeGuid, selection, true, tmpFile);
                        }

                        new Commands.Apply(ctx.RepositoryPath, tmpFile, true, "nowarn", "--reverse").Exec();
                        File.Delete(tmpFile);

                        repo.RefreshWorkingCopyChanges();
                        repo.SetWatcherEnabled(true);
                        e.Handled = true;
                    };

                    menu.Items.Add(stage);
                    menu.Items.Add(discard);
                } else {
                    var unstage = new MenuItem();
                    unstage.Header = App.Text("FileCM.UnstageSelectedLines");
                    unstage.Icon = App.CreateMenuIcon("Icons.File.Remove");
                    unstage.Click += (_, e) => {
                        var repo = repoView.DataContext as ViewModels.Repository;
                        repo.SetWatcherEnabled(false);

                        var treeGuid = new Commands.QueryStagedFileBlobGuid(ctx.RepositoryPath, change.Path).Result();
                        var tmpFile = Path.GetTempFileName();
                        if (change.Index == Models.ChangeState.Added) {
                            GenerateNewPatchFromSelection(change, treeGuid, selection, true, tmpFile);
                        } else if (UseCombined) {
                            GenerateCombinedPatchFromSelection(change, treeGuid, selection, true, tmpFile);
                        }

                        new Commands.Apply(ctx.RepositoryPath, tmpFile, true, "nowarn", "--cache --index --reverse").Exec();
                        File.Delete(tmpFile);

                        repo.RefreshWorkingCopyChanges();
                        repo.SetWatcherEnabled(true);
                        e.Handled = true;
                    };
                    menu.Items.Add(unstage);
                }
            }

            menu.Items.Add(new MenuItem() { Header = "-" });
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);

            if (change.Property == TextDiffProperty || change.Property == UseCombinedProperty) {
                if (TextDiff == null) {
                    Content = null;
                } else if (UseCombined) {
                    Content = TextDiff;
                } else {
                    Content = new ViewModels.TwoSideTextDiff(TextDiff);
                }
            }
        }

        private TextDiffUnifiedSelection GetUnifiedSelection(int startLine, int endLine, bool isOldSide) {
            var rs = new TextDiffUnifiedSelection();
            if (Content is Models.TextDiff combined) {
                rs.StartLine = startLine;
                rs.EndLine = endLine;

                for (int i = 0; i < startLine - 1; i++) {
                    var line = combined.Lines[i];
                    if (line.Type == Models.TextDiffLineType.Added) {
                        rs.HasLeftChanges = true;
                        rs.IgnoredAdds++;
                    } else if (line.Type == Models.TextDiffLineType.Deleted) {
                        rs.HasLeftChanges = true;
                        rs.IgnoredDeletes++;
                    }
                }

                for (int i = startLine - 1; i < endLine; i++) {
                    var line = combined.Lines[i];
                    if (line.Type == Models.TextDiffLineType.Added || line.Type == Models.TextDiffLineType.Deleted) {
                        rs.HasChanges = true;
                        break;
                    }
                }

                if (!rs.HasLeftChanges) {
                    for (int i = endLine; i < combined.Lines.Count; i++) {
                        var line = combined.Lines[i];
                        if (line.Type == Models.TextDiffLineType.Added || line.Type == Models.TextDiffLineType.Deleted) {
                            rs.HasLeftChanges = true;
                            break;
                        }
                    }
                }
            } else if (Content is ViewModels.TwoSideTextDiff twoSides) {

            }

            return rs;
        }

        private void GenerateNewPatchFromSelection(Models.Change change, string fileBlobGuid, TextDiffUnifiedSelection selection, bool revert, string output) {
            var isTracked = !string.IsNullOrEmpty(fileBlobGuid);
            var fileGuid = isTracked ? fileBlobGuid.Substring(0, 8) : "00000000";

            var builder = new StringBuilder();
            builder.Append("diff --git a/").Append(change.Path).Append(" b/").Append(change.Path).Append('\n');
            if (!revert && !isTracked) builder.Append("new file mode 100644\n");
            builder.Append("index 00000000...").Append(fileGuid).Append('\n');
            builder.Append("--- ").Append((revert || isTracked) ? $"a/{change.Path}\n" : "/dev/null\n");
            builder.Append("+++ b/").Append(change.Path).Append('\n');

            var additions = selection.EndLine - selection.StartLine;
            if (selection.StartLine != 1) additions++;

            if (revert) {
                var totalLines = TextDiff.Lines.Count - 1;
                builder.Append($"@@ -0,").Append(totalLines - additions).Append(" +0,").Append(totalLines).Append(" @@");
                for (int i = 1; i <= totalLines; i++) {
                    var line = TextDiff.Lines[i];
                    if (line.Type != Models.TextDiffLineType.Added) continue;
                    builder.Append(selection.IsInRange(i) ? "\n+" : "\n ").Append(line.Content);
                }
            } else {
                builder.Append("@@ -0,0 +0,").Append(additions).Append(" @@");
                for (int i = selection.StartLine - 1; i < selection.EndLine; i++) {
                    var line = TextDiff.Lines[i];
                    if (line.Type != Models.TextDiffLineType.Added) continue;
                    builder.Append("\n+").Append(line.Content);
                }
            }

            builder.Append("\n\\ No newline at end of file\n");
            File.WriteAllText(output, builder.ToString());
        }

        private void GenerateCombinedPatchFromSelection(Models.Change change, string fileTreeGuid, TextDiffUnifiedSelection selection, bool revert, string output) {
            var orgFile = !string.IsNullOrEmpty(change.OriginalPath) ? change.OriginalPath : change.Path;
            var indicatorRegex = new Regex(@"^@@ \-(\d+),?\d* \+(\d+),?\d* @@");
            var diff = TextDiff;

            var builder = new StringBuilder();
            builder.Append("diff --git a/").Append(change.Path).Append(" b/").Append(change.Path).Append('\n');
            builder.Append("index 00000000...").Append(fileTreeGuid).Append(" 100644\n");
            builder.Append("--- a/").Append(orgFile).Append('\n');
            builder.Append("+++ b/").Append(change.Path).Append('\n');

            // If last line of selection is a change. Find one more line.
            var tail = null as string;
            if (selection.EndLine < diff.Lines.Count) {
                var lastLine = diff.Lines[selection.EndLine - 1];
                if (lastLine.Type == Models.TextDiffLineType.Added || lastLine.Type == Models.TextDiffLineType.Deleted) {
                    for (int i = selection.EndLine; i < diff.Lines.Count; i++) {
                        var line = diff.Lines[i];
                        if (line.Type == Models.TextDiffLineType.Indicator) break;
                        if (line.Type == Models.TextDiffLineType.Normal || line.Type == Models.TextDiffLineType.Deleted) {
                            tail = line.Content;
                            break;
                        }
                    }
                }
            }

            // If the first line is not indicator.
            if (diff.Lines[selection.StartLine - 1].Type != Models.TextDiffLineType.Indicator) {
                var indicator = selection.StartLine - 1;
                for (int i = selection.StartLine - 2; i >= 0; i--) {
                    var line = diff.Lines[i];
                    if (line.Type == Models.TextDiffLineType.Indicator) {
                        indicator = i;
                        break;
                    }
                }

                var ignoreAdds = 0;
                var ignoreRemoves = 0;
                for (int i = 0; i < indicator; i++) {
                    var line = diff.Lines[i];
                    if (line.Type == Models.TextDiffLineType.Added) {
                        ignoreAdds++;
                    } else if (line.Type == Models.TextDiffLineType.Deleted) {
                        ignoreRemoves++;
                    }
                }

                for (int i = indicator; i < selection.StartLine - 1; i++) {
                    var line = diff.Lines[i];
                    if (line.Type == Models.TextDiffLineType.Indicator) {
                        ProcessIndicatorForPatch(builder, line, i, selection.StartLine, selection.EndLine, ignoreRemoves, ignoreAdds, tail != null);
                    } else if (line.Type == Models.TextDiffLineType.Added) {
                        // Ignores
                    } else if (line.Type == Models.TextDiffLineType.Deleted || line.Type == Models.TextDiffLineType.Normal) {
                        // Traits ignored deleted as normal.
                        builder.Append("\n ").Append(line.Content);
                    }
                }
            }

            // Outputs the selected lines.
            for (int i = selection.StartLine - 1; i < selection.EndLine; i++) {
                var line = diff.Lines[i];
                if (line.Type == Models.TextDiffLineType.Indicator) {
                    if (!ProcessIndicatorForPatch(builder, line, i, selection.StartLine, selection.EndLine, selection.IgnoredDeletes, selection.IgnoredAdds, tail != null)) {
                        break;
                    }
                } else if (line.Type == Models.TextDiffLineType.Normal) {
                    builder.Append("\n ").Append(line.Content);
                } else if (line.Type == Models.TextDiffLineType.Added) {
                    builder.Append("\n+").Append(line.Content);
                } else if (line.Type == Models.TextDiffLineType.Deleted) {
                    builder.Append("\n-").Append(line.Content);
                }
            }

            builder.Append("\n ").Append(tail);
            builder.Append("\n");
            File.WriteAllText(output, builder.ToString());
        }

        private bool ProcessIndicatorForPatch(StringBuilder builder, Models.TextDiffLine indicator, int idx, int start, int end, int ignoreRemoves, int ignoreAdds, bool tailed) {
            var indicatorRegex = new Regex(@"^@@ \-(\d+),?\d* \+(\d+),?\d* @@");
            var diff = TextDiff;

            var match = indicatorRegex.Match(indicator.Content);
            var oldStart = int.Parse(match.Groups[1].Value);
            var newStart = int.Parse(match.Groups[2].Value) + ignoreRemoves - ignoreAdds;
            var oldCount = 0;
            var newCount = 0;
            for (int i = idx + 1; i < end; i++) {
                var test = diff.Lines[i];
                if (test.Type == Models.TextDiffLineType.Indicator) break;

                if (test.Type == Models.TextDiffLineType.Normal) {
                    oldCount++;
                    newCount++;
                } else if (test.Type == Models.TextDiffLineType.Added) {
                    if (i >= start - 1) newCount++;

                    if (i == end - 1 && tailed) {
                        newCount++;
                        oldCount++;
                    }
                } else if (test.Type == Models.TextDiffLineType.Deleted) {
                    if (i < start - 1) newCount++;
                    oldCount++;

                    if (i == end - 1 && tailed) {
                        newCount++;
                        oldCount++;
                    }
                }
            }

            if (oldCount == 0 && newCount == 0) return false;

            builder.Append($"@@ -{oldStart},{oldCount} +{newStart},{newCount} @@");
            return true;
        }
    }
}
