using System;

namespace SourceGit.Models
{
    [Flags]
    public enum HistoryShowFlags
    {
        None = 0,
        Reflog = 1 << 0,
        FirstParentOnly = 1 << 1,
        SimplifyByDecoration = 1 << 2,
    }
}
