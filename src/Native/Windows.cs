using Avalonia;
using Avalonia.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace SourceGit.Native {
    [SupportedOSPlatform("windows")]
    internal class Windows : OS.IBackend {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern bool PathFindOnPath([In, Out] StringBuilder pszFile, [In] string[] ppszOtherDirs);

        public void SetupApp(AppBuilder builder) {
            builder.With(new FontManagerOptions() {
                DefaultFamilyName = "Microsoft YaHei UI",
                FontFallbacks = [
                    new FontFallback { FontFamily = new FontFamily("Microsoft YaHei UI") }
                ]
            });
        }

        public string FindGitExecutable() {
            var reg = Microsoft.Win32.RegistryKey.OpenBaseKey(
                Microsoft.Win32.RegistryHive.LocalMachine,
                Microsoft.Win32.RegistryView.Registry64);

            var git = reg.OpenSubKey("SOFTWARE\\GitForWindows");
            if (git != null) {
                return Path.Combine(git.GetValue("InstallPath") as string, "bin", "git.exe");
            }

            var builder = new StringBuilder("git.exe", 259);
            if (!PathFindOnPath(builder, null)) {
                return null;
            }

            var exePath = builder.ToString();
            if (string.IsNullOrEmpty(exePath)) return null;

            return exePath;
        }

        public string FindVSCode() {
            var root = Microsoft.Win32.RegistryKey.OpenBaseKey(
                    Microsoft.Win32.RegistryHive.LocalMachine,
                    Environment.Is64BitOperatingSystem ? Microsoft.Win32.RegistryView.Registry64 : Microsoft.Win32.RegistryView.Registry32);

            var vscode = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C26E74D1-022E-4238-8B9D-1E7564A36CC9}_is1");
            if (vscode != null) {
                return vscode.GetValue("DisplayIcon") as string;
            }

            vscode = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{1287CAD5-7C8D-410D-88B9-0D1EE4A83FF2}_is1");
            if (vscode != null) {
                return vscode.GetValue("DisplayIcon") as string;
            }

            vscode = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{F8A2A208-72B3-4D61-95FC-8A65D340689B}_is1");
            if (vscode != null) {
                return vscode.GetValue("DisplayIcon") as string;
            }

            vscode = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{EA457B21-F73E-494C-ACAB-524FDE069978}_is1");
            if (vscode != null) {
                return vscode.GetValue("DisplayIcon") as string;
            }

            return string.Empty;
        }

        public void OpenBrowser(string url) {
            var info = new ProcessStartInfo("cmd", $"/c start {url}");
            info.CreateNoWindow = true;
            Process.Start(info);
        }

        public void OpenTerminal(string workdir) {
            var bash = Path.Combine(Path.GetDirectoryName(OS.GitInstallPath), "bash.exe");
            if (!File.Exists(bash)) {
                App.RaiseException(string.IsNullOrEmpty(workdir) ? "" : workdir, $"Can NOT found bash.exe under '{Path.GetDirectoryName(OS.GitInstallPath)}'");
                return;
            }

            var startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.FileName = bash;
            if (!string.IsNullOrEmpty(workdir) && Path.Exists(workdir)) startInfo.WorkingDirectory = workdir;
            Process.Start(startInfo);
        }

        public void OpenInFileManager(string path, bool select) {
            var fullpath = string.Empty;
            if (File.Exists(path)) {
                fullpath = new FileInfo(path).FullName;
            } else {
                fullpath = new DirectoryInfo(path).FullName;
            }

            if (select) {
                Process.Start("explorer", $"/select,\"{fullpath}\"");
            } else {
                Process.Start("explorer", fullpath);
            }
        }

        public void OpenWithDefaultEditor(string file) {
            var info = new FileInfo(file);
            var start = new ProcessStartInfo("cmd", $"/c start {info.FullName}");
            start.CreateNoWindow = true;
            Process.Start(start);
        }
    }
}
