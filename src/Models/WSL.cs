using System;
using System.Diagnostics;
using System.Text;

namespace SourceGit.Models
{
    public class WSL
    {
        public string Path { get; set; } = "";

        public bool IsWSLPath()
        {
            return OperatingSystem.IsWindows() && !string.IsNullOrEmpty(Path) &&
                (Path.StartsWith("//wsl.localhost/", StringComparison.OrdinalIgnoreCase) ||
                Path.StartsWith("//wsl$/", StringComparison.OrdinalIgnoreCase));
        }

        public void SetEnvironmentForProcess(ProcessStartInfo start)
        {
            start.Environment.Add("LANG", "C");
            start.Environment.Add("LC_ALL", "C");

            if (start.Environment.TryGetValue("SSH_ASKPASS", out var askPassPath) && !string.IsNullOrEmpty(askPassPath) && System.IO.Path.IsPathRooted(askPassPath))
            {
                // Convert Windows path to WSL path
                var driveLetter = askPassPath[0].ToString();
                start.Environment["SSH_ASKPASS"] = askPassPath
                    .Replace($"{driveLetter}:\\", $"/mnt/{driveLetter.ToLowerInvariant()}/")
                    .Replace('\\', '/');
            }

            var wslEnvirionment = new[] { "SSH_ASKPASS", "SSH_ASKPASS_REQUIRE", "SOURCEGIT_LAUNCH_AS_ASKPASS", "GIT_SSH_COMMAND", "LANG", "LC_ALL" };
            var wslEnvBuilder = new StringBuilder();

            foreach (string env in wslEnvirionment)
            {
                if (start.Environment.ContainsKey(env))
                    wslEnvBuilder.Append($"{env}:");
            }

            // Forward environment variables for WSL
            start.Environment.Add("WSLENV", wslEnvBuilder.ToString().TrimEnd(':'));
        }
    }
}
