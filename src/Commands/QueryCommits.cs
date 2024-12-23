using System;
using System.Collections.Generic;
using System.Text;

namespace SourceGit.Commands
{
    public class QueryCommits : Command
    {
        public QueryCommits(string repo, string limits, bool needFindHead = true)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"log --no-show-signature --decorate=full --pretty=format:%H%n%P%n%D%n%aN±%aE%n%at%n%cN±%cE%n%ct%n%s {limits}";
            _findFirstMerged = needFindHead;
        }

        public QueryCommits(string repo, string filter, Models.CommitSearchMethod method, bool onlyCurrentBranch)
        {
            string search = onlyCurrentBranch ? string.Empty : "--branches --remotes ";

            if (method == Models.CommitSearchMethod.ByUser)
            {
                search += $"-i --author=\"{filter}\" --committer=\"{filter}\"";
            }
            else if (method == Models.CommitSearchMethod.ByFile)
            {
                search += $"-- \"{filter}\"";
            }
            else
            {
                var argsBuilder = new StringBuilder();
                argsBuilder.Append(search);

                var words = filter.Split(new[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    var escaped = word.Trim().Replace("\"", "\\\"", StringComparison.Ordinal);
                    argsBuilder.Append($"--grep=\"{escaped}\" ");
                }
                argsBuilder.Append("--all-match -i");

                search = argsBuilder.ToString();
            }

            WorkingDirectory = repo;
            Context = repo;
            Args = $"log -1000 --date-order --no-show-signature --decorate=full --pretty=format:%H%n%P%n%D%n%aN±%aE%n%at%n%cN±%cE%n%ct%n%s " + search;
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

            _current.Parents.AddRange(data.Split(separator: ' ', options: StringSplitOptions.RemoveEmptyEntries));
        }

        private void MarkFirstMerged()
        {
            Args = $"log --since=\"{_commits[^1].CommitterTimeStr}\" --format=\"%H\"";

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
