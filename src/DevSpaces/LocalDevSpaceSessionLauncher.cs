using System;
using System.IO;

namespace SourceGit.DevSpaces
{
    public sealed class LocalDevSpaceSessionLauncher : IDevSpaceSessionLauncher
    {
        public DevSpaceLaunchSpec Create(string command, string workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("DevSpaces command must not be empty.", nameof(command));
            if (string.IsNullOrWhiteSpace(workingDirectory))
                throw new ArgumentException("DevSpaces working directory must not be empty.", nameof(workingDirectory));

            var normalized = command.Trim().ToLowerInvariant();
            if (OperatingSystem.IsWindows())
            {
                switch (normalized)
                {
                    case "pwsh":
                    case "__devspaces_pwsh__":
                        return new DevSpaceLaunchSpec(FindPowerShell7(), ["-NoLogo"], workingDirectory);
                    case "powershell":
                    case "powershell.exe":
                    case "__devspaces_powershell__":
                        return new DevSpaceLaunchSpec(FindWindowsPowerShell(), ["-NoLogo"], workingDirectory);
                    case "cmd":
                    case "cmd.exe":
                    case "__devspaces_cmd__":
                        return new DevSpaceLaunchSpec(FindCommandPrompt(), [], workingDirectory);
                    case "__devspaces_git_bash__":
                        return new DevSpaceLaunchSpec(FindGitBash(), ["--login", "-i"], workingDirectory);
                }

                var powerShell = Models.ShellOrTerminal.Supported.Find(x => x.Type == "pwsh");
                var process = Native.OS.FindTerminal(powerShell);
                if (string.IsNullOrWhiteSpace(process))
                    process = FindWindowsPowerShell();

                return new DevSpaceLaunchSpec(
                    process,
                    ["-NoLogo", "-NoProfile", "-Command", command],
                    workingDirectory);
            }

            var shell = Environment.GetEnvironmentVariable("SHELL");
            if (string.IsNullOrWhiteSpace(shell))
                shell = "/bin/sh";

            if (normalized == "__devspaces_shell__")
                return new DevSpaceLaunchSpec(shell, [], workingDirectory);

            return new DevSpaceLaunchSpec(shell, ["-lc", command], workingDirectory);
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

        private static string FindGitBash()
        {
            var git = Native.OS.GitExecutable;
            if (!string.IsNullOrWhiteSpace(git))
            {
                var gitDir = Path.GetDirectoryName(git);
                if (!string.IsNullOrWhiteSpace(gitDir))
                {
                    var sameDir = Path.Combine(gitDir, "bash.exe");
                    if (File.Exists(sameDir))
                        return sameDir;

                    var siblingBin = Path.GetFullPath(Path.Combine(gitDir, "..", "bin", "bash.exe"));
                    if (File.Exists(siblingBin))
                        return siblingBin;
                }
            }

            return "bash.exe";
        }
    }
}
