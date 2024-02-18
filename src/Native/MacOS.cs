using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace SourceGit.Native {
    [SupportedOSPlatform("macOS")]
    internal class MacOS : OS.IBackend {
        public string FindGitInstallDir() {
            if (File.Exists("/usr/bin/git")) return "/usr";
            return string.Empty;
        }

        public string FindVSCode() {
            if (File.Exists("/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code")) {
                return "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code";
            }

            return string.Empty;
        }

        public void OpenBrowser(string url) {
            Process.Start("open", url);
        }

        public void OpenInFileManager(string path, bool select) {
            if (Directory.Exists(path)) {
                Process.Start("open", path);
            } else if (File.Exists(path)) {
                Process.Start("open", $"\"{path}\" -R");
            }
        }

        public void OpenTerminal(string workdir) {
            Process.Start(new ProcessStartInfo() {
                WorkingDirectory = workdir,
                FileName = "/Applications/Utilities/Terminal.app/Contents/MacOS/Terminal",
                UseShellExecute = false,
            });
        }

        public void OpenWithDefaultEditor(string file) {
            Process.Start("open", file);
        }
    }
}
