using System;
using System.Collections.Generic;

namespace SourceGit.Models
{
    public class DateTimeFormat
    {
        public string DateOnly
        {
            get;
            set;
        }

        public string DateTime
        {
            get
            {
                if (_use24Hours != Use24Hours || string.IsNullOrEmpty(_dateTime))
                {
                    _use24Hours = Use24Hours;
                    _dateTime = _use24Hours ? $"{DateOnly} HH:mm:ss" : $"{DateOnly} hh:mm:ss tt";
                }

                return _dateTime;
            }
        }

        public string Example
        {
            get
            {
                return new DateTime(2025, 1, 31, 8, 0, 0, DateTimeKind.Local).ToString(DateOnly);
            }
        }

        public DateTimeFormat(string date)
        {
            DateOnly = date;
        }

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

        public static DateTimeFormat Active
        {
            get => Supported[ActiveIndex];
        }

        public static readonly List<DateTimeFormat> Supported = new List<DateTimeFormat>
        {
            new DateTimeFormat("yyyy/MM/dd"),
            new DateTimeFormat("yyyy.MM.dd"),
            new DateTimeFormat("yyyy-MM-dd"),
            new DateTimeFormat("MM/dd/yyyy"),
            new DateTimeFormat("MM.dd.yyyy"),
            new DateTimeFormat("MM-dd-yyyy"),
            new DateTimeFormat("dd/MM/yyyy"),
            new DateTimeFormat("dd.MM.yyyy"),
            new DateTimeFormat("dd-MM-yyyy"),
            new DateTimeFormat("MMM d yyyy"),
            new DateTimeFormat("d MMM yyyy"),
        };

        private bool _use24Hours = true;
        private string _dateTime = null;
    }
}
