using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Media;

namespace SourceGit.Native
{
    [SupportedOSPlatform("linux")]
    internal class Linux : OS.IBackend
    {
        public void SetupApp(AppBuilder builder)
        {
            builder.With(new FontManagerOptions()
            {
                DefaultFamilyName = "fonts:SourceGit#JetBrains Mono",
            });

            // Free-desktop file picker has an extra black background panel.
            builder.UseManagedSystemDialogs();
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
                    Icon = new Uri("avares://SourceGit/Resources/ExternalToolIcons/vscode.png", UriKind.Absolute),
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
                    Icon = new Uri("avares://SourceGit/Resources/ExternalToolIcons/vscode_insiders.png", UriKind.Absolute),
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
                    Icon = new Uri("avares://SourceGit/Resources/ExternalToolIcons/fleet.png", UriKind.Absolute),
                    Executable = fleet,
                    OpenCmdArgs = "\"{0}\"",
                });
            }

            return editors;
        }

        public void OpenBrowser(string url)
        {
            if (!File.Exists("/usr/bin/xdg-open"))
            {
                App.RaiseException("", $"You should install xdg-open first!");
                return;
            }

            Process.Start("xdg-open", $"\"{url}\"");
        }

        public void OpenInFileManager(string path, bool select)
        {
            if (!File.Exists("/usr/bin/xdg-open"))
            {
                App.RaiseException("", $"You should install xdg-open first!");
                return;
            }

            if (Directory.Exists(path))
            {
                Process.Start("xdg-open", $"\"{path}\"");
            }
            else
            {
                var dir = Path.GetDirectoryName(path);
                if (Directory.Exists(dir))
                {
                    Process.Start("xdg-open", $"\"{dir}\"");
                }
            }
        }

        public void OpenTerminal(string workdir)
        {
            var dir = string.IsNullOrEmpty(workdir) ? "~" : workdir;
            if (File.Exists("/usr/bin/gnome-terminal"))
            {
                Process.Start("/usr/bin/gnome-terminal", $"--working-directory=\"{dir}\"");
            }
            else if (File.Exists("/usr/bin/konsole"))
            {
                Process.Start("/usr/bin/konsole", $"--workdir \"{dir}\"");
            }
            else if (File.Exists("/usr/bin/xfce4-terminal"))
            {
                Process.Start("/usr/bin/xfce4-terminal", $"--working-directory=\"{dir}\"");
            }
            else
            {
                App.RaiseException("", $"Only supports gnome-terminal/konsole/xfce4-terminal!");
                return;
            }
        }

        public void OpenWithDefaultEditor(string file)
        {
            if (!File.Exists("/usr/bin/xdg-open"))
            {
                App.RaiseException("", $"You should install xdg-open first!");
                return;
            }

            var proc = Process.Start("xdg-open", $"\"{file}\"");
            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                App.RaiseException("", $"Failed to open \"{file}\"");
            }

            proc.Close();
        }

        #region EXTERNAL_EDITORS_FINDER
        private string FindVSCode()
        {
            var toolPath = "/usr/share/code/code";
            if (File.Exists(toolPath))
                return toolPath;

            var customPath = Environment.GetEnvironmentVariable("VSCODE_PATH");
            if (!string.IsNullOrEmpty(customPath))
                return customPath;

            return string.Empty;
        }

        private string FindVSCodeInsiders()
        {
            var toolPath = "/usr/share/code/code";
            if (File.Exists(toolPath))
                return toolPath;

            var customPath = Environment.GetEnvironmentVariable("VSCODE_INSIDERS_PATH");
            if (!string.IsNullOrEmpty(customPath))
                return customPath;

            return string.Empty;
        }

        private string FindFleet()
        {
            var toolPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.local/share/JetBrains/Toolbox/apps/fleet/bin/Fleet";
            if (File.Exists(toolPath))
                return toolPath;

            var customPath = Environment.GetEnvironmentVariable("FLEET_PATH");
            if (!string.IsNullOrEmpty(customPath))
                return customPath;

            return string.Empty;
        }
        #endregion
    }
}
