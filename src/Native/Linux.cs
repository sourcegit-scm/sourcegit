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
        class Terminal
        {
            public string FilePath { get; set; } = string.Empty;
            public string OpenArgFormat { get; set; } = string.Empty;

            public Terminal(string exec, string fmt)
            {
                FilePath = exec;
                OpenArgFormat = fmt;
            }

            public void Open(string dir)
            {
                Process.Start(FilePath, string.Format(OpenArgFormat, dir));
            }
        }

        public Linux()
        {
            _xdgOpenPath = FindExecutable("xdg-open");
            _terminal = FindTerminal();
        }

        public void SetupApp(AppBuilder builder)
        {
            builder.With(new FontManagerOptions()
            {
                DefaultFamilyName = "fonts:SourceGit#JetBrains Mono",
            });

            builder.With(new X11PlatformOptions()
            {
                EnableIme = true,
            });

            // Free-desktop file picker has an extra black background panel.
            builder.UseManagedSystemDialogs();
        }

        public string FindGitExecutable()
        {
            return FindExecutable("git");
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
            return finder.Founded;
        }

        public void OpenBrowser(string url)
        {
            if (string.IsNullOrEmpty(_xdgOpenPath))
                App.RaiseException("", $"Can NOT find `xdg-open` command!!!");
            else
                Process.Start(_xdgOpenPath, $"\"{url}\"");
        }

        public void OpenInFileManager(string path, bool select)
        {
            if (string.IsNullOrEmpty(_xdgOpenPath))
            {
                App.RaiseException("", $"Can NOT find `xdg-open` command!!!");
                return;
            }

            if (Directory.Exists(path))
            {
                Process.Start(_xdgOpenPath, $"\"{path}\"");
            }
            else
            {
                var dir = Path.GetDirectoryName(path);
                if (Directory.Exists(dir))
                    Process.Start(_xdgOpenPath, $"\"{dir}\"");
            }
        }

        public void OpenTerminal(string workdir)
        {
            var dir = string.IsNullOrEmpty(workdir) ? "~" : workdir;
            if (_terminal == null)
                App.RaiseException(dir, $"Only supports gnome-terminal/konsole/xfce4-terminal/lxterminal/deepin-terminal!");
            else
                _terminal.Open(dir);
        }

        public void OpenWithDefaultEditor(string file)
        {
            if (string.IsNullOrEmpty(_xdgOpenPath))
            {
                App.RaiseException("", $"Can NOT find `xdg-open` command!!!");
                return;
            }

            var proc = Process.Start(_xdgOpenPath, $"\"{file}\"");
            proc.WaitForExit();

            if (proc.ExitCode != 0)
                App.RaiseException("", $"Failed to open \"{file}\"");

            proc.Close();
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

        private Terminal FindTerminal()
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var pathes = pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var path in pathes)
            {
                var test = Path.Combine(path, "gnome-terminal");
                if (File.Exists(test))
                    return new Terminal(test, "--working-directory=\"{0}\"");

                test = Path.Combine(path, "konsole");
                if (File.Exists(test))
                    return new Terminal(test, "--workdir \"{0}\"");

                test = Path.Combine(path, "xfce4-terminal");
                if (File.Exists(test))
                    return new Terminal(test, "--working-directory=\"{0}\"");

                test = Path.Combine(path, "lxterminal");
                if (File.Exists(test))
                    return new Terminal(test, "--working-directory=\"{0}\"");

                test = Path.Combine(path, "deepin-terminal");
                if (File.Exists(test))
                    return new Terminal(test, "--work-directory \"{0}\"");

                test = Path.Combine(path, "mate-terminal");
                if (File.Exists(test))
                    return new Terminal(test, "--working-directory=\"{0}\"");
            }

            return null;
        }

        private string FindJetBrainsFleet()
        {
            var path = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}/JetBrains/Toolbox/apps/fleet/bin/Fleet";
            return File.Exists(path) ? path : FindExecutable("fleet");
        }

        private string _xdgOpenPath = string.Empty;
        private Terminal _terminal = null;
    }
}
