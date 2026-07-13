using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryPickableCommits : Command
    {
        public QueryPickableCommits(string repo, string based, string target)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"log --topo-order --cherry-pick --right-only --no-merges --no-show-signature --decorate=full --format=%H%x00%P%x00%D%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s {based}...{target}";
        }

        public async Task<List<Models.Commit>> GetResultAsync()
        {
            var commits = new List<Models.Commit>();
            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    var parts = line.Split('\0');
                    if (parts.Length != 8)
                        continue;

                    var commit = new Models.Commit() { SHA = parts[0] };
                    commit.ParseParents(parts[1]);
                    commit.ParseDecorators(parts[2]);
                    commit.Author = Models.User.FindOrAdd(parts[3]);
                    commit.AuthorTime = ulong.Parse(parts[4]);
                    commit.Committer = Models.User.FindOrAdd(parts[5]);
                    commit.CommitterTime = ulong.Parse(parts[6]);
                    commit.Subject = parts[7];
                    commits.Add(commit);
                }

                await proc.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                RaiseException($"Failed to query commits. Reason: {e.Message}");
            }

            return commits;
        }
    }
}
