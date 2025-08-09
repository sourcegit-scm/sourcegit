using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryCommits : Command
    {
        public QueryCommits(string repo, string limits, bool needFindHead = true, List<string> patterns = null)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"log --no-show-signature --decorate=full --format=%H%n%P%n%D%n%aN±%aE%n%at%n%cN±%cE%n%ct%n%s {limits}";
            _findFirstMerged = needFindHead;
            _patterns = patterns ?? new List<string>();
        }

        public QueryCommits(string repo, string filter, Models.CommitSearchMethod method, bool onlyCurrentBranch)
        {
            string search = onlyCurrentBranch ? string.Empty : "--branches --remotes ";

            if (method == Models.CommitSearchMethod.ByAuthor)
            {
                search += $"-i --author={filter.Quoted()}";
            }
            else if (method == Models.CommitSearchMethod.ByCommitter)
            {
                search += $"-i --committer={filter.Quoted()}";
            }
            else if (method == Models.CommitSearchMethod.ByMessage)
            {
                var argsBuilder = new StringBuilder();
                argsBuilder.Append(search);

                var words = filter.Split([' ', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                    argsBuilder.Append("--grep=").Append(word.Trim().Quoted()).Append(' ');
                argsBuilder.Append("--all-match -i");

                search = argsBuilder.ToString();
            }
            else if (method == Models.CommitSearchMethod.ByPath)
            {
                search += $"-- {filter.Quoted()}";
            }
            else
            {
                search = $"-G{filter.Quoted()}";
            }

            WorkingDirectory = repo;
            Context = repo;
            Args = $"log -1000 --date-order --no-show-signature --decorate=full --format=%H%n%P%n%D%n%aN±%aE%n%at%n%cN±%cE%n%ct%n%s {search}";
            _findFirstMerged = false;
        }

        public async Task<List<Models.Commit>> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
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
                        _current.IsCommitFilterHead = _patterns.Count > 0 && _patterns.Any(f => line.StartsWith(f));
                        _commits.Add(_current);
                        break;
                    case 1:
                        ParseParent(line);
                        break;
                    case 2:
                        _current.ParseDecorators(line);
                        if (_current.IsMerged && !_isHeadFound)
                            _isHeadFound = true;
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

            if (_findFirstMerged && !_isHeadFound && _commits.Count > 0)
                await MarkFirstMergedAsync().ConfigureAwait(false);

            return _commits;
        }

        private void ParseParent(string data)
        {
            if (data.Length < 8)
                return;

            _current.Parents.AddRange(data.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        private async Task MarkFirstMergedAsync()
        {
            Args = $"log --since={_commits[^1].CommitterTimeStr.Quoted()} --format=\"%H\"";

            var rs = await ReadToEndAsync().ConfigureAwait(false);
            var shas = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (shas.Length == 0)
                return;

            var set = new HashSet<string>(shas);

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
        private List<string> _patterns = new List<string>();
        private Models.Commit _current = null;
        private bool _findFirstMerged = false;
        private bool _isHeadFound = false;
    }
}
