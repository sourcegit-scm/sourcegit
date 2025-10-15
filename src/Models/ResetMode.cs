using Avalonia.Media;

namespace SourceGit.Models
{
    public class ResetMode(string n, string d, string a, string k, IBrush b)
    {
        public static readonly ResetMode[] Supported =
        [
            new ResetMode("Soft", "Keep all changes. Stage differences", "--soft", "S", Brushes.Green),
            new ResetMode("Mixed", "Keep all changes. Unstage differences", "--mixed", "M", Brushes.Orange),
            new ResetMode("Merge", "Reset while keeping unmerged changes", "--merge", "G", Brushes.Purple),
            new ResetMode("Keep", "Reset while keeping local modifications", "--keep", "K", Brushes.Purple),
            new ResetMode("Hard", "Discard all changes", "--hard", "H", Brushes.Red),
        ];

        public string Name { get; set; } = n;
        public string Desc { get; set; } = d;
        public string Arg { get; set; } = a;
        public string Key { get; set; } = k;
        public IBrush Color { get; set; } = b;
    }
}
