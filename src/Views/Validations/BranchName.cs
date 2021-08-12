using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace SourceGit.Views.Validations {
    public class BranchName : ValidationRule {
        private static readonly Regex REG_FORMAT = new Regex(@"^[\w\-/\.]+$");

        public Models.Repository Repo { get; set; }
        public string Prefix { get; set; } = "";

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            var name = value as string;
            if (string.IsNullOrEmpty(name)) return new ValidationResult(false, App.Text("EmptyBranchName"));
            if (!REG_FORMAT.IsMatch(name)) return new ValidationResult(false, App.Text("BadBranchName"));

            name = Prefix + name;
            foreach (var t in Repo.Branches) {
                var check = t.IsLocal ? t.Name : $"{t.Remote}/{t.Name}";
                if (check == name) {
                    return new ValidationResult(false, App.Text("DuplicatedBranchName"));
                }
            }

            return ValidationResult.ValidResult;
        }
    }
}