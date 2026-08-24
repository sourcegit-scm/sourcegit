using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace SourceGit.Native
{
    [SupportedOSPlatform("linux")]
    internal class Linux : OS.IBackend
    {
        [DllImport("libc", SetLastError = true)]
        private static extern int kill(int pid, int sig);

        public void SetupApp(AppBuilder builder)
        {
            builder.With(new X11PlatformOptions() { EnableIme = true });
        }

        public void SetupWindow(Window window)
        {
            window.BorderThickness = new Thickness(0);

            if (OS.UseSystemWindowFrame)
            {
                window.ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.Default;
                window.ExtendClientAreaToDecorationsHint = false;
            }
            else
            {
                window.ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
                window.ExtendClientAreaToDecorationsHint = true;
                window.Classes.Add("custom_window_frame");
            }
        }

        public OS.Directories GetOrCreateDirectories()
        {
            var dirs = new OS.Directories();
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // AppImage supports portable mode
            var appImage = Environment.GetEnvironmentVariable("APPIMAGE");
            if (!string.IsNullOrEmpty(appImage) && File.Exists(appImage))
            {
                var portableDir = Path.Combine(Path.GetDirectoryName(appImage)!, "data");
                if (Directory.Exists(portableDir))
                {
                    dirs.ConfigDir = portableDir;
                    dirs.CacheDir = portableDir;
                    return dirs;
                }
            }

            // XDG Base Directory Specification: https://specifications.freedesktop.org/basedir/latest/
            dirs.ConfigDir = GetXdgDirectory("XDG_CONFIG_HOME", Path.Combine(home, ".config"), "SourceGit");
            dirs.CacheDir = GetXdgDirectory("XDG_CACHE_HOME", Path.Combine(home, ".cache"), "SourceGit");

            // If the app basic dirs already exist, we can skip the migration step
            if (Directory.Exists(dirs.ConfigDir))
                return dirs;

            // Migrate legacy data dir: ~/.sourcegit to XDG standard directories
            var legacyDir = Path.Combine(home, ".sourcegit");
            if (Directory.Exists(legacyDir))
            {
                try
                {
                    File.Copy(Path.Combine(legacyDir, "preference.json"), Path.Combine(dirs.ConfigDir, "preference.json"), true);
                    Directory.Move(Path.Combine(legacyDir, "avatars"), Path.Combine(dirs.CacheDir, "avatars"));
                    Directory.Delete(legacyDir, true);
                }
                catch
                {
                    // Ignore any errors during migration
                }
            }

            // Create the config and cache directories if they don't exist
            if (!Directory.Exists(dirs.ConfigDir))
                Directory.CreateDirectory(dirs.ConfigDir);
            if (!Directory.Exists(dirs.CacheDir))
                Directory.CreateDirectory(dirs.CacheDir);

            return dirs;
        }

        public string FindGitExecutable()
        {
            return FindExecutable("git");
        }

        public string FindTerminal(Models.ShellOrTerminal shell)
        {
            if (shell.Type.Equals("custom", StringComparison.Ordinal))
                return string.Empty;

            return FindExecutable(shell.Exec);
        }

        public List<Models.ExternalTool> FindExternalTools()
        {
            var localAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var finder = new Models.ExternalToolsFinder();
            finder.VSCode(() => FindExecutable("code"));
            finder.VSCodeInsiders(() => FindExecutable("code-insiders"));
            finder.VSCodium(() => FindExecutable("codium"));
            finder.Cursor(() => FindExecutable("cursor"));
            finder.FindJetBrainsFromToolbox(() => Path.Combine(localAppDataDir, "JetBrains/Toolbox"));
            finder.SublimeText(() => FindExecutable("subl"));
            finder.Zed(() =>
            {
                var exec = FindExecutable("zeditor");
                return string.IsNullOrEmpty(exec) ? FindExecutable("zed") : exec;
            });
            return finder.Tools;
        }

        public void OpenBrowser(string url)
        {
            var browser = Environment.GetEnvironmentVariable("BROWSER");
            if (string.IsNullOrEmpty(browser))
                browser = "xdg-open";
            Process.Start(browser, url.Quoted());
        }

        public void OpenInFileManager(string path)
        {
            if (Directory.Exists(path))
            {
                Process.Start("xdg-open", path.Quoted());
            }
            else
            {
                var dir = Path.GetDirectoryName(path);
                if (Directory.Exists(dir))
                    Process.Start("xdg-open", dir.Quoted());
            }
        }

        public void OpenTerminal(string workdir, string args)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var cwd = string.IsNullOrEmpty(workdir) ? home : workdir;

            var startInfo = new ProcessStartInfo();
            startInfo.WorkingDirectory = cwd;
            startInfo.FileName = OS.ShellOrTerminal;
            startInfo.Arguments = args;

            try
            {
                Process.Start(startInfo);
            }
            catch (Exception e)
            {
                Models.Notification.Send(workdir, $"Failed to start '{OS.ShellOrTerminal}'. Reason: {e.Message}", true);
            }
        }

        public void OpenWithDefaultEditor(string file)
        {
            var proc = Process.Start("xdg-open", file.Quoted());
            if (proc != null)
            {
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                    Models.Notification.Send("", $"Failed to open: {file}", true);

                proc.Close();
            }
        }

        public void TerminateProcess(Process proc)
        {
            if (kill(-proc.Id, 15) != 0)
                proc.Kill(true); // Fallback to force kill if the process is not terminated by SIGTERM
        }

        private string FindExecutable(string filename)
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var paths = pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var path in paths)
            {
                var test = Path.Combine(path, filename);
                if (File.Exists(test))
                    return test;
            }

            var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", filename);
            return File.Exists(local) ? local : string.Empty;
        }

        private string GetXdgDirectory(string envVar, string fallback, string subDirName)
        {
            var dir = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                return Path.Combine(dir, subDirName);

            return Path.Combine(fallback, subDirName);
        }
    }
}
