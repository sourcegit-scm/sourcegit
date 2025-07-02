using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public static class MergeTool
    {
        public static bool OpenForMerge(string repo, int toolType, string toolPath, string file)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.RaiseError = true;

            // NOTE: If no <file> names are specified, 'git mergetool' will run the merge tool program on every file with merge conflicts.
            var fileArg = string.IsNullOrEmpty(file) ? "" : $"\"{file}\"";

            if (toolType == 0)
            {
                cmd.Args = $"mergetool {fileArg}";
                return cmd.Exec();
            }

            if (!File.Exists(toolPath))
            {
                App.RaiseException(repo, $"Can NOT find external merge tool in '{toolPath}'!");
                return false;
            }

            var supported = Models.ExternalMerger.Supported.Find(x => x.Type == toolType);
            if (supported == null)
            {
                App.RaiseException(repo, "Invalid merge tool in preference setting!");
                return false;
            }

            cmd.Args = $"-c mergetool.sourcegit.cmd=\"\\\"{toolPath}\\\" {supported.Cmd}\" -c mergetool.writeToTemp=true -c mergetool.keepBackup=false -c mergetool.trustExitCode=true mergetool --tool=sourcegit {fileArg}";
            return cmd.Exec();
        }

        public static bool OpenForDiff(string repo, int toolType, string toolPath, Models.DiffOption option)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.RaiseError = true;

            if (toolType == 0)
            {
                cmd.Args = $"difftool -g --no-prompt {option}";
                return cmd.Exec();
            }

            if (!File.Exists(toolPath))
            {
                App.RaiseException(repo, $"Can NOT find external diff tool in '{toolPath}'!");
                return false;
            }

            var supported = Models.ExternalMerger.Supported.Find(x => x.Type == toolType);
            if (supported == null)
            {
                App.RaiseException(repo, "Invalid merge tool in preference setting!");
                return false;
            }

            cmd.Args = $"-c difftool.sourcegit.cmd=\"\\\"{toolPath}\\\" {supported.DiffCmd}\" difftool --tool=sourcegit --no-prompt {option}";
            return cmd.Exec();
        }

        public static async Task<bool> OpenForMergeAsync(string repo, int toolType, string toolPath, string file)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.RaiseError = true;

            // NOTE: If no <file> names are specified, 'git mergetool' will run the merge tool program on every file with merge conflicts.
            var fileArg = string.IsNullOrEmpty(file) ? "" : $"\"{file}\"";

            if (toolType == 0)
            {
                cmd.Args = $"mergetool {fileArg}";
                return await cmd.ExecAsync();
            }

            if (!File.Exists(toolPath))
            {
                App.RaiseException(repo, $"Can NOT find external merge tool in '{toolPath}'!");
                return false;
            }

            var supported = Models.ExternalMerger.Supported.Find(x => x.Type == toolType);
            if (supported == null)
            {
                App.RaiseException(repo, "Invalid merge tool in preference setting!");
                return false;
            }

            cmd.Args = $"-c mergetool.sourcegit.cmd=\"\\\"{toolPath}\\\" {supported.Cmd}\" -c mergetool.writeToTemp=true -c mergetool.keepBackup=false -c mergetool.trustExitCode=true mergetool --tool=sourcegit {fileArg}";
            return await cmd.ExecAsync();
        }

        public static async Task<bool> OpenForDiffAsync(string repo, int toolType, string toolPath, Models.DiffOption option)
        {
            var cmd = new Command();
            cmd.WorkingDirectory = repo;
            cmd.Context = repo;
            cmd.RaiseError = true;

            if (toolType == 0)
            {
                cmd.Args = $"difftool -g --no-prompt {option}";
                return await cmd.ExecAsync();
            }

            if (!File.Exists(toolPath))
            {
                App.RaiseException(repo, $"Can NOT find external diff tool in '{toolPath}'!");
                return false;
            }

            var supported = Models.ExternalMerger.Supported.Find(x => x.Type == toolType);
            if (supported == null)
            {
                App.RaiseException(repo, "Invalid merge tool in preference setting!");
                return false;
            }

            cmd.Args = $"-c difftool.sourcegit.cmd=\"\\\"{toolPath}\\\" {supported.DiffCmd}\" difftool --tool=sourcegit --no-prompt {option}";
            return await cmd.ExecAsync();
        }
    }
}
