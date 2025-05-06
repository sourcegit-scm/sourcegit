using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public enum BisectState
    {
        None = 0,
        WaitingForRange,
        Detecting,
    }

    [Flags]
    public enum BisectCommitFlag
    {
        None = 0,
        Good = 1 << 0,
        Bad = 1 << 1,
    }

    public class Bisect
    {
        public HashSet<string> Bads
        {
            get;
            set;
        } = [];

        public HashSet<string> Goods
        {
            get;
            set;
        } = [];
    }
}
