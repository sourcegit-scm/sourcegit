using System;
using System.Collections.Generic;
using System.Diagnostics;
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

                var buffer = new char[8192];
                var record = new StringBuilder();
                var waitingOriginalPath = false;
                Models.Change renamed = null;
                int count;

                while ((count = await proc.StandardOutput.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    var start = 0;
                    while (start < count)
                    {
                        var end = Array.IndexOf(buffer, '\0', start, count - start);
                        if (end < 0)
                        {
                            record.Append(buffer, start, count - start);
                            break;
                        }

                        record.Append(buffer, start, end - start);
                        start = end + 1;

                        // For renamed/copied entries, the original path is output as an extra field.
                        if (waitingOriginalPath)
                        {
                            if (renamed != null)
                                renamed.OriginalPath = record.ToString();

                            waitingOriginalPath = false;
                            renamed = null;
                        }
                        else
                        {
                            var change = ParseRecord(record.ToString(), out waitingOriginalPath);
                            if (change != null)
                            {
                                outs.Add(change);
                                if (waitingOriginalPath)
                                    renamed = change;
                            }
                        }

                        record.Clear();
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

        private static Models.Change ParseRecord(string record, out bool hasOriginalPath)
        {
            hasOriginalPath = false;
            if (record.Length < 3)
                return null;

            var change = new Models.Change();
            switch (record[0])
            {
                case '1':
                    // 1 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <path>
                    var ordinary = record.Split(' ', 9);
                    if (ordinary.Length != 9)
                        return null;

                    change.Path = ordinary[8];
                    (change.Index, change.WorkTree) = ParseState(ordinary[1]);
                    break;
                case '2':
                    // 2 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <X><score> <path>, followed by <origPath> as the next field
                    hasOriginalPath = true;

                    var renamed = record.Split(' ', 10);
                    if (renamed.Length != 10)
                        return null;

                    change.Path = renamed[9];
                    (change.Index, change.WorkTree) = ParseState(renamed[1]);
                    break;
                case 'u':
                    // u <XY> <sub> <m1> <m2> <m3> <mW> <h1> <h2> <h3> <path>
                    var unmerged = record.Split(' ', 11);
                    if (unmerged.Length != 11)
                        return null;

                    change.Path = unmerged[10];
                    change.ConflictReason = unmerged[1] switch
                    {
                        "DD" => Models.ConflictReason.BothDeleted,
                        "AU" => Models.ConflictReason.AddedByUs,
                        "UD" => Models.ConflictReason.DeletedByThem,
                        "UA" => Models.ConflictReason.AddedByThem,
                        "DU" => Models.ConflictReason.DeletedByUs,
                        "AA" => Models.ConflictReason.BothAdded,
                        "UU" => Models.ConflictReason.BothModified,
                        _ => Models.ConflictReason.None,
                    };

                    if (change.ConflictReason != Models.ConflictReason.None)
                        change.WorkTree = Models.ChangeState.Conflicted;
                    break;
                case '?':
                    // ? <path>
                    change.Path = record.Substring(2);
                    change.WorkTree = Models.ChangeState.Untracked;
                    break;
            }

            if (change.Index == Models.ChangeState.None && change.WorkTree == Models.ChangeState.None)
                return null;

            return change;
        }

        private static (Models.ChangeState, Models.ChangeState) ParseState(string xy)
        {
            return xy switch
            {
                ".M" => (Models.ChangeState.None, Models.ChangeState.Modified),
                ".T" => (Models.ChangeState.None, Models.ChangeState.TypeChanged),
                ".A" => (Models.ChangeState.None, Models.ChangeState.Added),
                ".D" => (Models.ChangeState.None, Models.ChangeState.Deleted),
                ".R" => (Models.ChangeState.None, Models.ChangeState.Renamed),
                ".C" => (Models.ChangeState.None, Models.ChangeState.Copied),
                "M." => (Models.ChangeState.Modified, Models.ChangeState.None),
                "MM" => (Models.ChangeState.Modified, Models.ChangeState.Modified),
                "MT" => (Models.ChangeState.Modified, Models.ChangeState.TypeChanged),
                "MD" => (Models.ChangeState.Modified, Models.ChangeState.Deleted),
                "T." => (Models.ChangeState.TypeChanged, Models.ChangeState.None),
                "TM" => (Models.ChangeState.TypeChanged, Models.ChangeState.Modified),
                "TT" => (Models.ChangeState.TypeChanged, Models.ChangeState.TypeChanged),
                "TD" => (Models.ChangeState.TypeChanged, Models.ChangeState.Deleted),
                "A." => (Models.ChangeState.Added, Models.ChangeState.None),
                "AM" => (Models.ChangeState.Added, Models.ChangeState.Modified),
                "AT" => (Models.ChangeState.Added, Models.ChangeState.TypeChanged),
                "AD" => (Models.ChangeState.Added, Models.ChangeState.Deleted),
                "D." => (Models.ChangeState.Deleted, Models.ChangeState.None),
                "R." => (Models.ChangeState.Renamed, Models.ChangeState.None),
                "RM" => (Models.ChangeState.Renamed, Models.ChangeState.Modified),
                "RT" => (Models.ChangeState.Renamed, Models.ChangeState.TypeChanged),
                "RD" => (Models.ChangeState.Renamed, Models.ChangeState.Deleted),
                "C." => (Models.ChangeState.Copied, Models.ChangeState.None),
                "CM" => (Models.ChangeState.Copied, Models.ChangeState.Modified),
                "CT" => (Models.ChangeState.Copied, Models.ChangeState.TypeChanged),
                "CD" => (Models.ChangeState.Copied, Models.ChangeState.Deleted),
                _ => (Models.ChangeState.None, Models.ChangeState.None),
            };
        }

        private class CapturedProcess
        {
            public Process Process { get; set; } = null;
        }
    }
}
