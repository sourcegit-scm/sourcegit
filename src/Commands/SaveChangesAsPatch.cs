using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public static class SaveChangesAsPatch
    {
        public static async Task<bool> ProcessLocalChangesAsync(string repo, List<Models.Change> changes, bool isUnstaged, string saveTo)
        {
            await using (var sw = File.Create(saveTo))
            {
                foreach (var change in changes)
                {
                    if (!await ProcessSingleChangeAsync(repo, new Models.DiffOption(change, isUnstaged), sw))
                        return false;
                }
            }

            return true;
        }

        public static async Task<bool> ProcessRevisionCompareChangesAsync(string repo, List<Models.Change> changes, string baseRevision, string targetRevision, string saveTo)
        {
            await using (var sw = File.Create(saveTo))
            {
                foreach (var change in changes)
                {
                    if (!await ProcessSingleChangeAsync(repo, new Models.DiffOption(baseRevision, targetRevision, change), sw))
                        return false;
                }
            }

            return true;
        }

        public static async Task<bool> ProcessStashChangesAsync(string repo, List<Models.DiffOption> opts, string saveTo)
        {
            await using (var sw = File.Create(saveTo))
            {
                foreach (var opt in opts)
                {
                    if (!await ProcessSingleChangeAsync(repo, opt, sw))
                        return false;
                }
            }
            return true;
        }

        private static async Task<bool> ProcessSingleChangeAsync(string repo, Models.DiffOption opt, FileStream writer)
        {
            var starter = new ProcessStartInfo();
            starter.WorkingDirectory = repo;
            starter.FileName = Native.OS.GitExecutable;
            starter.Arguments = $"diff --no-color --no-ext-diff --ignore-cr-at-eol --unified=4 {opt}";
            starter.UseShellExecute = false;
            starter.CreateNoWindow = true;
            starter.WindowStyle = ProcessWindowStyle.Hidden;
            starter.RedirectStandardOutput = true;

            try
            {
                using var proc = Process.Start(starter)!;
                await proc.StandardOutput.BaseStream.CopyToAsync(writer).ConfigureAwait(false);
                await proc.WaitForExitAsync().ConfigureAwait(false);
                return proc.ExitCode == 0;
            }
            catch (Exception e)
            {
                App.RaiseException(repo, "Save change to patch failed: " + e.Message);
                return false;
            }
        }
    }
}
