using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class QueryCommits : Command
    {
        public QueryCommits(string repo, string limits, bool needFindHead = true)
        {
            _endOfBodyToken = $"----- END OF BODY {Guid.NewGuid()} -----";

            WorkingDirectory = repo;
            Context = repo;
            Args = $"log --date-order --no-show-signature --decorate=full --pretty=format:\"%H%n%P%n%D%n%aN±%aE%n%at%n%cN±%cE%n%ct%n%B%n{_endOfBodyToken}\" " + limits;
            _findFirstMerged = needFindHead;
        }

        public List<Models.Commit> Result()
        {
            Exec();

            if (_findFirstMerged && !_isHeadFounded && _commits.Count > 0)
                MarkFirstMerged();

            return _commits;
        }

        protected override void OnReadline(string line)
        {
            switch (_nextPartIdx)
            {
                case 0:
                    _current = new Models.Commit() { SHA = line };
                    _commits.Add(_current);
                    break;
                case 1:
                    if (!string.IsNullOrEmpty(line))
                        _current.Parents.AddRange(line.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                    break;
                case 2:
                    if (!string.IsNullOrEmpty(line))
                        ParseDecorators(line);
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
                default:
                    if (line.Equals(_endOfBodyToken, StringComparison.Ordinal))
                    {
                        _nextPartIdx = 0;
                        if (!string.IsNullOrEmpty(_current.Message))
                            _current.Message = _current.Message.Trim();
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(_current.Subject))
                            _current.Subject = line;
                        else
                            _current.Message += (line + "\n");
                    }
                    return;
            }

            _nextPartIdx++;
        }

        private void ParseDecorators(string data)
        {
            var subs = data.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var sub in subs)
            {
                var d = sub.Trim();
                if (d.StartsWith("tag: refs/tags/", StringComparison.Ordinal))
                {
                    _current.Decorators.Add(new Models.Decorator()
                    {
                        Type = Models.DecoratorType.Tag,
                        Name = d.Substring(15).Trim(),
                    });
                }
                else if (d.EndsWith("/HEAD", StringComparison.Ordinal))
                {
                    continue;
                }
                else if (d.StartsWith("HEAD -> refs/heads/", StringComparison.Ordinal))
                {
                    _current.IsMerged = true;
                    _current.Decorators.Add(new Models.Decorator()
                    {
                        Type = Models.DecoratorType.CurrentBranchHead,
                        Name = d.Substring(19).Trim(),
                    });
                }
                else if (d.Equals("HEAD"))
                {
                    _current.IsMerged = true;
                    _current.Decorators.Add(new Models.Decorator()
                    {
                        Type = Models.DecoratorType.CurrentCommitHead,
                        Name = d.Trim(),
                    });
                }
                else if (d.StartsWith("refs/heads/", StringComparison.Ordinal))
                {
                    _current.Decorators.Add(new Models.Decorator()
                    {
                        Type = Models.DecoratorType.LocalBranchHead,
                        Name = d.Substring(11).Trim(),
                    });
                }
                else if (d.StartsWith("refs/remotes/", StringComparison.Ordinal))
                {
                    _current.Decorators.Add(new Models.Decorator()
                    {
                        Type = Models.DecoratorType.RemoteBranchHead,
                        Name = d.Substring(13).Trim(),
                    });
                }
            }

            _current.Decorators.Sort((l, r) =>
            {
                if (l.Type != r.Type)
                {
                    return (int)l.Type - (int)r.Type;
                }
                else
                {
                    return l.Name.CompareTo(r.Name);
                }
            });

            if (_current.IsMerged && !_isHeadFounded)
                _isHeadFounded = true;
        }

        private void MarkFirstMerged()
        {
            Args = $"log --since=\"{_commits[_commits.Count - 1].CommitterTimeStr}\" --format=\"%H\"";

            var rs = ReadToEnd();
            var shas = rs.StdOut.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
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

        private string _endOfBodyToken = string.Empty;
        private List<Models.Commit> _commits = new List<Models.Commit>();
        private Models.Commit _current = null;
        private bool _isHeadFounded = false;
        private readonly bool _findFirstMerged = true;
        private int _nextPartIdx = 0;
    }
}
