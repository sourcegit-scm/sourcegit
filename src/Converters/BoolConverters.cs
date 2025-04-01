using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class BoolConverters
    {
        public static readonly FuncValueConverter<bool, double> ToPageTabWidth =
            new FuncValueConverter<bool, double>(x => x ? 200 : double.NaN);

        public static readonly FuncValueConverter<bool, FontWeight> IsBoldToFontWeight =
            new FuncValueConverter<bool, FontWeight>(x => x ? FontWeight.Bold : FontWeight.Normal);
    }
}
