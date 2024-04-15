using System.IO;

using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class PathConverters
    {
        public static readonly FuncValueConverter<string, string> PureFileName =
            new FuncValueConverter<string, string>(fullpath => Path.GetFileName(fullpath) ?? "");

        public static readonly FuncValueConverter<string, string> PureDirectoryName =
            new FuncValueConverter<string, string>(fullpath => Path.GetDirectoryName(fullpath) ?? "");
    }
}
