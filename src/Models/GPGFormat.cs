using System.Collections.Generic;

namespace SourceGit.Models
{
    public class GPGFormat(string name, string value, string desc)
    {
        public string Name { get; set; } = name;
        public string Value { get; set; } = value;
        public string Desc { get; set; } = desc;

        public static readonly GPGFormat OPENPGP = new GPGFormat("OPENPGP", "openpgp", "DEFAULT");

        public static readonly GPGFormat SSH = new GPGFormat("SSH", "ssh", "Git >= 2.34.0");

        public static readonly List<GPGFormat> Supported = new List<GPGFormat>() {
            OPENPGP,
            SSH,
        };

        public bool Equals(GPGFormat other)
        {
            return Value == other.Value;
        }
    }
}
