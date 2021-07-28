using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {
    /// <summary>
    ///     Diff命令（用于文件文件比对）
    /// </summary>
    public class Diff : Command {
        private static readonly Regex REG_INDICATOR = new Regex(@"^@@ \-(\d+),?\d* \+(\d+),?\d* @@");
        private Models.TextChanges changes = new Models.TextChanges();
        private List<Models.TextChanges.Line> deleted = new List<Models.TextChanges.Line>();
        private List<Models.TextChanges.Line> added = new List<Models.TextChanges.Line>();
        private int oldLine = 0;
        private int newLine = 0;

        public Diff(string repo, string args) {
            Cwd = repo;
            Args = $"diff --ignore-cr-at-eol {args}";
        }

        public Models.TextChanges Result() {
            Exec();
            ProcessChanges();
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
                    ProcessChanges();
                    changes.Lines.Add(new Models.TextChanges.Line(Models.TextChanges.LineMode.Normal, "", $"{oldLine}", $"{newLine}"));
                    oldLine++;
                    newLine++;
                    return;
                }

                var ch = line[0];
                if (ch == '-') {
                    deleted.Add(new Models.TextChanges.Line(Models.TextChanges.LineMode.Deleted, line.Substring(1), $"{oldLine}", ""));
                    oldLine++;
                } else if (ch == '+') {
                    added.Add(new Models.TextChanges.Line(Models.TextChanges.LineMode.Added, line.Substring(1), "", $"{newLine}"));
                    newLine++;
                } else if (ch != '\\') {
                    ProcessChanges();
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
    
        private void ProcessChanges() {
            if (deleted.Count > 0) {
                if (added.Count == deleted.Count) {
                    for (int i = added.Count - 1; i >= 0; i--) {
                        var left = deleted[i];
                        var right = added[i];
                        var result = DiffPlex.Differ.Instance.CreateCharacterDiffs(left.Content, right.Content, false, false);
                        foreach (var block in result.DiffBlocks) {
                            left.Highlights.Add(new Models.TextChanges.HighlightRange(block.DeleteStartA, block.DeleteCountA));
                            right.Highlights.Add(new Models.TextChanges.HighlightRange(block.InsertStartB, block.InsertCountB));
                        }
                    }
                }

                changes.Lines.AddRange(deleted);
                deleted.Clear();
            }

            if (added.Count > 0) {
                changes.Lines.AddRange(added);
                added.Clear();
            }
        }
    }
}
