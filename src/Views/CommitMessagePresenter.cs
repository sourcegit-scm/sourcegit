using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;

namespace SourceGit.Views
{
    public class CommitMessagePresenter : SelectableTextBlock
    {
        public static readonly StyledProperty<string> MessageProperty =
            AvaloniaProperty.Register<CommitMessagePresenter, string>(nameof(Message));

        public string Message
        {
            get => GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public static readonly StyledProperty<AvaloniaList<Models.IssueTrackerRule>> IssueTrackerRulesProperty =
            AvaloniaProperty.Register<CommitMessagePresenter, AvaloniaList<Models.IssueTrackerRule>>(nameof(IssueTrackerRules));

        public AvaloniaList<Models.IssueTrackerRule> IssueTrackerRules
        {
            get => GetValue(IssueTrackerRulesProperty);
            set => SetValue(IssueTrackerRulesProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == MessageProperty || change.Property == IssueTrackerRulesProperty)
            {
                Inlines.Clear();

                var message = Message;
                if (string.IsNullOrEmpty(message))
                    return;

                var rules = IssueTrackerRules;
                if (rules == null || rules.Count == 0)
                {
                    Inlines.Add(new Run(message));
                    return;
                }

                var matches = new List<Models.IssueTrackerMatch>();
                foreach (var rule in rules)
                    rule.Matches(matches, message);

                if (matches.Count == 0)
                {
                    Inlines.Add(new Run(message));
                    return;
                }

                matches.Sort((l, r) => l.Start - r.Start);

                int pos = 0;
                foreach (var match in matches)
                {
                    if (match.Start > pos)
                        Inlines.Add(new Run(message.Substring(pos, match.Start - pos)));

                    var link = new TextBlock();
                    link.SetValue(TextProperty, message.Substring(match.Start, match.Length));
                    link.SetValue(ToolTip.TipProperty, match.URL);
                    link.Classes.Add("issue_link");
                    link.PointerPressed += OnLinkPointerPressed;
                    Inlines.Add(link);

                    pos = match.Start + match.Length;
                }

                if (pos < message.Length)
                    Inlines.Add(new Run(message.Substring(pos)));
            }
        }

        private void OnLinkPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is TextBlock text)
            {
                var tooltip = text.GetValue(ToolTip.TipProperty) as string;
                if (!string.IsNullOrEmpty(tooltip))
                    Native.OS.OpenBrowser(tooltip);

                e.Handled = true;
            }
        }
    }
}
