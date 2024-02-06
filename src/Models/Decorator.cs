using Avalonia.Media;

namespace SourceGit.Models {
    public enum DecoratorType {
        None,
        CurrentBranchHead,
        LocalBranchHead,
        RemoteBranchHead,
        Tag,
    }

    public class Decorator {
        public DecoratorType Type { get; set; } = DecoratorType.None;
        public string Name { get; set; } = "";
    }

    public static class DecoratorResources {
        public static readonly IBrush[] Backgrounds = [
            new SolidColorBrush(0xFF02C302),
            new SolidColorBrush(0xFFFFB835),
        ];
    }
}
