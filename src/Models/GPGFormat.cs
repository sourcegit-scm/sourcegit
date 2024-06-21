using System.Collections.Generic;

namespace SourceGit.Models
{
    public class GPGFormat(string name, string value, string desc, string program)
    {
        public string Name { get; set; } = name;
        public string Value { get; set; } = value;
        public string Desc { get; set; } = desc;
        public string Program { get; set; } = program;

        public static readonly List<GPGFormat> Supported = [
            new GPGFormat("OPENPGP", "openpgp", "DEFAULT", "gpg"),
            new GPGFormat("X.509", "x509", "", "gpgsm"),
            new GPGFormat("SSH", "ssh", "Requires Git >= 2.34.0", "ssh-keygen"),
        ];
    }
}
