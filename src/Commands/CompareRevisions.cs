using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class CompareRevisions : Command
    {
        [GeneratedRegex(@"^([MADC])\s+(.+)$")]
        private static partial Regex REG_FORMAT();
        [GeneratedRegex(@"^R[0-9]{0,4}\s+(.+)$")]
        private static partial Regex REG_RENAME_FORMAT();

        public CompareRevisions(string repo, string start, string end)
        {
            WorkingDirectory = repo;
            Context = repo;

            var based = string.IsNullOrEmpty(start) ? "-R" : start;
            Args = $"diff --name-status {based} {end}";
        }

        public CompareRevisions(string repo, string start, string end, string path)
        {
            WorkingDirectory = repo;
            Context = repo;

            var based = string.IsNullOrEmpty(start) ? "-R" : start;
            Args = $"diff --name-status {based} {end} -- {path.Quoted()}";
        }

        public async Task<List<Models.Change>> ReadAsync()
        {
            var changes = new List<Models.Change>();
            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                while (await proc.StandardOutput.ReadLineAsync() is { } line)
                {
                    var match = REG_FORMAT().Match(line);
                    if (!match.Success)
                    {
                        match = REG_RENAME_FORMAT().Match(line);
                        if (match.Success)
                        {
                            var renamed = new Models.Change() { Path = match.Groups[1].Value };
                            renamed.Set(Models.ChangeState.Renamed);
                            changes.Add(renamed);
                        }

                        continue;
                    }

                    var change = new Models.Change() { Path = match.Groups[2].Value };
                    var status = match.Groups[1].Value;

                    switch (status[0])
                    {
                        case 'M':
                            change.Set(Models.ChangeState.Modified);
                            changes.Add(change);
                            break;
                        case 'A':
                            change.Set(Models.ChangeState.Added);
                            changes.Add(change);
                            break;
                        case 'D':
                            change.Set(Models.ChangeState.Deleted);
                            changes.Add(change);
                            break;
                        case 'C':
                            change.Set(Models.ChangeState.Copied);
                            changes.Add(change);
                            break;
                    }
                }

                await proc.WaitForExitAsync().ConfigureAwait(false);

                changes.Sort((l, r) => Models.NumericSort.Compare(l.Path, r.Path));
            }
            catch
            {
                //ignore changes;
            }

            return changes;
        }
    }
}
