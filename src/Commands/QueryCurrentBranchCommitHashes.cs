using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryCurrentBranchCommitHashes : Command
    {
        public QueryCurrentBranchCommitHashes(string repo, ulong sinceTimestamp)
        {
            var since = DateTime.UnixEpoch.AddSeconds(sinceTimestamp).ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
            WorkingDirectory = repo;
            Context = repo;
            Args = $"log --since={since.Quoted()} --format=%H";
        }

        public async Task<HashSet<string>> GetResultAsync()
        {
            var outs = new HashSet<string>();

            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                while (await proc.StandardOutput.ReadLineAsync() is { Length: > 8 } line)
                    outs.Add(line);

                await proc.WaitForExitAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore exceptions;
            }

            return outs;
        }
    }
}
