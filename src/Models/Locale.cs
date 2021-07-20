using System;
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

        public static void Change() {
            var lang = Preference.Instance.General.Locale;
            foreach (var rs in App.Current.Resources.MergedDictionaries) {
                if (rs.Source != null && rs.Source.OriginalString.StartsWith("pack://application:,,,/Resources/Locales/", StringComparison.Ordinal)) {
                    rs.Source = new Uri($"pack://application:,,,/Resources/Locales/{lang}.xaml", UriKind.Absolute);
                    break;
                }
            }
        }
    }
}
