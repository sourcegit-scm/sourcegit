using System.Collections.Generic;

namespace SourceGit.Models
{
    public class BlameLineInfo
    {
        public bool IsFirstInGroup { get; set; } = false;
        public string CommitSHA { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
    }

    public class BlameData
    {
        public string File { get; set; } = string.Empty;
        public List<BlameLineInfo> LineInfos { get; set; } = new List<BlameLineInfo>();
        public string Content { get; set; } = string.Empty;
        public bool IsBinary { get; set; } = false;
    }
}
