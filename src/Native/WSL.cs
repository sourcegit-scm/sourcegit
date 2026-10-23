using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceGit.Native
{
    // Repositories inside a WSL distro are addressed by UNC paths (//wsl.localhost/<distro>/...) everywhere in the app.
    // Linux paths only exist at the git process boundary, this class converts between the two.
    public static partial class WSL
    {
        // shortcut: assumes the default automount root, upgrade when someone changes [automount] root in /etc/wsl.conf.
        private const string MOUNT_ROOT = "/mnt/";
        private const string GCM_WINDOWS_PATH = @"C:\Program Files\Git\mingw64\bin\git-credential-manager.exe";
        private const string GCM_LINUX_PATH = "/mnt/c/Program Files/Git/mingw64/bin/git-credential-manager.exe";

        private static readonly ConcurrentDictionary<string, string> _homes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Version> _gitVersions = new(StringComparer.OrdinalIgnoreCase);

        [GeneratedRegex(@"^git version[\s\w]*(\d+)\.(\d+)[\.\-](\d+).*$")]
        private static partial Regex REG_GIT_VERSION();

        // Lets the preferences supply the credential helper configured for a server.
        public static Func<string, string> CredentialHelperOverride { get; set; }

        // The configured helper, else Git for Windows' credential manager if installed, else empty (the distro's own git config applies).
        public static string CredentialHelperFor(string distro)
        {
            var custom = CredentialHelperOverride?.Invoke(distro);
            if (!string.IsNullOrWhiteSpace(custom))
                return custom.Trim();

            return File.Exists(GCM_WINDOWS_PATH) ? GCM_LINUX_PATH : string.Empty;
        }

        // Names of the installed distros, empty if WSL is not available.
        public static string[] ListDistros()
        {
            var start = new ProcessStartInfo();
            start.FileName = "wsl.exe";
            start.Arguments = "-l -q";
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.StandardOutputEncoding = Encoding.Unicode; // wsl.exe prints its own output as UTF-16LE.

            try
            {
                using var proc = Process.Start(start)!;
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                return proc.ExitCode == 0 ? ParseDistros(output) : [];
            }
            catch
            {
                return [];
            }
        }

        public static string[] ParseDistros(string output)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var line in output.Split('\n'))
            {
                var name = line.Replace("\0", string.Empty).Trim('﻿', '\r', ' ', '\t');
                if (name.Length > 0 && !name.StartsWith("docker-desktop", StringComparison.OrdinalIgnoreCase))
                    names.Add(name);
            }

            return names.ToArray();
        }

        // Home directory (a Linux path) of the default user of the distro, empty if it can not be determined.
        public static string GetHomeDirectory(string distro)
        {
            if (_homes.TryGetValue(distro, out var cached))
                return cached;

            var start = new ProcessStartInfo();
            start.FileName = "wsl.exe";
            start.Arguments = $"-d {distro} --exec sh -c \"echo $HOME\"";
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.StandardOutputEncoding = Encoding.UTF8;

            try
            {
                using var proc = Process.Start(start)!;
                var home = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit();
                if (proc.ExitCode == 0 && home.StartsWith('/'))
                    return _homes[distro] = home;
            }
            catch
            {
                // Ignore errors
            }

            return string.Empty;
        }

        // Version of the git installed inside the distro, queried once and cached. 0.0.0 if it can not be determined.
        public static Version GetGitVersion(string distro, bool refresh = false)
        {
            if (!refresh && _gitVersions.TryGetValue(distro, out var cached))
                return cached;

            var version = QueryGitVersion(distro);
            if (version.Major > 0)
                _gitVersions[distro] = version;

            return version;
        }

        private static Version QueryGitVersion(string distro)
        {
            var start = new ProcessStartInfo();
            start.FileName = "wsl.exe";
            start.Arguments = $"-d {distro} --exec git --version";
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.StandardOutputEncoding = Encoding.UTF8;

            try
            {
                using var proc = Process.Start(start)!;
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                var match = REG_GIT_VERSION().Match(output.Trim());
                if (proc.ExitCode == 0 && match.Success)
                    return new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
            }
            catch
            {
                // Ignore errors
            }

            return new Version(0, 0, 0);
        }

        public static bool TryParse(string path, out string distro, out string linuxPath)
        {
            distro = string.Empty;
            linuxPath = string.Empty;

            if (string.IsNullOrEmpty(path))
                return false;

            var normalized = path.Replace('\\', '/');
            string rest;
            if (normalized.StartsWith("//wsl.localhost/", StringComparison.OrdinalIgnoreCase))
                rest = normalized.Substring("//wsl.localhost/".Length);
            else if (normalized.StartsWith("//wsl$/", StringComparison.OrdinalIgnoreCase))
                rest = normalized.Substring("//wsl$/".Length);
            else
                return false;

            var slash = rest.IndexOf('/');
            distro = slash < 0 ? rest : rest.Substring(0, slash);
            if (distro.Length == 0)
                return false;

            linuxPath = slash < 0 ? "/" : "/" + rest.Substring(slash + 1).TrimEnd('/');
            return true;
        }

        // UNC path of this distro -> /home/..., C:\x -> /mnt/c/x. Anything else is returned untouched.
        public static string ToLinux(string distro, string path)
        {
            if (TryParse(path, out var d, out var linux) && d.Equals(distro, StringComparison.OrdinalIgnoreCase))
                return linux;

            if (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
            {
                var rest = path.Substring(2).Replace('\\', '/').TrimStart('/');
                return MOUNT_ROOT + char.ToLowerInvariant(path[0]) + (rest.Length == 0 ? string.Empty : "/" + rest);
            }

            return path;
        }

        // /mnt/c/x -> C:/x, /home/... -> //wsl.localhost/<distro>/home/...
        public static string ToWindows(string distro, string linuxPath)
        {
            if (linuxPath.StartsWith(MOUNT_ROOT, StringComparison.Ordinal) &&
                linuxPath.Length >= MOUNT_ROOT.Length + 1 &&
                char.IsAsciiLetter(linuxPath[MOUNT_ROOT.Length]) &&
                (linuxPath.Length == MOUNT_ROOT.Length + 1 || linuxPath[MOUNT_ROOT.Length + 1] == '/'))
            {
                var drive = char.ToUpperInvariant(linuxPath[MOUNT_ROOT.Length]);
                return $"{drive}:{(linuxPath.Length == MOUNT_ROOT.Length + 1 ? "/" : linuxPath.Substring(MOUNT_ROOT.Length + 1))}";
            }

            return $"//wsl.localhost/{distro}{linuxPath}";
        }
    }
}
