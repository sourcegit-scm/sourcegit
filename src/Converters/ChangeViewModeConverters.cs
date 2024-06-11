using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class ChangeViewModeConverters
    {
        public static readonly FuncValueConverter<Models.ChangeViewMode, StreamGeometry> ToIcon =
            new FuncValueConverter<Models.ChangeViewMode, StreamGeometry>(v =>
            {
                switch (v)
                {
                    case Models.ChangeViewMode.List:
                        return App.Current?.FindResource("Icons.List") as StreamGeometry;
                    case Models.ChangeViewMode.Grid:
                        return App.Current?.FindResource("Icons.Grid") as StreamGeometry;
                    default:
                        return App.Current?.FindResource("Icons.Tree") as StreamGeometry;
                }
            });
    }
}
