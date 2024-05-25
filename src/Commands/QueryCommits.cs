using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class QueryCommits : Command
    {
        private const string GPGSIG_START = "gpgsig -----BEGIN PGP SIGNATURE-----";
        private const string GPGSIG_END = " -----END PGP SIGNATURE-----";

        private readonly List<Models.Commit> commits = new List<Models.Commit>();
        private Models.Commit current = null;
        private bool isSkipingGpgsig = false;
        private bool isHeadFounded = false;
        private readonly bool findFirstMerged = true;

        public QueryCommits(string repo, string limits, bool needFindHead = true)
        {
            WorkingDirectory = repo;
            Args = "log --date-order --decorate=full --pretty=raw " + limits;
            findFirstMerged = needFindHead;
        }

        public List<Models.Commit> Result()
        {
            Exec();

            if (current != null)
            {
                current.Message = current.Message.Trim();
                commits.Add(current);
            }

            if (findFirstMerged && !isHeadFounded && commits.Count > 0)
            {
                MarkFirstMerged();
            }

            return commits;
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
                if (current != null)
                {
                    current.Message = current.Message.Trim();
                    commits.Add(current);
                }

                current = new Models.Commit();
                line = line.Substring(7);

                var decoratorStart = line.IndexOf('(', StringComparison.Ordinal);
                if (decoratorStart < 0)
                {
                    current.SHA = line.Trim();
                }
                else
                {
                    current.SHA = line.Substring(0, decoratorStart).Trim();
                    current.IsMerged = ParseDecorators(current.Decorators, line.Substring(decoratorStart + 1));
                    if (!isHeadFounded)
                        isHeadFounded = current.IsMerged;
                }

                return;
            }

            if (current == null)
                return;

            if (line.StartsWith("tree ", StringComparison.Ordinal))
            {
                return;
            }
            else if (line.StartsWith("parent ", StringComparison.Ordinal))
            {
                current.Parents.Add(line.Substring("parent ".Length));
            }
            else if (line.StartsWith("author ", StringComparison.Ordinal))
            {
                Models.User user = Models.User.Invalid;
                ulong time = 0;
                Models.Commit.ParseUserAndTime(line.Substring(7), ref user, ref time);
                current.Author = user;
                current.AuthorTime = time;
            }
            else if (line.StartsWith("committer ", StringComparison.Ordinal))
            {
                Models.User user = Models.User.Invalid;
                ulong time = 0;
                Models.Commit.ParseUserAndTime(line.Substring(10), ref user, ref time);
                current.Committer = user;
                current.CommitterTime = time;
            }
            else if (string.IsNullOrEmpty(current.Subject))
            {
                current.Subject = line.Trim();
            }
            else
            {
                current.Message += (line.Trim() + "\n");
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

        private void MarkFirstMerged()
        {
            Args = $"log --since=\"{commits[commits.Count - 1].CommitterTimeStr}\" --format=\"%H\"";

            var rs = ReadToEnd();
            var shas = rs.StdOut.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (shas.Length == 0)
                return;

            var set = new HashSet<string>();
            foreach (var sha in shas)
                set.Add(sha);

            foreach (var c in commits)
            {
                if (set.Contains(c.SHA))
                {
                    c.IsMerged = true;
                    break;
                }
            }
        }
    }
}
