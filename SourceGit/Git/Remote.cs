using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Git {
    
    /// <summary>
    ///     Git remote
    /// </summary>
    public class Remote {
        private static readonly Regex FORMAT = new Regex(@"^([\w\.\-]+)\s*(\S+).*$");

        /// <summary>
        ///     Name of this remote
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     URL
        /// </summary>
        public string URL { get; set; }

        /// <summary>
        ///     Parsing remote
        /// </summary>
        /// <param name="repo">Repository</param>
        /// <returns></returns>
        public static List<Remote> Load(Repository repo) {
            var remotes = new List<Remote>();
            var added = new List<string>();

            repo.RunCommand("remote -v", data => {
                var match = FORMAT.Match(data);
                if (!match.Success) return;

                var remote = new Remote() {
                    Name = match.Groups[1].Value,
                    URL = match.Groups[2].Value,
                };

                if (added.Contains(remote.Name)) return;

                added.Add(remote.Name);
                remotes.Add(remote);
            });

            return remotes;
        }

        /// <summary>
        ///     Add new remote
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="name"></param>
        /// <param name="url"></param>
        public static void Add(Repository repo, string name, string url) {
            var errs = repo.RunCommand($"remote add {name} {url}", null);
            if (errs != null) {
                App.RaiseError(errs);
            } else {
                repo.Fetch(new Remote() { Name = name }, "--recurse-submodules=on-demand", true, null);
            }
        }

        /// <summary>
        ///     Delete remote.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="remote"></param>
        public static void Delete(Repository repo, string remote) {
            var errs = repo.RunCommand($"remote remove {remote}", null);
            if (errs != null) App.RaiseError(errs);
        }

        /// <summary>
        ///     Edit remote.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="name"></param>
        /// <param name="url"></param>
        public void Edit(Repository repo, string name, string url) {
            string errs = null;

            if (name != Name) {
                errs = repo.RunCommand($"remote rename {Name} {name}", null);
                if (errs != null) {
                    App.RaiseError(errs);
                    return;
                }
            }

            if (url != URL) {
                errs = repo.RunCommand($"remote set-url {name} {url}", null);
                if (errs != null) App.RaiseError(errs);
            }
        }
    }
}
