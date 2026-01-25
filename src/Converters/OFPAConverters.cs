#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public class PathToDisplayNameConverter : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2)
                return "";

            string path = values[0] as string ?? string.Empty;
            var decodedPaths = values[1] as IReadOnlyDictionary<string, string>;

            if (decodedPaths != null &&
                decodedPaths.TryGetValue(path, out var decoded) &&
                !string.IsNullOrEmpty(decoded))
            {
                return decoded;
            }

            if (parameter as string == "PureFileName")
                return Path.GetFileName(path);

            return path;
        }
    }

    public static class OFPAConverters
    {
        public static readonly PathToDisplayNameConverter PathToDisplayName = new();
    }
}
