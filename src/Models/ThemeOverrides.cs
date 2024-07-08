using System.Collections.Generic;

namespace SourceGit.Models
{
    public class ThemeOverrides
    {
        public Dictionary<string, string> BasicColors { get; set; } = new Dictionary<string, string>();
        public double GraphPenThickness { get; set; } = 1.5;
        public List<string> GraphColors { get; set; } = new List<string>();
    }
}
