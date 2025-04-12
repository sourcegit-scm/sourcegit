using System.Collections;
using System.Collections.Generic;

using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class ListConverters
    {
        public static readonly FuncValueConverter<IList, string> ToCount =
            new(v => v == null ? " (0)" : $" ({v.Count})");

        public static readonly FuncValueConverter<IList, bool> IsNullOrEmpty =
            new(v => v == null || v.Count == 0);

        public static readonly FuncValueConverter<IList, bool> IsNotNullOrEmpty =
            new(v => v != null && v.Count > 0);

        public static readonly FuncValueConverter<List<Models.Change>, List<Models.Change>> Top100Changes =
            new(v => (v == null || v.Count < 100) ? v : v.GetRange(0, 100));

        public static readonly FuncValueConverter<IList, bool> IsOnlyTop100Shows =
            new(v => v != null && v.Count > 100);
    }
}
