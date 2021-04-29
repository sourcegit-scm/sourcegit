using System.Collections.Generic;
using System.Windows.Media;

namespace SourceGit.Models {
    /// <summary>
    ///     重置方式
    /// </summary>
    public class ResetMode {
        public string Name { get; set; }
        public string Desc { get; set; }
        public string Arg { get; set; }
        public Brush Color { get; set; }

        public static List<ResetMode> Supported = new List<ResetMode>() {
            new ResetMode("Soft", "Keep all changes. Stage differences", "--soft", Brushes.Green),
            new ResetMode("Mixed", "Keep all changes. Unstage differences", "--mixed", Brushes.Yellow),
            new ResetMode("Hard", "Discard all changes", "--hard", Brushes.Red),
        };

        public ResetMode(string n, string d, string a, Brush b) {
            Name = n;
            Desc = d;
            Arg = a;
            Color = b;
        }
    }
}
