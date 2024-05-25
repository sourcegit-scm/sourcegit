using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SourceGit.ViewModels;

namespace SourceGit.Converters
{
    public class CheckoutTargetTypeConverters
    {
        public static readonly FuncValueConverter<CheckoutTargetType, StreamGeometry> ToIcon =
            new(x =>
                x == CheckoutTargetType.Branch
                    ? App.Current?.FindResource("Icons.Branch") as StreamGeometry
                    : App.Current?.FindResource("Icons.Commit") as StreamGeometry);

        public static readonly FuncValueConverter<CheckoutTargetType, string> ToTitle =
            new(x =>
                x == CheckoutTargetType.Branch ? App.Text("CheckoutBranch") : App.Text("CheckoutCommit"));

        public static readonly FuncValueConverter<CheckoutTargetType, string> ToTarget =
            new(x =>
                x == CheckoutTargetType.Branch ? App.Text("Checkout.TargetBranch") : App.Text("Checkout.TargetCommit"));

        public static readonly FuncValueConverter<CheckoutTargetType, bool> IsBranch =
            new(x => x == CheckoutTargetType.Branch);

        public static readonly FuncValueConverter<CheckoutTargetType, bool> IsCommit =
            new(x => x == CheckoutTargetType.Commit);
    }
}
