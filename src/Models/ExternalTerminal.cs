using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SourceGit.Models
{
    public record ExternalTerminal
    {
        public string Name { get; init; } = string.Empty;
        public string Icon { get; init; } = string.Empty;
        public string Executable { get; init; } = string.Empty;
        public string OpenCmdArgs { get; init; } = string.Empty;

        public virtual void Open(string repo)
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
    
    public class ExternalTerminalFinder
    {
        public List<ExternalTerminal> Terminals
        {
            get;
            private set;
        } = new List<ExternalTerminal>();

        public void WindowsGitBash(Func<string> platform_finder)
        {
            TryAdd("git-bash", "git-bash.png", "bash", "", platform_finder);
        }

        public void Gnome(Func<string> platform_finder)
        {
            TryAdd("gnome-terminal", "gnome.png", "gnome", "--working-directory=\"{0}\"", platform_finder);
        }

        public void Konsole(Func<string> platform_finder)
        {
            TryAdd("konsole", "konsole.png", "konsole", "--workdir \"{0}\"", platform_finder);
        }

        public void AppleScript(ExternalTerminal terminal)
        {
            var path = terminal.Executable;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            Terminals.Add(terminal with
            {
                Name = "osascript",
                Icon = "osascript.png",
            });
        }

        public void PowerShell(Func<string> platform_finder)
        {
            TryAdd("pwsh", "pwsh.png", "pwsh", "-WorkingDirectory \"{0}\"", platform_finder);
        }

        public void WindowsTerminal(Func<string> platform_finder)
        {
            TryAdd("wt", "wt.png", "wt", "-d \"{0}\"", platform_finder);
        }

        public void Xfce4(Func<string> platform_finder)
        {
            TryAdd("xfce4", "xfce4.png", "xfce4", "--working-directory=\"{0}\"", platform_finder);
        }

        private void TryAdd(string name, string icon, string cmd, string args, Func<string> finder)
        {
            var path = Environment.GetEnvironmentVariable(cmd);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                path = finder();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return;
            }

            Terminals.Add(new ExternalTerminal
            {
                Name = name,
                Icon = icon,
                OpenCmdArgs = args,
                Executable = path,
            });
        }
    }
}
