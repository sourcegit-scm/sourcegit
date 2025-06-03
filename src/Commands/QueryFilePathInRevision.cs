using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands
{
    public partial class QueryFilePathInRevision : Command
    {
        [GeneratedRegex(@"^R[0-9]{0,4}\s+(.+)\s+(.+)$")]
        private static partial Regex REG_RENAME_FORMAT();

        public QueryFilePathInRevision(string repo, string revision, string currentPath)
        {
            WorkingDirectory = repo;
            Context = repo;
            _revision = revision;
            _currentPath = currentPath;
        }

        public string Result()
        {
            if (CheckPathExistsInRevision(_currentPath))
                return _currentPath;

            string mappedPath = FindRenameHistory();
            return mappedPath ?? _currentPath;
        }

        private bool CheckPathExistsInRevision(string path)
        {
            Args = $"ls-tree -r {_revision} -- \"{path}\"";
            var rs = ReadToEnd();
            return rs.IsSuccess && !string.IsNullOrEmpty(rs.StdOut);
        }

        private string FindRenameHistory()
        {
            var fileHistory = BuildFileHistory();
            if (fileHistory == null || fileHistory.Count == 0)
                return null;

            foreach (var entry in fileHistory)
            {
                if (!IsTargetRevisionBefore(entry.CommitSHA))
                    continue;

                if (CheckPathExistsInRevision(entry.OldPath))
                    return entry.OldPath;
            }

            if (fileHistory.Count > 0)
            {
                var oldestPath = fileHistory[^1].OldPath;
                if (CheckPathExistsInRevision(oldestPath))
                    return oldestPath;
            }

            return null;
        }

        private bool IsTargetRevisionBefore(string commitSHA)
        {
            Args = $"merge-base --is-ancestor {_revision} {commitSHA}";
            var rs = ReadToEnd();
            return rs.IsSuccess;
        }

        private List<RenameHistoryEntry> BuildFileHistory()
        {
            Args = $"log --follow --name-status --pretty=format:\"commit %H\" -M -- \"{_currentPath}\"";
            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                return null;

            var result = new List<RenameHistoryEntry>();
            var lines = rs.StdOut.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            string currentCommit = null;
            string currentPath = _currentPath;

            foreach (var t in lines)
            {
                var line = t.Trim();

                if (line.StartsWith("commit ", StringComparison.Ordinal))
                {
                    currentCommit = line.Substring("commit ".Length);
                    continue;
                }

                var match = REG_RENAME_FORMAT().Match(line);
                if (match.Success && currentCommit != null)
                {
                    var oldPath = match.Groups[1].Value;
                    var newPath = match.Groups[2].Value;

                    if (newPath == currentPath)
                    {
                        result.Add(new RenameHistoryEntry
                        {
                            CommitSHA = currentCommit,
                            OldPath = oldPath,
                            NewPath = newPath
                        });

                        currentPath = oldPath;
                    }
                }
            }

            return result;
        }

        private class RenameHistoryEntry
        {
            public string CommitSHA { get; set; }
            public string OldPath { get; set; }
            public string NewPath { get; set; }
        }

        private readonly string _revision;
        private readonly string _currentPath;
    }
}
