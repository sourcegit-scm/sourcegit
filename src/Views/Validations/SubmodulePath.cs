using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace SourceGit.Views.Validations {
    public class SubmodulePath : ValidationRule {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            var path = value as string;
            if (string.IsNullOrEmpty(path)) return ValidationResult.ValidResult;

            var regex = new Regex(@"^[\w\-\._/]+$");
            var succ = regex.IsMatch(path.Trim());
            return !succ ? new ValidationResult(false, App.Text("BadSubmodulePath")) : ValidationResult.ValidResult;
        }
    }
}