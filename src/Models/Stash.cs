using System;

namespace SourceGit.Models
{
    public class Stash
    {
        private static readonly DateTime UTC_START = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToLocalTime();

        public string Name { get; set; } = "";
        public string SHA { get; set; } = "";
        public ulong Time { get; set; } = 0;
        public string Message { get; set; } = "";

        public string TimeStr => UTC_START.AddSeconds(Time).ToString("yyyy/MM/dd HH:mm:ss");
    }
}
