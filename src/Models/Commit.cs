using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public enum CommitSearchMethod
    {
        BySHA = 0,
        ByAuthor,
        ByCommitter,
        ByMessage,
        ByPath,
        ByContent,
    }

    public class Commit
    {
        public const string EmptyTreeSHA1 = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

        public string SHA { get; set; } = string.Empty;
        public User Author { get; set; } = User.Invalid;
        public ulong AuthorTime { get; set; } = 0;
        public User Committer { get; set; } = User.Invalid;
        public ulong CommitterTime { get; set; } = 0;
        public string Subject { get; set; } = string.Empty;
        public List<string> Parents { get; set; } = new();
        public List<Decorator> Decorators { get; set; } = new();

        public bool IsMerged { get; set; } = false;
        public int Color { get; set; } = 0;
        public double LeftMargin { get; set; } = 0;

        public string AuthorTimeStr => DateTime.UnixEpoch.AddSeconds(AuthorTime).ToLocalTime().ToString(DateTimeFormat.Active.DateTime);
        public string CommitterTimeStr => DateTime.UnixEpoch.AddSeconds(CommitterTime).ToLocalTime().ToString(DateTimeFormat.Active.DateTime);
        public string AuthorTimeShortStr => DateTime.UnixEpoch.AddSeconds(AuthorTime).ToLocalTime().ToString(DateTimeFormat.Active.DateOnly);
        public string CommitterTimeShortStr => DateTime.UnixEpoch.AddSeconds(CommitterTime).ToLocalTime().ToString(DateTimeFormat.Active.DateOnly);

        public bool IsCommitterVisible => !Author.Equals(Committer) || AuthorTime != CommitterTime;
        public bool IsCurrentHead => Decorators.Find(x => x.Type is DecoratorType.CurrentBranchHead or DecoratorType.CurrentCommitHead) != null;
        public bool HasDecorators => Decorators.Count > 0;

        public string GetFriendlyName()
        {
            var branchDecorator = Decorators.Find(x => x.Type is DecoratorType.LocalBranchHead or DecoratorType.RemoteBranchHead);
            if (branchDecorator != null)
                return branchDecorator.Name;

            var tagDecorator = Decorators.Find(x => x.Type is DecoratorType.Tag);
            if (tagDecorator != null)
                return tagDecorator.Name;

            return SHA[..10];
        }

        public void ParseParents(string data)
        {
            if (data.Length < 8)
                return;

            Parents.AddRange(data.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        public void ParseDecorators(string data)
        {
            if (data.Length < 3)
                return;

            var subs = data.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var sub in subs)
            {
                var d = sub.Trim();
                if (d.EndsWith("/HEAD", StringComparison.Ordinal))
                    continue;

                if (d.StartsWith("tag: refs/tags/", StringComparison.Ordinal))
                {
                    Decorators.Add(new Decorator()
                    {
                        Type = DecoratorType.Tag,
                        Name = d.Substring(15),
                    });
                }
                else if (d.StartsWith("HEAD -> refs/heads/", StringComparison.Ordinal))
                {
                    IsMerged = true;
                    Decorators.Add(new Decorator()
                    {
                        Type = DecoratorType.CurrentBranchHead,
                        Name = d.Substring(19),
                    });
                }
                else if (d.Equals("HEAD"))
                {
                    IsMerged = true;
                    Decorators.Add(new Decorator()
                    {
                        Type = DecoratorType.CurrentCommitHead,
                        Name = d,
                    });
                }
                else if (d.StartsWith("refs/heads/", StringComparison.Ordinal))
                {
                    Decorators.Add(new Decorator()
                    {
                        Type = DecoratorType.LocalBranchHead,
                        Name = d.Substring(11),
                    });
                }
                else if (d.StartsWith("refs/remotes/", StringComparison.Ordinal))
                {
                    Decorators.Add(new Decorator()
                    {
                        Type = DecoratorType.RemoteBranchHead,
                        Name = d.Substring(13),
                    });
                }
            }

            Decorators.Sort((l, r) =>
            {
                var delta = (int)l.Type - (int)r.Type;
                if (delta != 0)
                    return delta;
                return NumericSort.Compare(l.Name, r.Name);
            });
        }
    }

    public class CommitFullMessage
    {
        public string Message { get; set; } = string.Empty;
        public InlineElementCollector Inlines { get; set; } = new();
    }
}
