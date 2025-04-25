using System.Collections.Generic;
using System.Text.RegularExpressions;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
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

        public void Matches(List<InlineElement> outs, string message)
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

                var link = _urlTemplate;
                for (var j = 1; j < match.Groups.Count; j++)
                {
                    var group = match.Groups[j];
                    if (group.Success)
                        link = link.Replace($"${j}", group.Value);
                }

                outs.Add(new InlineElement(InlineElementType.Link, start, len, link));
            }
        }

        private string _name;
        private string _regexString;
        private string _urlTemplate;
        private Regex _regex = null;
    }
}
