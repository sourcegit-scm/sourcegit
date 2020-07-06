using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace SourceGit.Helpers {

    /// <summary>
    ///     Validate clone folder.
    /// </summary>
    public class CloneFolderRule : ValidationRule {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            var badPath = "EXISTS and FULL ACCESS CONTROL needed";
            var path = value as string;
            return Directory.Exists(path) ? ValidationResult.ValidResult : new ValidationResult(false, badPath);
        }
    }

    /// <summary>
    ///     Validate git remote URL
    /// </summary>
    public class RemoteUriRule : ValidationRule {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            var badUrl = "Remote git URL not supported";
            return Git.Repository.IsValidUrl(value as string) ? ValidationResult.ValidResult : new ValidationResult(false, badUrl);
        }
    }

    /// <summary>
    ///     Validate tag name.
    /// </summary>
    public class RemoteNameRule : ValidationRule {
        public Git.Repository Repo { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            var regex = new Regex(@"^[\w\-\.]+$");
            var name = value as string;
            var remotes = Repo.Remotes();

            if (string.IsNullOrEmpty(name)) return new ValidationResult(false, "Remote name can NOT be null");
            if (!regex.IsMatch(name)) return new ValidationResult(false, $"Bad name for remote. Regex: ^[\\w\\-\\.]+$");

            foreach (var t in remotes) {
                if (t.Name == name) {
                    return new ValidationResult(false, $"Remote '{name}' already exists");
                }
            }

            return ValidationResult.ValidResult;
        }
    }

    /// <summary>
    ///     Validate branch name.
    /// </summary>
    public class BranchNameRule : ValidationRule {
        public Git.Repository Repo { get; set; }
        public string Prefix { get; set; } = "";

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            var regex = new Regex(@"^[\w\-/\.]+$");
            var name = value as string;
            var branches = Repo.Branches();

            if (string.IsNullOrEmpty(name)) return new ValidationResult(false, "Branch name can NOT be null");
            if (!regex.IsMatch(name)) return new ValidationResult(false, $"Bad name for branch. Regex: ^[\\w\\-/\\.]+$");

            name = Prefix + name;

            foreach (var b in branches) {
                if (b.Name == name) {
                    return new ValidationResult(false, $"Branch '{name}' already exists");
                }
            }

            return ValidationResult.ValidResult;
        }
    }

    /// <summary>
    ///     Validate tag name.
    /// </summary>
    public class TagNameRule : ValidationRule {
        public Git.Repository Repo { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            var regex = new Regex(@"^[\w\-\.]+$");
            var name = value as string;
            var tags = Repo.Tags();

            if (string.IsNullOrEmpty(name)) return new ValidationResult(false, "Tag name can NOT be null");
            if (!regex.IsMatch(name)) return new ValidationResult(false, $"Bad name for tag. Regex: ^[\\w\\-\\.]+$");

            foreach (var t in tags) {
                if (t.Name == name) {
                    return new ValidationResult(false, $"Tag '{name}' already exists");
                }
            }

            return ValidationResult.ValidResult;
        }
    }

    /// <summary>
    ///     Required for commit subject.
    /// </summary>
    public class CommitSubjectRequiredRule : ValidationRule {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            var subject = value as string;
            return string.IsNullOrWhiteSpace(subject) ? new ValidationResult(false, "Commit subject can NOT be empty") : ValidationResult.ValidResult;
        }
    }

    /// <summary>
    ///     Required for patch file.
    /// </summary>
    public class PatchFileRequiredRule : ValidationRule {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            var path = value as string;
            var succ = !string.IsNullOrEmpty(path) && File.Exists(path);
            return !succ ? new ValidationResult(false, "Invalid path for patch file") : ValidationResult.ValidResult;
        }
    }
}
