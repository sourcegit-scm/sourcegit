using System;

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

            if (OperatingSystem.IsWindows())
            {
                return new DevSpaceLaunchSpec(
                    "pwsh",
                    ["-NoLogo", "-NoProfile", "-Command", command],
                    workingDirectory);
            }

            var shell = Environment.GetEnvironmentVariable("SHELL");
            if (string.IsNullOrWhiteSpace(shell))
                shell = "/bin/sh";

            return new DevSpaceLaunchSpec(shell, ["-lc", command], workingDirectory);
        }
    }
}
