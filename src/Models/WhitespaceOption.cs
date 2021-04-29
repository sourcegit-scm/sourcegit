using System.Collections.Generic;

namespace SourceGit.Models {
    /// <summary>
    ///     应用补丁时空白字符的处理方式
    /// </summary>
    public class WhitespaceOption {
        public string Name { get; set; }
        public string Desc { get; set; }
        public string Arg { get; set; }

        public static List<WhitespaceOption> Supported = new List<WhitespaceOption>() {
            new WhitespaceOption("Apply.NoWarn", "Apply.NoWarn.Desc", "nowarn"),
            new WhitespaceOption("Apply.Warn", "Apply.Warn.Desc", "warn"),
            new WhitespaceOption("Apply.Error", "Apply.Error.Desc", "error"),
            new WhitespaceOption("Apply.ErrorAll", "Apply.ErrorAll.Desc", "error-all")
        };

        public WhitespaceOption(string n, string d, string a) {
            Name = App.Text(n);
            Desc = App.Text(d);
            Arg = a;
        }
    }
}
