using System.Text.RegularExpressions;

namespace SourceGit.Models {
    public class Remote {
        private static readonly Regex[] URL_FORMATS = [
            new Regex(@"^http[s]?://([\w\-]+@)?[\w\.\-]+(\:[0-9]+)?/[\w\-]+/[\w\-\.]+\.git$"),
            new Regex(@"^[\w\-]+@[\w\.\-]+(\:[0-9]+)?:[\w\-]+/[\w\-\.]+\.git$"),
            new Regex(@"^ssh://([\w\-]+@)?[\w\.\-]+(\:[0-9]+)?/[\w\-]+/[\w\-\.]+\.git$"),
        ];

        public string Name { get; set; }
        public string URL { get; set; }

        public static bool IsSSH(string url) {
            if (string.IsNullOrWhiteSpace(url)) return false;

            for (int i = 1; i < URL_FORMATS.Length; i++) {
                if (URL_FORMATS[i].IsMatch(url)) return true;
            }

            return false;
        }

        public static bool IsValidURL(string url) {
            foreach (var fmt in URL_FORMATS) {
                if (fmt.IsMatch(url)) return true;
            }
            return false;
        }
    }
}
