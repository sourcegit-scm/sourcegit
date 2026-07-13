using Avalonia;
using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class IntConverters
    {
        public static readonly FuncValueConverter<int, bool> IsGreaterThanZero =
            new(v => v > 0);

        public static readonly FuncValueConverter<int, bool> IsGreaterThanFour =
            new(v => v > 4);

        public static readonly FuncValueConverter<int, bool> IsZero =
            new(v => v == 0);

        public static readonly FuncValueConverter<int, bool> IsNotOne =
            new(v => v != 1);

        public static readonly FuncValueConverter<int, Thickness> ToTreeMargin =
            new(v => new Thickness(v * 16, 0, 0, 0));

        public static readonly FuncValueConverter<int, string> ToUnsolvedDesc =
            new(v => v == 0 ? App.Text("MergeConflictEditor.AllResolved") : App.Text("MergeConflictEditor.ConflictsRemaining", v));
    }
}
