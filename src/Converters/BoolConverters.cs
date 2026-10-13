using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class BoolConverters
    {
        public static readonly FuncValueConverter<bool, FontWeight> IsBoldToFontWeight =
            new(x => x ? FontWeight.Bold : FontWeight.Regular);

        public static readonly FuncValueConverter<bool, double> IsMergedToOpacity =
            new(x => x ? 1 : 0.65);

        public static readonly FuncValueConverter<bool, IBrush> IsWarningToBrush =
            new(x => x ? Brushes.DarkGoldenrod : Application.Current?.FindResource("Brush.FG1") as IBrush);

        public static readonly FuncValueConverter<bool, int> IsStagedOnTopToUnstagedRow =
            new(x => x ? 3 : 1);

        public static readonly FuncValueConverter<bool, int> IsStagedOnTopToStagedRow =
            new(x => x ? 1 : 3);

        public static readonly FuncValueConverter<bool, StreamGeometry> IsStagedOnTopToStageIcon =
            new(x => Application.Current?.FindResource(x ? "Icons.Up" : "Icons.Down") as StreamGeometry);

        public static readonly FuncValueConverter<bool, StreamGeometry> IsStagedOnTopToStageAllIcon =
            new(x => Application.Current?.FindResource(x ? "Icons.DoubleUp" : "Icons.DoubleDown") as StreamGeometry);

        public static readonly FuncValueConverter<bool, StreamGeometry> IsStagedOnTopToUnstageIcon =
            new(x => Application.Current?.FindResource(x ? "Icons.Down" : "Icons.Up") as StreamGeometry);

        public static readonly FuncValueConverter<bool, StreamGeometry> IsStagedOnTopToUnstageAllIcon =
            new(x => Application.Current?.FindResource(x ? "Icons.DoubleDown" : "Icons.DoubleUp") as StreamGeometry);
    }
}
