using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class BoolConverters
    {
        public static readonly FuncValueConverter<bool, double> ToPageTabWidth =
            new FuncValueConverter<bool, double>(x => x ? 200 : double.NaN);
    }
}
