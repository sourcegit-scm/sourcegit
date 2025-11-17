using System;
using System.Collections.Generic;
using System.Text;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    public enum FilterType
    {
        LocalBranch = 0,
        LocalBranchFolder,
        RemoteBranch,
        RemoteBranchFolder,
        Tag,
    }

    public enum FilterMode
    {
        None = 0,
        Included,
        Excluded,
    }

    public class HistoryFilter : ObservableObject
    {
        public string Pattern
        {
            get => _pattern;
            set => SetProperty(ref _pattern, value);
        }

        public FilterType Type
        {
            get;
            set;
        } = FilterType.LocalBranch;

        public FilterMode Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }

        public bool IsBranch
        {
            get => Type != FilterType.Tag;
        }

        public HistoryFilter()
        {
        }

        public HistoryFilter(string pattern, FilterType type, FilterMode mode)
        {
            _pattern = pattern;
            _mode = mode;
            Type = type;
        }

        private string _pattern = string.Empty;
        private FilterMode _mode = FilterMode.None;
    }

    public class HistoryFilterCollection
    {
        public AvaloniaList<HistoryFilter> Filters
        {
            get;
            set;
        } = [];

        public FilterMode Mode => Filters.Count > 0 ? Filters[0].Mode : FilterMode.None;

        public Dictionary<string, FilterMode> ToMap()
        {
            var map = new Dictionary<string, FilterMode>();
            foreach (var filter in Filters)
                map.Add(filter.Pattern, filter.Mode);
            return map;
        }

        public bool Update(string pattern, FilterType type, FilterMode mode)
        {
            // Clear all filters when there's a filter that has different mode.
            if (mode != FilterMode.None)
            {
                var clear = false;
                foreach (var filter in Filters)
                {
                    if (filter.Mode != mode)
                    {
                        clear = true;
                        break;
                    }
                }

                if (clear)
                {
                    Filters.Clear();
                    Filters.Add(new HistoryFilter(pattern, type, mode));
                    return true;
                }
            }
            else
            {
                for (int i = 0; i < Filters.Count; i++)
                {
                    var filter = Filters[i];
                    if (filter.Type == type && filter.Pattern.Equals(pattern, StringComparison.Ordinal))
                    {
                        Filters.RemoveAt(i);
                        return true;
                    }
                }

                return false;
            }

            foreach (var filter in Filters)
            {
                if (filter.Type != type)
                    continue;

                if (filter.Pattern.Equals(pattern, StringComparison.Ordinal))
                    return false;
            }

            Filters.Add(new HistoryFilter(pattern, type, mode));
            return true;
        }

        public FilterMode GetFilterMode(string pattern)
        {
            foreach (var filter in Filters)
            {
                if (filter.Pattern.Equals(pattern, StringComparison.Ordinal))
                    return filter.Mode;
            }

            return FilterMode.None;
        }

        public void RemoveFilter(string pattern, FilterType type)
        {
            foreach (var filter in Filters)
            {
                if (filter.Type == type && filter.Pattern.Equals(pattern, StringComparison.Ordinal))
                {
                    Filters.Remove(filter);
                    break;
                }
            }
        }

        public void RemoveBranchFiltersByPrefix(string pattern)
        {
            var dirty = new List<HistoryFilter>();
            var prefix = $"{pattern}/";

            foreach (var filter in Filters)
            {
                if (filter.Type != FilterType.Tag)
                    continue;

                if (filter.Pattern.StartsWith(prefix, StringComparison.Ordinal))
                    dirty.Add(filter);
            }

            foreach (var filter in dirty)
                Filters.Remove(filter);
        }

        public string Build()
        {
            var includedRefs = new List<string>();
            var excludedBranches = new List<string>();
            var excludedRemotes = new List<string>();
            var excludedTags = new List<string>();
            foreach (var filter in Filters)
            {
                if (filter.Type == FilterType.LocalBranch)
                {
                    if (filter.Mode == FilterMode.Included)
                        includedRefs.Add(filter.Pattern);
                    else if (filter.Mode == FilterMode.Excluded)
                        excludedBranches.Add($"--exclude=\"{filter.Pattern.AsSpan(11)}\" --decorate-refs-exclude=\"{filter.Pattern}\"");
                }
                else if (filter.Type == FilterType.LocalBranchFolder)
                {
                    if (filter.Mode == FilterMode.Included)
                        includedRefs.Add($"--branches={filter.Pattern.AsSpan(11)}/*");
                    else if (filter.Mode == FilterMode.Excluded)
                        excludedBranches.Add($"--exclude=\"{filter.Pattern.AsSpan(11)}/*\" --decorate-refs-exclude=\"{filter.Pattern}/*\"");
                }
                else if (filter.Type == FilterType.RemoteBranch)
                {
                    if (filter.Mode == FilterMode.Included)
                        includedRefs.Add(filter.Pattern);
                    else if (filter.Mode == FilterMode.Excluded)
                        excludedRemotes.Add($"--exclude=\"{filter.Pattern.AsSpan(13)}\" --decorate-refs-exclude=\"{filter.Pattern}\"");
                }
                else if (filter.Type == FilterType.RemoteBranchFolder)
                {
                    if (filter.Mode == FilterMode.Included)
                        includedRefs.Add($"--remotes={filter.Pattern.AsSpan(13)}/*");
                    else if (filter.Mode == FilterMode.Excluded)
                        excludedRemotes.Add($"--exclude=\"{filter.Pattern.AsSpan(13)}/*\" --decorate-refs-exclude=\"{filter.Pattern}/*\"");
                }
                else if (filter.Type == FilterType.Tag)
                {
                    if (filter.Mode == FilterMode.Included)
                        includedRefs.Add($"refs/tags/{filter.Pattern}");
                    else if (filter.Mode == FilterMode.Excluded)
                        excludedTags.Add($"--exclude=\"{filter.Pattern}\" --decorate-refs-exclude=\"refs/tags/{filter.Pattern}\"");
                }
            }

            var builder = new StringBuilder();
            if (includedRefs.Count > 0)
            {
                foreach (var r in includedRefs)
                {
                    builder.Append(r);
                    builder.Append(' ');
                }
            }
            else if (excludedBranches.Count + excludedRemotes.Count + excludedTags.Count > 0)
            {
                foreach (var b in excludedBranches)
                {
                    builder.Append(b);
                    builder.Append(' ');
                }

                builder.Append("--exclude=HEAD --branches ");

                foreach (var r in excludedRemotes)
                {
                    builder.Append(r);
                    builder.Append(' ');
                }

                builder.Append("--exclude=origin/HEAD --remotes ");

                foreach (var t in excludedTags)
                {
                    builder.Append(t);
                    builder.Append(' ');
                }

                builder.Append("--tags ");
            }

            return builder.ToString();
        }
    }
}
