using System;
using System.Collections.Generic;

namespace SourceGit.Commands
{
    public class QuerySingleCommit : Command
    {
        public QuerySingleCommit(string repo, string sha)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"show --no-show-signature --decorate=full --pretty=format:%H%n%P%n%D%n%aN±%aE%n%at%n%cN±%cE%n%ct%n%s -s {sha}";
        }

        public Models.Commit Result()
        {
            var rs = ReadToEnd();
            if (rs.IsSuccess && !string.IsNullOrEmpty(rs.StdOut))
            {
                var commit = new Models.Commit();
                var lines = rs.StdOut.Split('\n');
                if (lines.Length < 8)
                    return null;

                commit.SHA = lines[0];
                if (!string.IsNullOrEmpty(lines[1]))
                    commit.Parents.AddRange(lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries));
                if (!string.IsNullOrEmpty(lines[2]))
                    commit.IsMerged = ParseDecorators(commit.Decorators, lines[2]);
                commit.Author = Models.User.FindOrAdd(lines[3]);
                commit.AuthorTime = ulong.Parse(lines[4]);
                commit.Committer = Models.User.FindOrAdd(lines[5]);
                commit.CommitterTime = ulong.Parse(lines[6]);
                commit.Subject = lines[7];

                return commit;
            }

            return null;
        }

        private bool ParseDecorators(List<Models.Decorator> decorators, string data)
        {
            bool isHeadOfCurrent = false;

            var subs = data.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var sub in subs)
            {
                var d = sub.Trim();
                if (d.StartsWith("tag: refs/tags/", StringComparison.Ordinal))
                {
                    decorators.Add(new Models.Decorator()
                    {
                        Type = Models.DecoratorType.Tag,
                        Name = d.Substring(15),
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
                        Name = d.Substring(19),
                    });
                }
                else if (d.Equals("HEAD"))
                {
                    isHeadOfCurrent = true;
                    decorators.Add(new Models.Decorator()
                    {
                        Type = Models.DecoratorType.CurrentCommitHead,
                        Name = d,
                    });
                }
                else if (d.StartsWith("refs/heads/", StringComparison.Ordinal))
                {
                    decorators.Add(new Models.Decorator()
                    {
                        Type = Models.DecoratorType.LocalBranchHead,
                        Name = d.Substring(11),
                    });
                }
                else if (d.StartsWith("refs/remotes/", StringComparison.Ordinal))
                {
                    decorators.Add(new Models.Decorator()
                    {
                        Type = Models.DecoratorType.RemoteBranchHead,
                        Name = d.Substring(13),
                    });
                }
            }

            decorators.Sort((l, r) =>
            {
                if (l.Type != r.Type)
                    return (int)l.Type - (int)r.Type;
                else
                    return string.Compare(l.Name, r.Name, StringComparison.Ordinal);
            });

            return isHeadOfCurrent;
        }
    }
}
