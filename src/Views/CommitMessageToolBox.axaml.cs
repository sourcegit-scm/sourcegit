using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace SourceGit.Views
{
    public record CommitMessageTextBoxSuggestion(TextBox Target, string Text, int ReplaceStart, int ReplaceLen)
    {
        public void Use()
        {
            var text = Target.Text ?? string.Empty;
            if (ReplaceStart + ReplaceLen > text.Length)
                return;

            var builder = new StringBuilder();
            builder
                .Append(text.Substring(0, ReplaceStart))
                .Append(Text)
                .Append(text.Substring(ReplaceStart + ReplaceLen));

            Target.Text = builder.ToString();
            Target.CaretIndex = ReplaceStart + Text.Length;
        }
    }

    public class CommitMessageTextBox : TextBox
    {
        public static readonly DirectProperty<CommitMessageTextBox, int> ColumnProperty =
            AvaloniaProperty.RegisterDirect<CommitMessageTextBox, int>(
                nameof(Column),
                static o => o.Column);

        public int Column
        {
            get => _column;
            set => SetAndRaise(ColumnProperty, ref _column, value);
        }

        public static readonly DirectProperty<CommitMessageTextBox, int> SubjectLengthProperty =
            AvaloniaProperty.RegisterDirect<CommitMessageTextBox, int>(
                nameof(SubjectLength),
                static o => o.SubjectLength);

        public int SubjectLength
        {
            get => _subjectLen;
            set => SetAndRaise(SubjectLengthProperty, ref _subjectLen, value);
        }

        public static readonly DirectProperty<CommitMessageTextBox, int> SubjectGuideLengthProperty =
            AvaloniaProperty.RegisterDirect<CommitMessageTextBox, int>(
                nameof(SubjectGuideLength),
                static o => o.SubjectGuideLength,
                static (o, v) => o.SubjectGuideLength = v);

        public int SubjectGuideLength
        {
            get => _subjectGuideLen;
            set => SetAndRaise(SubjectGuideLengthProperty, ref _subjectGuideLen, value);
        }

        public static readonly DirectProperty<CommitMessageTextBox, double> SubjectEndYProperty =
            AvaloniaProperty.RegisterDirect<CommitMessageTextBox, double>(
                nameof(SubjectEndY),
                static o => o.SubjectEndY);

        public double SubjectEndY
        {
            get => _subjectEndY;
            set => SetAndRaise(SubjectEndYProperty, ref _subjectEndY, value);
        }

        public static readonly DirectProperty<CommitMessageTextBox, bool> WarnSubjectLengthProperty =
            AvaloniaProperty.RegisterDirect<CommitMessageTextBox, bool>(
                nameof(WarnSubjectLength),
                static o => o.WarnSubjectLength);

        public bool WarnSubjectLength
        {
            get => _warnSubjectLen;
            set => SetAndRaise(WarnSubjectLengthProperty, ref _warnSubjectLen, value);
        }

        public static readonly DirectProperty<CommitMessageTextBox, List<CommitMessageTextBoxSuggestion>> SuggestionsProperty =
            AvaloniaProperty.RegisterDirect<CommitMessageTextBox, List<CommitMessageTextBoxSuggestion>>(
                nameof(Suggestions),
                static o => o.Suggestions);

        public List<CommitMessageTextBoxSuggestion> Suggestions
        {
            get => _suggestions;
            set => SetAndRaise(SuggestionsProperty, ref _suggestions, value);
        }

        public static readonly DirectProperty<CommitMessageTextBox, int> SelectedSuggestionIndexProperty =
            AvaloniaProperty.RegisterDirect<CommitMessageTextBox, int>(
                nameof(SelectedSuggestionIndex),
                static o => o.SelectedSuggestionIndex,
                static (o, v) => o.SelectedSuggestionIndex = v);

        public int SelectedSuggestionIndex
        {
            get => _selectedSuggestionIdx;
            set => SetAndRaise(SelectedSuggestionIndexProperty, ref _selectedSuggestionIdx, value);
        }

        public static readonly DirectProperty<CommitMessageTextBox, double> SuggestionPopupYProperty =
            AvaloniaProperty.RegisterDirect<CommitMessageTextBox, double>(
                nameof(SuggestionPopupY),
                static o => o.SuggestionPopupY);

        public double SuggestionPopupY
        {
            get => _suggestionPopupY;
            set => SetAndRaise(SuggestionPopupYProperty, ref _suggestionPopupY, value);
        }

        protected override Type StyleKeyOverride => typeof(TextBox);

        public CommitMessageTextBox()
        {
            AcceptsReturn = true;
            AcceptsTab = true;
            TextWrapping = TextWrapping.Wrap;
            HorizontalContentAlignment = HorizontalAlignment.Left;
            VerticalContentAlignment = VerticalAlignment.Top;
            Padding = new Thickness(4);

            SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _textPresenter = e.NameScope.Get<TextPresenter>("PART_TextPresenter");
            _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            LayoutUpdated += OnLayoutUpdated;
            OnLayoutUpdated(null, null);
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            LayoutUpdated -= OnLayoutUpdated;
            base.OnUnloaded(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextProperty)
            {
                var text = Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    _subjectEndCharIdx = -1;
                    SubjectLength = 0;
                    return;
                }

                var subjectLen = 0;
                var lastNonLineBreakCharIdx = 0;
                var lastLineStart = 0;
                for (var i = 0; i < text.Length; i++)
                {
                    var ch = text[i];
                    if (ch == '\n')
                    {
                        var line = (i > lastLineStart) ? text.Substring(lastLineStart, i - lastLineStart) : string.Empty;
                        lastLineStart = i + 1;

                        if (string.IsNullOrWhiteSpace(line))
                        {
                            if (subjectLen > 0)
                                break;

                            continue;
                        }

                        var validCharLen = line.TrimEnd().Length;
                        if (subjectLen > 0)
                            subjectLen += (validCharLen + 1);
                        else
                            subjectLen = validCharLen;
                    }
                    else if (ch != '\r')
                    {
                        lastNonLineBreakCharIdx = i;
                    }
                }

                if (lastLineStart < lastNonLineBreakCharIdx)
                {
                    var validCharLen = text.Substring(lastLineStart).TrimEnd().Length;
                    if (subjectLen > 0)
                        subjectLen += (validCharLen + 1);
                    else
                        subjectLen = validCharLen;
                }

                SubjectLength = subjectLen;
                _subjectEndCharIdx = lastNonLineBreakCharIdx;
            }
            else if (change.Property == SubjectLengthProperty || change.Property == SubjectGuideLengthProperty)
            {
                WarnSubjectLength = _subjectLen > _subjectGuideLen;
            }
            else if (change.Property == CaretIndexProperty)
            {
                var text = Text ?? string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    _suggestionMatchStartIdx = -1;
                    Column = 0;
                    Suggestions = null;
                    return;
                }

                var caretIdx = CaretIndex;
                var startIdx = Math.Max(Math.Min(text.Length - 1, caretIdx - 1), 0);
                var hasWhitespace = false;
                for (var i = startIdx; i >= 0; i--)
                {
                    if (i == 0)
                    {
                        Column = startIdx + 2;
                        break;
                    }

                    var ch = text[i];
                    if (ch == '\n')
                    {
                        Column = startIdx - i + 1;
                        break;
                    }

                    if (!hasWhitespace)
                        hasWhitespace = char.IsWhiteSpace(ch);
                }

                var suggestionMatchStartIdx = Math.Max(caretIdx - _column + 1, 0);
                if (hasWhitespace || _column == 1 || suggestionMatchStartIdx < _subjectEndCharIdx)
                {
                    _suggestionMatchStartIdx = -1;
                    Suggestions = null;
                    return;
                }

                var editLine = text.Substring(suggestionMatchStartIdx);
                var prefixEndIdx = editLine.IndexOfAny([' ', '\t', '\r', '\n']);
                var prefix = prefixEndIdx > 0 ? editLine.Substring(0, prefixEndIdx) : editLine;
                var matches = new List<CommitMessageTextBoxSuggestion>();
                if (prefix.Length >= 2)
                {
                    foreach (var t in _trailers)
                    {
                        if (t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                            !editLine.StartsWith(t, StringComparison.Ordinal))
                            matches.Add(new(this, t, suggestionMatchStartIdx, prefix.Length));
                    }
                }

                if (matches.Count > 0)
                {
                    _suggestionMatchStartIdx = suggestionMatchStartIdx;
                    Suggestions = matches;
                    SelectedSuggestionIndex = 0;
                }
                else
                {
                    _suggestionMatchStartIdx = -1;
                    Suggestions = null;
                }
            }
            else if (change.Property == BoundsProperty)
            {
                // Sync the actual width to TextPresenter. Otherwise, `TextWrapping` will not work well without
                // a fixed width. See https://github.com/AvaloniaUI/Avalonia/issues/5819
                if (_textPresenter != null)
                    _textPresenter.Width = Bounds.Width;
            }
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            Suggestions = null;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_suggestions != null)
            {
                if (e.Key == Key.Up)
                {
                    if (_selectedSuggestionIdx > 0)
                        SelectedSuggestionIndex = _selectedSuggestionIdx - 1;
                    else
                        SelectedSuggestionIndex = _suggestions.Count - 1;

                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    if (_selectedSuggestionIdx < _suggestions.Count - 1)
                        SelectedSuggestionIndex = _selectedSuggestionIdx + 1;
                    else
                        SelectedSuggestionIndex = 0;

                    e.Handled = true;
                }
                else if (e.Key == Key.Enter || e.Key == Key.Tab)
                {
                    var selected = _suggestions[_selectedSuggestionIdx];
                    selected.Use();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    Suggestions = null;
                    e.Handled = true;
                }
            }

            if (!e.Handled)
                base.OnKeyDown(e);
        }

        private void OnLayoutUpdated(object sender, EventArgs e)
        {
            if (_subjectEndCharIdx < 0)
            {
                SubjectEndY = 0;
            }
            else
            {
                var y = _textPresenter?.TextLayout.HitTestTextPosition(_subjectEndCharIdx).Bottom ?? 0.0;
                var offset = _scrollViewer?.Offset.Y ?? 0;
                SubjectEndY = y - offset + 6;

                if (_suggestionMatchStartIdx >= 0)
                {
                    var popupY = _textPresenter?.TextLayout.HitTestTextPosition(_suggestionMatchStartIdx).Bottom ?? 0;
                    y = popupY - offset;
                    if (y < 0.05 || y > Bounds.Height - 0.05)
                    {
                        _suggestionMatchStartIdx = -1;
                        SuggestionPopupY = 0;
                        Suggestions = null;
                    }
                    else
                    {
                        SuggestionPopupY = y;
                    }
                }
            }
        }

        private readonly List<string> _trailers =
        [
            "Acked-by: ",
            "Assisted-by: ",
            "BREAKING CHANGE: ",
            "Co-authored-by: ",
            "Fixes: ",
            "Helped-by: ",
            "Issue: ",
            "Milestone: ",
            "on-behalf-of: @",
            "Reference-to: ",
            "Refs: ",
            "Reviewed-by: ",
            "See-also: ",
            "Signed-off-by: ",
        ];

        private TextPresenter _textPresenter = null;
        private ScrollViewer _scrollViewer = null;
        private int _column = 0;
        private int _subjectLen = 0;
        private int _subjectGuideLen = 0;
        private int _subjectEndCharIdx = -1;
        private double _subjectEndY = 0;
        private bool _warnSubjectLen = false;
        private int _suggestionMatchStartIdx = -1;
        private List<CommitMessageTextBoxSuggestion> _suggestions = null;
        private int _selectedSuggestionIdx = 0;
        private double _suggestionPopupY = 0;
    }

    public class CommitMessageSubjectEndIndicator : Control
    {
        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            AvaloniaProperty.Register<CommitMessageSubjectEndIndicator, FontFamily>(nameof(FontFamily));

        public FontFamily FontFamily
        {
            get => GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public static readonly StyledProperty<IBrush> LineBrushProperty =
            AvaloniaProperty.Register<CommitMessageSubjectEndIndicator, IBrush>(nameof(LineBrush), Brushes.Gray);

        public IBrush LineBrush
        {
            get => GetValue(LineBrushProperty);
            set => SetValue(LineBrushProperty, value);
        }

        public static readonly DirectProperty<CommitMessageSubjectEndIndicator, double> SubjectEndYProperty =
            AvaloniaProperty.RegisterDirect<CommitMessageSubjectEndIndicator, double>(
                nameof(SubjectEndY),
                static o => o.SubjectEndY,
                static (o, v) => o.SubjectEndY = v);

        public double SubjectEndY
        {
            get => _subjectEndY;
            set => SetAndRaise(SubjectEndYProperty, ref _subjectEndY, value);
        }

        public CommitMessageSubjectEndIndicator()
        {
            IsHitTestVisible = false;
        }

        public override void Render(DrawingContext context)
        {
            var y = SubjectEndY;
            if (y < 0.05 || y > Bounds.Height - 0.05)
                return;

            var font = FontFamily ?? FontFamily.Default;
            var pen = new Pen(LineBrush) { DashStyle = DashStyle.Dash };
            var w = Bounds.Width;

            var subjectEndTip = new FormattedText(
                "SUBJECT END",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(font, FontStyle.Italic),
                10,
                Brushes.Gray);
            context.DrawLine(pen, new Point(0, y), new Point(w, y));
            context.DrawText(subjectEndTip, new Point(w - subjectEndTip.WidthIncludingTrailingWhitespace - 18, y + 1));
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SubjectEndYProperty)
                InvalidateVisual();
        }

        private double _subjectEndY = 0;
    }

    public partial class CommitMessageToolBox : UserControl
    {
        public static readonly DirectProperty<CommitMessageToolBox, bool> ShowAdvancedOptionsProperty =
            AvaloniaProperty.RegisterDirect<CommitMessageToolBox, bool>(
                nameof(ShowAdvancedOptions),
                static o => o.ShowAdvancedOptions,
                static (o, v) => o.ShowAdvancedOptions = v);

        public bool ShowAdvancedOptions
        {
            get => _showAdvancedOptions;
            set => SetAndRaise(ShowAdvancedOptionsProperty, ref _showAdvancedOptions, value);
        }

        public static readonly DirectProperty<CommitMessageToolBox, string> CommitMessageProperty =
            AvaloniaProperty.RegisterDirect<CommitMessageToolBox, string>(
                nameof(CommitMessage),
                static o => o.CommitMessage,
                static (o, v) => o.CommitMessage = v);

        public string CommitMessage
        {
            get => _commitMessage;
            set => SetAndRaise(CommitMessageProperty, ref _commitMessage, value);
        }

        public CommitMessageToolBox()
        {
            InitializeComponent();
        }

        private void OnSuggestionTapped(object sender, TappedEventArgs e)
        {
            if (sender is Control { DataContext: CommitMessageTextBoxSuggestion suggestion })
                suggestion.Use();

            e.Handled = true;
        }

        private async void OnOpenCommitMessagePicker(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.WorkingCopy vm && _showAdvancedOptions)
            {
                var repo = vm.Repository;
                var foreground = this.FindResource("Brush.FG1") as IBrush;
                var menu = new ContextMenu() { MaxWidth = 480 };

                var gitTemplate = await new Commands.Config(repo.FullPath).GetAsync("commit.template");
                var templateCount = repo.Settings.CommitTemplates.Count;
                if (templateCount == 0 && string.IsNullOrEmpty(gitTemplate))
                {
                    menu.Items.Add(new MenuItem()
                    {
                        Header = App.Text("WorkingCopy.NoCommitTemplates"),
                        Icon = this.CreateMenuIcon("Icons.Code"),
                        IsEnabled = false
                    });
                }
                else
                {
                    for (int i = 0; i < templateCount; i++)
                    {
                        var icon = this.CreateMenuIcon("Icons.Code");
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

                        var icon = this.CreateMenuIcon("Icons.Code");
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

                var historiesCount = repo.UIStates.RecentCommitMessages.Count;
                if (historiesCount == 0)
                {
                    menu.Items.Add(new MenuItem()
                    {
                        Header = App.Text("WorkingCopy.NoCommitHistories"),
                        Icon = this.CreateMenuIcon("Icons.Histories"),
                        IsEnabled = false
                    });
                }
                else
                {
                    for (int i = 0; i < historiesCount; i++)
                    {
                        var dup = repo.UIStates.RecentCommitMessages[i].Trim();
                        var header = new TextBlock()
                        {
                            Text = dup.ReplaceLineEndings(" "),
                            VerticalAlignment = VerticalAlignment.Center,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        };

                        var icon = this.CreateMenuIcon("Icons.Histories");
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

                    var clearIcon = this.CreateMenuIcon("Icons.Clear");
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
                menu.HorizontalOffset = -2;
                menu.VerticalOffset = 1;
                menu.Closed += (_, _) => button.IsEnabled = true;
                menu.Open(button);
            }

            e.Handled = true;
        }

        private void OnOpenOpenAIHelper(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.WorkingCopy vm && sender is Button button && _showAdvancedOptions)
            {
                var repo = vm.Repository;

                if (vm.Staged == null || vm.Staged.Count == 0)
                {
                    repo.SendNotification("No files added to commit!", true);
                    e.Handled = true;
                    return;
                }

                var services = repo.GetPreferredOpenAIServices();
                if (services.Count == 0)
                {
                    repo.SendNotification("Bad configuration for OpenAI", true);
                    e.Handled = true;
                    return;
                }

                if (services.Count == 1)
                {
                    DoOpenAIAssistant(repo, services[0], vm.Staged);
                    e.Handled = true;
                    return;
                }

                var menu = new ContextMenu();
                foreach (var service in services)
                {
                    var dup = service;
                    var item = new MenuItem();
                    item.Header = service.Name;
                    item.Click += (_, ev) =>
                    {
                        DoOpenAIAssistant(repo, dup, vm.Staged);
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

        private void DoOpenAIAssistant(ViewModels.Repository repo, AI.Service service, List<Models.Change> changes)
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null)
                return;

            var assistant = new ViewModels.AIAssistant(repo, service, changes);
            var view = new AIAssistant() { DataContext = assistant };
            view.Show(owner);
        }

        private string _commitMessage = string.Empty;
        private bool _showAdvancedOptions = false;
    }
}
