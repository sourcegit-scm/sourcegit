using Avalonia.Data.Converters;

namespace SourceGit.Converters {
    public static class IntConverters {
        public static FuncValueConverter<int, bool> IsGreaterThanZero =
            new FuncValueConverter<int, bool>(v => v > 0);

        public static FuncValueConverter<int, bool> IsZero =
            new FuncValueConverter<int, bool>(v => v == 0);

        public static FuncValueConverter<int, bool> IsOne =
            new FuncValueConverter<int, bool>(v => v == 1);
    }
}
