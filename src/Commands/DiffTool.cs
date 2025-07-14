using System.IO;

namespace SourceGit.Commands
{
    public class DiffTool : Command
    {
        public DiffTool(string repo, int type, string exec, Models.DiffOption option)
        {
            WorkingDirectory = repo;
            Context = repo;

            _merger = Models.ExternalMerger.Supported.Find(x => x.Type == type);
            _exec = exec;
            _option = option;
        }

        public void Open()
        {
            if (_merger == null)
            {
                App.RaiseException(Context, "Invalid merge tool in preference setting!");
                return;
            }

            if (_merger.Type == 0)
            {
                Args = $"difftool -g --no-prompt {_option}";
            }
            else if (File.Exists(_exec))
            {
                var cmd = $"{_exec.Quoted()} {_merger.DiffCmd}";
                Args = $"-c difftool.sourcegit.cmd={cmd.Quoted()} difftool --tool=sourcegit --no-prompt {_option}";
            }
            else
            {
                App.RaiseException(Context, $"Can NOT find external diff tool in '{_exec}'!");
                return;
            }

            Exec();
        }

        private Models.ExternalMerger _merger;
        private string _exec;
        private Models.DiffOption _option;
    }
}
