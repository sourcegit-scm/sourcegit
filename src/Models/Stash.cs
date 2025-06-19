using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public class Stash
    {
        public string Name { get; set; } = "";
        public string SHA { get; set; } = "";
        public List<string> Parents { get; set; } = [];
        public ulong Time { get; set; } = 0;
        public string Message { get; set; } = "";

        public string Subject
        {
            get
            {
                var idx = Message.IndexOf('\n', StringComparison.Ordinal);
                return idx > 0 ? Message.Substring(0, idx).Trim() : Message;
            }
        }

        public string TimeStr
        {
            get
            {
                return DateTime.UnixEpoch
                    .AddSeconds(Time)
                    .ToLocalTime()
                    .ToString(DateTimeFormat.Active.DateTime);
            }
        }
    }
}
