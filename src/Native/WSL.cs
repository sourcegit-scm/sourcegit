using System;
using System.IO;

namespace SourceGit.Native
{
    // Repositories inside a WSL distro are addressed by UNC paths (//wsl.localhost/<distro>/...) everywhere in the app.
    // Linux paths only exist at the git process boundary, this class converts between the two.
    public static class WSL
    {
        // shortcut: assumes the default automount root, upgrade when someone changes [automount] root in /etc/wsl.conf.
        private const string MOUNT_ROOT = "/mnt/";
        private const string GCM_WINDOWS_PATH = @"C:\Program Files\Git\mingw64\bin\git-credential-manager.exe";
        private const string GCM_LINUX_PATH = "/mnt/c/Program Files/Git/mingw64/bin/git-credential-manager.exe";

        // Empty if Git for Windows' credential manager is not installed, then the distro's own git config is used.
        public static string CredentialHelper => File.Exists(GCM_WINDOWS_PATH) ? GCM_LINUX_PATH : string.Empty;

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
