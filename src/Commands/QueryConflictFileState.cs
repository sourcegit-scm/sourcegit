using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DevBoard.Commands
{
    public class QueryConflictFileState : Command
    {
        public QueryConflictFileState(string repo, Models.Change change)
        {
            var opt = new Models.DiffOption(change, true);

            WorkingDirectory = repo;
            Context = repo;
            Args = $"diff --no-color --no-ext-diff --no-textconv --full-index --patch {opt}";
        }

        public async Task<Models.ConflictFileState> GetResultAsync()
        {
            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                var isBinary = false;
                var tokenCount = 0;

                while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    if (isBinary)
                        continue;

                    if (line.StartsWith("Binary files ", StringComparison.Ordinal))
                        isBinary = true;
                    else if (line.StartsWith("++<<<<<<<", StringComparison.Ordinal) ||
                        line.StartsWith("++=======", StringComparison.Ordinal) ||
                        line.StartsWith("++>>>>>>>", StringComparison.Ordinal))
                        tokenCount++;
                }

                await proc.WaitForExitAsync().ConfigureAwait(false);

                if (isBinary)
                    return Models.ConflictFileState.UnmergedBinary;

                return tokenCount == 0 ? Models.ConflictFileState.Resolved : Models.ConflictFileState.UnmergedText;
            }
            catch
            {
                return Models.ConflictFileState.Unknown;
            }
        }
    }
}
