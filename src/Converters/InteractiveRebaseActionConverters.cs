using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SourceGit.Converters
{
    public static class InteractiveRebaseActionConverters
    {
        public static readonly FuncValueConverter<Models.InteractiveRebaseAction, IBrush> ToIconBrush =
            new FuncValueConverter<Models.InteractiveRebaseAction, IBrush>(v =>
            {
                switch (v)
                {
                    case Models.InteractiveRebaseAction.Pick:
                        return Brushes.Green;
                    case Models.InteractiveRebaseAction.Edit:
                        return Brushes.Orange;
                    case Models.InteractiveRebaseAction.Reword:
                        return Brushes.Orange;
                    case Models.InteractiveRebaseAction.Squash:
                        return Brushes.LightGray;
                    case Models.InteractiveRebaseAction.Fixup:
                        return Brushes.LightGray;
                    default:
                        return Brushes.Red;
                }
            });

        public static readonly FuncValueConverter<Models.InteractiveRebaseAction, string> ToName =
            new FuncValueConverter<Models.InteractiveRebaseAction, string>(v =>
            {
                switch (v)
                {
                    case Models.InteractiveRebaseAction.Pick:
                        return "Pick";
                    case Models.InteractiveRebaseAction.Edit:
                        return "Edit";
                    case Models.InteractiveRebaseAction.Reword:
                        return "Reword";
                    case Models.InteractiveRebaseAction.Squash:
                        return "Squash";
                    case Models.InteractiveRebaseAction.Fixup:
                        return "Fixup";
                    default:
                        return "Drop";
                }
            });

        public static readonly FuncValueConverter<Models.InteractiveRebaseAction, bool> CanEditMessage =
            new FuncValueConverter<Models.InteractiveRebaseAction, bool>(v => v == Models.InteractiveRebaseAction.Reword || v == Models.InteractiveRebaseAction.Squash);
    }
}
