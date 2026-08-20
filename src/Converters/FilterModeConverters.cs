using System.Collections.Generic;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class FilterModeConverters
    {
        public static readonly FuncValueConverter<Models.FilterMode, IBrush> ToBorderBrush =
            new FuncValueConverter<Models.FilterMode, IBrush>(v =>
            {
                return v switch
                {
                    Models.FilterMode.Included => Brushes.Green,
                    Models.FilterMode.Excluded => Brushes.Red,
                    _ => Brushes.Transparent,
                };
            });

        public static readonly IMultiValueConverter ToBorderBrushWithMatchState =
            new FuncMultiValueConverter<object, IBrush>(values =>
            {
                var list = new List<object>(values);
                if (list.Count < 2 || list[0] is not Models.FilterMode mode)
                    return Brushes.Transparent;

                if (list[1] is true)
                    return Brushes.Gray;

                return mode switch
                {
                    Models.FilterMode.Included => Brushes.Green,
                    Models.FilterMode.Excluded => Brushes.Red,
                    _ => Brushes.Transparent,
                };
            });
    }
}
