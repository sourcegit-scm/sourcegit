using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class LongConverters
    {
        public static readonly FuncValueConverter<long, string> ToFileSize = new(bytes =>
        {
            if (bytes < KB)
                return $"{bytes} B";

            if (bytes < MB)
                return $"{(bytes / KB):F3} KB ({bytes} B)";

            if (bytes < GB)
                return $"{(bytes / MB):F3} MB ({bytes} B)";

            return $"{(bytes / GB):F3} GB ({bytes} B)";
        });

        private const double KB = 1024;
        private const double MB = 1024 * 1024;
        private const double GB = 1024 * 1024 * 1024;
    }
}
