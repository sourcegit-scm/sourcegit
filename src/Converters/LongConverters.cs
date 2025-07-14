using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class LongConverters
    {
        public static readonly FuncValueConverter<long, string> ToFileSize = new(bytes =>
        {
            if (bytes < KB)
                return $"{bytes:N0} B";

            if (bytes < MB)
                return $"{(bytes / KB):G3} KB ({bytes:N0})";

            if (bytes < GB)
                return $"{(bytes / MB):G3} MB ({bytes:N0})";

            return $"{(bytes / GB):G3} GB ({bytes:N0})";
        });

        private const double KB = 1024;
        private const double MB = 1024 * 1024;
        private const double GB = 1024 * 1024 * 1024;
    }
}
