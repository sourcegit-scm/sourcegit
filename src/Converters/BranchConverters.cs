using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class BranchConverters
    {
        public static readonly FuncValueConverter<Models.Branch, string> ToName =
            new FuncValueConverter<Models.Branch, string>(v => v.IsLocal ? v.Name : $"{v.Remote}/{v.Name}");
    }
}
