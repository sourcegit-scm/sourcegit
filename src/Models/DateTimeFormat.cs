using System;
using System.Collections.Generic;
using System.Globalization;

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

        public static int DayOfWeekStyle
        {
            get;
            set;
        } = 0;

        public static bool UseLocalizedCulture
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
            get => DateTime.Now.ToString(DateFormat, ActiveCulture);
        }

        // Raised when the formatting culture changes at runtime (e.g. the app language
        // was switched). Views that render dates in code subscribe to refresh themselves,
        // since such a change is invisible to their bound properties.
        public static event Action Changed;

        private static CultureInfo ActiveCulture => UseLocalizedCulture ? _localizedCulture : _invariantCulture;

        private static CultureInfo _localizedCulture = CreateCulture(CultureInfo.CurrentCulture);
        private static readonly CultureInfo _invariantCulture = CreateCulture(CultureInfo.InvariantCulture);

        // Ties the localized day/month names to the application's selected language
        // (e.g. locale key "el_GR"), instead of the operating-system culture.
        public static void UseCulture(string localeKey)
        {
            var baseCulture = CultureInfo.CurrentCulture;
            if (!string.IsNullOrEmpty(localeKey))
            {
                try
                {
                    baseCulture = CultureInfo.GetCultureInfo(localeKey.Replace('_', '-'));
                }
                catch (CultureNotFoundException)
                {
                    // fall back to the operating-system culture
                }
            }

            _localizedCulture = CreateCulture(baseCulture);
            Changed?.Invoke();
        }

        private static CultureInfo CreateCulture(CultureInfo baseCulture)
        {
            var culture = (CultureInfo)baseCulture.Clone();
            culture.DateTimeFormat.DateSeparator = "/";
            culture.DateTimeFormat.TimeSeparator = ":";
            return culture;
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
            var dateFormat = DayOfWeekStyle switch
            {
                1 => $"ddd {actived.DateFormat}",
                2 => $"dddd {actived.DateFormat}",
                _ => actived.DateFormat,
            };

            if (dateOnly)
                return localTime.ToString(dateFormat, ActiveCulture);

            var format = Use24Hours ? $"{dateFormat} HH:mm:ss" : $"{dateFormat} hh:mm:ss tt";
            return localTime.ToString(format, ActiveCulture);
        }
    }
}
