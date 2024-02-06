using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters {
    public static class ChangeViewModeConverters {
        public static FuncValueConverter<Models.ChangeViewMode, StreamGeometry> ToIcon =
            new FuncValueConverter<Models.ChangeViewMode, StreamGeometry>(v => {
                switch (v) {
                case Models.ChangeViewMode.List:
                    return App.Current?.FindResource("Icons.List") as StreamGeometry;
                case Models.ChangeViewMode.Grid:
                    return App.Current?.FindResource("Icons.Grid") as StreamGeometry;
                default:
                    return App.Current?.FindResource("Icons.Tree") as StreamGeometry;
                }
            });

        public static FuncValueConverter<Models.ChangeViewMode, bool> IsList =
            new FuncValueConverter<Models.ChangeViewMode, bool>(v => v == Models.ChangeViewMode.List);

        public static FuncValueConverter<Models.ChangeViewMode, bool> IsGrid =
            new FuncValueConverter<Models.ChangeViewMode, bool>(v => v == Models.ChangeViewMode.Grid);

        public static FuncValueConverter<Models.ChangeViewMode, bool> IsTree =
            new FuncValueConverter<Models.ChangeViewMode, bool>(v => v == Models.ChangeViewMode.Tree);
    }
}
