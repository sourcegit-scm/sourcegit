using System;
using System.Collections.Generic;
using System.Linq;

namespace SourceGit.Models
{
    public enum SubmoduleHistoryMode
    {
        ChangedRange,
        FullHistory,
    }

    public class SubmoduleHistory
    {
        public string OwnerSHA { get; init; }
        public string Path { get; init; }
        public string RepositoryPath { get; set; }
        public string NewSHA { get; init; }
        public Dictionary<string, string> Previous { get; } = new();
        public List<Commit> Commits { get; set; } = [];
        public string Error { get; set; } = string.Empty;
        public bool IsTruncated { get; set; }
        public string Key => OwnerSHA + ":" + Path;
        public string GraphKey(string sha) => Key + ":" + sha;

        // Build display-only edges. Never change the commits returned by Git.
        public static List<Commit> Compose(List<Commit> roots, IReadOnlyList<SubmoduleHistory> expanded)
        {
            var result = new List<Commit>();
            var rootPositions = roots.Select((c, i) => (c.SHA, i)).ToDictionary(x => x.SHA, x => x.i);
            foreach (var root in roots)
            {
                var sections = expanded.Where(x => x.OwnerSHA == root.SHA && x.Commits.Count > 0).ToList();
                if (sections.Count == 0)
                {
                    result.Add(root);
                    continue;
                }

                var anchor = Copy(root);
                anchor.GraphParents = new(root.Parents);
                result.Add(anchor);
                foreach (var section in sections)
                {
                    var visible = section.Commits.Select(c => c.SHA).ToHashSet();
                    if (visible.Contains(section.NewSHA))
                        anchor.GraphParents.Add(section.GraphKey(section.NewSHA));
                    // A rewind/divergent update must also expose the previous pointer.
                    foreach (var old in section.Previous.Keys)
                    {
                        if (visible.Contains(old) && old != section.NewSHA)
                            anchor.GraphParents.Add(section.GraphKey(old));
                    }

                    foreach (var commit in section.Commits)
                    {
                        var row = Copy(commit);
                        row.SubmoduleHistory = section;
                        row.GraphParents = commit.Parents.Where(visible.Contains).Select(section.GraphKey).ToList();
                        row.Subject = $"[{section.Path}] {commit.Subject}";
                        row.Decorators = [];
                        row.IsMerged = true;
                        if (section.Previous.TryGetValue(commit.SHA, out var previousChange) &&
                            rootPositions.TryGetValue(previousChange, out var position) && position > rootPositions[root.SHA])
                            row.GraphParents.Add(previousChange);
                        result.Add(row);
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
