using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class LongConverters
    {
        public static readonly FuncValueConverter<long, string> ToFileSize = new(bytes =>
        {
            var suffixes = new[] { "", "ki", "Mi", "Gi", "Ti", "Pi", "Ei" };
            double dbl = bytes;
            var i = 0;

            while (dbl > 1024 && i < suffixes.Length - 1)
            {
                dbl /= 1024;
                i++;
            }

            return $"{dbl:0.#} {suffixes[i]}B";
        });
    }
}
