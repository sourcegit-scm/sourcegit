﻿using System;
using System.Collections.Generic;

using Avalonia;

namespace SourceGit.Models
{
    public class Commit
    {
        public string SHA { get; set; } = string.Empty;
        public User Author { get; set; } = User.Invalid;
        public ulong AuthorTime { get; set; } = 0;
        public User Committer { get; set; } = User.Invalid;
        public ulong CommitterTime { get; set; } = 0;
        public string Subject { get; set; } = string.Empty;
        public List<string> Parents { get; set; } = new List<string>();
        public List<Decorator> Decorators { get; set; } = new List<Decorator>();
        public bool HasDecorators => Decorators.Count > 0;
        public bool IsMerged { get; set; } = false;
        public Thickness Margin { get; set; } = new Thickness(0);

        
        public string AuthorTimeStr => DateTime.UnixEpoch.AddSeconds(AuthorTime).ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
        public string CommitterTimeStr => DateTime.UnixEpoch.AddSeconds(CommitterTime).ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
        public string AuthorTimeShortStr => DateTime.UnixEpoch.AddSeconds(AuthorTime).ToLocalTime().ToString("yyyy/MM/dd");
        public string CommitterTimeShortStr => DateTime.UnixEpoch.AddSeconds(CommitterTime).ToString("yyyy/MM/dd");

        public bool IsCommitterVisible => Author != Committer || AuthorTime != CommitterTime;
        public bool IsCurrentHead => Decorators.Find(x => x.Type is DecoratorType.CurrentBranchHead or DecoratorType.CurrentCommitHead) != null;
    }
}
