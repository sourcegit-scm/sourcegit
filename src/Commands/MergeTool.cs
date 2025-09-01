using System;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class MergeTool : Command
    {
        public MergeTool(string repo, string file)
        {
            WorkingDirectory = repo;
            Context = repo;
            _file = string.IsNullOrEmpty(file) ? string.Empty : file.Quoted();
        }

        public async Task<bool> OpenAsync()
        {
            var tool = Native.OS.GetDiffMergeTool(false);
            if (tool == null)
            {
                App.RaiseException(Context, "Invalid diff/merge tool in preference setting!");
                return false;
            }

            if (string.IsNullOrEmpty(tool.Cmd))
            {
                var ok = await CheckGitConfigurationAsync();
                if (!ok)
                    return false;

                Args = $"mergetool -g --no-prompt {_file}";
            }
            else
            {
                var cmd = $"{tool.Exec.Quoted()} {tool.Cmd}";
                Args = $"-c mergetool.sourcegit.cmd={cmd.Quoted()} -c mergetool.writeToTemp=true -c mergetool.keepBackup=false -c mergetool.trustExitCode=true mergetool --tool=sourcegit {_file}";
            }

            return await ExecAsync().ConfigureAwait(false);
        }

        private async Task<bool> CheckGitConfigurationAsync()
        {
            var tool = await new Config(WorkingDirectory).GetAsync("merge.guitool");
            if (string.IsNullOrEmpty(tool))
                tool = await new Config(WorkingDirectory).GetAsync("merge.tool");

            if (string.IsNullOrEmpty(tool))
            {
                App.RaiseException(Context, "Missing git configuration: merge.guitool");
                return false;
            }

            if (tool.StartsWith("vimdiff", StringComparison.Ordinal) ||
                tool.StartsWith("nvimdiff", StringComparison.Ordinal))
            {
                App.RaiseException(Context, $"CLI based merge tool \"{tool}\" is not supported by this app!");
                return false;
            }

            return true;
        }

        private string _file;
    }
}
