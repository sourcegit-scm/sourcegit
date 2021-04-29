using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace SourceGit.Views.Validations {
    public class RemoteName : ValidationRule {
        private static readonly Regex REG_FORMAT = new Regex(@"^[\w\-\.]+$");

        public Models.Repository Repo { get; set; }
        public bool IsOptional { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            var name = value as string;
            if (string.IsNullOrEmpty(name)) {
                return IsOptional ? ValidationResult.ValidResult : new ValidationResult(false, App.Text("EmptyRemoteName"));
            }

            if (!REG_FORMAT.IsMatch(name)) return new ValidationResult(false, App.Text("BadRemoteName"));

            if (Repo != null) {
                foreach (var t in Repo.Remotes) {
                    if (t.Name == name) {
                        return new ValidationResult(false, App.Text("DuplicatedRemoteName"));
                    }
                }
            }

            return ValidationResult.ValidResult;
        }
    }
}
