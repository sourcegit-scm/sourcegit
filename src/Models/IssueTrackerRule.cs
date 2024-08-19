using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Controls.Documents;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    public class IssueTrackerMatch
    {
        public int Start { get; set; } = 0;
        public int Length { get; set; } = 0;
        public string URL { get; set; } = "";
        public Run Link { get; set; } = null;

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
            for (var i = 0; i < matches.Count; i++)
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
                for (var j = 1; j < match.Groups.Count; j++)
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
}
