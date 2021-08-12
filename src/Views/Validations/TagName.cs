using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace SourceGit.Views.Validations {
    public class TagName : ValidationRule {
        private static readonly Regex REG_FORMAT = new Regex(@"^[\w\-\.]+$");

        public List<Models.Tag> Tags { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            var name = value as string;
            if (string.IsNullOrEmpty(name)) return new ValidationResult(false, App.Text("EmptyTagName"));
            if (!REG_FORMAT.IsMatch(name)) return new ValidationResult(false, App.Text("BadTagName"));

            foreach (var t in Tags) {
                if (t.Name == name) {
                    return new ValidationResult(false, App.Text("DuplicatedTagName"));
                }
            }

            return ValidationResult.ValidResult;
        }
    }
}