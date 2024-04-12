using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class ObjectConverters
    {
        public static readonly FuncValueConverter<object, bool> IsNull =
            new FuncValueConverter<object, bool>(v => v == null);

        public static readonly FuncValueConverter<object, bool> IsNotNull =
            new FuncValueConverter<object, bool>(v => v != null);
    }
}
