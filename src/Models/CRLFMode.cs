using System.Collections.Generic;

namespace SourceGit.Models
{
    public class CRLFMode
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Desc { get; set; }

        public static readonly List<CRLFMode> Supported = new List<CRLFMode>() {
            new CRLFMode("TRUE", "true", "Commit as LF, checkout as CRLF"),
            new CRLFMode("INPUT", "input", "Only convert for commit"),
            new CRLFMode("FALSE", "false", "Do NOT convert"),
        };

        public CRLFMode(string name, string value, string desc)
        {
            Name = name;
            Value = value;
            Desc = desc;
        }
    }
}
