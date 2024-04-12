using System.Collections;

using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class ListConverters
    {
        public static readonly FuncValueConverter<IList, string> ToCount =
            new FuncValueConverter<IList, string>(v => v == null ? " (0)" : $" ({v.Count})");

        public static readonly FuncValueConverter<IList, bool> IsNotNullOrEmpty =
            new FuncValueConverter<IList, bool>(v => v != null && v.Count > 0);
    }
}
