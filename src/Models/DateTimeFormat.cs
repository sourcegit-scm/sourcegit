using System;
using System.Collections.Generic;
using System.Linq;

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

        public static DateTimeFormat Active
        {
            get => Supported[ActiveIndex];
        }

        public static readonly List<DateTimeFormat> Supported = GenerateSupported().ToList();

        private static IEnumerable<DateTimeFormat> GenerateSupported()
        {
            var dateFormats = new string[]
            {
                "yyyy/MM/dd",
                "yyyy.MM.dd",
                "yyyy-MM-dd",
                "MM/dd/yyyy",
                "MM.dd.yyyy",
                "MM-dd-yyyy",
                "dd/MM/yyyy",
                "dd.MM.yyyy",
                "dd-MM-yyyy",
                "MMM d yyyy",
                "d MMM yyyy",
            };

            var timeFormats = new string[]
            {
                "HH:mm:ss",
                "h:mm:ss tt",
            };

            foreach (var timeFormat in timeFormats)
            {
                foreach (var dateFormat in dateFormats)
                {
                    yield return new DateTimeFormat(dateFormat, dateFormat + ", " + timeFormat);
                }
            }
        }

        private static readonly DateTime _example = new DateTime(2025, 1, 31, 8, 0, 0, DateTimeKind.Local);
    }
}
