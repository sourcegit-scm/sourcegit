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
    }
}
