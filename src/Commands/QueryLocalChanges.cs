using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class QueryLocalChanges : Command
    {
        [GeneratedRegex(@"^(\s?[\w\?]{1,4})\s+(.+)$")]
        private static partial Regex REG_FORMAT();
        private bool _includeUntracked;
        private bool _useFastPathForUntrackedFiles;

        public QueryLocalChanges(string repo, bool includeUntracked = true, bool useFastPathForUntrackedFiles = false)
        {
            WorkingDirectory = repo;
            Context = repo;
            _includeUntracked = includeUntracked;
            _useFastPathForUntrackedFiles = useFastPathForUntrackedFiles;
        }

        private async Task<(List<Models.Change>, List<string>)> RunGitAndParseOutput()
        {
            var outChanges = new List<Models.Change>();
            var outUntrackedDirs = new List<string>();

            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    var match = REG_FORMAT().Match(line);
                    if (!match.Success)
                        continue;

                    var change = new Models.Change() { Path = match.Groups[2].Value };
                    var status = match.Groups[1].Value;

                    switch (status)
                    {
                        case " M":
                            change.Set(Models.ChangeState.None, Models.ChangeState.Modified);
                            break;
                        case " T":
                            change.Set(Models.ChangeState.None, Models.ChangeState.TypeChanged);
                            break;
                        case " A":
                            change.Set(Models.ChangeState.None, Models.ChangeState.Added);
                            break;
                        case " D":
                            change.Set(Models.ChangeState.None, Models.ChangeState.Deleted);
                            break;
                        case " R":
                            change.Set(Models.ChangeState.None, Models.ChangeState.Renamed);
                            break;
                        case " C":
                            change.Set(Models.ChangeState.None, Models.ChangeState.Copied);
                            break;
                        case "M":
                            change.Set(Models.ChangeState.Modified);
                            break;
                        case "MM":
                            change.Set(Models.ChangeState.Modified, Models.ChangeState.Modified);
                            break;
                        case "MT":
                            change.Set(Models.ChangeState.Modified, Models.ChangeState.TypeChanged);
                            break;
                        case "MD":
                            change.Set(Models.ChangeState.Modified, Models.ChangeState.Deleted);
                            break;
                        case "T":
                            change.Set(Models.ChangeState.TypeChanged);
                            break;
                        case "TM":
                            change.Set(Models.ChangeState.TypeChanged, Models.ChangeState.Modified);
                            break;
                        case "TT":
                            change.Set(Models.ChangeState.TypeChanged, Models.ChangeState.TypeChanged);
                            break;
                        case "TD":
                            change.Set(Models.ChangeState.TypeChanged, Models.ChangeState.Deleted);
                            break;
                        case "A":
                            change.Set(Models.ChangeState.Added);
                            break;
                        case "AM":
                            change.Set(Models.ChangeState.Added, Models.ChangeState.Modified);
                            break;
                        case "AT":
                            change.Set(Models.ChangeState.Added, Models.ChangeState.TypeChanged);
                            break;
                        case "AD":
                            change.Set(Models.ChangeState.Added, Models.ChangeState.Deleted);
                            break;
                        case "D":
                            change.Set(Models.ChangeState.Deleted);
                            break;
                        case "R":
                            change.Set(Models.ChangeState.Renamed);
                            break;
                        case "RM":
                            change.Set(Models.ChangeState.Renamed, Models.ChangeState.Modified);
                            break;
                        case "RT":
                            change.Set(Models.ChangeState.Renamed, Models.ChangeState.TypeChanged);
                            break;
                        case "RD":
                            change.Set(Models.ChangeState.Renamed, Models.ChangeState.Deleted);
                            break;
                        case "C":
                            change.Set(Models.ChangeState.Copied);
                            break;
                        case "CM":
                            change.Set(Models.ChangeState.Copied, Models.ChangeState.Modified);
                            break;
                        case "CT":
                            change.Set(Models.ChangeState.Copied, Models.ChangeState.TypeChanged);
                            break;
                        case "CD":
                            change.Set(Models.ChangeState.Copied, Models.ChangeState.Deleted);
                            break;
                        case "DD":
                            change.ConflictReason = Models.ConflictReason.BothDeleted;
                            change.Set(Models.ChangeState.None, Models.ChangeState.Conflicted);
                            break;
                        case "AU":
                            change.ConflictReason = Models.ConflictReason.AddedByUs;
                            change.Set(Models.ChangeState.None, Models.ChangeState.Conflicted);
                            break;
                        case "UD":
                            change.ConflictReason = Models.ConflictReason.DeletedByThem;
                            change.Set(Models.ChangeState.None, Models.ChangeState.Conflicted);
                            break;
                        case "UA":
                            change.ConflictReason = Models.ConflictReason.AddedByThem;
                            change.Set(Models.ChangeState.None, Models.ChangeState.Conflicted);
                            break;
                        case "DU":
                            change.ConflictReason = Models.ConflictReason.DeletedByUs;
                            change.Set(Models.ChangeState.None, Models.ChangeState.Conflicted);
                            break;
                        case "AA":
                            change.ConflictReason = Models.ConflictReason.BothAdded;
                            change.Set(Models.ChangeState.None, Models.ChangeState.Conflicted);
                            break;
                        case "UU":
                            change.ConflictReason = Models.ConflictReason.BothModified;
                            change.Set(Models.ChangeState.None, Models.ChangeState.Conflicted);
                            break;
                        case "??":
                            change.Set(Models.ChangeState.None, Models.ChangeState.Untracked);
                            break;
                    }

                    if (change.WorkTree == Models.ChangeState.Untracked && change.Path.EndsWith("/"))
                    {
                        outUntrackedDirs.Add(change.Path);
                    }
                    else
                    {
                        if (change.Index != Models.ChangeState.None || change.WorkTree != Models.ChangeState.None)
                            outChanges.Add(change);
                    }
                }
            }
            catch
            {
                // Ignore exceptions.
            }

            return (outChanges, outUntrackedDirs);
        }
        public async Task<List<Models.Change>> GetResultAsync()
        {
            if (!_useFastPathForUntrackedFiles)
            {
                Args = $"--no-optional-locks status -u{(_includeUntracked ? "all" : "no")} --ignore-submodules=dirty --porcelain";
                var (changes, _) = await RunGitAndParseOutput().ConfigureAwait(false);
                return changes;
            }
            else
            {
                // Collect untracked dirs
                Args = $"--no-optional-locks status --ignore-submodules=dirty --porcelain";
                var (changes, untrackedDirs) = await RunGitAndParseOutput().ConfigureAwait(false);

                // 'git status' does not support pathspec-from-file
                for (int i = 0; i < untrackedDirs.Count; i += 32)
                {
                    var count = Math.Min(32, untrackedDirs.Count - i);
                    var step = untrackedDirs.GetRange(i, count);

                    Args = $"--no-optional-locks status -uall --ignore-submodules=dirty --porcelain --";
                    foreach (var dir in step)
                    {
                        Args += $" \"{dir}\"";
                    }

                    var (stepChanges, dirs) = await RunGitAndParseOutput().ConfigureAwait(false);
                    Debug.Assert(dirs.Count == 0);
                    changes.AddRange(stepChanges);
                }

                return changes;
            }
        }
    }
}
