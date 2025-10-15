using System;

namespace SourceGit.Models
{
    public enum TagSortMode
    {
        CreatorDate = 0,
        Name,
    }

    public class Tag
    {
        public string Name { get; set; } = string.Empty;
        public bool IsAnnotated { get; set; } = false;
        public string SHA { get; set; } = string.Empty;
        public User Creator { get; set; } = null;
        public ulong CreatorDate { get; set; } = 0;
        public string Message { get; set; } = string.Empty;

        public string CreatorDateStr
        {
            get => DateTime.UnixEpoch.AddSeconds(CreatorDate).ToLocalTime().ToString(DateTimeFormat.Active.DateTime);
        }
    }
}
