using System;
using System.Collections.Generic;

using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SourceGit.Models
{
    public class ShellOrTerminal
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string Exec { get; set; }

        public Bitmap Icon
        {
            get
            {
                var icon = AssetLoader.Open(new Uri($"avares://SourceGit/Resources/Images/ShellIcons/{Type}.png", UriKind.RelativeOrAbsolute));
                return new Bitmap(icon);
            }
        }

        public static readonly List<ShellOrTerminal> Supported;

        static ShellOrTerminal()
        {
            if (OperatingSystem.IsWindows())
            {
                Supported = new List<ShellOrTerminal>()
                {
                    new ShellOrTerminal("git-bash", "Git Bash", "bash.exe"),
                    new ShellOrTerminal("pwsh", "PowerShell", "pwsh.exe|powershell.exe"),
                    new ShellOrTerminal("cmd", "Command Prompt", "cmd.exe"),
                    new ShellOrTerminal("wt", "Windows Terminal", "wt.exe")
                };
            }
            else if (OperatingSystem.IsMacOS())
            {
                Supported = new List<ShellOrTerminal>()
                {
                    new ShellOrTerminal("mac-terminal", "Terminal", ""),
                    new ShellOrTerminal("iterm2", "iTerm", ""),
                    new ShellOrTerminal("warp", "Warp", ""),
                };
            }
            else
            {
                Supported = new List<ShellOrTerminal>()
                {
                    new ShellOrTerminal("gnome-terminal", "Gnome Terminal", "gnome-terminal"),
                    new ShellOrTerminal("konsole", "Konsole", "konsole"),
                    new ShellOrTerminal("xfce4-terminal", "Xfce4 Terminal", "xfce4-terminal"),
                    new ShellOrTerminal("lxterminal", "LXTerminal", "lxterminal"),
                    new ShellOrTerminal("deepin-terminal", "Deepin Terminal", "deepin-terminal"),
                    new ShellOrTerminal("mate-terminal", "MATE Terminal", "mate-terminal"),
                    new ShellOrTerminal("foot", "Foot", "foot"),
                    new ShellOrTerminal("wezterm", "WezTerm", "wezterm"),
                    new ShellOrTerminal("custom", "Custom", ""),
                };
            }
        }

        public ShellOrTerminal(string type, string name, string exec)
        {
            Type = type;
            Name = name;
            Exec = exec;
        }
    }
}
