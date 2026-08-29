using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class WorktreeBaseBranchConverters
    {
        public static readonly FuncValueConverter<Models.WorktreeBaseBranchKind, IBrush> ToBrush =
            new FuncValueConverter<Models.WorktreeBaseBranchKind, IBrush>(kind =>
            {
                if (kind == Models.WorktreeBaseBranchKind.None)
                    return Brushes.Transparent;

                return new SolidColorBrush(Color.Parse(Models.WorktreeBaseBranch.GetBadgeColor(kind)));
            });
    }
}
