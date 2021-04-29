using System.Globalization;
using System.IO;
using System.Windows.Controls;

namespace SourceGit.Views.Validations {
    public class PatchFile : ValidationRule {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            return File.Exists(value as string)
                ? ValidationResult.ValidResult
                : new ValidationResult(false, App.Text("BadPatchFile"));
        }
    }
}