using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public class BlameLineInfo
    {
        public bool IsFirstInGroup { get; set; } = false;
        public string CommitSHA { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public ulong Timestamp { get; set; } = 0;
        public string Time => DateTime.UnixEpoch.AddSeconds(Timestamp).ToLocalTime().ToString(DateTimeFormat.Active.DateOnly);
    }

    public class BlameData
    {
        public bool IsBinary { get; set; } = false;
        public string Content { get; set; } = string.Empty;
        public List<BlameLineInfo> LineInfos { get; set; } = [];
    }
}
