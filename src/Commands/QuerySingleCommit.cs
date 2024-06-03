using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class QuerySingleCommit : Command
    {
        private const string GPGSIG_START = "gpgsig -----BEGIN PGP SIGNATURE-----";
        private const string GPGSIG_END = " -----END PGP SIGNATURE-----";

        public QuerySingleCommit(string repo, string sha) {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"show --pretty=raw --decorate=full -s {sha}";
        }

        public Models.Commit Result()
        {
            var succ = Exec();
            if (!succ)
                return null;

            _commit.Message.Trim();
            return _commit;
        }

        protected override void OnReadline(string line)
        {
            if (isSkipingGpgsig)
            {
                if (line.StartsWith(GPGSIG_END, StringComparison.Ordinal))
                    isSkipingGpgsig = false;
                return;
            }
            else if (line.StartsWith(GPGSIG_START, StringComparison.Ordinal))
            {
                isSkipingGpgsig = true;
                return;
            }

            if (line.StartsWith("commit ", StringComparison.Ordinal))
            {
                line = line.Substring(7);

                var decoratorStart = line.IndexOf('(', StringComparison.Ordinal);
                if (decoratorStart < 0)
                {
                    _commit.SHA = line.Trim();
                }
                else
                {
                    _commit.SHA = line.Substring(0, decoratorStart).Trim();
                    ParseDecorators(_commit.Decorators, line.Substring(decoratorStart + 1));
                }

                return;
            }

            if (line.StartsWith("tree ", StringComparison.Ordinal))
            {
                return;
            }
            else if (line.StartsWith("parent ", StringComparison.Ordinal))
            {
                _commit.Parents.Add(line.Substring("parent ".Length));
            }
            else if (line.StartsWith("author ", StringComparison.Ordinal))
            {
                Models.User user = Models.User.Invalid;
                ulong time = 0;
                Models.Commit.ParseUserAndTime(line.Substring(7), ref user, ref time);
                _commit.Author = user;
                _commit.AuthorTime = time;
            }
            else if (line.StartsWith("committer ", StringComparison.Ordinal))
            {
                Models.User user = Models.User.Invalid;
                ulong time = 0;
                Models.Commit.ParseUserAndTime(line.Substring(10), ref user, ref time);
                _commit.Committer = user;
                _commit.CommitterTime = time;
            }
            else if (string.IsNullOrEmpty(_commit.Subject))
            {
                _commit.Subject = line.Trim();
            }
            else
            {
                _commit.Message += (line.Trim() + "\n");
            }
        }

        private bool ParseDecorators(List<Models.Decorator> decorators, string data)
        {
            bool isHeadOfCurrent = false;

            var subs = data.Split(new char[] { ',', ')', '(' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var sub in subs)
            {
                var d = sub.Trim();
                if (d.StartsWith("tag: refs/tags/", StringComparison.Ordinal))
                {
                    decorators.Add(new Models.Decorator()
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
                    isHeadOfCurrent = true;
                    decorators.Add(new Models.Decorator()
                    {
                        Type = Models.DecoratorType.CurrentBranchHead,
                        Name = d.Substring(19).Trim(),
                    });
                }
                else if (d.Equals("HEAD"))
                {
                    isHeadOfCurrent = true;
                    decorators.Add(new Models.Decorator()
                    {
                        Type = Models.DecoratorType.CurrentCommitHead,
                        Name = d.Trim(),
                    });
                }
                else if (d.StartsWith("refs/heads/", StringComparison.Ordinal))
                {
                    decorators.Add(new Models.Decorator()
                    {
                        Type = Models.DecoratorType.LocalBranchHead,
                        Name = d.Substring(11).Trim(),
                    });
                }
                else if (d.StartsWith("refs/remotes/", StringComparison.Ordinal))
                {
                    decorators.Add(new Models.Decorator()
                    {
                        Type = Models.DecoratorType.RemoteBranchHead,
                        Name = d.Substring(13).Trim(),
                    });
                }
            }

            decorators.Sort((l, r) =>
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

            return isHeadOfCurrent;
        }

        private Models.Commit _commit = new Models.Commit();
        private bool isSkipingGpgsig = false;
    }
}
