using System;
using System.IO;

namespace SourceGit.DevSpaces
{
    public static class DevSpaceToolHealth
    {
        public static ViewModels.DevSpaceCapabilityState CheckCommand(string command, string path = null)
        {
            if (string.IsNullOrWhiteSpace(command))
                return ViewModels.DevSpaceCapabilityState.Unavailable;

            try
            {
                var searchPath = path ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                foreach (var directory in searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (CommandExists(directory, command))
                        return ViewModels.DevSpaceCapabilityState.Available;
                }
            }
            catch
            {
                return ViewModels.DevSpaceCapabilityState.Failed;
            }

            return ViewModels.DevSpaceCapabilityState.Unavailable;
        }

        private static bool CommandExists(string directory, string command)
        {
            if (File.Exists(Path.Combine(directory, command)))
                return true;

            if (!OperatingSystem.IsWindows())
                return false;

            var extensions = Environment.GetEnvironmentVariable("PATHEXT");
            if (string.IsNullOrWhiteSpace(extensions))
                extensions = ".COM;.EXE;.BAT;.CMD";

            foreach (var extension in extensions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (File.Exists(Path.Combine(directory, command + extension.ToLowerInvariant())) ||
                    File.Exists(Path.Combine(directory, command + extension.ToUpperInvariant())))
                    return true;
            }

            return false;
        }
    }
}
