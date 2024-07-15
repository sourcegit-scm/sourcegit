using System;
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

        public bool IsCommitterVisible => !Author.Equals(Committer) || AuthorTime != CommitterTime;
        public bool IsCurrentHead => Decorators.Find(x => x.Type is DecoratorType.CurrentBranchHead or DecoratorType.CurrentCommitHead) != null;
    
        public string CommitterTimeFromNowString
        {
            get
            {
                var today = DateTime.Today;
                var committerTime = DateTime.UnixEpoch.AddSeconds(CommitterTime).ToLocalTime();

                if (committerTime >= today)
                {
                    var now = DateTime.Now;
                    var timespan = now - committerTime;
                    if (timespan.TotalHours > 1)
                        return $"{(int)timespan.TotalHours} hours ago";

                    if (timespan.TotalMinutes > 1)
                        return $"{(int)timespan.TotalMinutes} minutes ago";

                    return $"Just now";
                }

                var diffYear = today.Year - committerTime.Year;
                if (diffYear == 0)
                {
                    var diffMonth = today.Month - committerTime.Month;
                    if (diffMonth > 0)
                        return diffMonth == 1 ? "Last month" : $"{diffMonth} months ago";

                    var diffDay = today.Day - committerTime.Day;
                    if (diffDay > 0)
                        return diffDay == 1 ? "Yesterday" : $"{diffDay} days ago";

                    return "Today";
                }

                if (diffYear == 1)
                    return "Last year";

                return $"{diffYear} years ago";
            }
        }
    }
}
