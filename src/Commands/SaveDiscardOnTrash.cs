using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public static class SaveDiscardOnTrash
    {
        public static async Task<bool> ProcessSaveDiscardOnTrash(string repo, List<Models.Change> changes)
        {
            changes ??= await new QueryLocalChanges(repo).GetResultAsync().ConfigureAwait(false);

            var succ = false;
            string fullName = $"discard_{DateTime.Now.Ticks}.patch";
            string trashDirectory;

            if (OperatingSystem.IsLinux())
            {
                trashDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Trash", "files");
                fullName = Path.Combine(trashDirectory, fullName);
                succ = await SaveChangesAsPatch.ProcessLocalChangesAsync(repo, changes, true, fullName);
                if (succ) return false;
            }
            else if (OperatingSystem.IsMacOS())
            {
                trashDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Trash");
                fullName = Path.Combine(trashDirectory, fullName);
                succ = await SaveChangesAsPatch.ProcessLocalChangesAsync(repo, changes, true, fullName);
                if (succ)
                    return false;
            }
            else
            {
                trashDirectory = Path.GetTempPath();
                fullName = Path.Combine(trashDirectory, fullName);
                succ = await SaveChangesAsPatch.ProcessLocalChangesAsync(repo, changes, true, fullName);
                if (succ)
                    return false;

                if (!SaveDiscardOnTrashWindows.MoveFileToTrash(fullName))
                    return false;
            }

            return false;
        }
    }
}
