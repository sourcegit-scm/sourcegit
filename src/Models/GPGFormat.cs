using System.Collections.Generic;

namespace SourceGit.Models
{
    public class GPGFormat(string name, string value, string desc, string program, bool needFindProgram)
    {
        public string Name { get; set; } = name;
        public string Value { get; set; } = value;
        public string Desc { get; set; } = desc;
        public string Program { get; set; } = program;
        public bool NeedFindProgram { get; set; } = needFindProgram;

        public static readonly List<GPGFormat> Supported = [
            new GPGFormat("OPENPGP", "openpgp", "DEFAULT", "gpg", true),
            new GPGFormat("X.509", "x509", "", "gpgsm", true),
            new GPGFormat("SSH", "ssh", "Requires Git >= 2.34.0", "ssh-keygen", false),
        ];
    }
}
