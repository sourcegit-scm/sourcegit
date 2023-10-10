using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    /// <summary>
    ///     逐行追溯
    /// </summary>
    public class Blame : Command {
        private static readonly Regex REG_FORMAT = new Regex(@"^\^?([0-9a-f]+)\s+.*\((.*)\s+(\d+)\s+[\-\+]?\d+\s+\d+\) (.*)");
        private static readonly DateTime UTC_START = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToLocalTime();

        private Data data = new Data();
        private bool needUnifyCommitSHA = false;
        private int minSHALen = 0;

        public class Data {
            public List<Models.BlameLine> Lines = new List<Models.BlameLine>();
            public bool IsBinary = false;
        }

        public Blame(string repo, string file, string revision) {
            Cwd = repo;
            Args = $"blame -t {revision} -- \"{file}\"";
        }

        public Data Result() {
            Exec();

            if (needUnifyCommitSHA) {
                foreach (var line in data.Lines) {
                    if (line.CommitSHA.Length > minSHALen) {
                        line.CommitSHA = line.CommitSHA.Substring(0, minSHALen);
                    }
                }
            }

            return data;
        }

        public override void OnReadline(string line) {
            if (data.IsBinary) return;
            if (string.IsNullOrEmpty(line)) return;

            if (line.IndexOf('\0') >= 0) {
                data.IsBinary = true;
                data.Lines.Clear();
                return;
            }

            var match = REG_FORMAT.Match(line);
            if (!match.Success) return;

            var commit = match.Groups[1].Value;
            var author = match.Groups[2].Value;
            var timestamp = int.Parse(match.Groups[3].Value);
            var content = match.Groups[4].Value;
            var when = UTC_START.AddSeconds(timestamp).ToString("yyyy/MM/dd");

            var blameLine = new Models.BlameLine() {
                LineNumber = $"{data.Lines.Count + 1}",
                CommitSHA = commit,
                Author = author,
                Time = when,
                Content = content,
            };

            if (line[0] == '^') {
                needUnifyCommitSHA = true;
                if (minSHALen == 0) minSHALen = commit.Length;
                else if (commit.Length < minSHALen) minSHALen = commit.Length;
            }

            data.Lines.Add(blameLine);
        }
    }
}
