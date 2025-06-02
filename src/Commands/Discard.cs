using System;
using System.Collections.Generic;
using System.IO;

using Avalonia.Threading;

namespace SourceGit.Commands
{
    public static class Discard
    {
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

            if (Native.OS.GitVersion >= Models.GitVersions.RESTORE_WITH_PATHSPECFILE)
            {
                var tmpFile = Path.GetTempFileName();
                File.WriteAllLines(tmpFile, restores);
                new Restore(repo, tmpFile, "--worktree --recurse-submodules") { Log = log }.Exec();
                File.Delete(tmpFile);
            }
            else
            {
                for (int i = 0; i < restores.Count; i += 32)
                {
                    var count = Math.Min(32, restores.Count - i);
                    new Restore(repo, restores.GetRange(i, count), "--worktree --recurse-submodules") { Log = log }.Exec();
                }
            }
        }
    }
}
