using System;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    /// <summary>
    ///     Diff命令（用于文件文件比对）
    /// </summary>
    public class Diff : Command {
        private static readonly Regex REG_INDICATOR = new Regex(@"^@@ \-(\d+),?\d* \+(\d+),?\d* @@");
        private Models.TextChanges changes = new Models.TextChanges();
        private int oldLine = 0;
        private int newLine = 0;

        public Diff(string repo, string args) {
            Cwd = repo;
            Args = $"diff --ignore-cr-at-eol {args}";
        }

        public Models.TextChanges Result() {
            Exec();
            if (changes.IsBinary) changes.Lines.Clear();
            return changes;
        }

        public override void OnReadline(string line) {
            if (changes.IsBinary) return;

            if (changes.Lines.Count == 0) {
                var match = REG_INDICATOR.Match(line);
                if (!match.Success) {
                    if (line.StartsWith("Binary", StringComparison.Ordinal)) changes.IsBinary = true;
                    return;
                }

                oldLine = int.Parse(match.Groups[1].Value);
                newLine = int.Parse(match.Groups[2].Value);
                changes.Lines.Add(new Models.TextChanges.Line(Models.TextChanges.LineMode.Indicator, line, "", ""));
            } else {
                if (line.Length == 0) {
                    changes.Lines.Add(new Models.TextChanges.Line(Models.TextChanges.LineMode.Normal, "", $"{oldLine}", $"{newLine}"));
                    oldLine++;
                    newLine++;
                    return;
                }

                var ch = line[0];
                if (ch == '-') {
                    changes.Lines.Add(new Models.TextChanges.Line(Models.TextChanges.LineMode.Deleted, line.Substring(1), $"{oldLine}", ""));
                    oldLine++;
                } else if (ch == '+') {
                    changes.Lines.Add(new Models.TextChanges.Line(Models.TextChanges.LineMode.Added, line.Substring(1), "", $"{newLine}"));
                    newLine++;
                } else if (ch != '\\') {
                    var match = REG_INDICATOR.Match(line);
                    if (match.Success) {
                        oldLine = int.Parse(match.Groups[1].Value);
                        newLine = int.Parse(match.Groups[2].Value);
                        changes.Lines.Add(new Models.TextChanges.Line(Models.TextChanges.LineMode.Indicator, line, "", ""));
                    } else {
                        changes.Lines.Add(new Models.TextChanges.Line(Models.TextChanges.LineMode.Normal, line.Substring(1), $"{oldLine}", $"{newLine}"));
                        oldLine++;
                        newLine++;
                    }
                }
            }
        }
    }
}
