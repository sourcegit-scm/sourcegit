using System.Globalization;
using System.IO;
using System.Windows.Controls;

namespace SourceGit.Views.Validations {
    public class CloneDir : ValidationRule {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            return Directory.Exists(value as string) 
                ? ValidationResult.ValidResult 
                : new ValidationResult(false, App.Text("BadCloneFolder"));
        }
    }
}
