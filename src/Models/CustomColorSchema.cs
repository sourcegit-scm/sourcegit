using System.Collections.Generic;

namespace SourceGit.Models
{
    public class CustomColorSchema
    {
        public Dictionary<string, string> Basic { get; set; } = new Dictionary<string, string>();
        public List<string> Graph { get; set; } = new List<string>();
    }
}
