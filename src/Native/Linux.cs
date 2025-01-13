using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

using Avalonia;

namespace SourceGit.Native
{
    [SupportedOSPlatform("linux")]
    internal class Linux : OS.IBackend
    {
        private FileStream _fs = null;
        public void SetupApp(AppBuilder builder)
        {
            builder.With(new X11PlatformOptions() { EnableIme = true });
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
            finder.VSCode(() => FindExecutable("code"));
            finder.VSCodeInsiders(() => FindExecutable("code-insiders"));
            finder.VSCodium(() => FindExecutable("codium"));
            finder.Fleet(FindJetBrainsFleet);
            finder.FindJetBrainsFromToolbox(() => $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}/JetBrains/Toolbox");
            finder.SublimeText(() => FindExecutable("subl"));
            finder.Zed(() => FindExecutable("zeditor"));
            return finder.Founded;
        }

        public void OpenBrowser(string url)
        {
            Process.Start("xdg-open", $"\"{url}\"");
        }

        public void OpenInFileManager(string path, bool select)
        {
            if (Directory.Exists(path))
            {
                Process.Start("xdg-open", $"\"{path}\"");
            }
            else
            {
                var dir = Path.GetDirectoryName(path);
                if (Directory.Exists(dir))
                    Process.Start("xdg-open", $"\"{dir}\"");
            }
        }

        public void OpenTerminal(string workdir)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var cwd = string.IsNullOrEmpty(workdir) ? home : workdir;

            var startInfo = new ProcessStartInfo();
            startInfo.WorkingDirectory = cwd;
            startInfo.FileName = OS.ShellOrTerminal;

            if (OS.ShellOrTerminal.EndsWith("wezterm", StringComparison.OrdinalIgnoreCase))
                startInfo.Arguments = $"start --cwd \"{cwd}\"";

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
            var proc = Process.Start("xdg-open", $"\"{file}\"");
            if (proc != null)
            {
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                    App.RaiseException("", $"Failed to open \"{file}\"");

                proc.Close();
            }
        }

        public bool EnsureSingleInstance()
        {
            var pidfile = Path.Combine(Path.GetTempPath(), "sourcegit.pid");
            var pid = Process.GetCurrentProcess().Id.ToString();
            Console.WriteLine("pid " + pid);

            try
            {
                _fs = File.OpenWrite(pidfile);
                _fs.Lock(0, 1000);
                new StreamWriter(_fs).Write(pid);
                return true;
            }
            catch (IOException)
            {
                Console.WriteLine("another SourceGit is running");
                return false;
            }
        }
        
        private string FindExecutable(string filename)
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var pathes = pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var path in pathes)
            {
                var test = Path.Combine(path, filename);
                if (File.Exists(test))
                    return test;
            }

            return string.Empty;
        }

        private string FindJetBrainsFleet()
        {
            var path = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}/JetBrains/Toolbox/apps/fleet/bin/Fleet";
            return File.Exists(path) ? path : FindExecutable("fleet");
        }
    }
}
