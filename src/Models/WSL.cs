using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using SourceGit.Commands;

namespace SourceGit.Models
{
    public class WSL
    {
        public string Path { get; set; } = "";
        public string GitDir { get; set; } = "";
        public long RefreshInterval { get; set; } = TimeSpan.FromSeconds(2).Ticks;

        private static readonly ConcurrentDictionary<string, Process> s_persistentShell = new();
        private static readonly ConcurrentDictionary<string, Lazy<string>> s_linuxPath = new();
        private static readonly Lock s_persistentLock = new();
        private static volatile bool s_shutdownRequested;

        public string DistroName
        {
            get
            {
                if (!IsWSLPath())
                    return string.Empty;

                var name = Path.Split(["//", "/"], StringSplitOptions.None)[2];
                return name;
            }
        }

        private long _lastRefresh;
        private readonly IDictionary<string, DateTime> _gitFileTimestamps;

        static WSL()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (OperatingSystem.IsWindows() && Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Exit += CleanupPersistentWSLShell;
                    desktop.ShutdownRequested += (_, e) => s_shutdownRequested = true;
                }
            });
        }

        public WSL()
        {
            _lastRefresh = DateTime.Now.ToFileTime();
            _gitFileTimestamps = new Dictionary<string, DateTime>();
        }

        public bool ShouldWSLRefresh()
        {
            if (!IsWSLPath())
                return false;

            if (DateTime.Now.ToFileTime() - _lastRefresh > RefreshInterval)
            {
                _lastRefresh = DateTime.Now.ToFileTime();
                return true;
            }

            return false;
        }

        public bool IsWSLPath()
        {
            return OperatingSystem.IsWindows() && 
                !string.IsNullOrEmpty(Path) &&
                (Path.StartsWith("//wsl.localhost/", StringComparison.OrdinalIgnoreCase) ||
                Path.StartsWith("//wsl$/", StringComparison.OrdinalIgnoreCase));
        }

        public void SetEnvironmentForProcess(ProcessStartInfo start)
        {
            start.Environment["LANG"] = "C";
            start.Environment["LC_ALL"] = "C";

            if (start.Environment.TryGetValue("SSH_ASKPASS", out var askPassPath) && !string.IsNullOrEmpty(askPassPath) && System.IO.Path.IsPathRooted(askPassPath))
            {
                // Convert Windows path to WSL path
                var driveLetter = askPassPath[0].ToString();
                start.Environment["SSH_ASKPASS"] = askPassPath
                    .Replace($"{driveLetter}:\\", $"/mnt/{driveLetter.ToLowerInvariant()}/")
                    .Replace('\\', '/');
            }

            // Strip Windows paths from PATH if present and reconstruct with UNC paths
            start.Environment["PATH"] = string.Join(';', s_linuxPath.GetOrAdd(DistroName, ReadSharedLinuxPath).Value
                .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(path => !(path.StartsWith("/mnt/") && path.Length > 6 && char.IsAsciiLetter(path[5]) && path[6] == '/'))
                .Select(path => $@"\\wsl.localhost\{DistroName}{path.Replace('/', '\\')}"));

            var wslEnvironment = new[] { "SSH_ASKPASS", "SSH_ASKPASS_REQUIRE", "SOURCEGIT_LAUNCH_AS_ASKPASS", "GIT_SSH_COMMAND", "LANG", "LC_ALL" };
            var wslEnvBuilder = new StringBuilder();

            foreach (string env in wslEnvironment)
            {
                if (start.Environment.ContainsKey(env))
                    wslEnvBuilder.Append($"{env}:");
            }

            // Forward environment variables for WSL
            start.Environment["WSLENV"] = wslEnvBuilder.ToString().TrimEnd(':');
            start.Environment["WSLENV"] = $"{start.Environment["WSLENV"]}:PATH/lp/u";
        }

        public string ConvertArgumentPaths(string args)
        {
            if (args.Contains("--pathspec-from-file="))
            {
                args = Regex.Replace(args,
                    @"--pathspec-from-file=""([A-Z]):(\\[^""]+)""",
                    match => $"--pathspec-from-file=\"/mnt/{match.Groups[1].Value.ToLowerInvariant()}{match.Groups[2].Value.Replace('\\', '/')}\"",
                    RegexOptions.IgnoreCase);
            }

            if (args.Contains("-F "))
            {
                args = Regex.Replace(args,
                    @"-F\s+""([A-Z]):(\\[^""]+)""",
                    match => $"-F \"/mnt/{match.Groups[1].Value.ToLowerInvariant()}{match.Groups[2].Value.Replace('\\', '/')}\"",
                    RegexOptions.IgnoreCase);
            }

            if (args.Contains("--file="))
            {
                args = Regex.Replace(args,
                    @"--file=""([A-Z]):(\\[^""]+)""",
                    match => $"--file=\"/mnt/{match.Groups[1].Value.ToLowerInvariant()}{match.Groups[2].Value.Replace('\\', '/')}\"",
                    RegexOptions.IgnoreCase);
            }

            // Handle bare paths as last argument
            args = Regex.Replace(args,
                @"(\s|^)""([A-Z]):(\\[^""]+)""$",
                match => $"{match.Groups[1].Value}\"/mnt/{match.Groups[2].Value.ToLowerInvariant()}{match.Groups[3].Value.Replace("\\", "/")}\"",
                RegexOptions.IgnoreCase);

            return args;
        }

        public List<string> GetModifiedGitFiles()
        {
            var modifiedFiles = new List<string>();

            CheckGitFile("HEAD", modifiedFiles);
            CheckGitFile("MERGE_HEAD", modifiedFiles);
            CheckGitFile("AUTO_MERGE", modifiedFiles);
            CheckGitFile("BISECT_START", modifiedFiles);
            CheckGitFile("index", modifiedFiles);

            CheckGitDirectory("refs/heads", modifiedFiles);
            CheckGitDirectory("refs/remotes", modifiedFiles);
            CheckGitDirectory("refs/tags", modifiedFiles);

            CheckGitFile("refs/stash", modifiedFiles);
            CheckGitFile("logs/refs/stash", modifiedFiles);

            CheckGitDirectory("modules", modifiedFiles);
            CheckGitDirectory("worktrees", modifiedFiles);

            return modifiedFiles;
        }

        public Command.Result ReadFromPersistentShell(Command command, ProcessStartInfo start)
        {
            lock (s_persistentLock)
            {
                var process = s_persistentShell.GetOrAdd(DistroName, StartWSLShell);
                if (process.HasExited)
                {
                    if (command is not QueryLocalChanges || s_shutdownRequested)
                        return new Command.Result() { StdErr = $"WSL process was terminated in {DistroName}", IsSuccess = false };

                    process = StartWSLShell(DistroName);
                    s_persistentShell[DistroName] = process;
                }

                var linuxPath = "/" + string.Join("/", Path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Skip(2));
                var commandline = $"git -C \"{linuxPath}\" {start.Arguments[4..]}";

                process.StandardInput.WriteLine($"{commandline}; echo __STDOUT_COMPLETE__; echo __STDERR_COMPLETE__ >&2");
                process.StandardInput.Flush();

                var standardOutput = new StringBuilder();
                var standardError = new StringBuilder();

                string buffer;
                while ((buffer = process.StandardOutput.ReadLine()) != null)
                {
                    if (buffer == "__STDOUT_COMPLETE__")
                        break;
                    standardOutput.Append(buffer).Append('\n');
                }

                while ((buffer = process.StandardError.ReadLine()) != null)
                {
                    if (buffer == "__STDERR_COMPLETE__")
                        break;
                    standardError.Append(buffer).Append('\n');
                }

                var rs = new Command.Result()
                {
                    StdOut = standardOutput.ToString(),
                    StdErr = standardError.ToString(),
                };

                rs.IsSuccess = string.IsNullOrWhiteSpace(rs.StdErr);
                return rs;
            }
        }

        public void ReleaseFileWatcherLock(Watcher watcher, IRepository repo)
        {
            watcher.MarkWorkingCopyUpdated();
            watcher.MarkTagUpdated();

            repo.RefreshWorkingCopyChanges();
            repo.RefreshTags();
        }

        public void RefreshWorkingCopy(Watcher watcher, IRepository repo)
        {
            watcher.MarkWorkingCopyUpdated();
            repo.RefreshWorkingCopyChanges();
        }

        private void CheckGitFile(string fileName, List<string> modifiedFiles)
        {
            var fullPath = System.IO.Path.Combine(GitDir, fileName);
            if (File.Exists(fullPath))
            {
                var lastWrite = File.GetLastWriteTime(fullPath);
                if (!_gitFileTimestamps.TryGetValue(fileName, out var lastKnown) || lastWrite > lastKnown)
                {
                    _gitFileTimestamps[fileName] = lastWrite;
                    modifiedFiles.Add(fileName);
                }
            }
            else if (_gitFileTimestamps.ContainsKey(fileName))
            {
                _gitFileTimestamps.Remove(fileName);
                modifiedFiles.Add(fileName);
            }
        }

        private void CheckGitDirectory(string dirName, List<string> modifiedFiles)
        {
            var fullDirPath = System.IO.Path.Combine(GitDir, dirName);
            if (!Directory.Exists(fullDirPath))
                return;

            var files = Directory.GetFiles(fullDirPath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relativePath = System.IO.Path.GetRelativePath(GitDir, file).Replace('\\', '/');
                var lastWrite = File.GetLastWriteTime(file);

                if (!_gitFileTimestamps.TryGetValue(relativePath, out var lastKnown) || lastWrite > lastKnown)
                {
                    _gitFileTimestamps[relativePath] = lastWrite;
                    modifiedFiles.Add(relativePath);
                }
            }
        }

        private static Process StartWSLShell(string distroName)
        {
            var startInfo = new ProcessStartInfo()
            {
                FileName = "wsl.exe",
                Arguments = $"-d {distroName} -e bash --noprofile --norc -i",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            startInfo.Environment["HISTFILE"] = "/dev/null";
            startInfo.Environment["LANG"] = "C";
            startInfo.Environment["LC_ALL"] = "C";
            startInfo.Environment["WSLENV"] = "HISTFILE:LANG:LC_ALL";

            return Process.Start(startInfo);
        }

        private static void CleanupPersistentWSLShell(object sender, EventArgs e)
        {
            if (s_persistentShell.Count == 0)
                return;

            lock (s_persistentLock)
            {
                foreach (var process in s_persistentShell.Values)
                {
                    if (!process.HasExited)
                        process.Kill();

                    process.Dispose();
                }

                s_persistentShell.Clear();
            }
        }

        private static Lazy<string> ReadSharedLinuxPath(string distroName)
        {
            return new Lazy<string>(() => ReadLinuxPath(distroName), isThreadSafe: true);
        }

        private static string ReadLinuxPath(string distroName)
        {
            var startInfo = new ProcessStartInfo()
            {
                FileName = "wsl.exe",
                Arguments = $"-d {distroName} -e bash -ilc \"printf %s \\\"$PATH\\\"\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            startInfo.Environment["HISTFILE"] = "/dev/null";
            startInfo.Environment["LANG"] = "C";
            startInfo.Environment["LC_ALL"] = "C";
            startInfo.Environment["WSLENV"] = "HISTFILE:LANG:LC_ALL";

            using var process = Process.Start(startInfo);
            var path = process.StandardOutput.ReadToEnd();
            process.StandardInput.Close();
            process.Kill();
            process.WaitForExit();
            return path;
        }
    }
}
