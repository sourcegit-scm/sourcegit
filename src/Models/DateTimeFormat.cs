using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public class DateTimeFormat
    {
        public string DateOnly { get; set; }
        public string DateTime { get; set; }

        public string Example
        {
            get => _example.ToString(DateTime);
        }

        public DateTimeFormat(string dateOnly, string dateTime)
        {
            DateOnly = dateOnly;
            DateTime = dateTime;
        }

        public static int ActiveIndex
        {
            get;
            set;
        } = 0;

        public static DateTimeFormat Actived
        {
            get => Supported[ActiveIndex];
        }

        public static readonly List<DateTimeFormat> Supported = new List<DateTimeFormat>
        {
            new DateTimeFormat("yyyy/MM/dd", "yyyy/MM/dd HH:mm:ss"),
            new DateTimeFormat("yyyy.MM.dd", "yyyy.MM.dd HH:mm:ss"),
            new DateTimeFormat("yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss"),
            new DateTimeFormat("MM/dd/yyyy", "MM/dd/yyyy HH:mm:ss"),
            new DateTimeFormat("MM.dd.yyyy", "MM.dd.yyyy HH:mm:ss"),
            new DateTimeFormat("MM-dd-yyyy", "MM-dd-yyyy HH:mm:ss"),
            new DateTimeFormat("dd/MM/yyyy", "dd/MM/yyyy HH:mm:ss"),
            new DateTimeFormat("dd.MM.yyyy", "dd.MM.yyyy HH:mm:ss"),
            new DateTimeFormat("dd-MM-yyyy", "dd-MM-yyyy HH:mm:ss"),
            new DateTimeFormat("MMM d yyyy", "MMM d yyyy HH:mm:ss"),
            new DateTimeFormat("d MMM yyyy", "d MMM yyyy HH:mm:ss"),
        };

        private static readonly DateTime _example = new DateTime(2025, 1, 31, 8, 0, 0, DateTimeKind.Local);
    }
}
