using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public partial class Command
    {
        public class Result
        {
            public bool IsSuccess { get; set; } = false;
            public string StdOut { get; set; } = string.Empty;
            public string StdErr { get; set; } = string.Empty;

            public static Result Failed(string reason) => new Result() { StdErr = reason };
        }

        public enum EditorType
        {
            None,
            CoreEditor,
            RebaseEditor,
        }

        public string Context { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = null;
        public EditorType Editor { get; set; } = EditorType.CoreEditor;
        public string SSHKey { get; set; } = string.Empty;
        public string Args { get; set; } = string.Empty;

        // Only used in `ExecAsync` mode.
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        public bool RaiseError { get; set; } = true;
        public Models.ICommandLog Log { get; set; } = null;

        public async Task<bool> ExecAsync()
        {
            Log?.AppendLine($"$ git {Args}\n");

            var errs = new List<string>();

            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);
            proc.OutputDataReceived += (_, e) => HandleOutput(e.Data, errs);
            proc.ErrorDataReceived += (_, e) => HandleOutput(e.Data, errs);

            var captured = new CapturedProcess() { Process = proc };
            var capturedLock = new object();
            try
            {
                proc.Start();

                // Not safe, please only use `CancellationToken` in readonly commands.
                if (CancellationToken.CanBeCanceled)
                {
                    CancellationToken.Register(() =>
                    {
                        lock (capturedLock)
                        {
                            if (captured is { Process: { HasExited: false } })
                                Native.OS.TerminateProcess(captured.Process);
                        }
                    });
                }
            }
            catch (Exception e)
            {
                if (RaiseError)
                    RaiseException(e.Message);

                Log?.AppendLine(string.Empty);
                return false;
            }

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            try
            {
                await proc.WaitForExitAsync(CancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                HandleOutput(e.Message, errs);
            }

            lock (capturedLock)
            {
                captured.Process = null;
            }

            Log?.AppendLine(string.Empty);

            if (!CancellationToken.IsCancellationRequested && proc.ExitCode != 0)
            {
                if (RaiseError)
                {
                    var errMsg = string.Join("\n", errs).Trim();
                    if (!string.IsNullOrEmpty(errMsg))
                        RaiseException(errMsg);
                }

                return false;
            }

            return true;
        }

        protected Result ReadToEnd()
        {
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);

            try
            {
                proc.Start();
            }
            catch (Exception e)
            {
                return Result.Failed(e.Message);
            }

            var rs = new Result() { IsSuccess = true };
            rs.StdOut = proc.StandardOutput.ReadToEnd();
            rs.StdErr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            rs.IsSuccess = proc.ExitCode == 0;
            return rs;
        }

        protected async Task<Result> ReadToEndAsync()
        {
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);

            try
            {
                proc.Start();
            }
            catch (Exception e)
            {
                return Result.Failed(e.Message);
            }

            var rs = new Result() { IsSuccess = true };
            rs.StdOut = await proc.StandardOutput.ReadToEndAsync(CancellationToken).ConfigureAwait(false);
            rs.StdErr = await proc.StandardError.ReadToEndAsync(CancellationToken).ConfigureAwait(false);
            await proc.WaitForExitAsync(CancellationToken).ConfigureAwait(false);

            rs.IsSuccess = proc.ExitCode == 0;
            return rs;
        }

        // For callers that stream binary data or feed stdin themselves and so manage the process on their own.
        public static ProcessStartInfo CreateStartInfo(string repo, string args)
        {
            return new Command() { WorkingDirectory = repo, Args = args, Editor = EditorType.None }.CreateGitStartInfo(false);
        }

        // Absolute paths printed by git are Linux paths when the repository lives in WSL, the app works with UNC paths.
        protected string FromGitPath(string path)
        {
            if (path.StartsWith('/') && Native.WSL.TryParse(WorkingDirectory, out var distro, out _))
                return Native.WSL.ToWindows(distro, path);

            return path;
        }

        // The reverse: Windows paths (UNC or drive) handed to git must be Linux paths when the repository lives in WSL.
        protected string ToGitPath(string path)
        {
            if (Native.WSL.TryParse(WorkingDirectory, out var distro, out _))
                return Native.WSL.ToLinux(distro, path);

            return path;
        }

        protected ProcessStartInfo CreateGitStartInfo(bool redirect)
        {
            // Repositories inside a WSL distro are driven by the distro's own git through wsl.exe.
            // shortcut: the `-d` value stays unquoted (wsl.exe does not strip quotes there), so distro names with spaces fail.
            var inWsl = Native.WSL.TryParse(WorkingDirectory, out var distro, out var linuxDir);
            var useSetSid = !inWsl && CancellationToken.CanBeCanceled && Native.OS.SupportSetSid();
            var selfExecFile = Environment.ProcessPath;
            var editorExecFile = inWsl ? Native.WSL.ToLinux(distro, selfExecFile) : selfExecFile;
            var builder = new StringBuilder(2048);

            if (inWsl)
                builder.Append("-d ").Append(distro).Append(" --cd ").Append(linuxDir.Quoted()).Append(" --exec git ");
            else if (useSetSid)
                builder.Append(Native.OS.GitExecutable.Quoted()).Append(' ');

            builder.Append("--no-pager -c core.quotepath=off ");

            if (!inWsl)
            {
                builder.Append("-c credential.helper=").Append(Native.OS.CredentialHelper).Append(' ');
            }
            else
            {
                // An absolute path needs the same quoting as the editor, a helper name is passed as it is.
                var helper = Native.WSL.CredentialHelperFor(distro);
                if (helper.StartsWith('/'))
                    builder.Append($"""-c credential.helper="\"{helper}\"" """);
                else if (helper.Length > 0)
                    builder.Append("-c credential.helper=").Append(helper).Append(' ');
            }

            switch (Editor)
            {
                case EditorType.CoreEditor:
                    builder.Append($"""-c core.editor="\"{editorExecFile}\" --core-editor" """);
                    break;
                case EditorType.RebaseEditor:
                    builder.Append($"""-c core.editor="\"{editorExecFile}\" --rebase-message-editor" -c sequence.editor="\"{editorExecFile}\" --rebase-todo-editor" -c rebase.abbreviateCommands=true """);
                    break;
                default:
                    builder.Append("-c core.editor=true ");
                    break;
            }

            builder.Append(Args);

            var start = new ProcessStartInfo();
            start.FileName = inWsl ? "wsl.exe" : useSetSid ? Native.OS.GetSetSidExecutable() : Native.OS.GitExecutable;
            start.Arguments = builder.ToString();
            start.UseShellExecute = false;
            start.CreateNoWindow = true;

            if (redirect)
            {
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.StandardOutputEncoding = Encoding.UTF8;
                start.StandardErrorEncoding = Encoding.UTF8;
            }

            // Force using this app as SSH askpass program
            start.Environment.Add("SSH_ASKPASS", selfExecFile); // Can not use parameter here, because it invoked by SSH with `exec`
            start.Environment.Add("SSH_ASKPASS_REQUIRE", "prefer");
            start.Environment.Add("SOURCEGIT_LAUNCH_AS_ASKPASS", "TRUE");
            if (!OperatingSystem.IsLinux())
                start.Environment.Add("DISPLAY", "required");

            // If an SSH private key was provided, sets the environment.
            var setSshCommand = false;
            if (!start.Environment.ContainsKey("GIT_SSH_COMMAND") && !string.IsNullOrEmpty(SSHKey))
            {
                var key = inWsl ? Native.WSL.ToLinux(distro, SSHKey) : SSHKey;
                start.Environment.Add("GIT_SSH_COMMAND", $"ssh -i '{key}' -o AddKeysToAgent=yes");
                setSshCommand = true;
            }

            // Force the C locale on Unix, so that git's output (including fatal errors) can always be parsed.
            if (!OperatingSystem.IsWindows() || inWsl)
            {
                start.Environment["LANG"] = "C";
                start.Environment["LC_ALL"] = "C";
            }

            if (inWsl)
            {
                // WSLENV forwards these into the distro, `/p` translates the Windows path into a Linux one.
                start.Environment["SOURCEGIT_WSL_DISTRO"] = distro;
                var forwarded = "SSH_ASKPASS/p:SSH_ASKPASS_REQUIRE:SOURCEGIT_LAUNCH_AS_ASKPASS:SOURCEGIT_WSL_DISTRO:DISPLAY:LANG:LC_ALL";
                if (setSshCommand)
                    forwarded += ":GIT_SSH_COMMAND";

                start.Environment.TryGetValue("WSLENV", out var wslEnv);
                start.Environment["WSLENV"] = string.IsNullOrEmpty(wslEnv) ? forwarded : $"{wslEnv}:{forwarded}";
            }

            // Working directory
            if (!string.IsNullOrEmpty(WorkingDirectory))
                start.WorkingDirectory = WorkingDirectory;

            return start;
        }

        protected void RaiseException(string error)
        {
            Models.Notification.Send(Context, error, true);
        }

        private void HandleOutput(string line, List<string> errs)
        {
            if (line == null)
                return;

            Log?.AppendLine(line);

            // Lines to hide in error message.
            if (line.Length > 0)
            {
                if (line.StartsWith("remote: Enumerating objects:", StringComparison.Ordinal) ||
                    line.StartsWith("remote: Counting objects:", StringComparison.Ordinal) ||
                    line.StartsWith("remote: Compressing objects:", StringComparison.Ordinal) ||
                    line.StartsWith("Filtering content:", StringComparison.Ordinal) ||
                    line.StartsWith("hint:", StringComparison.Ordinal))
                    return;

                if (REG_PROGRESS().IsMatch(line))
                    return;
            }

            errs.Add(line);
        }

        private class CapturedProcess
        {
            public Process Process { get; set; } = null;
        }

        [GeneratedRegex(@"\d+%")]
        private static partial Regex REG_PROGRESS();
    }
}
