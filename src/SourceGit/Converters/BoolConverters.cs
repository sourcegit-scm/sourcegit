using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class BoolConverters
    {
        public static readonly FuncValueConverter<bool, double> ToCommitOpacity =
            new FuncValueConverter<bool, double>(x => x ? 1 : 0.5);
    }
}
