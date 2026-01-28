using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class InteractiveRebaseActionConverters
    {
        public static readonly FuncValueConverter<Models.InteractiveRebaseAction, IBrush> ToIconBrush =
            new FuncValueConverter<Models.InteractiveRebaseAction, IBrush>(v =>
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
            new FuncValueConverter<Models.InteractiveRebaseAction, string>(v => v.ToString());
    }
}
