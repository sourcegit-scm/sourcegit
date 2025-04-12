using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class IntConverters
    {
        public static readonly FuncValueConverter<int, bool> IsGreaterThanZero = new(v => v > 0);

        public static readonly FuncValueConverter<int, bool> IsGreaterThanFour = new(v => v > 4);

        public static readonly FuncValueConverter<int, bool> IsZero = new(v => v == 0);

        public static readonly FuncValueConverter<int, bool> IsOne = new(v => v == 1);

        public static readonly FuncValueConverter<int, bool> IsNotOne = new(v => v != 1);

        public static readonly FuncValueConverter<int, bool> IsSubjectLengthBad = new(v => v > ViewModels.Preferences.Instance.SubjectGuideLength);

        public static readonly FuncValueConverter<int, bool> IsSubjectLengthGood = new(v => v <= ViewModels.Preferences.Instance.SubjectGuideLength);

        public static readonly FuncValueConverter<int, Thickness> ToTreeMargin = new(v => new Thickness(v * 16, 0, 0, 0));

        public static readonly FuncValueConverter<int, IBrush> ToBookmarkBrush =
            new(bookmark =>
            {
                if (bookmark == 0)
                    return Application.Current?.FindResource("Brush.FG1") as IBrush;
                else
                    return Models.Bookmarks.Brushes[bookmark];
            });
    }
}
