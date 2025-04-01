using System;
using System.Diagnostics;
using System.Text;

using Avalonia.Threading;

namespace SourceGit.Commands
{
    public static class ExecuteCustomAction
    {
        public static void Run(string repo, string file, string args)
        {
            var start = new ProcessStartInfo();
            start.FileName = file;
            start.Arguments = args;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WorkingDirectory = repo;

            try
            {
                Process.Start(start);
            }
            catch (Exception e)
            {
                Dispatcher.UIThread.Invoke(() => App.RaiseException(repo, e.Message));
            }
        }

        public static void RunAndWait(string repo, string file, string args, Action<string> outputHandler)
        {
            var start = new ProcessStartInfo();
            start.FileName = file;
            start.Arguments = args;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;
            start.WorkingDirectory = repo;

            var proc = new Process() { StartInfo = start };
            var builder = new StringBuilder();

            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    outputHandler?.Invoke(e.Data);
            };

            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    outputHandler?.Invoke(e.Data);
                    builder.AppendLine(e.Data);
                }
            };

            try
            {
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();

                var exitCode = proc.ExitCode;
                if (exitCode != 0)
                {
                    var errMsg = builder.ToString().Trim();
                    if (!string.IsNullOrEmpty(errMsg))
                        Dispatcher.UIThread.Invoke(() => App.RaiseException(repo, errMsg));
                }
            }
            catch (Exception e)
            {
                Dispatcher.UIThread.Invoke(() => App.RaiseException(repo, e.Message));
            }

            proc.Close();
        }
    }
}
