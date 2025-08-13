using System;
using System.Diagnostics;

namespace SourceGit.Commands
{
    public class DiffTool : Command
    {
        public DiffTool(string repo, Models.DiffOption option)
        {
            WorkingDirectory = repo;
            Context = repo;
            _option = option;
        }

        public void Open()
        {
            var tool = Native.OS.GetDiffMergeTool(true);
            if (tool == null)
            {
                App.RaiseException(Context, "Invalid merge tool in preference setting!");
                return;
            }

            if (string.IsNullOrEmpty(tool.Cmd))
            {
                Args = $"difftool -g --no-prompt {_option}";
            }
            else
            {
                var cmd = $"{tool.Exec.Quoted()} {tool.Cmd}";
                Args = $"-c difftool.sourcegit.cmd={cmd.Quoted()} difftool --tool=sourcegit --no-prompt {_option}";
            }

            try
            {
                Process.Start(CreateGitStartInfo(false));
            }
            catch (Exception ex)
            {
                App.RaiseException(Context, ex.Message);
            }
        }

        private Models.DiffOption _option;
    }
}
