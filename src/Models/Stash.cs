using System;

namespace SourceGit.Models
{
    public class Stash
    {
        public string Name { get; set; } = "";
        public string SHA { get; set; } = "";
        public ulong Time { get; set; } = 0;
        public string Message { get; set; } = "";

        public string TimeStr => DateTime.UnixEpoch.AddSeconds(Time).ToLocalTime().ToString(DateTimeFormat.Actived.DateTime);
    }
}
