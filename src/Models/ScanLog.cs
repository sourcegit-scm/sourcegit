using System;
using System.IO;

namespace SourceGit.Models
{
    /// <summary>
    ///     Minimal append-only diagnostic log for repository scanning. Only enabled when a
    ///     log file has been opened (either by the `--scan-log` command line switch or the
    ///     SOURCEGIT_SCAN_LOG environment variable), so it costs nothing in normal use.
    /// </summary>
    public static class ScanLog
    {
        private static readonly object _lock = new object();
        private static StreamWriter _writer = null;

        public static bool Enabled
        {
            get
            {
                lock (_lock)
                    return _writer != null;
            }
        }

        public static void Open(string path)
        {
            lock (_lock)
            {
                try
                {
                    _writer = new StreamWriter(path, false);
                    _writer.AutoFlush = true;
                    _writer.WriteLine($"# scan log opened at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                }
                catch
                {
                    _writer = null;
                }
            }
        }

        public static void Write(string message)
        {
            lock (_lock)
            {
                if (_writer == null)
                    return;

                try
                {
                    _writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {message}");
                }
                catch
                {
                    // Never let diagnostics break the scan.
                }
            }
        }
    }

    /// <summary>
    ///     IProgress implementation that writes straight into <see cref="ScanLog"/> instead
    ///     of marshalling to a UI thread. Used by the headless `--scan-log` mode.
    /// </summary>
    public class ScanLogProgress : IProgress<string>
    {
        public void Report(string value)
        {
            ScanLog.Write($"progress: {value}");
        }
    }
}
