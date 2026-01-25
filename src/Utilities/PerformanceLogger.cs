using System;
using System.IO;

namespace SourceGit.Utilities
{
    public static class PerformanceLogger
    {
        private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ofpa_perf.log");
        private static readonly object _lock = new object();

        public static void Log(string message)
        {
            lock (_lock)
            {
                try
                {
                    File.AppendAllText(LogFile, message + Environment.NewLine);
                }
                catch { }
            }
        }
    }
}
