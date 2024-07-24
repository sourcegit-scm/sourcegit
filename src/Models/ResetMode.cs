using Avalonia.Media;

namespace SourceGit.Models
{
    public class ResetMode
    {
        public static readonly ResetMode[] Supported = 
        [
            new ResetMode("Soft", "Keep all changes. Stage differences", "--soft", Brushes.Green),
            new ResetMode("Mixed", "Keep all changes. Unstage differences", "--mixed", Brushes.Orange),
            new ResetMode("Hard", "Discard all changes", "--hard", Brushes.Red),
        ];

        public string Name { get; set; }
        public string Desc { get; set; }
        public string Arg { get; set; }
        public IBrush Color { get; set; }

        public ResetMode(string n, string d, string a, IBrush b)
        {
            Name = n;
            Desc = d;
            Arg = a;
            Color = b;
        }
    }
}
