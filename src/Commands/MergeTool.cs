using System.IO;

using Avalonia.Threading;

namespace SourceGit.Commands
{
    public static class MergeTool
    {
        public static bool OpenForMerge(string repo, string tool, string mergeCmd, string file)
        {
            if (string.IsNullOrWhiteSpace(tool) || string.IsNullOrWhiteSpace(mergeCmd))
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    App.RaiseException(repo, "Invalid external merge tool settings!");
                });
                return false;
            }

            if (!File.Exists(tool))
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    App.RaiseException(repo, $"Can NOT found external merge tool in '{tool}'!");
                });
                return false;
            }

            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.RaiseError = false;
            cmd.Args = $"-c mergetool.sourcegit.cmd=\"\\\"{tool}\\\" {mergeCmd}\" -c mergetool.writeToTemp=true -c mergetool.keepBackup=false -c mergetool.trustExitCode=true mergetool --tool=sourcegit \"{file}\"";
            return cmd.Exec();
        }

        public static bool OpenForDiff(string repo, string tool, string diffCmd, Models.DiffOption option)
        {
            if (string.IsNullOrWhiteSpace(tool) || string.IsNullOrWhiteSpace(diffCmd))
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    App.RaiseException(repo, "Invalid external merge tool settings!");
                });
                return false;
            }

            if (!File.Exists(tool))
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    App.RaiseException(repo, $"Can NOT found external merge tool in '{tool}'!");
                });
                return false;
            }

            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.RaiseError = false;
            cmd.Args = $"-c difftool.sourcegit.cmd=\"\\\"{tool}\\\" {diffCmd}\" difftool --tool=sourcegit --no-prompt {option}";
            return cmd.Exec();
        }
    }
}