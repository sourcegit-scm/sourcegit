using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SourceGit.Models
{
    public class ExternalEditor
    {
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
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

    public class ExternalEditorFinder
    {
        public List<ExternalEditor> Editors
        {
            get;
            private set;
        } = new List<ExternalEditor>();

        public void VSCode(Func<string> platform_finder)
        {
            TryAdd("Visual Studio Code", "vscode.png", "\"{0}\"", "VSCODE_PATH", platform_finder);
        }

        public void VSCodeInsiders(Func<string> platform_finder)
        {
            TryAdd("Visual Studio Code - Insiders", "vscode_insiders.png", "\"{0}\"", "VSCODE_INSIDERS_PATH", platform_finder);
        }

        public void Fleet(Func<string> platform_finder)
        {
            TryAdd("JetBrains Fleet", "fleet.png", "\"{0}\"", "FLEET_PATH", platform_finder);
        }

        public void SublimeText(Func<string> platform_finder)
        {
            TryAdd("Sublime Text", "sublime_text.png", "\"{0}\"", "SUBLIME_TEXT_PATH", platform_finder);
        }

        private void TryAdd(string name, string icon, string args, string env, Func<string> finder)
        {
            var path = Environment.GetEnvironmentVariable(env);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                path = finder();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return;
            }

            Editors.Add(new ExternalEditor
            {
                Name = name,
                Icon = icon,
                OpenCmdArgs = args,
                Executable = path,
            });
        }
    }
}
