using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class QueryCommitsWithFullMessage : Command
    {
        public QueryCommitsWithFullMessage(string repo, string args)
        {
            _boundary = $"----- BOUNDARY OF COMMIT {Guid.NewGuid()} -----";

            WorkingDirectory = repo;
            Context = repo;
            Args = $"log --date-order --no-show-signature --decorate=full --pretty=format:\"%H%n%P%n%D%n%aN±%aE%n%at%n%cN±%cE%n%ct%n%B%n{_boundary}\" {args}";
        }

        public List<Models.CommitWithMessage> Result()
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
                        _current = new Models.CommitWithMessage();
                        _current.Commit.SHA = line;
                        _commits.Add(_current);
                        break;
                    case 1:
                        ParseParent(line);
                        break;
                    case 2:
                        _current.Commit.ParseDecorators(line);
                        break;
                    case 3:
                        _current.Commit.Author = Models.User.FindOrAdd(line);
                        break;
                    case 4:
                        _current.Commit.AuthorTime = ulong.Parse(line);
                        break;
                    case 5:
                        _current.Commit.Committer = Models.User.FindOrAdd(line);
                        break;
                    case 6:
                        _current.Commit.CommitterTime = ulong.Parse(line);
                        break;
                    default:
                        var boundary = rs.StdOut.IndexOf(_boundary, end + 1);
                        if (boundary > end)
                        {
                            _current.Message = rs.StdOut.Substring(start, boundary - start - 1);
                            end = boundary + _boundary.Length;
                        }
                        else
                        {
                            _current.Message = rs.StdOut.Substring(start);
                            end = rs.StdOut.Length - 2;
                        }

                        nextPartIdx = -1;
                        break;
                }

                nextPartIdx++;

                start = end + 1;
                if (start >= rs.StdOut.Length - 1)
                    break;

                end = rs.StdOut.IndexOf('\n', start);
            }

            return _commits;
        }

        private void ParseParent(string data)
        {
            if (data.Length < 8)
                return;

            _current.Commit.Parents.AddRange(data.Split(separator: ' ', options: StringSplitOptions.RemoveEmptyEntries));
        }

        private List<Models.CommitWithMessage> _commits = new List<Models.CommitWithMessage>();
        private Models.CommitWithMessage _current = null;
        private string _boundary = "";
    }
}
