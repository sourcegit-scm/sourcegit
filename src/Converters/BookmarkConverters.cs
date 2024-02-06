using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters {
    public static class BookmarkConverters {
        public static FuncValueConverter<int, IBrush> ToBrush =
            new FuncValueConverter<int, IBrush>(bookmark => Models.Bookmarks.Brushes[bookmark]);

        public static FuncValueConverter<int, double> ToStrokeThickness =
            new FuncValueConverter<int, double>(bookmark => bookmark == 0 ? 1.0 : 0);
    }
}
