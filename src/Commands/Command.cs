using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Avalonia.Threading;

namespace SourceGit.Commands
{
    public partial class Command
    {
        public class ReadToEndResult
        {
            public bool IsSuccess { get; set; } = false;
            public string StdOut { get; set; } = "";
            public string StdErr { get; set; } = "";
        }

        public enum EditorType
        {
            None,
            CoreEditor,
            RebaseEditor,
        }

        public string Context { get; set; } = string.Empty;
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        public string WorkingDirectory { get; set; } = null;
        public EditorType Editor { get; set; } = EditorType.CoreEditor; // Only used in Exec() mode
        public string SSHKey { get; set; } = string.Empty;
        public string Args { get; set; } = string.Empty;
        public bool RaiseError { get; set; } = true;
        public Models.ICommandLog Log { get; set; } = null;

        public bool Exec()
        {
            var start = CreateGitStartInfo();
            var errs = new List<string>();
            var proc = new Process() { StartInfo = start };

            Log?.AppendLine($"$ git {Args}\n");

            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null)
                    return;

                Log?.AppendLine(e.Data);
            };

            proc.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                {
                    errs.Add(string.Empty);
                    return;
                }

                Log?.AppendLine(e.Data);

                // Ignore progress messages
                if (e.Data.StartsWith("remote: Enumerating objects:", StringComparison.Ordinal))
                    return;
                if (e.Data.StartsWith("remote: Counting objects:", StringComparison.Ordinal))
                    return;
                if (e.Data.StartsWith("remote: Compressing objects:", StringComparison.Ordinal))
                    return;
                if (e.Data.StartsWith("Filtering content:", StringComparison.Ordinal))
                    return;
                if (REG_PROGRESS().IsMatch(e.Data))
                    return;

                errs.Add(e.Data);
            };

            var dummy = null as Process;
            var dummyProcLock = new object();
            try
            {
                proc.Start();

                // It not safe, please only use `CancellationToken` in readonly commands.
                if (CancellationToken.CanBeCanceled)
                {
                    dummy = proc;
                    CancellationToken.Register(() =>
                    {
                        lock (dummyProcLock)
                        {
                            if (dummy is { HasExited: false })
                                dummy.Kill();
                        }
                    });
                }
            }
            catch (Exception e)
            {
                if (RaiseError)
                    Dispatcher.UIThread.Post(() => App.RaiseException(Context, e.Message));

                Log?.AppendLine(string.Empty);
                return false;
            }

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            if (dummy != null)
            {
                lock (dummyProcLock)
                {
                    dummy = null;
                }
            }

            int exitCode = proc.ExitCode;
            proc.Close();
            Log?.AppendLine(string.Empty);

            if (!CancellationToken.IsCancellationRequested && exitCode != 0)
            {
                if (RaiseError)
                {
                    var errMsg = string.Join("\n", errs).Trim();
                    if (!string.IsNullOrEmpty(errMsg))
                        Dispatcher.UIThread.Post(() => App.RaiseException(Context, errMsg));
                }

                return false;
            }

            return true;
        }

        public ReadToEndResult ReadToEnd()
        {
            var start = CreateGitStartInfo();
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

        private ProcessStartInfo CreateGitStartInfo()
        {
            var start = new ProcessStartInfo();
            start.FileName = Native.OS.GitExecutable;
            start.Arguments = "--no-pager -c core.quotepath=off -c credential.helper=manager ";
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;

            // Force using this app as SSH askpass program
            var selfExecFile = Process.GetCurrentProcess().MainModule!.FileName;
            if (!OperatingSystem.IsLinux())
                start.Environment.Add("DISPLAY", "required");
            start.Environment.Add("SSH_ASKPASS", selfExecFile); // Can not use parameter here, because it invoked by SSH with `exec`
            start.Environment.Add("SSH_ASKPASS_REQUIRE", "prefer");
            start.Environment.Add("SOURCEGIT_LAUNCH_AS_ASKPASS", "TRUE");

            // If an SSH private key was provided, sets the environment.
            if (!start.Environment.ContainsKey("GIT_SSH_COMMAND") && !string.IsNullOrEmpty(SSHKey))
                start.Environment.Add("GIT_SSH_COMMAND", $"ssh -i '{SSHKey}'");

            // Force using en_US.UTF-8 locale
            if (OperatingSystem.IsLinux())
            {
                start.Environment.Add("LANG", "C");
                start.Environment.Add("LC_ALL", "C");
            }

            // Force using this app as git editor.
            switch (Editor)
            {
                case EditorType.CoreEditor:
                    start.Arguments += $"-c core.editor=\"\\\"{selfExecFile}\\\" --core-editor\" ";
                    break;
                case EditorType.RebaseEditor:
                    start.Arguments += $"-c core.editor=\"\\\"{selfExecFile}\\\" --rebase-message-editor\" -c sequence.editor=\"\\\"{selfExecFile}\\\" --rebase-todo-editor\" -c rebase.abbreviateCommands=true ";
                    break;
                default:
                    start.Arguments += "-c core.editor=true ";
                    break;
            }

            // Append command args
            start.Arguments += Args;

            // Working directory
            if (!string.IsNullOrEmpty(WorkingDirectory))
                start.WorkingDirectory = WorkingDirectory;

            return start;
        }

        [GeneratedRegex(@"\d+%")]
        private static partial Regex REG_PROGRESS();
    }
}
