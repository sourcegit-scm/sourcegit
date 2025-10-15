using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace SourceGit.Native
{
    [SupportedOSPlatform("linux")]
    internal class Linux : OS.IBackend
    {
        private static readonly string LOCAL_APP_DATA_DIR = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public void SetupApp(AppBuilder builder)
        {
            builder.With(new X11PlatformOptions() { EnableIme = true });
        }

        public void SetupWindow(Window window)
        {
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
            var finder = new Models.ExternalToolsFinder();
            finder.VSCode(() => FindExecutable("code", "com.visualstudio.code"));
            finder.VSCodeInsiders(() => FindExecutable("code-insiders", "com.vscodium.codium-insiders"));
            finder.VSCodium(() => FindExecutable("codium", "com.vscodium.codium"));
            finder.Cursor(() => FindExecutable("cursor"));
            finder.Fleet(FindJetBrainsFleet);
            finder.FindJetBrainsFromToolbox(() => Path.Combine(LOCAL_APP_DATA_DIR, "JetBrains/Toolbox"));
            FindJetBrainsFromFlatpak(finder);
            finder.SublimeText(() => FindExecutable("subl", "com.sublimetext.three"));
            finder.Zed(() => FindExecutable("zeditor", "dev.zed.Zed"));
            return finder.Tools;
        }

        public void OpenBrowser(string url)
        {
            Process.Start("xdg-open", url.Quoted());
        }

        public void OpenInFileManager(string path, bool select)
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

        public void OpenTerminal(string workdir)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var cwd = string.IsNullOrEmpty(workdir) ? home : workdir;
            var terminal = OS.ShellOrTerminal;

            var startInfo = new ProcessStartInfo();
            startInfo.WorkingDirectory = cwd;
            startInfo.FileName = terminal;

            if (terminal.EndsWith("wezterm", StringComparison.OrdinalIgnoreCase))
                startInfo.Arguments = $"start --cwd {cwd.Quoted()}";
            else if (terminal.EndsWith("ptyxis", StringComparison.OrdinalIgnoreCase))
                startInfo.Arguments = $"--new-window --working-directory={cwd.Quoted()}";

            try
            {
                Process.Start(startInfo);
            }
            catch (Exception e)
            {
                App.RaiseException(workdir, $"Failed to start '{OS.ShellOrTerminal}'. Reason: {e.Message}");
            }
        }

        public void OpenWithDefaultEditor(string file)
        {
            var proc = Process.Start("xdg-open", file.Quoted());
            if (proc != null)
            {
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                    App.RaiseException("", $"Failed to open: {file}");

                proc.Close();
            }
        }

        private static string FindExecutable(string filename, string flatpakAppId = null)
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var paths = pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var path in paths)
            {
                var test = Path.Combine(path, filename);
                if (File.Exists(test))
                    return test;
            }

            if (flatpakAppId != null)
            {
                foreach (var path in new[] { "/var/lib", LOCAL_APP_DATA_DIR })
                {
                    var test = Path.Combine(path, "flatpak/exports/bin", flatpakAppId);
                    if (File.Exists(test))
                        return test;
                }
            }

            return string.Empty;
        }

        private static string FindJetBrainsFleet()
        {
            var path = Path.Combine(LOCAL_APP_DATA_DIR, "JetBrains/Toolbox/apps/fleet/bin/Fleet");
            return File.Exists(path) ? path : FindExecutable("fleet");
        }

        private static void FindJetBrainsFromFlatpak(Models.ExternalToolsFinder finder)
        {
            foreach (var basePath in new[] { "/var/lib", LOCAL_APP_DATA_DIR })
            {
                var binPath = Path.Combine(basePath, "flatpak/exports/bin");
                if (Directory.Exists(binPath))
                {
                    foreach (var file in Directory.GetFiles(binPath, "com.jetbrains.*"))
                    {
                        var fileName = Path.GetFileName(file);
                        var appName = fileName[14..].Replace("-", " ");
                        var icon = new string(Array.FindAll(fileName.ToCharArray(), char.IsUpper));
                        if (icon.Length > 2)
                            icon = icon[..2];
                        icon = icon switch
                        {
                            "DG" => "DB", // DataGrip
                            "GL" => "GO", // GoLand
                            "IJ" => "JB", // IntelliJ
                            "R" => "RD",  // Rider
                            _ => icon
                        };
                        finder.Tools.Add(new Models.ExternalTool(appName, "JetBrains/" + icon, file));
                    }
                }
            }
        }
    }
}
