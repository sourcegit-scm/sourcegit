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
        public static async Task AllAsync(string repo, bool includeModified, bool includeUntracked, bool includeIgnored, bool useTrash, Models.ICommandLog log)
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
                                Models.TrashBin.Delete(fullPath, useTrash, log);
                        }
                    }
                }
                catch (Exception e)
                {
                    Models.Notification.Send(repo, $"Failed to discard changes. Reason: {e.Message}", true);
                }

                var mode = includeIgnored ? Models.CleanMode.UntrackedAndIgnoredFiles : Models.CleanMode.OnlyUntrackedFiles;
                await CleanAsync(repo, mode, useTrash, log).ConfigureAwait(false);
            }
            else if (includeIgnored)
            {
                await CleanAsync(repo, Models.CleanMode.OnlyIgnoredFiles, useTrash, log).ConfigureAwait(false);
            }

            if (includeModified)
            {
                if (useTrash)
                    await SnapshotTrackedChangesToTrashAsync(repo, log).ConfigureAwait(false);

                await new Reset(repo, "", "--hard").Use(log).ExecAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Before `git reset --hard` overwrites tracked files with their committed (HEAD) version,
        ///     move the current on-disk version of each to the trash bin so the discard stays undoable
        ///     (restoring from the bin overwrites the reverted file at its original path).
        ///
        ///     Untracked and newly-added files are skipped: `reset --hard` does not overwrite them
        ///     (they are not in HEAD), so there is nothing here to preserve — and recycling them would
        ///     wrongly remove them from the working copy. Deleted files have nothing on disk to snapshot.
        /// </summary>
        private static async Task SnapshotTrackedChangesToTrashAsync(string repo, Models.ICommandLog log)
        {
            var changes = await new QueryLocalChanges(repo).GetResultAsync().ConfigureAwait(false);
            foreach (var c in changes)
            {
                if (c.WorkTree == Models.ChangeState.Untracked ||
                    c.WorkTree == Models.ChangeState.Added ||
                    c.Index == Models.ChangeState.Added)
                    continue;

                var fullPath = Path.Combine(repo, c.Path);
                if (File.Exists(fullPath))
                    Models.TrashBin.Delete(fullPath, true, log);
            }
        }

        /// <summary>
        ///     Remove the untracked/ignored files for the given clean mode. When the safetynet is
        ///     enabled, each path that `git clean` would remove is moved to the trash bin instead
        ///     (so it can be recovered); otherwise the original `git clean` is used.
        /// </summary>
        private static async Task CleanAsync(string repo, Models.CleanMode mode, bool useTrash, Models.ICommandLog log)
        {
            if (!useTrash)
            {
                await new Clean(repo, mode).Use(log).ExecAsync().ConfigureAwait(false);
                return;
            }

            var paths = await new QueryCleanablePaths(repo, mode).GetResultAsync().ConfigureAwait(false);
            foreach (var path in paths)
            {
                var fullPath = Path.Combine(repo, path);
                Models.TrashBin.Delete(fullPath, true, log);
            }
        }

        /// <summary>
        ///     Discard selected changes (only unstaged).
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="changes"></param>
        /// <param name="log"></param>
        public static async Task ChangesAsync(string repo, List<Models.Change> changes, bool useTrash, Models.ICommandLog log)
        {
            var restores = new List<string>();

            try
            {
                foreach (var c in changes)
                {
                    if (c.WorkTree == Models.ChangeState.Untracked || c.WorkTree == Models.ChangeState.Added)
                    {
                        var fullPath = Path.Combine(repo, c.Path);
                        Models.TrashBin.Delete(fullPath, useTrash, log);
                    }
                    else
                    {
                        // `git restore --worktree` recreates the file from the index. Snapshot the
                        // current on-disk (working) version to the trash first so the revert is
                        // undoable. Deleted files have nothing on disk to snapshot.
                        if (useTrash)
                        {
                            var fullPath = Path.Combine(repo, c.Path);
                            if (File.Exists(fullPath))
                                Models.TrashBin.Delete(fullPath, true, log);
                        }

                        restores.Add(c.Path);
                    }
                }
            }
            catch (Exception e)
            {
                Models.Notification.Send(repo, $"Failed to discard changes. Reason: {e.Message}", true);
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
