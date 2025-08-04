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
                App.RaiseException(Context, "Invalid merge tool in preference setting!");
                return false;
            }

            if (string.IsNullOrEmpty(tool.Cmd))
            {
                Args = $"mergetool {_file}";
            }
            else
            {
                var cmd = $"{tool.Exec.Quoted()} {tool.Cmd}";
                Args = $"-c mergetool.sourcegit.cmd={cmd.Quoted()} -c mergetool.writeToTemp=true -c mergetool.keepBackup=false -c mergetool.trustExitCode=true mergetool --tool=sourcegit {_file}";
            }

            return await ExecAsync().ConfigureAwait(false);
        }

        private string _file;
    }
}
