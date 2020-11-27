using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceGit.Git {

    /// <summary>
    ///     Diff helper.
    /// </summary>
    public class Diff {
        private static readonly Regex REG_INDICATOR = new Regex(@"^@@ \-(\d+),?\d* \+(\d+),?\d* @@", RegexOptions.None);

        /// <summary>
        ///     Line mode.
        /// </summary>
        public enum LineMode {
            None,
            Normal,
            Indicator,
            Added,
            Deleted,
        }

        /// <summary>
        ///     Line change.
        /// </summary>
        public class LineChange {
            public LineMode Mode = LineMode.Normal;
            public string Content = "";
            public string OldLine = "";
            public string NewLine = "";

            public LineChange(LineMode mode, string content, string oldLine = "", string newLine = "") {
                Mode = mode;
                Content = content;
                OldLine = oldLine;
                NewLine = newLine;
            }
        }

        /// <summary>
        ///     Text change.
        /// </summary>
        public class TextChange {
            public List<LineChange> Lines = new List<LineChange>();
            public bool IsBinary = false;
        }

        /// <summary>
        ///     Binary change.
        /// </summary>
        public class BinaryChange {
            public long Size = 0;
            public long PreSize = 0;
        }

        /// <summary>
        ///     Change for LFS object information.
        /// </summary>
        public class LFSChange {
            public LFSObject Old;
            public LFSObject New;
            public bool IsValid => Old != null || New != null;
        }

        /// <summary>
        ///     Run diff process.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static TextChange GetTextChange(Repository repo, string args) {
            var rs = new TextChange();
            var started = false;
            var oldLine = 0;
            var newLine = 0;

            repo.RunCommand($"diff --ignore-cr-at-eol {args}", line => {
                if (rs.IsBinary) return;

                if (!started) {
                    var match = REG_INDICATOR.Match(line);
                    if (!match.Success) {
                        if (line.StartsWith("Binary ")) rs.IsBinary = true;
                        return;
                    }

                    started = true;
                    oldLine = int.Parse(match.Groups[1].Value);
                    newLine = int.Parse(match.Groups[2].Value);
                    rs.Lines.Add(new LineChange(LineMode.Indicator, line));
                } else {
                    if (line[0] == '-') {
                        rs.Lines.Add(new LineChange(LineMode.Deleted, line.Substring(1), $"{oldLine}", ""));
                        oldLine++;
                    } else if (line[0] == '+') {
                        rs.Lines.Add(new LineChange(LineMode.Added, line.Substring(1), "", $"{newLine}"));
                        newLine++;
                    } else if (line[0] == '\\') {
                        // IGNORE \ No new line end of file.
                    } else {
                        var match = REG_INDICATOR.Match(line);
                        if (match.Success) {
                            oldLine = int.Parse(match.Groups[1].Value);
                            newLine = int.Parse(match.Groups[2].Value);
                            rs.Lines.Add(new LineChange(LineMode.Indicator, line));
                        } else {
                            rs.Lines.Add(new LineChange(LineMode.Normal, line.Substring(1), $"{oldLine}", $"{newLine}"));
                            oldLine++;
                            newLine++;
                        }
                    }
                }
            });

            if (rs.IsBinary) rs.Lines.Clear();
            return rs;
        }

        /// <summary>
        ///     Get file size changes for binary file.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="revisions"></param>
        /// <param name="path"></param>
        /// <param name="orgPath"></param>
        /// <returns></returns>
        public static BinaryChange GetSizeChange(Repository repo, string[] revisions, string path, string orgPath = null) {
            var change = new BinaryChange();

            if (revisions.Length == 0) { // Compare working copy with HEAD
                change.Size = new FileInfo(Path.Combine(repo.Path, path)).Length;
                change.PreSize = repo.GetFileSize("HEAD", path);
            } else if (revisions.Length == 1) { // Compare HEAD with given revision.
                change.Size = repo.GetFileSize("HEAD", path);
                if (!string.IsNullOrEmpty(orgPath)) {
                    change.PreSize = repo.GetFileSize(revisions[0], orgPath);
                } else {
                    change.PreSize = repo.GetFileSize(revisions[0], path);
                }
            } else {
                change.Size = repo.GetFileSize(revisions[1], path);
                if (!string.IsNullOrEmpty(orgPath)) {
                    change.PreSize = repo.GetFileSize(revisions[0], orgPath);
                } else {
                    change.PreSize = repo.GetFileSize(revisions[0], path);
                }
            }

            return change;
        }

        /// <summary>
        ///     Get LFS object changes.
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static LFSChange GetLFSChange(Repository repo, string args) {
            var rc = new LFSChange();

            repo.RunCommand($"diff --ignore-cr-at-eol {args}", line => {
                if (line[0] == '-') {
                    if (rc.Old == null) rc.Old = new LFSObject();
                    line = line.Substring(1);
                    if (line.StartsWith("oid sha256:")) {
                        rc.Old.OID = line.Substring(11);
                    } else if (line.StartsWith("size ")) {
                        rc.Old.Size = int.Parse(line.Substring(5));
                    }
                } else if (line[0] == '+') {
                    if (rc.New == null) rc.New = new LFSObject();
                    line = line.Substring(1);
                    if (line.StartsWith("oid sha256:")) {
                        rc.New.OID = line.Substring(11);
                    } else if (line.StartsWith("size ")) {
                        rc.New.Size = int.Parse(line.Substring(5));
                    }
                } else if (line.StartsWith(" size ")) {
                    rc.New.Size = rc.Old.Size = int.Parse(line.Substring(6));
                }
            });

            return rc;
        }
    }
}
