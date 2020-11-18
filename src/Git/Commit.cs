using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SourceGit.Git {

    /// <summary>
    ///     Git commit information.
    /// </summary>
    public class Commit {
        private static readonly string GPGSIG_START = "gpgsig -----BEGIN PGP SIGNATURE-----";
        private static readonly string GPGSIG_END = " -----END PGP SIGNATURE-----";
        private static readonly Regex REG_TESTBINARY = new Regex(@"^\-\s+\-\s+.*$");

        /// <summary>
        ///     Object in commit.
        /// </summary>
        public class Object {
            public enum Type {
                Tag,
                Blob,
                Tree,
                Commit,
            }

            public string Path { get; set; }
            public Type Kind { get; set; }
            public string SHA { get; set; }
        }

        /// <summary>
        ///     SHA
        /// </summary>
        public string SHA { get; set; }

        /// <summary>
        ///     Short SHA.
        /// </summary>
        public string ShortSHA => SHA.Substring(0, 8);

        /// <summary>
        ///     Parent commit SHAs.
        /// </summary>
        public List<string> Parents { get; set; } = new List<string>();

        /// <summary>
        ///     Author
        /// </summary>
        public User Author { get; set; } = new User();

        /// <summary>
        ///     Committer.
        /// </summary>
        public User Committer { get; set; } = new User();

        /// <summary>
        ///     Subject
        /// </summary>
        public string Subject { get; set; } = "";

        /// <summary>
        ///     Extra message.
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        ///     HEAD commit?
        /// </summary>
        public bool IsHEAD { get; set; } = false;

        /// <summary>
        ///     Merged in current branch?
        /// </summary>
        public bool IsMerged { get; set; } = false;

        /// <summary>
        ///     X offset in graph
        /// </summary>
        public double GraphOffset { get; set; } = 0;

        /// <summary>
        ///     Has decorators.
        /// </summary>
        public bool HasDecorators => Decorators.Count > 0;

        /// <summary>
        ///     Decorators.
        /// </summary>
        public List<Decorator> Decorators { get; set; } = new List<Decorator>();

        /// <summary>
        ///     Read commits.
        /// </summary>
        /// <param name="repo">Repository</param>
        /// <param name="limit">Limitations</param>
        /// <returns>Parsed commits.</returns>
        public static List<Commit> Load(Repository repo, string limit) {
            List<Commit> commits = new List<Commit>();
            Commit current = null;
            bool bSkippingGpgsig = false;
            bool findHead = false;

            repo.RunCommand("log --date-order --decorate=full --pretty=raw " + limit, line => {
                if (bSkippingGpgsig) {
                    if (line.StartsWith(GPGSIG_END, StringComparison.Ordinal)) bSkippingGpgsig = false;
                    return;
                } else if (line.StartsWith(GPGSIG_START, StringComparison.Ordinal)) {
                    bSkippingGpgsig = true;
                    return;
                }

                if (line.StartsWith("commit ", StringComparison.Ordinal)) {
                    if (current != null) {
                        current.Message = current.Message.TrimEnd();
                        commits.Add(current);
                    }

                    current = new Commit();
                    ParseSHA(current, line.Substring("commit ".Length));
                    if (!findHead) findHead = current.IsHEAD;
                    return;
                }

                if (current == null) return;

                if (line.StartsWith("tree ", StringComparison.Ordinal)) {
                    return;
                } else if (line.StartsWith("parent ", StringComparison.Ordinal)) {
                    current.Parents.Add(line.Substring("parent ".Length));
                } else if (line.StartsWith("author ", StringComparison.Ordinal)) {
                    current.Author.Parse(line);
                } else if (line.StartsWith("committer ", StringComparison.Ordinal)) {
                    current.Committer.Parse(line);
                } else if (string.IsNullOrEmpty(current.Subject)) {
                    current.Subject = line.Trim();
                } else {
                    current.Message += (line.Trim() + "\n");   
                }
            });

            if (current != null) {
                current.Message = current.Message.TrimEnd();
                commits.Add(current);
            }

            if (!findHead && commits.Count > 0) {
                var startInfo = new ProcessStartInfo();
                startInfo.FileName = Preference.Instance.GitExecutable;
                startInfo.Arguments = $"merge-base --is-ancestor {commits[0].SHA} HEAD";
                startInfo.WorkingDirectory = repo.Path;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardOutput = false;
                startInfo.RedirectStandardError = false;

                var proc = new Process() { StartInfo = startInfo };
                proc.Start();
                proc.WaitForExit();

                commits[0].IsMerged = proc.ExitCode == 0;
                proc.Close();
            }

            return commits;
        }

        /// <summary>
        ///     Get changed file list.
        /// </summary>
        /// <param name="repo"></param>
        /// <returns></returns>
        public List<Change> GetChanges(Repository repo) {
            var changes = new List<Change>();
            var regex = new Regex(@"^[MADRC]\d*\s*.*$");

            var errs = repo.RunCommand($"show --name-status {SHA}", line => {
                if (!regex.IsMatch(line)) return;

                var change = Change.Parse(line, true);
                if (change != null) changes.Add(change);
            });

            if (errs != null) App.RaiseError(errs);
            return changes;
        }

        /// <summary>
        ///     Get revision files.
        /// </summary>
        /// <param name="repo"></param>
        /// <returns></returns>
        public List<Object> GetFiles(Repository repo) {
            var files = new List<Object>();
            var test = new Regex(@"^\d+\s+(\w+)\s+([0-9a-f]+)\s+(.*)$");

            var errs = repo.RunCommand($"ls-tree -r {SHA}", line => {
                var match = test.Match(line);
                if (!match.Success) return;

                var obj = new Object();
                obj.Path = match.Groups[3].Value;
                obj.Kind = Object.Type.Blob;
                obj.SHA = match.Groups[2].Value;

                switch (match.Groups[1].Value) {
                case "tag": obj.Kind = Object.Type.Tag; break;
                case "blob": obj.Kind = Object.Type.Blob; break;
                case "tree": obj.Kind = Object.Type.Tree; break;
                case "commit": obj.Kind = Object.Type.Commit; break;
                }

                files.Add(obj);
            });

            if (errs != null) App.RaiseError(errs);
            return files;
        }

        /// <summary>
        ///     Get file content.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public string GetTextFileContent(Repository repo, string file, out bool isBinary) {
            var data = new List<string>();
            var count = 0;
            var binary = false;

            repo.RunCommand($"diff 4b825dc642cb6eb9a060e54bf8d69288fbee4904 {SHA} --numstat -- \"{file}\"", line => {
                if (REG_TESTBINARY.IsMatch(line)) binary = true;
            });

            if (!binary) {
                var errs = repo.RunCommand($"show {SHA}:\"{file}\"", line => {
                    if (binary) return;

                    count++;
                    if (data.Count >= 1000) return;

                    if (line.IndexOf('\0') >= 0) {
                        binary = true;
                        data.Clear();
                        data.Add("BINARY FILE PREVIEW NOT SUPPORTED!");
                        return;
                    }

                    data.Add(line);
                });

                if (errs != null) App.RaiseError(errs);
            }            

            if (!binary && count > 1000) {
                data.Add("...");
                data.Add($"Total {count} lines. Hide {count-1000} lines.");
            }

            isBinary = binary;            
            return string.Join("\n", data);
        }

        /// <summary>
        ///     Save file to.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="file"></param>
        /// <param name="saveTo"></param>
        public void SaveFileTo(Repository repo, string file, string saveTo) {
            var tmp = Path.GetTempFileName();
            var bat = tmp + ".bat";
            var cmd = "";

            if (repo.IsLFSFiltered(file)) {
                cmd += $"git --no-pager show {SHA}:\"{file}\" > {tmp}.lfs\n";
                cmd += $"git --no-pager lfs smudge < {tmp}.lfs > {saveTo}\n";
            } else {
                cmd = $"git --no-pager show {SHA}:\"{file}\" > {saveTo}\n";
            }

            File.WriteAllText(bat, cmd);

            var starter = new ProcessStartInfo();
            starter.FileName = bat;
            starter.WorkingDirectory = repo.Path;
            starter.CreateNoWindow = true;
            starter.WindowStyle = ProcessWindowStyle.Hidden;

            var proc = Process.Start(starter);
            proc.WaitForExit();
            proc.Close();

            File.Delete(bat);
        }

        private static void ParseSHA(Commit commit, string data) {
            var decoratorStart = data.IndexOf('(');
            if (decoratorStart < 0) {
                commit.SHA = data.Trim();
                return;
            }

            commit.SHA = data.Substring(0, decoratorStart).Trim();

            var subs = data.Substring(decoratorStart + 1).Split(new char[] { ',', ')', '(' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var sub in subs) {
                var d = sub.Trim();
                if (d.StartsWith("tag: refs/tags/", StringComparison.Ordinal)) {
                    commit.Decorators.Add(new Decorator() {
                        Type = DecoratorType.Tag,
                        Name = d.Substring(15).Trim()
                    });
                } else if (d.EndsWith("/HEAD")) {
                    continue;
                } else if (d.StartsWith("HEAD -> refs/heads/", StringComparison.Ordinal)) {
                    commit.IsHEAD = true;
                    commit.Decorators.Add(new Decorator() {
                        Type = DecoratorType.CurrentBranchHead,
                        Name = d.Substring(19).Trim()
                    });
                } else if (d.StartsWith("refs/heads/", StringComparison.Ordinal)) {
                    commit.Decorators.Add(new Decorator() {
                        Type = DecoratorType.LocalBranchHead,
                        Name = d.Substring(11).Trim()
                    });
                } else if (d.StartsWith("refs/remotes/", StringComparison.Ordinal)) {
                    commit.Decorators.Add(new Decorator() {
                        Type = DecoratorType.RemoteBranchHead,
                        Name = d.Substring(13).Trim()
                    });
                }
            }
        }
    }
}
