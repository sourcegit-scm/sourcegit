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

            string tempFolder = Path.GetTempPath();
            string fileName = $"discard_{DateTime.Now.Ticks}.patch";
            string fileNameTemp = Path.Combine(tempFolder, fileName);

            // Save the patch in the Temp Folder
            var succ = await SaveChangesAsPatch.ProcessLocalChangesAsync(repo, changes, true, fileNameTemp);
            if (succ) return false;

            // Move the file in the Trash
            if(!Native.OS.MoveFileToTrash(fileNameTemp)) return false;

            return false;
        }
    }
}
