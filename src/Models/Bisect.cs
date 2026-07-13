using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public enum BisectState
    {
        None = 0,
        WaitingForFirstBad,
        WaitingForCheckoutAnother,
        WaitingForFirstGood,
        WaitingForMark,
    }

    [Flags]
    public enum BisectCommitFlag
    {
        None = 0,
        Good,
        Bad,
        Skipped,
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

        public HashSet<string> Skipped
        {
            get;
            set;
        } = [];
    }
}
