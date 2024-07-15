using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class BookmarkConverters
    {
        public static readonly FuncValueConverter<int, IBrush> ToBrush =
            new FuncValueConverter<int, IBrush>(bookmark =>
            {
                if (bookmark == 0)
                    return Application.Current?.FindResource("Brush.FG1") as IBrush;
                else
                    return Models.Bookmarks.Brushes[bookmark];
            });
    }
}
