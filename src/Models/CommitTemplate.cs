using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    public partial class CommitTemplate : ObservableObject
    {
        [GeneratedRegex(@"\$\{files(\:\d+)?\}")]
        private static partial Regex REG_COMMIT_TEMPLATE_FILES();

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        public string Apply(List<Change> changes)
        {
            var content = _content.Replace("${files_num}", $"{changes.Count}");
            var matches = REG_COMMIT_TEMPLATE_FILES().Matches(content);
            if (matches.Count == 0)
                return content;

            var builder = new StringBuilder();
            var last = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (!match.Success)
                    continue;

                var start = match.Index;
                if (start != last)
                    builder.Append(content.Substring(last, start - last));

                var countStr = match.Groups[1].Value;
                var paths = new List<string>();
                var more = string.Empty;
                if (countStr is { Length: <= 1 })
                {
                    foreach (var c in changes)
                        paths.Add(c.Path);
                }
                else
                {
                    var count = Math.Min(int.Parse(countStr.Substring(1)), changes.Count);
                    for (int j = 0; j < count; j++)
                        paths.Add(changes[i].Path);

                    if (count < changes.Count)
                        more = $" and {changes.Count - count} other files";
                }

                builder.Append(string.Join(", ", paths));
                if (!string.IsNullOrEmpty(more))
                    builder.Append(more);

                last = start + match.Length;
            }

            if (last != content.Length - 1)
                builder.Append(content.Substring(last));

            return builder.ToString();
        }

        private string _name = string.Empty;
        private string _content = string.Empty;
    }
}
