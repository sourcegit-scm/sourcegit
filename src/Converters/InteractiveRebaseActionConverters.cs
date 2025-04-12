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
            new(v =>
            {
                return v switch
                {
                    Models.InteractiveRebaseAction.Pick => "Pick",
                    Models.InteractiveRebaseAction.Edit => "Edit",
                    Models.InteractiveRebaseAction.Reword => "Reword",
                    Models.InteractiveRebaseAction.Squash => "Squash",
                    Models.InteractiveRebaseAction.Fixup => "Fixup",
                    _ => "Drop",
                };
            });

        public static readonly FuncValueConverter<Models.InteractiveRebaseAction, bool> CanEditMessage =
            new(v => v == Models.InteractiveRebaseAction.Reword || v == Models.InteractiveRebaseAction.Squash);
    }
}
