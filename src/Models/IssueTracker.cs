using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    public class IssueTracker : ObservableObject
    {
        public bool IsShared
        {
            get => _isShared;
            set => SetProperty(ref _isShared, value);
        }

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
                        _regex = new Regex(_regexString, RegexOptions.Multiline);
                    }
                    catch
                    {
                        _regex = null;
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

        public void Matches(InlineElementCollector outs, string message)
        {
            if (_regex == null || string.IsNullOrEmpty(_urlTemplate))
                return;

            var matches = _regex.Matches(message);
            foreach (Match match in matches)
            {
                var start = match.Index;
                var len = match.Length;
                if (outs.Intersect(start, len) != null)
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

        private bool _isShared;
        private string _name;
        private string _regexString;
        private string _urlTemplate;
        private Regex _regex = null;
    }
}
