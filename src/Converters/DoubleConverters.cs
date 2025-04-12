using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class DoubleConverters
    {
        public static readonly FuncValueConverter<double, double> Increase = new(v => v + 1.0);

        public static readonly FuncValueConverter<double, double> Decrease = new(v => v - 1.0);

        public static readonly FuncValueConverter<double, string> ToPercentage = new(v => $"{v * 100:F3}%");

        public static readonly FuncValueConverter<double, string> OneMinusToPercentage = new(v => $"{(1.0 - v) * 100:F3}%");
    }
}
