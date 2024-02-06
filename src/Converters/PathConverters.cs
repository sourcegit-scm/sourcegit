using Avalonia.Data.Converters;
using System.IO;

namespace SourceGit.Converters {
    public static class PathConverters {
        public static FuncValueConverter<string, string> PureFileName =
            new FuncValueConverter<string, string>(fullpath => Path.GetFileName(fullpath) ?? "");

        public static FuncValueConverter<string, string> PureDirectoryName =
            new FuncValueConverter<string, string>(fullpath => Path.GetDirectoryName(fullpath) ?? "");

        public static FuncValueConverter<string, string> TruncateIfTooLong =
            new FuncValueConverter<string, string>(fullpath => {
                if (fullpath.Length <= 50) return fullpath;
                return fullpath.Substring(0, 20) + ".../" + Path.GetFileName(fullpath);
            });
    }
}
