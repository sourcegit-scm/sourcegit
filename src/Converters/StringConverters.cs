using System;
using System.Globalization;
using System.Text.RegularExpressions;

using Avalonia.Data.Converters;
using Avalonia.Styling;

namespace SourceGit.Converters
{
    public static partial class StringConverters
    {
        public class ToLocaleConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return Models.Locale.Supported.Find(x => x.Key == value as string);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return (value as Models.Locale).Key;
            }
        }

        public static readonly ToLocaleConverter ToLocale = new ToLocaleConverter();

        public class ToThemeConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var theme = (string)value;
                if (theme.Equals("Light", StringComparison.OrdinalIgnoreCase))
                    return ThemeVariant.Light;
                else if (theme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                    return ThemeVariant.Dark;
                else
                    return ThemeVariant.Default;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var theme = (ThemeVariant)value;
                return theme.Key;
            }
        }

        public static readonly ToThemeConverter ToTheme = new ToThemeConverter();

        public class FormatByResourceKeyConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var key = parameter as string;
                return App.Text(key, value);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        public static readonly FormatByResourceKeyConverter FormatByResourceKey = new FormatByResourceKeyConverter();

        public static readonly FuncValueConverter<string, string> ToShortSHA =
            new FuncValueConverter<string, string>(v => v == null ? string.Empty : (v.Length > 10 ? v.Substring(0, 10) : v));

        public static readonly FuncValueConverter<string, bool> UnderRecommendGitVersion =
            new(v =>
            {
                var match = REG_GIT_VERSION().Match(v ?? "");
                if (match.Success)
                {
                    var major = int.Parse(match.Groups[1].Value);
                    var minor = int.Parse(match.Groups[2].Value);
                    var build = int.Parse(match.Groups[3].Value);

                    return new Version(major, minor, build) < MINIMAL_GIT_VERSION;
                }

                return true;
            });

        [GeneratedRegex(@"^[\s\w]*(\d+)\.(\d+)[\.\-](\d+).*$")]
        private static partial Regex REG_GIT_VERSION();

        private static readonly Version MINIMAL_GIT_VERSION = new Version(2, 23, 0);
    }
}
