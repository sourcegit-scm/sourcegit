using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Git {
    
    /// <summary>
    ///     Git branch
    /// </summary>
    public class Branch {
        private static readonly string PRETTY_FORMAT = @"$%(refname)$%(objectname)$%(HEAD)$%(upstream)$%(upstream:track)$%(contents:subject)";
        private static readonly Regex PARSE = new Regex(@"\$(.*)\$(.*)\$([\* ])\$(.*)\$(.*?)\$(.*)");
        private static readonly Regex AHEAD = new Regex(@"ahead (\d+)");
        private static readonly Regex BEHIND = new Regex(@"behind (\d+)");

        /// <summary>
        ///     Branch type.
        /// </summary>
        public enum Type {
            Normal,
            Feature,
            Release,
            Hotfix,
        }

        /// <summary>
        ///     Branch name
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        ///     Full name.
        /// </summary>
        public string FullName { get; set; } = "";

        /// <summary>
        ///     Head ref
        /// </summary>
        public string Head { get; set; } = "";

        /// <summary>
        ///     Subject for head ref.
        /// </summary>
        public string HeadSubject { get; set; } = "";

        /// <summary>
        ///     Is local branch
        /// </summary>
        public bool IsLocal { get; set; } = false;

        /// <summary>
        ///     Branch type.
        /// </summary>
        public Type Kind { get; set; } = Type.Normal;

        /// <summary>
        ///     Remote name. Only used for remote branch
        /// </summary>
        public string Remote { get; set; } = "";

        /// <summary>
        ///     Upstream. Only used for local branches.
        /// </summary>
        public string Upstream { get; set; }

        /// <summary>
        ///     Track information for upstream. Only used for local branches.
        /// </summary>
        public string UpstreamTrack { get; set; }

        /// <summary>
        ///     Is current branch. Only used for local branches.
        /// </summary>
        public bool IsCurrent { get; set; }

        /// <summary>
        ///     Is this branch's HEAD same with upstream? 
        /// </summary>
        public bool IsSameWithUpstream => string.IsNullOrEmpty(UpstreamTrack);

        /// <summary>
        ///     Enable filter in log histories.
        /// </summary>
        public bool IsFiltered { get; set; }

        /// <summary>
        ///     Load branches.
        /// </summary>
        /// <param name="repo"></param>
        public static List<Branch> Load(Repository repo) {
            var localPrefix = "refs/heads/";
            var remotePrefix = "refs/remotes/";
            var branches = new List<Branch>();
            var remoteBranches = new List<string>();

            repo.RunCommand("branch -l --all -v --format=\"" + PRETTY_FORMAT + "\"", line => {
                var match = PARSE.Match(line);
                if (!match.Success) return;

                var branch = new Branch();
                var refname = match.Groups[1].Value;
                if (refname.EndsWith("/HEAD")) return;

                if (refname.StartsWith(localPrefix, StringComparison.Ordinal)) {
                    branch.Name = refname.Substring(localPrefix.Length);
                    branch.IsLocal = true;
                } else if (refname.StartsWith(remotePrefix, StringComparison.Ordinal)) {
                    var name = refname.Substring(remotePrefix.Length);
                    branch.Remote = name.Substring(0, name.IndexOf('/'));
                    branch.Name = name;
                    branch.IsLocal = false;
                    remoteBranches.Add(refname);
                }

                branch.FullName = refname;
                branch.Head = match.Groups[2].Value;
                branch.IsCurrent = match.Groups[3].Value == "*";
                branch.Upstream = match.Groups[4].Value;
                branch.UpstreamTrack = ParseTrack(match.Groups[5].Value);
                branch.HeadSubject = match.Groups[6].Value;

                branches.Add(branch);
            });

            // Fixed deleted remote branch
            foreach (var b in branches) {
                if (!string.IsNullOrEmpty(b.Upstream) && !remoteBranches.Contains(b.Upstream)) {
                    b.Upstream = null;
                }
            }

            return branches;
        }

        /// <summary>
        ///     Create new branch.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="name"></param>
        /// <param name="startPoint"></param>
        public static void Create(Repository repo, string name, string startPoint) {
            var errs = repo.RunCommand($"branch {name} {startPoint}", null);
            if (errs != null) App.RaiseError(errs);
        }

        /// <summary>
        ///     Rename branch
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="name"></param>
        public void Rename(Repository repo, string name) {
            var errs = repo.RunCommand($"branch -M {Name} {name}", null);
            if (errs != null) App.RaiseError(errs);
        }

        /// <summary>
        ///     Delete branch.
        /// </summary>
        /// <param name="repo"></param>
        public void Delete(Repository repo) {
            string errs = null;

            if (!IsLocal) {
                errs = repo.RunCommand($"-c credential.helper=manager push {Remote} --delete {Name.Substring(Name.IndexOf('/')+1)}", null);
            } else {
                errs = repo.RunCommand($"branch -D {Name}", null);
            }

            if (errs != null) App.RaiseError(errs);
        } 

        private static string ParseTrack(string data) {
            if (string.IsNullOrEmpty(data)) return "";

            string track = "";

            var ahead = AHEAD.Match(data);
            if (ahead.Success) {
                track += ahead.Groups[1].Value + "↑ ";
            }

            var behind = BEHIND.Match(data);
            if (behind.Success) {
                track += behind.Groups[1].Value + "↓";
            }

            return track.Trim();
        }
    }
}
