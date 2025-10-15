using System;
using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Styling;

namespace SourceGit.Converters
{
    public static class StringConverters
    {
        public class ToLocaleConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return Models.Locale.Supported.Find(x => x.Key == value as string);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return (value as Models.Locale)?.Key;
            }
        }

        public static readonly ToLocaleConverter ToLocale = new ToLocaleConverter();

        public class ToThemeConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var theme = (string)value;
                if (string.IsNullOrEmpty(theme))
                    return ThemeVariant.Default;

                if (theme.Equals("Light", StringComparison.OrdinalIgnoreCase))
                    return ThemeVariant.Light;

                if (theme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                    return ThemeVariant.Dark;

                return ThemeVariant.Default;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return (value as ThemeVariant)?.Key;
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

        public static readonly FuncValueConverter<string, string> TrimRefsPrefix =
            new FuncValueConverter<string, string>(v =>
            {
                if (v == null)
                    return string.Empty;
                if (v.StartsWith("refs/heads/", StringComparison.Ordinal))
                    return v.Substring(11);
                if (v.StartsWith("refs/remotes/", StringComparison.Ordinal))
                    return v.Substring(13);
                return v;
            });

        public static readonly FuncValueConverter<string, bool> ContainsSpaces =
            new FuncValueConverter<string, bool>(v => v != null && v.Contains(' '));

        public static readonly FuncValueConverter<string, bool> IsNotNullOrWhitespace =
            new FuncValueConverter<string, bool>(v => v != null && v.Trim().Length > 0);

        public static readonly FuncValueConverter<string, string> ToFriendlyUpstream =
            new FuncValueConverter<string, string>(v => v is { Length: > 13 } ? v.Substring(13) : string.Empty);
    }
}
