using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public class DateTimeFormat
    {
        public static readonly List<DateTimeFormat> Supported = new List<DateTimeFormat>
        {
            new("yyyy/MM/dd"),
            new("yyyy.MM.dd"),
            new("yyyy-MM-dd"),
            new("MM/dd/yyyy"),
            new("MM.dd.yyyy"),
            new("MM-dd-yyyy"),
            new("dd/MM/yyyy"),
            new("dd.MM.yyyy"),
            new("dd-MM-yyyy"),
            new("MMM d yyyy"),
            new("d MMM yyyy"),
        };

        public static int ActiveIndex
        {
            get;
            set;
        } = 0;

        public static bool Use24Hours
        {
            get;
            set;
        } = true;

        public string DateFormat
        {
            get;
        }

        public string Example
        {
            get => DateTime.Now.ToString(DateFormat);
        }

        public DateTimeFormat(string date)
        {
            DateFormat = date;
        }

        public static string Format(ulong timestamp, bool dateOnly = false)
        {
            var localTime = DateTime.UnixEpoch.AddSeconds(timestamp).ToLocalTime();
            return Format(localTime, dateOnly);
        }

        public static string Format(DateTime localTime, bool dateOnly = false)
        {
            var actived = Supported[ActiveIndex];
            if (dateOnly)
                return localTime.ToString(actived.DateFormat);

            var format = Use24Hours ? $"{actived.DateFormat} HH:mm:ss" : $"{actived.DateFormat} hh:mm:ss tt";
            return localTime.ToString(format);
        }
    }
}
