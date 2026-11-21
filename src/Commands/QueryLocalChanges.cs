using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryLocalChanges : Command
    {
        public QueryLocalChanges(string repo, bool includeUntracked = true, bool noOptionalLocks = true)
        {
            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder();
            if (noOptionalLocks)
                builder.Append("--no-optional-locks ");
            if (includeUntracked)
                builder.Append("-c core.untrackedCache=true -c status.showUntrackedFiles=all status -uall --ignore-submodules=dirty --porcelain=v2 -z");
            else
                builder.Append("status -uno --ignore-submodules=dirty --porcelain=v2 -z");

            Args = builder.ToString();
        }

        public async Task<List<Models.Change>> GetResultAsync()
        {
            var outs = new List<Models.Change>();

            try
            {
                using var proc = new Process();
                var captured = new CapturedProcess() { Process = proc };
                var capturedLock = new object();

                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                if (CancellationToken.CanBeCanceled)
                {
                    CancellationToken.Register(() =>
                    {
                        lock (capturedLock)
                        {
                            if (captured is { Process: { HasExited: false } proc })
                                Native.OS.TerminateProcess(proc);
                        }
                    });
                }

                using var ms = new MemoryStream();
                await proc.StandardOutput.BaseStream.CopyToAsync(ms, CancellationToken).ConfigureAwait(false);

                if (!CancellationToken.IsCancellationRequested && ms.TryGetBuffer(out var buffer))
                {
                    var start = buffer.Offset;
                    var end = buffer.Offset + buffer.Count;
                    var waitingOriginalPath = false;
                    while (start < end)
                    {
                        var lineEnd = Array.IndexOf(buffer.Array, (byte)'\0', start, end - start);
                        var line = Encoding.UTF8.GetString(lineEnd < 0 ? buffer[start..] : buffer[start..lineEnd]);
                        if (waitingOriginalPath)
                        {
                            if (outs.Count > 0)
                                outs[^1].OriginalPath = line;

                            waitingOriginalPath = false;
                        }
                        else if (line.Length >= 3)
                        {
                            var change = ParseLine(line, out waitingOriginalPath);
                            if (change != null)
                                outs.Add(change);
                        }

                        if (lineEnd < 0)
                            break;

                        start = lineEnd + 1;
                    }
                }

                lock (capturedLock)
                {
                    captured.Process = null;
                }
            }
            catch
            {
                // Ignore exceptions.
            }

            return outs;
        }

        private static Models.Change ParseLine(string line, out bool hasOriginalPath)
        {
            var ch = line[0];
            hasOriginalPath = false;

            // ? <path> - Untracked files
            if (ch == '?')
            {
                var change = new Models.Change();
                change.WorkTree = Models.ChangeState.Untracked;
                change.Path = line.Substring(2);
                return change;
            }

            // 1 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <path>
            if (ch == '1')
            {
                var ordinary = line.Split(' ', 9);
                if (ordinary.Length != 9)
                    return null;

                var index = Models.Change.ParseState(line[2]);
                var worktree = Models.Change.ParseState(line[3]);
                if (index == Models.ChangeState.None && worktree == Models.ChangeState.None)
                    return null;

                var change = new Models.Change();
                change.Path = ordinary[8];
                change.Index = index;
                change.WorkTree = worktree;
                return change;
            }

            // 2 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <X><score> <path> - Renamed Change. The next line is the original path.
            if (ch == '2')
            {
                var renamed = line.Split(' ', 10);
                if (renamed.Length != 10)
                    return null;

                var change = new Models.Change();
                change.Path = renamed[9];
                change.Index = Models.Change.ParseState(line[2]);
                change.WorkTree = Models.Change.ParseState(line[3]);
                hasOriginalPath = true;
                return change;
            }

            // u <XY> <sub> <m1> <m2> <m3> <mW> <h1> <h2> <h3> <path> - Unmerged (Conflicted)
            if (ch == 'u')
            {
                var unmerged = line.Split(' ', 11);
                if (unmerged.Length != 11)
                    return null;

                var change = new Models.Change();
                change.Path = unmerged[10];
                change.WorkTree = Models.ChangeState.Conflicted;
                change.ConflictReason = unmerged[1] switch
                {
                    "DD" => Models.ConflictReason.BothDeleted,
                    "AU" => Models.ConflictReason.AddedByUs,
                    "UD" => Models.ConflictReason.DeletedByThem,
                    "UA" => Models.ConflictReason.AddedByThem,
                    "DU" => Models.ConflictReason.DeletedByUs,
                    "AA" => Models.ConflictReason.BothAdded,
                    _ => Models.ConflictReason.BothModified,
                };

                return change;
            }

            return null;
        }

        private class CapturedProcess
        {
            public Process Process { get; set; } = null;
        }
    }
}
