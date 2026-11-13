using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Models
{
    /// <summary>
    ///     Answers "does this repository have local changes?" for repositories that are not
    ///     open in a tab, so the project tree can show a status without opening anything.
    ///
    ///     Only the working copy is asked about, and untracked files are deliberately left
    ///     out (`status -uno`): a tree root like C:/docs contains repositories with
    ///     node_modules and build outputs, and counting those would mark every node dirty
    ///     while telling the user nothing useful.
    /// </summary>
    public static class RepositoryStatusScanner
    {
        public const int Concurrency = 4;

        /// <summary>
        ///     Queries the given repositories concurrently and returns, for the ones with local
        ///     changes, the number of changed files keyed by <see cref="RepositoryScanner.Key"/>.
        /// </summary>
        public static async Task<Dictionary<string, int>> QueryDirtyAsync(IList<string> repositories, CancellationToken token)
        {
            var result = new Dictionary<string, int>();
            if (repositories == null || repositories.Count == 0)
                return result;

            var dirty = new ConcurrentDictionary<string, int>();
            var cursor = -1;

            async Task Worker()
            {
                while (!token.IsCancellationRequested)
                {
                    var index = Interlocked.Increment(ref cursor);
                    if (index >= repositories.Count)
                        return;

                    var path = repositories[index];
                    try
                    {
                        var changes = await new Commands.QueryLocalChanges(path, false, true)
                            .WithCancellation(token)
                            .GetResultAsync()
                            .ConfigureAwait(false);

                        if (changes.Count > 0)
                            dirty[RepositoryScanner.Key(path)] = changes.Count;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch
                    {
                        // One unreadable repository must not abort the whole pass.
                    }
                }
            }

            var workers = Math.Min(Concurrency, repositories.Count);
            var tasks = new List<Task>(workers);
            for (var i = 0; i < workers; i++)
                tasks.Add(Task.Run(Worker, token));

            await Task.WhenAll(tasks).ConfigureAwait(false);

            foreach (var pair in dirty)
                result[pair.Key] = pair.Value;

            return result;
        }
    }
}
