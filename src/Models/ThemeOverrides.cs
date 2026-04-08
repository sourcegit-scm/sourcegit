using System.Collections.Generic;

using Avalonia.Media;

namespace SourceGit.Models
{
    public class ThemeOverrides
    {
        public Dictionary<string, Color> BasicColors { get; set; } = new Dictionary<string, Color>();
        public double GraphPenThickness { get; set; } = 2;
        public double OpacityForNotMergedCommits { get; set; } = 0.5;
        public List<Color> GraphColors { get; set; } = new List<Color>();
    }
}
