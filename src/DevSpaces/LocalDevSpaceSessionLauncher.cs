using System;
using System.IO;

namespace SourceGit.DevSpaces
{
    public sealed class LocalDevSpaceSessionLauncher : IDevSpaceSessionLauncher
    {
        public DevSpaceLaunchSpec Create(string terminal, string workingDirectory, string startupCommand = null)
        {
            if (string.IsNullOrWhiteSpace(workingDirectory))
                throw new ArgumentException("DevSpaces working directory must not be empty.", nameof(workingDirectory));

            var normalized = terminal?.Trim().ToLowerInvariant();
            if (normalized is "pwsh" or DevSpaceProfileSettings.PowerShell7)
            {
                var process = OperatingSystem.IsWindows() ? FindPowerShell7() : "pwsh";
                return new DevSpaceLaunchSpec(process, ["-NoLogo"], workingDirectory, startupCommand);
            }

            if (OperatingSystem.IsWindows())
            {
                switch (normalized)
                {
                    case "powershell":
                    case "powershell.exe":
                    case DevSpaceProfileSettings.WindowsPowerShell:
                        return new DevSpaceLaunchSpec(FindWindowsPowerShell(), ["-NoLogo"], workingDirectory, startupCommand);
                    case "cmd":
                    case "cmd.exe":
                    case DevSpaceProfileSettings.CommandPrompt:
                        return new DevSpaceLaunchSpec(FindCommandPrompt(), [], workingDirectory, startupCommand);
                    default:
                        return new DevSpaceLaunchSpec(FindPowerShell7(), ["-NoLogo"], workingDirectory, startupCommand);
                }
            }

            var shell = Environment.GetEnvironmentVariable("SHELL");
            if (string.IsNullOrWhiteSpace(shell))
                shell = "/bin/sh";

            return new DevSpaceLaunchSpec(shell, [], workingDirectory, startupCommand);
        }

        private static string FindPowerShell7()
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                var candidate = Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe");
                if (File.Exists(candidate))
                    return candidate;
            }

            return "pwsh.exe";
        }

        private static string FindWindowsPowerShell()
        {
            var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (!string.IsNullOrWhiteSpace(system))
            {
                var candidate = Path.Combine(system, "WindowsPowerShell", "v1.0", "powershell.exe");
                if (File.Exists(candidate))
                    return candidate;
            }

            return "powershell.exe";
        }

        private static string FindCommandPrompt()
        {
            var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (!string.IsNullOrWhiteSpace(system))
            {
                var candidate = Path.Combine(system, "cmd.exe");
                if (File.Exists(candidate))
                    return candidate;
            }

            return "cmd.exe";
        }
    }
}
