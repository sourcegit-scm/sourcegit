using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;

namespace SourceGit.Views
{
    public class CommitMessageCodeCompletionData : ICompletionData
    {
        public IImage Image
        {
            get => null;
        }

        public string Text
        {
            get;
        }

        public object Content
        {
            get => Text;
        }

        public object Description
        {
            get => null;
        }

        public double Priority
        {
            get => 0;
        }

        public CommitMessageCodeCompletionData(string text)
        {
            Text = text;
        }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }

    public class CommitMessageTextEditor : TextEditor
    {
        public static readonly StyledProperty<string> CommitMessageProperty =
            AvaloniaProperty.Register<CommitMessageTextEditor, string>(nameof(CommitMessage), string.Empty);

        public string CommitMessage
        {
            get => GetValue(CommitMessageProperty);
            set => SetValue(CommitMessageProperty, value);
        }

        public static readonly StyledProperty<string> PlaceholderProperty =
            AvaloniaProperty.Register<CommitMessageTextEditor, string>(nameof(Placeholder), string.Empty);

        public string Placeholder
        {
            get => GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public static readonly StyledProperty<int> ColumnProperty =
            AvaloniaProperty.Register<CommitMessageTextEditor, int>(nameof(Column), 1);

        public int Column
        {
            get => GetValue(ColumnProperty);
            set => SetValue(ColumnProperty, value);
        }

        public static readonly StyledProperty<int> SubjectLengthProperty =
            AvaloniaProperty.Register<CommitMessageTextEditor, int>(nameof(SubjectLength));

        public int SubjectLength
        {
            get => GetValue(SubjectLengthProperty);
            set => SetValue(SubjectLengthProperty, value);
        }

        public static readonly StyledProperty<int> SubjectGuideLengthProperty =
            AvaloniaProperty.Register<CommitMessageTextEditor, int>(nameof(SubjectGuideLength));

        public int SubjectGuideLength
        {
            get => GetValue(SubjectGuideLengthProperty);
            set => SetValue(SubjectGuideLengthProperty, value);
        }

        public static readonly StyledProperty<bool> IsSubjectWarningIconVisibleProperty =
            AvaloniaProperty.Register<CommitMessageTextEditor, bool>(nameof(IsSubjectWarningIconVisible));

        public bool IsSubjectWarningIconVisible
        {
            get => GetValue(IsSubjectWarningIconVisibleProperty);
            set => SetValue(IsSubjectWarningIconVisibleProperty, value);
        }

        public static readonly StyledProperty<IBrush> SubjectLineBrushProperty =
            AvaloniaProperty.Register<CommitMessageTextEditor, IBrush>(nameof(SubjectLineBrush), Brushes.Gray);

        public IBrush SubjectLineBrush
        {
            get => GetValue(SubjectLineBrushProperty);
            set => SetValue(SubjectLineBrushProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public CommitMessageTextEditor() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = false;
            WordWrap = true;
            ShowLineNumbers = false;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            ClipToBounds = true;

            TextArea.TextView.Margin = new Thickness(4, 2);
            TextArea.TextView.ClipToBounds = false;
            TextArea.TextView.Options.EnableHyperlinks = false;
            TextArea.TextView.Options.EnableEmailHyperlinks = false;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var w = Bounds.Width;
            var pixelHeight = PixelSnapHelpers.GetPixelSize(this).Height;
            var pen = new Pen(SubjectLineBrush) { DashStyle = DashStyle.Dash };

            if (SubjectLength == 0)
            {
                var placeholder = Placeholder;
                if (!string.IsNullOrEmpty(placeholder))
                {
                    var formatted = new FormattedText(
                        Placeholder,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(FontFamily),
                        FontSize,
                        Brushes.Gray);

                    context.DrawText(formatted, new Point(4, 2));

                    var y = PixelSnapHelpers.PixelAlign(6 + formatted.Height, pixelHeight);
                    context.DrawLine(pen, new Point(0, y), new Point(w, y));
                }

                return;
            }

            if (TextArea.TextView is not { VisualLinesValid: true } view)
                return;

            var lines = new List<VisualLine>();
            foreach (var line in view.VisualLines)
            {
                if (line.IsDisposed || line.FirstDocumentLine == null || line.FirstDocumentLine.IsDeleted)
                    continue;

                lines.Add(line);
            }

            if (lines.Count == 0)
                return;

            lines.Sort((l, r) => l.StartOffset - r.StartOffset);

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.FirstDocumentLine.LineNumber == _subjectEndLine)
                {
                    var y = line.GetTextLineVisualYPosition(line.TextLines[^1], VisualYPosition.LineBottom) - view.VerticalOffset + 4;
                    y = PixelSnapHelpers.PixelAlign(y, pixelHeight);
                    context.DrawLine(pen, new Point(0, y), new Point(w, y));
                    return;
                }
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            TextArea.TextView.VisualLinesChanged += OnTextViewVisualLinesChanged;
            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
            TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;
            TextArea.TextView.VisualLinesChanged -= OnTextViewVisualLinesChanged;
            TextArea.Caret.PositionChanged -= OnCaretPositionChanged;

            base.OnUnloaded(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == CommitMessageProperty)
            {
                if (!_isEditing)
                    Text = CommitMessage;

                var lines = CommitMessage.ReplaceLineEndings("\n").Split('\n');
                var subjectLen = 0;
                var foundSubjectEnd = false;

                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (subjectLen == 0)
                            continue;

                        _subjectEndLine = i;
                        foundSubjectEnd = true;
                        break;
                    }

                    var validCharLen = line.TrimEnd().Length;
                    if (subjectLen > 0)
                        subjectLen += (validCharLen + 1);
                    else
                        subjectLen = validCharLen;
                }

                if (!foundSubjectEnd)
                    _subjectEndLine = lines.Length;

                SetCurrentValue(SubjectLengthProperty, subjectLen);
            }
            else if (change.Property == PlaceholderProperty && IsLoaded)
            {
                if (string.IsNullOrWhiteSpace(CommitMessage))
                    InvalidateVisual();
            }
            else if (change.Property == SubjectLengthProperty ||
                     change.Property == SubjectGuideLengthProperty)
            {
                SetCurrentValue(IsSubjectWarningIconVisibleProperty, SubjectLength > SubjectGuideLength);
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);

            if (!IsLoaded)
                return;

            _isEditing = true;
            SetCurrentValue(CommitMessageProperty, Text);
            _isEditing = false;

            var caretOffset = CaretOffset;
            var start = caretOffset;
            for (; start > 0; start--)
            {
                var ch = Text[start - 1];
                if (ch == '\n')
                    break;

                if (!char.IsAscii(ch))
                    return;
            }

            if (caretOffset < start + 2)
            {
                _completionWnd?.Close();
                return;
            }

            var word = Text.Substring(start, caretOffset - start);
            var matches = new List<CommitMessageCodeCompletionData>();
            foreach (var keyword in _keywords)
            {
                if (keyword.StartsWith(word, StringComparison.OrdinalIgnoreCase) && keyword.Length != word.Length)
                    matches.Add(new(keyword));
            }

            if (matches.Count > 0)
            {
                if (_completionWnd == null)
                {
                    _completionWnd = new CompletionWindow(TextArea);
                    _completionWnd.Closed += (_, _) => _completionWnd = null;
                    _completionWnd.Show();
                }

                _completionWnd.CompletionList.CompletionData.Clear();
                _completionWnd.CompletionList.CompletionData.AddRange(matches);
                _completionWnd.StartOffset = start;
                _completionWnd.EndOffset = caretOffset;
            }
            else
            {
                _completionWnd?.Close();
            }
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selection = TextArea.Selection;
            var hasSelected = selection is { IsEmpty: false };

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.IsEnabled = hasSelected;
            copy.Click += (_, ev) =>
            {
                Copy();
                ev.Handled = true;
            };

            var cut = new MenuItem();
            cut.Header = App.Text("Cut");
            cut.Icon = App.CreateMenuIcon("Icons.Cut");
            cut.IsEnabled = hasSelected;
            cut.Click += (_, ev) =>
            {
                Cut();
                ev.Handled = true;
            };

            var paste = new MenuItem();
            paste.Header = App.Text("Paste");
            paste.Icon = App.CreateMenuIcon("Icons.Paste");
            paste.Click += (_, ev) =>
            {
                Paste();
                ev.Handled = true;
            };

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Items.Add(cut);
            menu.Items.Add(paste);
            menu.Open(TextArea.TextView);
            e.Handled = true;
        }

        private void OnTextViewVisualLinesChanged(object sender, EventArgs e)
        {
            InvalidateVisual();
        }

        private void OnCaretPositionChanged(object sender, EventArgs e)
        {
            var col = TextArea.Caret.Column;
            SetCurrentValue(ColumnProperty, col);
        }

        private readonly List<string> _keywords = ["Acked-by: ", "Co-authored-by: ", "Reviewed-by: ", "Signed-off-by: ", "on-behalf-of: @", "BREAKING CHANGE: ", "Refs: "];
        private bool _isEditing = false;
        private int _subjectEndLine = 0;
        private CompletionWindow _completionWnd = null;
    }

    public partial class CommitMessageToolBox : UserControl
    {
        public static readonly StyledProperty<bool> ShowAdvancedOptionsProperty =
            AvaloniaProperty.Register<CommitMessageToolBox, bool>(nameof(ShowAdvancedOptions));

        public bool ShowAdvancedOptions
        {
            get => GetValue(ShowAdvancedOptionsProperty);
            set => SetValue(ShowAdvancedOptionsProperty, value);
        }

        public static readonly StyledProperty<string> CommitMessageProperty =
            AvaloniaProperty.Register<CommitMessageToolBox, string>(nameof(CommitMessage), string.Empty);

        public string CommitMessage
        {
            get => GetValue(CommitMessageProperty);
            set => SetValue(CommitMessageProperty, value);
        }

        public CommitMessageToolBox()
        {
            InitializeComponent();
        }

        private async void OnOpenCommitMessagePicker(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.WorkingCopy vm && ShowAdvancedOptions)
            {
                var repo = vm.Repository;
                var foreground = this.FindResource("Brush.FG1") as IBrush;

                var menu = new ContextMenu();
                menu.MaxWidth = 480;

                var gitTemplate = await new Commands.Config(repo.FullPath).GetAsync("commit.template");
                var templateCount = repo.Settings.CommitTemplates.Count;
                if (templateCount == 0 && string.IsNullOrEmpty(gitTemplate))
                {
                    menu.Items.Add(new MenuItem()
                    {
                        Header = App.Text("WorkingCopy.NoCommitTemplates"),
                        Icon = App.CreateMenuIcon("Icons.Code"),
                        IsEnabled = false
                    });
                }
                else
                {
                    for (int i = 0; i < templateCount; i++)
                    {
                        var icon = App.CreateMenuIcon("Icons.Code");
                        icon.Fill = foreground;

                        var template = repo.Settings.CommitTemplates[i];
                        var item = new MenuItem();
                        item.Header = App.Text("WorkingCopy.UseCommitTemplate", template.Name);
                        item.Icon = icon;
                        item.Click += (_, ev) =>
                        {
                            vm.ApplyCommitMessageTemplate(template);
                            ev.Handled = true;
                        };
                        menu.Items.Add(item);
                    }

                    if (!string.IsNullOrEmpty(gitTemplate))
                    {
                        if (!Path.IsPathRooted(gitTemplate))
                            gitTemplate = Native.OS.GetAbsPath(repo.FullPath, gitTemplate);

                        var friendlyName = gitTemplate;
                        if (!OperatingSystem.IsWindows())
                        {
                            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                            var prefixLen = home.EndsWith('/') ? home.Length - 1 : home.Length;
                            if (gitTemplate.StartsWith(home, StringComparison.Ordinal))
                                friendlyName = $"~{gitTemplate.AsSpan(prefixLen)}";
                        }

                        var icon = App.CreateMenuIcon("Icons.Code");
                        icon.Fill = foreground;

                        var gitTemplateItem = new MenuItem();
                        gitTemplateItem.Header = App.Text("WorkingCopy.UseCommitTemplate", friendlyName);
                        gitTemplateItem.Icon = icon;
                        gitTemplateItem.Click += (_, ev) =>
                        {
                            if (File.Exists(gitTemplate))
                                vm.CommitMessage = File.ReadAllText(gitTemplate);
                            ev.Handled = true;
                        };
                        menu.Items.Add(gitTemplateItem);
                    }
                }

                menu.Items.Add(new MenuItem() { Header = "-" });

                var historiesCount = repo.Settings.CommitMessages.Count;
                if (historiesCount == 0)
                {
                    menu.Items.Add(new MenuItem()
                    {
                        Header = App.Text("WorkingCopy.NoCommitHistories"),
                        Icon = App.CreateMenuIcon("Icons.Histories"),
                        IsEnabled = false
                    });
                }
                else
                {
                    for (int i = 0; i < historiesCount; i++)
                    {
                        var dup = repo.Settings.CommitMessages[i].Trim();
                        var header = new TextBlock()
                        {
                            Text = dup.ReplaceLineEndings(" "),
                            VerticalAlignment = VerticalAlignment.Center,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        };

                        var icon = App.CreateMenuIcon("Icons.Histories");
                        icon.Fill = foreground;

                        var item = new MenuItem();
                        item.Header = header;
                        item.Icon = icon;
                        item.Click += (_, ev) =>
                        {
                            vm.CommitMessage = dup;
                            ev.Handled = true;
                        };

                        menu.Items.Add(item);
                    }

                    menu.Items.Add(new MenuItem() { Header = "-" });

                    var clearIcon = App.CreateMenuIcon("Icons.Clear");
                    clearIcon.Fill = foreground;

                    var clearHistoryItem = new MenuItem();
                    clearHistoryItem.Header = App.Text("WorkingCopy.ClearCommitHistories");
                    clearHistoryItem.Icon = clearIcon;
                    clearHistoryItem.Click += async (_, ev) =>
                    {
                        await vm.ClearCommitMessageHistoryAsync();
                        ev.Handled = true;
                    };

                    menu.Items.Add(clearHistoryItem);
                }

                button.IsEnabled = false;
                menu.Placement = PlacementMode.TopEdgeAlignedLeft;
                menu.Closed += (_, _) => button.IsEnabled = true;
                menu.Open(button);
            }

            e.Handled = true;
        }

        private async void OnOpenOpenAIHelper(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm && sender is Button button && ShowAdvancedOptions)
            {
                var repo = vm.Repository;

                if (vm.Staged == null || vm.Staged.Count == 0)
                {
                    App.RaiseException(repo.FullPath, "No files added to commit!");
                    return;
                }

                var services = repo.GetPreferredOpenAIServices();
                if (services.Count == 0)
                {
                    App.RaiseException(repo.FullPath, "Bad configuration for OpenAI");
                    return;
                }

                if (services.Count == 1)
                {
                    await App.ShowDialog(new ViewModels.AIAssistant(repo, services[0], vm.Staged));
                    return;
                }

                var menu = new ContextMenu();
                foreach (var service in services)
                {
                    var dup = service;
                    var item = new MenuItem();
                    item.Header = service.Name;
                    item.Click += async (_, ev) =>
                    {
                        await App.ShowDialog(new ViewModels.AIAssistant(repo, dup, vm.Staged));
                        ev.Handled = true;
                    };

                    menu.Items.Add(item);
                }

                button.IsEnabled = false;
                menu.Placement = PlacementMode.TopEdgeAlignedLeft;
                menu.Closed += (_, _) => button.IsEnabled = true;
                menu.Open(button);
            }

            e.Handled = true;
        }

        private void OnOpenConventionalCommitHelper(object _, RoutedEventArgs e)
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
                return;

            var conventionalTypesOverride = owner switch
            {
                Launcher { DataContext: ViewModels.Launcher { ActivePage: { Data: ViewModels.Repository repo } } } => repo.Settings.ConventionalTypesOverride,
                RepositoryConfigure { DataContext: ViewModels.RepositoryConfigure config } => config.ConventionalTypesOverride,
                CommitMessageEditor editor => editor.ConventionalTypesOverride,
                _ => string.Empty
            };

            var vm = new ViewModels.ConventionalCommitMessageBuilder(conventionalTypesOverride, text => CommitMessage = text);
            var builder = new ConventionalCommitMessageBuilder() { DataContext = vm };
            builder.Show(owner);

            e.Handled = true;
        }
    }
}
