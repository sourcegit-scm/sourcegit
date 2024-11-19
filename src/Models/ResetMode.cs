using Avalonia.Media;

namespace SourceGit.Models
{
    public class ResetMode
    {
        public static readonly ResetMode[] Supported =
        [
            new ResetMode("Soft", "Keep all changes. Stage differences", "--soft", "S", Brushes.Green),
            new ResetMode("Mixed", "Keep all changes. Unstage differences", "--mixed", "M", Brushes.Orange),
            new ResetMode("Merge", "Reset while keeping unmerged changes", "--merge", "G", Brushes.Purple),
            new ResetMode("Keep", "Reset while keeping local modifications", "--keep", "K", Brushes.Purple),
            new ResetMode("Hard", "Discard all changes", "--hard", "H", Brushes.Red),
        ];

        public string Name { get; set; }
        public string Desc { get; set; }
        public string Arg { get; set; }
        public string Key { get; set; }
        public IBrush Color { get; set; }

        public ResetMode(string n, string d, string a, string k, IBrush b)
        {
            Name = n;
            Desc = d;
            Arg = a;
            Key = k;
            Color = b;
        }
    }
}
