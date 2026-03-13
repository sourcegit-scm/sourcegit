using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class InteractiveRebaseActionConverters
    {
        public static readonly FuncValueConverter<Models.InteractiveRebaseAction, IBrush> ToIconBrush =
            new(v =>
            {
                return v switch
                {
                    Models.InteractiveRebaseAction.Pick => Brushes.Green,
                    Models.InteractiveRebaseAction.Edit => Brushes.Orange,
                    Models.InteractiveRebaseAction.Reword => Brushes.Orange,
                    Models.InteractiveRebaseAction.Squash => Brushes.LightGray,
                    Models.InteractiveRebaseAction.Fixup => Brushes.LightGray,
                    _ => Brushes.Red,
                };
            });

        public static readonly FuncValueConverter<Models.InteractiveRebaseAction, string> ToName =
            new(v => v.ToString());

        public static readonly FuncValueConverter<Models.InteractiveRebaseAction, bool> IsDrop =
            new(v => v == Models.InteractiveRebaseAction.Drop);

        public static readonly FuncValueConverter<Models.InteractiveRebaseAction, double> ToOpacity =
            new(v => v > Models.InteractiveRebaseAction.Reword ? 0.65 : 1.0);
    }
}
