using System.Collections.Generic;

namespace SourceGit.Models {

    /// <summary>
    ///     支持的语言
    /// </summary>
    public class Locale {
        public string Name { get; set; }
        public string Resource { get; set; }

        public static List<Locale> Supported = new List<Locale>() {
            new Locale("English", "en_US"),
            new Locale("简体中文", "zh_CN"),
        };

        public Locale(string name, string res) {
            Name = name;
            Resource = res;
        }
    }
}
