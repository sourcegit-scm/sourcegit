using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SourceGit.Commands {
    public static class SaveChangesAsPatch {
        public static bool Exec(string repo, List<Models.Change> changes, bool isUnstaged, string saveTo) {
            using (var sw = File.Create(saveTo)) {
                foreach (var change in changes) {
                    if (!ProcessSingleChange(repo, new Models.DiffOption(change, isUnstaged), sw)) return false;
                }
            }

            return true;
        }

        private static bool ProcessSingleChange(string repo, Models.DiffOption opt, FileStream writer) {
            var starter = new ProcessStartInfo();
            starter.WorkingDirectory = repo;
            starter.FileName = Native.OS.GitInstallPath;
            starter.Arguments = $"diff --ignore-cr-at-eol --unified=4 {opt}";
            starter.UseShellExecute = false;
            starter.CreateNoWindow = true;
            starter.WindowStyle = ProcessWindowStyle.Hidden;
            starter.RedirectStandardOutput = true;

            try {
                var proc = new Process() { StartInfo = starter };
                proc.Start();
                proc.StandardOutput.BaseStream.CopyTo(writer);
                proc.WaitForExit();
                var rs = proc.ExitCode == 0;
                proc.Close();

                return rs;
            } catch (Exception e) {
                App.RaiseException(repo, "Save change to patch failed: " + e.Message);
                return false;
            }
        }
    }
}
