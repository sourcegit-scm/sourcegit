using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class FilterModeConverters
    {
        public static readonly FuncValueConverter<Models.FilterMode, IBrush> ToBorderBrush =
            new FuncValueConverter<Models.FilterMode, IBrush>(v =>
            {
                switch (v)
                {
                    case Models.FilterMode.Included:
                        return Brushes.Green;
                    case Models.FilterMode.Excluded:
                        return Brushes.Red;
                    default:
                        return Brushes.Transparent;
                }
            });
    }
}
