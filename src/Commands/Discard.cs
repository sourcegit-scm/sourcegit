using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public static class Discard
    {
        /// <summary>
        ///     Discard all local changes (unstaged & staged)
        /// </summary>
        public static async Task AllAsync(string repo, bool includeUntracked, bool includeIgnored, Models.ICommandLog log)
        {
            if (includeUntracked)
            {
                // Untracked paths that contains `.git` file (detached submodule) must be removed manually.
                var changes = await new QueryLocalChanges(repo).GetResultAsync().ConfigureAwait(false);
                try
                {
                    foreach (var c in changes)
                    {
                        if (c.WorkTree == Models.ChangeState.Untracked ||
                            c.WorkTree == Models.ChangeState.Added ||
                            c.Index == Models.ChangeState.Added ||
                            c.Index == Models.ChangeState.Renamed)
                        {
                            var fullPath = Path.Combine(repo, c.Path);
                            if (Directory.Exists(fullPath))
                                Directory.Delete(fullPath, true);
                        }
                    }
                }
                catch (Exception e)
                {
                    App.RaiseException(repo, $"Failed to discard changes. Reason: {e.Message}");
                }

                if (includeIgnored)
                    await new Clean(repo, Models.CleanMode.All).Use(log).ExecAsync().ConfigureAwait(false);
                else
                    await new Clean(repo, Models.CleanMode.OnlyUntrackedFiles).Use(log).ExecAsync().ConfigureAwait(false);
            }
            else if (includeIgnored)
            {
                await new Clean(repo, Models.CleanMode.OnlyIgnoredFiles).Use(log).ExecAsync().ConfigureAwait(false);
            }

            await new Reset(repo, "HEAD", "--hard").Use(log).ExecAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///     Discard selected changes (only unstaged).
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="changes"></param>
        /// <param name="log"></param>
        public static async Task ChangesAsync(string repo, List<Models.Change> changes, Models.ICommandLog log)
        {
            var restores = new List<string>();

            try
            {
                foreach (var c in changes)
                {
                    if (c.WorkTree == Models.ChangeState.Untracked || c.WorkTree == Models.ChangeState.Added)
                    {
                        var fullPath = Path.Combine(repo, c.Path);
                        if (Directory.Exists(fullPath))
                            Directory.Delete(fullPath, true);
                        else
                            File.Delete(fullPath);
                    }
                    else
                    {
                        restores.Add(c.Path);
                    }
                }
            }
            catch (Exception e)
            {
                App.RaiseException(repo, $"Failed to discard changes. Reason: {e.Message}");
            }

            if (restores.Count > 0)
            {
                var pathSpecFile = Path.GetTempFileName();
                await File.WriteAllLinesAsync(pathSpecFile, restores).ConfigureAwait(false);
                await new Restore(repo, pathSpecFile).Use(log).ExecAsync().ConfigureAwait(false);
                File.Delete(pathSpecFile);
            }
        }
    }
}
