using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text;

using Avalonia;
using Avalonia.Media;

namespace SourceGit.Native
{
    [SupportedOSPlatform("macOS")]
    internal class MacOS : OS.IBackend
    {
        public void SetupApp(AppBuilder builder)
        {
            builder.With(new FontManagerOptions()
            {
                DefaultFamilyName = "PingFang SC",
            });
        }

        public string FindGitExecutable()
        {
            if (File.Exists("/usr/bin/git"))
                return "/usr/bin/git";
            return string.Empty;
        }

        public List<Models.ExternalEditor> FindExternalEditors()
        {
            var editors = new List<Models.ExternalEditor>();

            var vscode = FindVSCode();
            if (!string.IsNullOrEmpty(vscode) && File.Exists(vscode))
            {
                editors.Add(new Models.ExternalEditor
                {
                    Name = "Visual Studio Code",
                    Icon = "vscode.png",
                    Executable = vscode,
                    OpenCmdArgs = "\"{0}\"",
                });
            }

            var vscodeInsiders = FindVSCodeInsiders();
            if (!string.IsNullOrEmpty(vscodeInsiders) && File.Exists(vscodeInsiders))
            {
                editors.Add(new Models.ExternalEditor
                {
                    Name = "Visual Studio Code - Insiders",
                    Icon = "vscode_insiders.png",
                    Executable = vscodeInsiders,
                    OpenCmdArgs = "\"{0}\"",
                });
            }

            var fleet = FindFleet();
            if (!string.IsNullOrEmpty(fleet) && File.Exists(fleet))
            {
                editors.Add(new Models.ExternalEditor
                {
                    Name = "JetBrains Fleet",
                    Icon = "fleet.png",
                    Executable = fleet,
                    OpenCmdArgs = "\"{0}\"",
                });
            }

            var sublime = FindSublimeText();
            if (!string.IsNullOrEmpty(sublime) && File.Exists(sublime))
            {
                editors.Add(new Models.ExternalEditor
                {
                    Name = "Sublime Text",
                    Icon = "sublime_text.png",
                    Executable = sublime,
                    OpenCmdArgs = "\"{0}\"",
                });
            }

            return editors;
        }

        public void OpenBrowser(string url)
        {
            Process.Start("open", url);
        }

        public void OpenInFileManager(string path, bool select)
        {
            if (Directory.Exists(path))
            {
                Process.Start("open", path);
            }
            else if (File.Exists(path))
            {
                Process.Start("open", $"\"{path}\" -R");
            }
        }

        public void OpenTerminal(string workdir)
        {
            var dir = string.IsNullOrEmpty(workdir) ? "~" : workdir;
            var builder = new StringBuilder();
            builder.AppendLine("on run argv");
            builder.AppendLine("    tell application \"Terminal\"");
            builder.AppendLine($"        do script \"cd '{dir}'\"");
            builder.AppendLine("        activate");
            builder.AppendLine("    end tell");
            builder.AppendLine("end run");

            var tmp = Path.GetTempFileName();
            File.WriteAllText(tmp, builder.ToString());

            var proc = Process.Start("/usr/bin/osascript", $"\"{tmp}\"");
            proc.Exited += (o, e) => File.Delete(tmp);
        }

        public void OpenWithDefaultEditor(string file)
        {
            Process.Start("open", file);
        }

        #region EXTERNAL_EDITORS_FINDER
        private string FindVSCode()
        {
            var toolPath = "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code";
            if (File.Exists(toolPath))
                return toolPath;

            var customPath = Environment.GetEnvironmentVariable("VSCODE_PATH");
            if (!string.IsNullOrEmpty(customPath))
                return customPath;

            return string.Empty;
        }

        private string FindVSCodeInsiders()
        {
            var toolPath = "/Applications/Visual Studio Code - Insiders.app/Contents/Resources/app/bin/code";
            if (File.Exists(toolPath))
                return toolPath;

            var customPath = Environment.GetEnvironmentVariable("VSCODE_INSIDERS_PATH");
            if (!string.IsNullOrEmpty(customPath))
                return customPath;

            return string.Empty;
        }

        private string FindFleet()
        {
            var toolPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Applications/Fleet.app/Contents/MacOS/Fleet";
            if (File.Exists(toolPath))
                return toolPath;

            var customPath = Environment.GetEnvironmentVariable("FLEET_PATH");
            if (!string.IsNullOrEmpty(customPath))
                return customPath;

            return string.Empty;
        }

        private string FindSublimeText()
        {
            if (File.Exists("/Applications/Sublime Text.app/Contents/SharedSupport/bin"))
            {
                return "/Applications/Sublime Text.app/Contents/SharedSupport/bin";
            }

            var customPath = Environment.GetEnvironmentVariable("SUBLIME_TEXT_PATH");
            if (!string.IsNullOrEmpty(customPath))
                return customPath;

            return string.Empty;
        }
        #endregion
    }
}
