using System;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class EnhancedTextBox : TextBox
    {
        public static readonly RoutedEvent<KeyEventArgs> PreviewKeyDownEvent =
            RoutedEvent.Register<EnhancedTextBox, KeyEventArgs>(nameof(KeyEventArgs), RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        public event EventHandler<KeyEventArgs> PreviewKeyDown
        {
            add { AddHandler(PreviewKeyDownEvent, value); }
            remove { RemoveHandler(PreviewKeyDownEvent, value); }
        }

        protected override Type StyleKeyOverride => typeof(TextBox);

        public void Paste(string text)
        {
            OnTextInput(new TextInputEventArgs() { Text = text });
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            var dump = new KeyEventArgs()
            {
                RoutedEvent = PreviewKeyDownEvent,
                Route = RoutingStrategies.Direct,
                Source = e.Source,
                Key = e.Key,
                KeyModifiers = e.KeyModifiers,
                PhysicalKey = e.PhysicalKey,
                KeySymbol = e.KeySymbol,
            };

            RaiseEvent(dump);

            if (dump.Handled)
                e.Handled = true;
            else
                base.OnKeyDown(e);
        }
    }

    public partial class CommitMessageTextBox : UserControl
    {
        public enum TextChangeWay
        {
            None,
            FromSource,
            FromEditor,
        }

        public static readonly StyledProperty<bool> ShowAdvancedOptionsProperty =
            AvaloniaProperty.Register<CommitMessageTextBox, bool>(nameof(ShowAdvancedOptions));

        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<CommitMessageTextBox, string>(nameof(Text), string.Empty);

        public static readonly StyledProperty<string> SubjectProperty =
            AvaloniaProperty.Register<CommitMessageTextBox, string>(nameof(Subject), string.Empty);

        public static readonly StyledProperty<string> DescriptionProperty =
            AvaloniaProperty.Register<CommitMessageTextBox, string>(nameof(Description), string.Empty);

        public bool ShowAdvancedOptions
        {
            get => GetValue(ShowAdvancedOptionsProperty);
            set => SetValue(ShowAdvancedOptionsProperty, value);
        }

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Subject
        {
            get => GetValue(SubjectProperty);
            set => SetValue(SubjectProperty, value);
        }

        public string Description
        {
            get => GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public CommitMessageTextBox()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextProperty && _changingWay == TextChangeWay.None)
            {
                _changingWay = TextChangeWay.FromSource;
                var normalized = Text.ReplaceLineEndings("\n");
                var parts = normalized.Split("\n\n", 2);
                if (parts.Length != 2)
                    parts = [normalized, string.Empty];
                SetCurrentValue(SubjectProperty, parts[0].ReplaceLineEndings(" "));
                SetCurrentValue(DescriptionProperty, parts[1]);
                _changingWay = TextChangeWay.None;
            }
            else if ((change.Property == SubjectProperty || change.Property == DescriptionProperty) && _changingWay == TextChangeWay.None)
            {
                _changingWay = TextChangeWay.FromEditor;
                SetCurrentValue(TextProperty, $"{Subject}\n\n{Description}");
                _changingWay = TextChangeWay.None;
            }
        }

        private async void OnSubjectTextBoxPreviewKeyDown(object _, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || (e.Key == Key.Right && SubjectEditor.CaretIndex == Subject.Length))
            {
                DescriptionEditor.Focus();
                DescriptionEditor.CaretIndex = 0;
                e.Handled = true;
            }
            else if (e.Key == Key.V && e.KeyModifiers == (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
            {
                e.Handled = true;

                var text = await App.GetClipboardTextAsync();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    text = text.Trim();

                    if (SubjectEditor.CaretIndex == Subject.Length)
                    {
                        var parts = text.Split('\n', 2);
                        if (parts.Length != 2)
                        {
                            SubjectEditor.Paste(text);
                        }
                        else
                        {
                            SubjectEditor.Paste(parts[0]);
                            DescriptionEditor.Focus();
                            DescriptionEditor.CaretIndex = 0;
                            DescriptionEditor.Paste(parts[1].Trim());
                        }
                    }
                    else
                    {
                        SubjectEditor.Paste(text.ReplaceLineEndings(" "));
                    }
                }
            }
        }

        private void OnDescriptionTextBoxPreviewKeyDown(object _, KeyEventArgs e)
        {
            if ((e.Key == Key.Back || e.Key == Key.Left) && DescriptionEditor.CaretIndex == 0)
            {
                SubjectEditor.Focus();
                SubjectEditor.CaretIndex = Subject.Length;
                e.Handled = true;
            }
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

            var vm = new ViewModels.ConventionalCommitMessageBuilder(conventionalTypesOverride, text => Text = text);
            var builder = new ConventionalCommitMessageBuilder() { DataContext = vm };
            await builder.ShowDialog(owner);

            e.Handled = true;
        }

        private async void CopyAllText(object sender, RoutedEventArgs e)
        {
            await App.CopyTextAsync(Text);
            e.Handled = true;
        }

        private async void PasteAndReplaceAllText(object sender, RoutedEventArgs e)
        {
            try
            {
                var text = await App.GetClipboardTextAsync();
                if (!string.IsNullOrEmpty(text))
                {
                    var parts = text.ReplaceLineEndings("\n").Split("\n", 2);
                    var subject = parts[0];
                    Text = parts.Length > 1 ? $"{subject}\n\n{parts[1].Trim()}" : subject;
                }
            }
            catch
            {
                // Ignore exceptions.
            }

            e.Handled = true;
        }

        private TextChangeWay _changingWay = TextChangeWay.None;
    }
}
