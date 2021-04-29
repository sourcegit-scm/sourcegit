using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace SourceGit.Views.Validations {
    public class LocalRepositoryName : ValidationRule {
        private static readonly Regex REG_FORMAT = new Regex(@"^[\w\-]+$");

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            var name = value as string;
            if (string.IsNullOrEmpty(name)) return ValidationResult.ValidResult;
            if (!REG_FORMAT.IsMatch(name)) return new ValidationResult(false, App.Text("BadLocalName"));
            return ValidationResult.ValidResult;
        }
    }
}
