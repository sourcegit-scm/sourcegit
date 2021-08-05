using System.Globalization;
using System.Windows.Controls;

namespace SourceGit.Views.Validations {
    public class Required : ValidationRule {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            var path = value as string;
            return string.IsNullOrEmpty(path) ?
                new ValidationResult(false, App.Text("Required")) :
                ValidationResult.ValidResult;
        }
    }
}
