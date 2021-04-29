using System.Collections.Generic;

namespace SourceGit.Models {

    /// <summary>
    ///     自动换行处理方式
    /// </summary>
    public class CRLFOption {
        public string Display { get; set; }
        public string Value { get; set; }
        public string Desc { get; set; }

        public static List<CRLFOption> Supported = new List<CRLFOption>() {
            new CRLFOption("TRUE", "true", "Commit as LF, checkout as CRLF"),
            new CRLFOption("INPUT", "input", "Only convert for commit"),
            new CRLFOption("FALSE", "false", "Do NOT convert"),
        };

        public CRLFOption(string display, string value, string desc) {
            Display = display;
            Value = value;
            Desc = desc;
        }
    }
}
