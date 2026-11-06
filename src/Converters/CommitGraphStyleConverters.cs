using System.Collections.Generic;

using Avalonia;
using Avalonia.Data.Converters;

namespace SourceGit.Converters
{
    public static class CommitGraphStyleConverters
    {
        public static readonly FuncValueConverter<Models.CommitGraphStyle, bool> IsClassic =
            new(v => v == Models.CommitGraphStyle.Classic);

        public static readonly FuncValueConverter<Models.CommitGraphStyle, bool> IsAvatars =
            new(v => v == Models.CommitGraphStyle.Avatars);

        public static readonly FuncValueConverter<Models.CommitGraphStyle, double> ToRowHeight =
            new(v => v == Models.CommitGraphStyle.Avatars ? 28 : 26);

        // Places a hit-area centered on the commit's avatar in the graph column.
        public static readonly FuncValueConverter<double, Thickness> ToAvatarHitAreaMargin =
            new(v => new Thickness(Views.CommitGraph.GetAvatarsStyleX(v) - 12, 0, 0, 0));

        // Values: [Commit.LeftMargin, CommitGraphStyle]. Only the classic style draws the graph under the subject.
        public static readonly FuncMultiValueConverter<object, Thickness> ToSubjectMargin =
            new(v =>
            {
                var values = new List<object>(v);
                if (values.Count == 2 && values[0] is double margin && values[1] is Models.CommitGraphStyle.Classic)
                    return new Thickness(margin, 0, 0, 0);

                return new Thickness(4, 0, 0, 0);
            });
    }
}
