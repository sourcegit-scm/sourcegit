using System;
using System.IO;

using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class PathConverters
    {
        public static readonly FuncValueConverter<string, string> PureFileName =
            new(v => Path.GetFileName(v) ?? "");

        public static readonly FuncValueConverter<string, string> PureDirectoryName =
            new(v => Path.GetDirectoryName(v) ?? "");

        public static readonly FuncValueConverter<string, string> RelativeToHome =
            new(v =>
            {
                if (OperatingSystem.IsWindows())
                    return v;

                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var prefixLen = home.EndsWith('/') ? home.Length - 1 : home.Length;
                if (v.StartsWith(home, StringComparison.Ordinal))
                    return $"~{v.AsSpan(prefixLen)}";

                return v;
            });
    }
}
