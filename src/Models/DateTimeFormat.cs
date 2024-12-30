using System.Collections.Generic;

namespace SourceGit.Models
{
    public class DateTimeFormat
    {
        public string DisplayText { get; set; }
        public string DateOnly { get; set; }
        public string DateTime { get; set; }

        public DateTimeFormat(string displayText, string dateOnly, string dateTime)
        {
            DisplayText = displayText;
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
            new DateTimeFormat("2025/01/31 08:00:00", "yyyy/MM/dd", "yyyy/MM/dd HH:mm:ss"),
            new DateTimeFormat("2025.01.31 08:00:00", "yyyy.MM.dd", "yyyy.MM.dd HH:mm:ss"),
            new DateTimeFormat("2025-01-31 08:00:00", "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss"),
            new DateTimeFormat("01/31/2025 08:00:00", "MM/dd/yyyy", "MM/dd/yyyy HH:mm:ss"),
            new DateTimeFormat("01.31.2025 08:00:00", "MM.dd.yyyy", "MM.dd.yyyy HH:mm:ss"),
            new DateTimeFormat("01-31-2025 08:00:00", "MM-dd-yyyy", "MM-dd-yyyy HH:mm:ss"),
            new DateTimeFormat("31/01/2025 08:00:00", "dd/MM/yyyy", "dd/MM/yyyy HH:mm:ss"),
            new DateTimeFormat("31.01.2025 08:00:00", "dd.MM.yyyy", "dd.MM.yyyy HH:mm:ss"),
            new DateTimeFormat("31-01-2025 08:00:00", "dd-MM-yyyy", "dd-MM-yyyy HH:mm:ss"),

            new DateTimeFormat("Jun 31 2025 08:00:00", "MMM d yyyy", "MMM d yyyy HH:mm:ss"),
            new DateTimeFormat("31 Jun 2025 08:00:00", "d MMM yyyy", "d MMM yyyy HH:mm:ss"),
        };
    }
}
