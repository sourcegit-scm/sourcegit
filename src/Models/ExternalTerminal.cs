using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SourceGit.Models
{
    public class ExternalTerminal
    {
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Executable { get; set; } = string.Empty;
        public string OpenCmdArgs { get; set; } = string.Empty;

        public void Open(string repo)
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
            TryAdd("Git Bash", "git-bash.png", "bash", "\"{0}\"", platform_finder);
        }

        public void Gnome(Func<string> platform_finder)
        {
            TryAdd("gnome-terminal", "gnome.png", "/usr/bin/gnome-terminal", "--working-directory=\"{0}\"", platform_finder);
        }

        public void Konsole(Func<string> platform_finder)
        {
            TryAdd("gnome-terminal", "konsole.png", "/usr/bin/konsole", "--workdir \"{0}\"", platform_finder);
        }

        public void osaScript(Func<string> platform_finder)
        {
            TryAdd("AppleScript", "osascript.png", "/usr/bin/osascript",
                """
                on run argv
                   tell application "Terminal"
                        do script "cd '{0}'"
                        activate
                    end tell
                end run
                """,
                platform_finder);
        }

        public void PowerShell(Func<string> platform_finder)
        {
            TryAdd("PowerShell", "pwsh.png", "pwsh", "-WorkingDirectory \"{0}\"", platform_finder);
        }

        public void WindowsTerminal(Func<string> platform_finder)
        {
            TryAdd("Windows Terminal", "wt.png", "wt", "-d \"{0}\"", platform_finder);
        }

        public void Xfce4(Func<string> platform_finder)
        {
            TryAdd("gnome-terminal", "xfce4.png", "/usr/bin/xfce4-terminal", "--working-directory=\"{0}\"", platform_finder);
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
