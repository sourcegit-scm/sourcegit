using System;
using System.Collections.Generic;
using System.Linq;

namespace SourceGit.Models
{
    public enum SubmoduleHistoryScope
    {
        DirectOnly,
        Recursive,
    }

    public enum SubmoduleHistoryMode
    {
        ChangedRange,
        FullHistory,
    }

    public enum SubmoduleHistoryLabelFormat
    {
        PathAndBranch,
        RepositoryPathAndBranch,
        PathOnly,
    }

    public class SubmoduleHistory
    {
        public string OwnerSHA { get; init; }
        public string Path { get; init; }
        public string LocalPath { get; init; }
        public SubmoduleHistory ParentHistory { get; init; }
        public string OwnerKey => ParentHistory?.GraphKey(OwnerSHA) ?? OwnerSHA;
        public string RepositoryPath { get; set; }
        public string NewSHA { get; init; }
        public string RepositoryName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public Dictionary<string, string> Previous { get; } = new();
        public List<Commit> Commits { get; set; } = [];
        public string Error { get; set; } = string.Empty;
        public bool IsTruncated { get; set; }
        public bool IsExpanded { get; set; }
        public bool IsLoading { get; set; }
        public string ExpansionState { get; set; } = string.Empty;
        public Commit PreviewCommit { get; set; }
        public Commit HeaderCommit => Commits.FirstOrDefault() ?? PreviewCommit;
        public string HeaderKey => HeaderCommit == null ? Key + ":unavailable" : GraphKey(HeaderCommit.SHA);
        public string ToggleGlyph => IsExpanded ? "▾" : "▸";
        public string ToggleDescription => $"{App.Text(IsExpanded ? "SubmoduleHistory.Collapse" : "SubmoduleHistory.Expand")} {Path}";
        public string UnavailableLabel => $"{(string.IsNullOrEmpty(RepositoryName) ? System.IO.Path.GetFileName(Path) : RepositoryName)} · {Path}" +
            (string.IsNullOrEmpty(BranchName) ? string.Empty : $" · {BranchName}");
        public string Key => OwnerKey + ":" + Path;
        public string GraphKey(string sha) => Key + ":" + sha;

        public string GetLabel(SubmoduleHistoryLabelFormat format) => format switch
        {
            SubmoduleHistoryLabelFormat.RepositoryPathAndBranch => $"[{RepositoryName}:{Path}:{BranchName}]",
            SubmoduleHistoryLabelFormat.PathOnly => $"[{Path}]",
            _ => $"[{Path}:{BranchName}]",
        };

        public static string GetRepositoryName(string remote, string fallback)
        {
            // Never expose URL userinfo, query parameters or fragments in a label.
            var path = Uri.TryCreate(remote, UriKind.Absolute, out var uri)
                ? uri.AbsolutePath : remote.Split('?', '#')[0];
            path = path.Replace('\\', '/').TrimEnd('/');
            var start = Math.Max(path.LastIndexOf('/'), path.LastIndexOf(':')) + 1;
            var name = path[start..];
            if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }

        // Build display-only edges. Never change the commits returned by Git.
        public static List<Commit> Compose(List<Commit> roots, IReadOnlyList<SubmoduleHistory> expanded, SubmoduleHistoryLabelFormat labelFormat = SubmoduleHistoryLabelFormat.PathAndBranch, IReadOnlyList<SubmoduleHistory> known = null)
        {
            var result = new List<Commit>();
            var sectionsByOwner = (known ?? expanded).ToLookup(s => s.OwnerKey);
            var expandedKeys = expanded.Select(s => s.Key).ToHashSet();
            Append(roots, null, new());

            void Append(List<Commit> commits, SubmoduleHistory context, Dictionary<string, string> returns, bool showNested = true)
            {
                var positions = commits.Select((c, i) => (c.SHA, i)).ToDictionary(x => x.SHA, x => x.i);
                string Key(string sha) => context?.GraphKey(sha) ?? sha;
                foreach (var commit in commits)
                {
                    var row = Copy(commit);
                    row.SubmoduleHistory = context;
                    row.IsSubmoduleHeader = context != null && commit == commits[0];
                    row.GraphParents = context == null ? new(commit.Parents) :
                        commit.Parents.Where(positions.ContainsKey).Select(Key).ToList();
                    if (context != null)
                    {
                        row.Subject = $"{context.GetLabel(labelFormat)} {commit.Subject}";
                        row.Decorators = [];
                        row.IsMerged = true;
                    }
                    if (returns.TryGetValue(commit.SHA, out var target))
                        row.GraphParents.Add(target);
                    result.Add(row);

                    if (!showNested)
                        continue;
                    foreach (var section in sectionsByOwner[row.GraphKey])
                    {
                        if (section.HeaderCommit == null)
                        {
                            // Only an unavailable/loading repository needs a non-commit placeholder.
                            result.Add(new Commit
                            {
                                SHA = section.OwnerSHA,
                                SubmoduleHistory = section,
                                IsSubmoduleHeader = true,
                                IsSubmodulePlaceholder = true,
                                Subject = section.UnavailableLabel,
                                GraphParents = [],
                                IsMerged = true,
                            });
                            continue;
                        }
                        if (!expandedKeys.Contains(section.Key) || section.Commits.Count == 0)
                        {
                            row.GraphParents.Add(section.HeaderKey);
                            Append([section.HeaderCommit], section, new(), false);
                            continue;
                        }
                        var visible = section.Commits.Select(c => c.SHA).ToHashSet();
                        if (visible.Contains(section.NewSHA))
                            row.GraphParents.Add(section.GraphKey(section.NewSHA));
                        // Rewinds and divergent updates must also expose previous pointers.
                        foreach (var old in section.Previous.Keys)
                        {
                            if (visible.Contains(old) && old != section.NewSHA)
                                row.GraphParents.Add(section.GraphKey(old));
                        }

                        var boundaries = new Dictionary<string, string>();
                        foreach (var (old, previousChange) in section.Previous)
                        {
                            if (positions.TryGetValue(previousChange, out var position) && position > positions[commit.SHA])
                                boundaries[old] = Key(previousChange);
                        }
                        Append(section.Commits, section, boundaries);
                    }
                }
            }
            return result;
        }

        private static Commit Copy(Commit c) => new()
        {
            SHA = c.SHA,
            Parents = new(c.Parents),
            Subject = c.Subject,
            Author = c.Author,
            AuthorTime = c.AuthorTime,
            Committer = c.Committer,
            CommitterTime = c.CommitterTime,
            Decorators = new(c.Decorators),
            IsMerged = c.IsMerged,
        };
    }
}
