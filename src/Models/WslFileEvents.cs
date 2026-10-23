using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Models
{
    /// <summary>
    ///     Streams file system events of directories inside a WSL distro by running `inotifywait` there,
    ///     because `FileSystemWatcher` does not see changes made on the Linux side of the share.
    /// </summary>
    public sealed class WslFileEvents : IDisposable
    {
        public WslFileEvents(string distro, IEnumerable<string> linuxRoots, Action<string> onPath, Action onLost)
        {
            _distro = distro;
            _roots = new List<string>(linuxRoots);
            _onPath = onPath;
            _onLost = onLost;
        }

        // False if `inotifywait` is not installed in the distro (package `inotify-tools`) or could not be started.
        public async Task<bool> StartAsync()
        {
            return await Task.Run(() =>
            {
                if (!IsInstalled())
                    return false;

                var start = CreateStartInfo("inotifywait", "-mrq -e modify,create,delete,move --format %w%f " + string.Join(' ', _roots.ConvertAll(Quote)));
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.StandardOutputEncoding = Encoding.UTF8;

                try
                {
                    _proc = Process.Start(start);
                }
                catch
                {
                    return false;
                }

                _proc.ErrorDataReceived += (_, _) => { }; // Drain, a full pipe would block inotifywait.
                _proc.BeginErrorReadLine();

                if (_disposed) // Disposed while starting.
                {
                    Dispose();
                    return true;
                }

                _ = Task.Run(ReadLoop);
                return true;
            }).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _disposed = true;

            try
            {
                if (_proc is { HasExited: false })
                    _proc.Kill(true);
            }
            catch
            {
                // Ignore, the process may already be gone.
            }

            _proc?.Dispose();
        }

        private bool IsInstalled()
        {
            try
            {
                using var proc = Process.Start(CreateStartInfo("sh", "-c \"command -v inotifywait\""));
                proc.WaitForExit();
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task ReadLoop()
        {
            string last = null;
            string line;
            while (!_disposed && (line = await _proc.StandardOutput.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                // A single write raises several identical events in a row.
                if (line.Equals(last, StringComparison.Ordinal))
                    continue;

                last = line;
                _onPath(line);
            }

            if (!_disposed)
                _onLost();
        }

        private ProcessStartInfo CreateStartInfo(string program, string args)
        {
            var start = new ProcessStartInfo();
            start.FileName = "wsl.exe";
            start.Arguments = $"-d {_distro} --exec {program} {args}"; // shortcut: -d stays unquoted, see Command.CreateGitStartInfo.
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            return start;
        }

        private static string Quote(string path) => "\"" + path.Replace("\"", "\\\"") + "\"";

        private readonly string _distro;
        private readonly List<string> _roots;
        private readonly Action<string> _onPath;
        private readonly Action _onLost;
        private Process _proc;
        private volatile bool _disposed;
    }
}
