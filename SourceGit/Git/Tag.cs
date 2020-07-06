using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SourceGit.Git {

    /// <summary>
    ///     Git tag.
    /// </summary>
    public class Tag {
        private static readonly Regex FORMAT = new Regex(@"\$(.*)\$(.*)\$(.*)");

        /// <summary>
        ///     SHA
        /// </summary>
        public string SHA { get; set; }

        /// <summary>
        ///     Display name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Enable filter in log histories.
        /// </summary>
        public bool IsFiltered { get; set; }

        /// <summary>
        ///     Load all tags
        /// </summary>
        /// <param name="repo"></param>
        /// <returns></returns>
        public static List<Tag> Load(Repository repo) {
            var args = "for-each-ref --sort=-creatordate --format=\"$%(refname:short)$%(objectname)$%(*objectname)\" refs/tags";
            var tags = new List<Tag>();

            repo.RunCommand(args, line => {
                var match = FORMAT.Match(line);
                if (!match.Success) return;

                var name = match.Groups[1].Value;
                var commit = match.Groups[2].Value;
                var dereference = match.Groups[3].Value;

                if (string.IsNullOrEmpty(dereference)) {
                    tags.Add(new Tag() {
                        Name = name,
                        SHA = commit,
                    });
                } else {
                    tags.Add(new Tag() {
                        Name = name,
                        SHA = dereference,
                    });
                }
            });

            return tags;
        }

        /// <summary>
        ///     Add new tag.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="name"></param>
        /// <param name="startPoint"></param>
        /// <param name="message"></param>
        public static void Add(Repository repo, string name, string startPoint, string message) {
            var args = $"tag -a {name} {startPoint} ";

            if (!string.IsNullOrEmpty(message)) {
                string temp = Path.GetTempFileName();
                File.WriteAllText(temp, message);
                args += $"-F \"{temp}\"";
            } else {
                args += $"-m {name}";
            }

            var errs = repo.RunCommand(args, null);
            if (errs != null) App.RaiseError(errs);
            else repo.OnCommitsChanged?.Invoke();
        }

        /// <summary>
        ///     Delete tag.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="name"></param>
        /// <param name="push"></param>
        public static void Delete(Repository repo, string name, bool push) {
            var errs = repo.RunCommand($"tag --delete {name}", null);
            if (errs != null) {
                App.RaiseError(errs);
                return; 
            }

            if (push) {
                var remotes = repo.Remotes();
                foreach (var r in remotes) {
                    repo.RunCommand($"-c credential.helper=manager push --delete {r.Name} refs/tags/{name}", null);
                }
            }

            repo.OnCommitsChanged?.Invoke();
        }

        /// <summary>
        ///     Push tag to remote.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="name"></param>
        /// <param name="remote"></param>
        public static void Push(Repository repo, string name, string remote) {
            var errs = repo.RunCommand($"-c credential.helper=manager push {remote} refs/tags/{name}", null);
            if (errs != null) App.RaiseError(errs);
        }
    }
}
