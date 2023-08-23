using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace SourceGit.Views.Validations {

    public class GitURL : ValidationRule {
        private static readonly Regex[] VALID_FORMATS = new Regex[] {
            new Regex(@"^http[s]?://([\w\-]+@)?[\w\.\-]+(\:[0-9]+)?/[\w\-]+/[\w\-]+\.git$"),
            new Regex(@"^[\w\-]+@[\w\.\-]+(\:[0-9]+)?:[\w\-]+/[\w\-]+\.git$"),
            new Regex(@"^ssh://([\w\-]+@)?[\w\.\-]+(\:[0-9]+)?/[\w\-]+/[\w\-]+\.git$"),
        };

        public static bool IsSSH(string url) {
            if (string.IsNullOrEmpty(url)) return false;
            
            for (int i = 1; i < VALID_FORMATS.Length; i++) {
                if (VALID_FORMATS[i].IsMatch(url)) return true;
            }

            return false;
        }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            string url = value as string;
            if (!string.IsNullOrEmpty(url)) {
                foreach (var format in VALID_FORMATS) {
                    if (format.IsMatch(url)) return ValidationResult.ValidResult;
                }
            }            

            return new ValidationResult(false, App.Text("BadRemoteUri")); ;
        }
    }
}
