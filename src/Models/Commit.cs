﻿using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Media;

namespace SourceGit.Models
{
    public enum CommitSearchMethod
    {
        ByUser,
        ByMessage,
        ByFile,
    }

    public class Commit
    {
        public static double OpacityForNotMerged
        {
            get;
            set;
        } = 0.65;

        public string SHA { get; set; } = string.Empty;
        public User Author { get; set; } = User.Invalid;
        public ulong AuthorTime { get; set; } = 0;
        public User Committer { get; set; } = User.Invalid;
        public ulong CommitterTime { get; set; } = 0;
        public string Subject { get; set; } = string.Empty;
        public List<string> Parents { get; set; } = new List<string>();
        public List<Decorator> Decorators { get; set; } = new List<Decorator>();
        public bool HasDecorators => Decorators.Count > 0;

        public string AuthorTimeStr => DateTime.UnixEpoch.AddSeconds(AuthorTime).ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
        public string CommitterTimeStr => DateTime.UnixEpoch.AddSeconds(CommitterTime).ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
        public string AuthorTimeShortStr => DateTime.UnixEpoch.AddSeconds(AuthorTime).ToLocalTime().ToString("yyyy/MM/dd");

        public bool IsMerged { get; set; } = false;
        public bool IsCommitterVisible => !Author.Equals(Committer) || AuthorTime != CommitterTime;
        public bool IsCurrentHead => Decorators.Find(x => x.Type is DecoratorType.CurrentBranchHead or DecoratorType.CurrentCommitHead) != null;

        public int Color { get; set; } = 0;
        public double Opacity => IsMerged ? 1 : OpacityForNotMerged;
        public FontWeight FontWeight => IsCurrentHead ? FontWeight.Bold : FontWeight.Regular;
        public Thickness Margin { get; set; } = new Thickness(0);
        public IBrush Brush => CommitGraph.Pens[Color].Brush;

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
                if (l.Type != r.Type)
                    return (int)l.Type - (int)r.Type;
                else
                    return string.Compare(l.Name, r.Name, StringComparison.Ordinal);
            });
        }
    }

    public class CommitWithMessage
    {
        public Commit Commit { get; set; } = new Commit();
        public string Message { get; set; } = "";
    }
}
