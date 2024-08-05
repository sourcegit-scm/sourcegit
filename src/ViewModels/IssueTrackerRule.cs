using System.Collections.Generic;
using System.Text.RegularExpressions;

using Avalonia.Collections;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class IssueTrackerMatch
    {
        public int Start { get; set; } = 0;
        public int Length { get; set; } = 0;
        public string URL { get; set; } = "";

        public bool Intersect(int start, int length)
        {
            if (start == Start)
                return true;

            if (start < Start)
                return start + length > Start;

            return start < Start + Length;
        }
    }

    public class IssueTrackerRule : ObservableObject
    {
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string RegexString
        {
            get => _regexString;
            set
            {
                if (SetProperty(ref _regexString, value))
                {
                    try
                    {
                        _regex = null;
                        _regex = new Regex(_regexString, RegexOptions.Multiline);
                    }
                    catch
                    {
                        // Ignore errors.
                    }
                }

                OnPropertyChanged(nameof(IsRegexValid));
            }
        }

        public bool IsRegexValid
        {
            get => _regex != null;
        }

        public string URLTemplate
        {
            get => _urlTemplate;
            set => SetProperty(ref _urlTemplate, value);
        }

        public void Matches(List<IssueTrackerMatch> outs, string message)
        {
            if (_regex == null || string.IsNullOrEmpty(_urlTemplate))
                return;

            var matches = _regex.Matches(message);
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (!match.Success)
                    continue;

                var start = match.Index;
                var len = match.Length;
                var intersect = false;
                foreach (var exist in outs)
                {
                    if (exist.Intersect(start, len))
                    {
                        intersect = true;
                        break;
                    }
                }

                if (intersect)
                    continue;

                var range = new IssueTrackerMatch();
                range.Start = start;
                range.Length = len;
                range.URL = _urlTemplate;
                for (int j = 1; j < match.Groups.Count; j++)
                {
                    var group = match.Groups[j];
                    if (group.Success)
                        range.URL = range.URL.Replace($"${j}", group.Value);
                }

                outs.Add(range);
            }
        }

        private string _name;
        private string _regexString;
        private string _urlTemplate;
        private Regex _regex = null;
    }

    public class IssueTrackerRuleSetting
    {
        public AvaloniaList<IssueTrackerRule> Rules
        {
            get;
            set;
        } = new AvaloniaList<IssueTrackerRule>();

        public IssueTrackerRule Add()
        {
            var rule = new IssueTrackerRule()
            {
                Name = "New Issue Tracker",
                RegexString = "#(\\d+)",
                URLTemplate = "https://xxx/$1",
            };

            Rules.Add(rule);
            return rule;
        }

        public IssueTrackerRule AddGithub(string repoURL)
        {
            var rule = new IssueTrackerRule()
            {
                Name = "Github ISSUE",
                RegexString = "#(\\d+)",
                URLTemplate = string.IsNullOrEmpty(repoURL) ? "https://github.com/username/repository/issues/$1" : $"{repoURL}/issues/$1",
            };

            Rules.Add(rule);
            return rule;
        }

        public IssueTrackerRule AddJira()
        {
            var rule = new IssueTrackerRule()
            {
                Name = "Jira Tracker",
                RegexString = "PROJ-(\\d+)",
                URLTemplate = "https://jira.yourcompany.com/browse/PROJ-$1",
            };

            Rules.Add(rule);
            return rule;
        }
    }
}
