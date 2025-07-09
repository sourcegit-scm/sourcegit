using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class LongConverters
    {
        public static readonly FuncValueConverter<long, string> ToFileSize = new(bytes =>
        {
            var full = $"{bytes:N0} B";
            if (bytes < KB)
                return full;

            if (bytes < MB)
                return $"{(bytes / KB):G3} KB ({full})";

            if (bytes < GB)
                return $"{(bytes / MB):G3} MB ({full})";

            if (bytes < 1000 * GB)
                return $"{(bytes / GB):G3} GB ({full})";

            return $"{(bytes / GB):N0} GB ({full})";
        });

        private const double KB = 1024;
        private const double MB = 1024 * KB;
        private const double GB = 1024 * MB;
    }
}
