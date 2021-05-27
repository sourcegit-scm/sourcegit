using System.Globalization;
using System.Windows.Controls;

namespace SourceGit.Views.Validations {
    public class ArchiveFile : ValidationRule {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            var path = value as string;
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".zip")) return new ValidationResult(false, App.Text("BadArchiveFile"));
            return ValidationResult.ValidResult;
        }
    }
}