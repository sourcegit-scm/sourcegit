using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace SourceGit.Native {
    [SupportedOSPlatform("linux")]
    internal class Linux : OS.IBackend {
        public string FindGitExecutable() {
            if (File.Exists("/usr/bin/git")) return "/usr/bin/git";
            return string.Empty;
        }

        public string FindVSCode() {
            if (File.Exists("/usr/share/code/code")) return "/usr/share/code/code";
            return string.Empty;
        }

        public void OpenBrowser(string url) {
            if (!File.Exists("/usr/bin/xdg-open")) {
                App.RaiseException("", $"You should install xdg-open first!");
                return;
            }

            Process.Start("xdg-open", $"\"{url}\"");
        }

        public void OpenInFileManager(string path, bool select) {
            if (!File.Exists("/usr/bin/xdg-open")) {
                App.RaiseException("", $"You should install xdg-open first!");
                return;
            }

            if (Directory.Exists(path)) {
                Process.Start("xdg-open", $"\"{path}\"");
            } else {
                var dir = Path.GetDirectoryName(path);
                if (Directory.Exists(dir)) {
                    Process.Start("xdg-open", $"\"{dir}\"");
                }
            }
        }

        public void OpenTerminal(string workdir) {
            var dir = string.IsNullOrEmpty(workdir) ? "~" : workdir;
            if (File.Exists("/usr/bin/gnome-ternimal")) {
                Process.Start("/usr/bin/gnome-ternimal", $"--working-directory=\"{dir}\"");
            } else if (File.Exists("/usr/bin/konsole")) {
                Process.Start("/usr/bin/konsole", $"--workdir \"{dir}\"");
            } else if (File.Exists("/usr/bin/xfce4-terminal")) {
                Process.Start("/usr/bin/xfce4-terminal", $"--working-directory=\"{dir}\"");
            } else {
                App.RaiseException("", $"Only supports gnome-ternimal/konsole/xfce4-terminal!");
                return;
            }
        }

        public void OpenWithDefaultEditor(string file) {
            if (!File.Exists("/usr/bin/xdg-open")) {
                App.RaiseException("", $"You should install xdg-open first!");
                return;
            }

            var proc = Process.Start("xdg-open", $"\"{file}\"");
            proc.WaitForExit();

            if (proc.ExitCode != 0) {
                App.RaiseException("", $"Failed to open \"{file}\"");
            }

            proc.Close();
        }
    }
}
