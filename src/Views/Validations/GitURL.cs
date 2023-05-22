using System;
using System.Globalization;
using System.Windows.Controls;

namespace SourceGit.Views.Validations {

    public class GitURL : ValidationRule {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            string url = value as string;
            bool valid = !string.IsNullOrEmpty(url)
                && (url.StartsWith("http://", StringComparison.Ordinal)
                || url.StartsWith("https://", StringComparison.Ordinal)
                || url.StartsWith("git@", StringComparison.Ordinal)
                || url.StartsWith("file://", StringComparison.Ordinal)
                || url.StartsWith("ssh://", StringComparison.Ordinal));
            return valid ? ValidationResult.ValidResult : new ValidationResult(false, App.Text("BadRemoteUri"));
        }
    }
}
