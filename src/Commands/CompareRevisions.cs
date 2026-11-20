using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class CompareRevisions : Command
    {
        [GeneratedRegex(@"^([CR])[0-9]{0,4}$")]
        private static partial Regex REG_RENAME_FORMAT();

        public CompareRevisions(string repo, string start, string end)
        {
            WorkingDirectory = repo;
            Context = repo;

            var based = string.IsNullOrEmpty(start) ? "-R" : start;
            Args = $"diff --name-status -z {based} {end}";
        }

        public CompareRevisions(string repo, string start, string end, string path)
        {
            WorkingDirectory = repo;
            Context = repo;

            var based = string.IsNullOrEmpty(start) ? "-R" : start;
            Args = $"diff --name-status -z {based} {end} -- {path.Quoted()}";
        }

        public async Task<List<Models.Change>> ReadAsync()
        {
            var changes = new List<Models.Change>();
            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                using var ms = new MemoryStream();
                await proc.StandardOutput.BaseStream.CopyToAsync(ms, CancellationToken).ConfigureAwait(false);

                if (ms.TryGetBuffer(out var buffer))
                {
                    var start = buffer.Offset;
                    var end = buffer.Offset + buffer.Count;
                    var waitingOriginalPath = false;
                    var waitingForPath = false;

                    while (start < end)
                    {
                        var lineEnd = Array.IndexOf(buffer.Array, (byte)'\0', start);
                        var line = Encoding.UTF8.GetString(lineEnd < 0 ? buffer[start..] : buffer[start..lineEnd]);

                        if (waitingOriginalPath)
                        {
                            if (changes.Count > 0)
                                changes[^1].OriginalPath = line;

                            waitingOriginalPath = false;
                        }
                        else if (waitingForPath)
                        {
                            if (changes.Count > 0)
                                changes[^1].Path = line;

                            waitingForPath = false;
                        }
                        else if (line.Equals("M", StringComparison.Ordinal))
                        {
                            changes.Add(new Models.Change() { Index = Models.ChangeState.Modified });
                            waitingForPath = true;
                        }
                        else if (line.Equals("A", StringComparison.Ordinal))
                        {
                            changes.Add(new Models.Change() { Index = Models.ChangeState.Added });
                            waitingForPath = true;
                        }
                        else if (line.Equals("D", StringComparison.Ordinal))
                        {
                            changes.Add(new Models.Change() { Index = Models.ChangeState.Deleted });
                            waitingForPath = true;
                        }
                        else if (line.Equals("T", StringComparison.Ordinal))
                        {
                            changes.Add(new Models.Change() { Index = Models.ChangeState.TypeChanged });
                            waitingForPath = true;
                        }
                        else if (REG_RENAME_FORMAT().IsMatch(line))
                        {
                            changes.Add(new Models.Change() { Index = Models.Change.ParseState(line[0]) });
                            waitingOriginalPath = true;
                            waitingForPath = true;
                        }

                        if (lineEnd < 0)
                            break;

                        start = lineEnd + 1;
                    }
                }

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
