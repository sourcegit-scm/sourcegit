using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Git {

    /// <summary>
    ///     Git stash
    /// </summary>
    public class Stash {

        /// <summary>
        ///     SHA for this stash
        /// </summary>
        public string SHA { get; set; }

        /// <summary>
        ///     Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Author
        /// </summary>
        public User Author { get; set; } = new User();

        /// <summary>
        ///     Message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        ///     Stash push.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="includeUntracked"></param>
        /// <param name="message"></param>
        /// <param name="files"></param>
        public static void Push(Repository repo, bool includeUntracked, string message, List<string> files) {
            string specialFiles = "";

            if (files.Count > 0) {
                specialFiles = " --";
                foreach (var f in files) specialFiles += $" \"{f}\"";
            }

            string args = "stash push ";
            if (includeUntracked) args += "-u ";
            if (!string.IsNullOrEmpty(message)) args += $"-m \"{message}\" ";

            var errs = repo.RunCommand(args + specialFiles, null);
            if (errs != null) App.RaiseError(errs);
        }

        /// <summary>
        ///     Get changed file list in this stash.
        /// </summary>
        /// <param name="repo"></param>
        /// <returns></returns>
        public List<Change> GetChanges(Repository repo) {
            List<Change> changes = new List<Change>();

            var errs = repo.RunCommand($"diff --name-status --pretty=format: {SHA}^ {SHA}", line => {
                var change = Change.Parse(line);
                if (change != null) changes.Add(change);
            });

            if (errs != null) App.RaiseError(errs);
            return changes;
        }

        /// <summary>
        ///     Apply stash.
        /// </summary>
        /// <param name="repo"></param>
        public void Apply(Repository repo) {
            var errs = repo.RunCommand($"stash apply -q {Name}", null);
            if (errs != null) App.RaiseError(errs);
        }

        /// <summary>
        ///     Pop stash
        /// </summary>
        /// <param name="repo"></param>
        public void Pop(Repository repo) {
            var errs = repo.RunCommand($"stash pop -q {Name}", null);
            if (errs != null) App.RaiseError(errs);
        }

        /// <summary>
        ///     Drop stash
        /// </summary>
        /// <param name="repo"></param>
        public void Drop(Repository repo) {
            var errs = repo.RunCommand($"stash drop -q {Name}", null);
            if (errs != null) App.RaiseError(errs);
        }
    }
}
