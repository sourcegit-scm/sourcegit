using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    // Search only the commits currently loaded in history, including their full messages.
    public class QueryHistoryQuickSearch : Command
    {
        public QueryHistoryQuickSearch(string repo, IReadOnlyList<Models.Commit> commits, string filter, CancellationToken cancellationToken)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "log --no-walk=unsorted --stdin --no-show-signature --format=%H%x00%aN%x00%aE%x00%B%x00";
            CancellationToken = cancellationToken;
            _commits = commits;
            _filter = filter;
        }

        public async Task<HashSet<string>> GetResultAsync()
        {
            var matches = new HashSet<string>(StringComparer.Ordinal);
            if (_filter.Length == 0 || _commits.Count == 0)
                return matches;

            CancellationToken.ThrowIfCancellationRequested();
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);
            proc.StartInfo.RedirectStandardInput = true;
            proc.Start();
            var input = WriteCommitsAsync(proc);
            var errors = proc.StandardError.ReadToEndAsync(CancellationToken);
            try
            {
                // Stream NUL-delimited fields as Mixed search does. One Git process handles
                // the loaded history, without retaining every full message in memory.
                var fields = new string[4];
                var fieldIndex = 0;
                var field = new StringBuilder();
                var buffer = new char[4096];
                int count;
                while ((count = await proc.StandardOutput.ReadAsync(buffer.AsMemory(), CancellationToken).ConfigureAwait(false)) > 0)
                {
                    for (var i = 0; i < count; i++)
                    {
                        if (buffer[i] != '\0')
                        {
                            field.Append(buffer[i]);
                            continue;
                        }

                        fields[fieldIndex++] = field.ToString();
                        field.Clear();
                        if (fieldIndex < fields.Length)
                            continue;

                        var sha = fields[0].TrimStart('\r', '\n');
                        // Same literal, case-insensitive OR matching as Commit Search's Mixed mode.
                        if (sha.StartsWith(_filter, StringComparison.OrdinalIgnoreCase) ||
                            (fields[1] + "±" + fields[2]).Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                            fields[3].Contains(_filter, StringComparison.OrdinalIgnoreCase))
                            matches.Add(sha);
                        fieldIndex = 0;
                    }
                }
                await proc.WaitForExitAsync(CancellationToken).ConfigureAwait(false);
                var error = await errors.ConfigureAwait(false);
                if (proc.ExitCode != 0)
                    throw new InvalidOperationException(error);
                await input.ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    if (!proc.HasExited)
                        proc.Kill(true);
                }
                catch (InvalidOperationException)
                {
                    // The process may have exited between HasExited and Kill.
                }
                await proc.WaitForExitAsync().ConfigureAwait(false);
                // Observe the input/error tasks even when Git exits early or the query is cancelled.
                try
                {
                    await input.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Preserve the original query error.
                }
                try
                {
                    await errors.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // The query was superseded.
                }
            }
            return matches;
        }

        private async Task WriteCommitsAsync(Process proc)
        {
            foreach (var commit in _commits)
                await proc.StandardInput.WriteLineAsync(commit.SHA.AsMemory(), CancellationToken).ConfigureAwait(false);
            proc.StandardInput.Close();
        }

        private readonly IReadOnlyList<Models.Commit> _commits;
        private readonly string _filter;
    }
}
