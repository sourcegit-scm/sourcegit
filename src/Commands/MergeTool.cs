using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class MergeTool : Command
    {
        public MergeTool(string repo, int type, string exec, string file)
        {
            WorkingDirectory = repo;
            Context = exec;

            _merger = Models.ExternalMerger.Supported.Find(x => x.Type == type);
            _exec = exec;
            _file = string.IsNullOrEmpty(file) ? "" : $"{file.Quoted()}";
        }

        public async Task<bool> OpenAsync()
        {
            if (_merger == null)
            {
                App.RaiseException(Context, "Invalid merge tool in preference setting!");
                return false;
            }

            if (_merger.Type == 0)
            {
                Args = $"mergetool {_file}";
            }
            else if (File.Exists(_exec))
            {
                var cmd = $"{_exec.Quoted()} {_merger.Cmd}";
                Args = $"-c mergetool.sourcegit.cmd={cmd.Quoted()} -c mergetool.writeToTemp=true -c mergetool.keepBackup=false -c mergetool.trustExitCode=true mergetool --tool=sourcegit {_file}";
            }
            else
            {
                App.RaiseException(Context, $"Can NOT find external merge tool in '{_exec}'!");
                return false;
            }

            return await ExecAsync().ConfigureAwait(false);
        }

        private Models.ExternalMerger _merger;
        private string _exec;
        private string _file;
    }
}
