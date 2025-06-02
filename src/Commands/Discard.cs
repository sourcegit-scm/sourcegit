using System;
using System.Collections.Generic;
using System.IO;

using Avalonia.Threading;

namespace SourceGit.Commands
{
    public static class Discard
    {
        /// <summary>
        ///     Discard all local changes (unstaged & staged)
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="includeIgnored"></param>
        /// <param name="log"></param>
        public static void All(string repo, bool includeIgnored, Models.ICommandLog log)
        {
            var changes = new QueryLocalChanges(repo).Result();
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
                        else
                            File.Delete(fullPath);
                    }
                }
            }
            catch (Exception e)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    App.RaiseException(repo, $"Failed to discard changes. Reason: {e.Message}");
                });
            }

            new Reset(repo, "HEAD", "--hard") { Log = log }.Exec();

            if (includeIgnored)
                new Clean(repo) { Log = log }.Exec();
        }

        /// <summary>
        ///     Discard selected changes (only unstaged).
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="changes"></param>
        /// <param name="log"></param>
        public static void Changes(string repo, List<Models.Change> changes, Models.ICommandLog log)
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
                Dispatcher.UIThread.Invoke(() =>
                {
                    App.RaiseException(repo, $"Failed to discard changes. Reason: {e.Message}");
                });
            }

            if (restores.Count > 0)
            {
                var pathSpecFile = Path.GetTempFileName();
                File.WriteAllLines(pathSpecFile, restores);
                new Restore(repo, pathSpecFile, false) { Log = log }.Exec();
                File.Delete(pathSpecFile);
            }
        }
    }
}
