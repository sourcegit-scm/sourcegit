using System.Globalization;
using System.Windows.Controls;

namespace SourceGit.Views.Validations {
    public class CommitMessage : ValidationRule {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            var subject = value as string;
            return string.IsNullOrWhiteSpace(subject) 
                ? new ValidationResult(false, App.Text("EmptyCommitMessage")) 
                : ValidationResult.ValidResult;
        }
    }
}
