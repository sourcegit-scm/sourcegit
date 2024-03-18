using System.Collections;

using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class ListConverters
    {
        public static FuncValueConverter<IList, string> ToCount =
            new FuncValueConverter<IList, string>(v => $" ({v.Count})");

        public static FuncValueConverter<IList, bool> IsNotNullOrEmpty =
            new FuncValueConverter<IList, bool>(v => v != null && v.Count > 0);
    }
}