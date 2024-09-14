using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class QueryCommits : Command
    {
        public QueryCommits(string repo, IEnumerable<string> limits, bool needFindHead = true)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = [
                "log", "--date-order", "--no-show-signature", "--decorate=full",
                "--pretty=format:%H%n%P%n%D%n%aN±%aE%n%at%n%cN±%cE%n%ct%n%s",
                ..limits
            ];
            _findFirstMerged = needFindHead;
        }

        public QueryCommits(string repo, string filter, Models.CommitSearchMethod method)
        {
            var search = new List<string>();

            if (method == Models.CommitSearchMethod.ByUser)
            {
                search = ["-i", $"--author={filter}", $"--commiter={filter}"];
            }
            else if (method == Models.CommitSearchMethod.ByFile)
            {
                search = ["--", filter];
            }
            else
            {
                var words = filter.Split(new[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    search.Add($"--grep={word.Trim()}");
                }
                search.AddRange(["--all-match", "-i"]);
            }

            WorkingDirectory = repo;
            Context = repo;
            Args = [
                "log", "-1000", "--date-order", "--no-show-signature", "--decorate=full",
                "--pretty=format:%H%n%P%n%D%n%aN±%aE%n%at%n%cN±%cE%n%ct%n%s",
                "--branches", "--remotes", ..search
            ];
            _findFirstMerged = false;
        }

        public List<Models.Commit> Result()
        {
            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                return _commits;

            var nextPartIdx = 0;
            var start = 0;
            var end = rs.StdOut.IndexOf('\n', start);
            while (end > 0)
            {
                var line = rs.StdOut.Substring(start, end - start);
                switch (nextPartIdx)
                {
                    case 0:
                        _current = new Models.Commit() { SHA = line };
                        _commits.Add(_current);
                        break;
                    case 1:
                        ParseParent(line);
                        break;
                    case 2:
                        _current.ParseDecorators(line);
                        if (_current.IsMerged && !_isHeadFounded)
                            _isHeadFounded = true;
                        break;
                    case 3:
                        _current.Author = Models.User.FindOrAdd(line);
                        break;
                    case 4:
                        _current.AuthorTime = ulong.Parse(line);
                        break;
                    case 5:
                        _current.Committer = Models.User.FindOrAdd(line);
                        break;
                    case 6:
                        _current.CommitterTime = ulong.Parse(line);
                        break;
                    case 7:
                        _current.Subject = line;
                        nextPartIdx = -1;
                        break;
                }

                nextPartIdx++;

                start = end + 1;
                end = rs.StdOut.IndexOf('\n', start);
            }

            if (start < rs.StdOut.Length)
                _current.Subject = rs.StdOut.Substring(start);

            if (_findFirstMerged && !_isHeadFounded && _commits.Count > 0)
                MarkFirstMerged();

            return _commits;
        }

        private void ParseParent(string data)
        {
            if (data.Length < 8)
                return;

            var idx = data.IndexOf(' ', StringComparison.Ordinal);
            if (idx == -1)
            {
                _current.Parents.Add(data);
                return;
            }

            _current.Parents.Add(data.Substring(0, idx));
            _current.Parents.Add(data.Substring(idx + 1));
        }

        private void MarkFirstMerged()
        {
            Args = ["log", $"--since={_commits[^1].CommitterTimeStr}", "--format=%H"];

            var rs = ReadToEnd();
            var shas = rs.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (shas.Length == 0)
                return;

            var set = new HashSet<string>();
            foreach (var sha in shas)
                set.Add(sha);

            foreach (var c in _commits)
            {
                if (set.Contains(c.SHA))
                {
                    c.IsMerged = true;
                    break;
                }
            }
        }

        private List<Models.Commit> _commits = new List<Models.Commit>();
        private Models.Commit _current = null;
        private bool _findFirstMerged = false;
        private bool _isHeadFounded = false;
    }
}
