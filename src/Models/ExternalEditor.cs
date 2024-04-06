using System;
using System.Diagnostics;

namespace SourceGit.Models
{
    public class ExternalEditor
    {
        public string Name { get; set; } = string.Empty;
        public Uri Icon { get; set; } = null;
        public string Executable { get; set; } = string.Empty;
        public string OpenCmdArgs { get; set; } = string.Empty;

        public void Open(string repo)
        {
            Process.Start(new ProcessStartInfo()
            {
                WorkingDirectory = repo,
                FileName = Executable,
                Arguments = string.Format(OpenCmdArgs, repo),
                UseShellExecute = false,
            });
        }
    }
}
