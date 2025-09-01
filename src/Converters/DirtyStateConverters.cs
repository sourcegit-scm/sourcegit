using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class DirtyStateConverters
    {
        public static readonly FuncValueConverter<Models.DirtyState, IBrush> ToBrush =
            new FuncValueConverter<Models.DirtyState, IBrush>(v =>
            {
                if (v.HasFlag(Models.DirtyState.HasLocalChanges))
                    return Brushes.Gray;
                if (v.HasFlag(Models.DirtyState.HasPendingPullOrPush))
                    return Brushes.RoyalBlue;
                return Brushes.Transparent;
            });

        public static readonly FuncValueConverter<Models.DirtyState, string> ToDesc =
            new FuncValueConverter<Models.DirtyState, string>(v =>
            {
                if (v.HasFlag(Models.DirtyState.HasLocalChanges))
                    return " • " + App.Text("DirtyState.HasLocalChanges");
                if (v.HasFlag(Models.DirtyState.HasPendingPullOrPush))
                    return " • " + App.Text("DirtyState.HasPendingPullOrPush");
                return " • " + App.Text("DirtyState.UpToDate");
            });
    }
}
