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
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace SourceGit.Views
{
    public class CommitMessageTextEditor : TextEditor
    {
        public static readonly StyledProperty<string> CommitMessageProperty =
            AvaloniaProperty.Register<CommitMessageTextEditor, string>(nameof(CommitMessage), string.Empty);

        public string CommitMessage
        {
            get => GetValue(CommitMessageProperty);
            set => SetValue(CommitMessageProperty, value);
        }

        public static readonly StyledProperty<int> SubjectLengthProperty =
            AvaloniaProperty.Register<CommitMessageTextEditor, int>(nameof(SubjectLength), 0);

        public int SubjectLength
        {
            get => GetValue(SubjectLengthProperty);
            set => SetValue(SubjectLengthProperty, value);
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

            TextArea.TextView.Margin = new Thickness(4, 2);
            TextArea.TextView.Options.EnableHyperlinks = false;
            TextArea.TextView.Options.EnableEmailHyperlinks = false;
            TextArea.TextView.Options.AllowScrollBelowDocument = false;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var w = Bounds.Width;
            var pen = new Pen(SubjectLineBrush) { DashStyle = DashStyle.Dash };

            if (SubjectLength == 0 || CommitMessage.Trim().Length == 0)
            {
                var placeholder = new FormattedText(
                    App.Text("CommitMessageTextBox.Placeholder"),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(FontFamily),
                    FontSize,
                    Brushes.Gray);

                context.DrawText(placeholder, new Point(4, 2));

                var y = 6 + placeholder.Height;
                context.DrawLine(pen, new Point(0, y), new Point(w, y));
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

            var lastSubjectLine = lines[0];
            if (lastSubjectLine.StartOffset > SubjectLength)
                return;

            for (var i = 1; i < lines.Count; i++)
            {
                if (lines[i].StartOffset > SubjectLength)
                    break;

                lastSubjectLine = lines[i];
            }

            var endY = lastSubjectLine.GetTextLineVisualYPosition(lastSubjectLine.TextLines[^1], VisualYPosition.LineBottom) - view.VerticalOffset + 4;
            context.DrawLine(pen, new Point(0, endY), new Point(w, endY));
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            TextArea.TextView.VisualLinesChanged += OnTextViewVisualLinesChanged;
            TextArea.TextView.ContextRequested += OnTextViewContextRequested;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            TextArea.TextView.ContextRequested -= OnTextViewContextRequested;
            TextArea.TextView.VisualLinesChanged -= OnTextViewVisualLinesChanged;

            base.OnUnloaded(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == CommitMessageProperty)
            {
                if (!_isEditing)
                    Text = CommitMessage;

                var chars = CommitMessage.ToCharArray();
                var lastLinebreakIndex = 0;
                var lastLinebreakCount = 0;
                var foundSubjectEnd = false;
                for (var i = 0; i < chars.Length; i++)
                {
                    var ch = chars[i];
                    if (ch == '\r')
                        continue;

                    if (ch == '\n')
                    {
                        if (lastLinebreakCount > 0)
                        {
                            SetCurrentValue(SubjectLengthProperty, lastLinebreakIndex);
                            foundSubjectEnd = true;
                            break;
                        }
                        else
                        {
                            lastLinebreakIndex = i;
                            lastLinebreakCount = 1;
                        }
                    }
                    else
                    {
                        lastLinebreakCount = 0;
                    }
                }

                if (!foundSubjectEnd)
                    SetCurrentValue(SubjectLengthProperty, CommitMessage?.Length ?? 0);
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);

            _isEditing = true;
            SetCurrentValue(CommitMessageProperty, Text);
            _isEditing = false;
        }

        private void OnTextViewContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var selection = TextArea.Selection;
            var hasSelected = selection is { IsEmpty: false };

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = App.CreateMenuIcon("Icons.Copy");
            copy.IsEnabled = hasSelected;
            copy.Click += (o, ev) =>
            {
                Copy();
                ev.Handled = true;
            };

            var cut = new MenuItem();
            cut.Header = App.Text("Cut");
            cut.Icon = App.CreateMenuIcon("Icons.Cut");
            cut.IsEnabled = hasSelected;
            cut.Click += (o, ev) =>
            {
                Cut();
                ev.Handled = true;
            };

            var paste = new MenuItem();
            paste.Header = App.Text("Paste");
            paste.Icon = App.CreateMenuIcon("Icons.Paste");
            paste.Click += (o, ev) =>
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

        private bool _isEditing = false;
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
                        var template = repo.Settings.CommitTemplates[i];
                        var item = new MenuItem();
                        item.Header = App.Text("WorkingCopy.UseCommitTemplate", template.Name);
                        item.Icon = App.CreateMenuIcon("Icons.Code");
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

                        var gitTemplateItem = new MenuItem();
                        gitTemplateItem.Header = App.Text("WorkingCopy.UseCommitTemplate", friendlyName);
                        gitTemplateItem.Icon = App.CreateMenuIcon("Icons.Code");
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

                        var item = new MenuItem();
                        item.Header = header;
                        item.Icon = App.CreateMenuIcon("Icons.Histories");
                        item.Click += (_, ev) =>
                        {
                            vm.CommitMessage = dup;
                            ev.Handled = true;
                        };

                        menu.Items.Add(item);
                    }

                    menu.Items.Add(new MenuItem() { Header = "-" });

                    var clearHistoryItem = new MenuItem();
                    clearHistoryItem.Header = App.Text("WorkingCopy.ClearCommitHistories");
                    clearHistoryItem.Icon = App.CreateMenuIcon("Icons.Clear");
                    clearHistoryItem.Click += async (_, ev) =>
                    {
                        await vm.ClearCommitMessageHistoryAsync();
                        ev.Handled = true;
                    };

                    menu.Items.Add(clearHistoryItem);
                }

                menu.Placement = PlacementMode.TopEdgeAlignedLeft;
                menu.Open(button);
            }

            e.Handled = true;
        }

        private async void OnOpenOpenAIHelper(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm && sender is Control control && ShowAdvancedOptions)
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
                    await App.ShowDialog(new ViewModels.AIAssistant(repo, services[0], vm.Staged, t => vm.CommitMessage = t));
                    return;
                }

                var menu = new ContextMenu() { Placement = PlacementMode.TopEdgeAlignedLeft };
                foreach (var service in services)
                {
                    var dup = service;
                    var item = new MenuItem();
                    item.Header = service.Name;
                    item.Click += async (_, ev) =>
                    {
                        await App.ShowDialog(new ViewModels.AIAssistant(repo, dup, vm.Staged, t => vm.CommitMessage = t));
                        ev.Handled = true;
                    };

                    menu.Items.Add(item);
                }
                menu.Open(control);
            }

            e.Handled = true;
        }

        private async void OnOpenConventionalCommitHelper(object _, RoutedEventArgs e)
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
            await builder.ShowDialog(owner);

            e.Handled = true;
        }
    }
}
