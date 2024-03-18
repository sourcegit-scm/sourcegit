using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

using Avalonia.Threading;

namespace SourceGit.Commands
{
    public partial class Command
    {
        public class CancelToken
        {
            public bool Requested { get; set; } = false;
        }

        public class ReadToEndResult
        {
            public bool IsSuccess { get; set; }
            public string StdOut { get; set; }
            public string StdErr { get; set; }
        }

        public string Context { get; set; } = string.Empty;
        public CancelToken Cancel { get; set; } = null;
        public string WorkingDirectory { get; set; } = null;
        public string Args { get; set; } = string.Empty;
        public bool RaiseError { get; set; } = true;
        public bool TraitErrorAsOutput { get; set; } = false;

        public bool Exec()
        {
            var start = new ProcessStartInfo();
            start.FileName = Native.OS.GitInstallPath;
            start.Arguments = "--no-pager -c core.quotepath=off " + Args;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;

            if (!string.IsNullOrEmpty(WorkingDirectory)) start.WorkingDirectory = WorkingDirectory;

            var errs = new List<string>();
            var proc = new Process() { StartInfo = start };
            var isCancelled = false;

            proc.OutputDataReceived += (_, e) =>
            {
                if (Cancel != null && Cancel.Requested)
                {
                    isCancelled = true;
                    proc.CancelErrorRead();
                    proc.CancelOutputRead();
                    if (!proc.HasExited) proc.Kill(true);
                    return;
                }

                if (e.Data != null) OnReadline(e.Data);
            };

            proc.ErrorDataReceived += (_, e) =>
            {
                if (Cancel != null && Cancel.Requested)
                {
                    isCancelled = true;
                    proc.CancelErrorRead();
                    proc.CancelOutputRead();
                    if (!proc.HasExited) proc.Kill(true);
                    return;
                }

                if (string.IsNullOrEmpty(e.Data)) return;
                if (TraitErrorAsOutput) OnReadline(e.Data);

                // Ignore progress messages
                if (e.Data.StartsWith("remote: Enumerating objects:", StringComparison.Ordinal)) return;
                if (e.Data.StartsWith("remote: Counting objects:", StringComparison.Ordinal)) return;
                if (e.Data.StartsWith("remote: Compressing objects:", StringComparison.Ordinal)) return;
                if (e.Data.StartsWith("Filtering content:", StringComparison.Ordinal)) return;
                if (_progressRegex().IsMatch(e.Data)) return;
                errs.Add(e.Data);
            };

            try
            {
                proc.Start();
            }
            catch (Exception e)
            {
                if (RaiseError)
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        App.RaiseException(Context, e.Message);
                    });
                }
                return false;
            }

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            int exitCode = proc.ExitCode;
            proc.Close();

            if (!isCancelled && exitCode != 0 && errs.Count > 0)
            {
                if (RaiseError)
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        App.RaiseException(Context, string.Join("\n", errs));
                    });
                }
                return false;
            }
            else
            {
                return true;
            }
        }

        public ReadToEndResult ReadToEnd()
        {
            var start = new ProcessStartInfo();
            start.FileName = Native.OS.GitInstallPath;
            start.Arguments = "--no-pager -c core.quotepath=off " + Args;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;

            if (!string.IsNullOrEmpty(WorkingDirectory)) start.WorkingDirectory = WorkingDirectory;

            var proc = new Process() { StartInfo = start };
            try
            {
                proc.Start();
            }
            catch (Exception e)
            {
                return new ReadToEndResult()
                {
                    IsSuccess = false,
                    StdOut = string.Empty,
                    StdErr = e.Message,
                };
            }

            var rs = new ReadToEndResult()
            {
                StdOut = proc.StandardOutput.ReadToEnd(),
                StdErr = proc.StandardError.ReadToEnd(),
            };

            proc.WaitForExit();
            rs.IsSuccess = proc.ExitCode == 0;
            proc.Close();

            return rs;
        }

        protected virtual void OnReadline(string line) { }

        [GeneratedRegex(@"\d+%")]
        private static partial Regex _progressRegex();
    }
}