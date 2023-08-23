using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceGit.Commands {

    /// <summary>
    ///     用于取消命令执行的上下文对象
    /// </summary>
    public class Context {
        public bool IsCancelRequested { get; set; } = false;
    }

    /// <summary>
    ///     命令接口
    /// </summary>
    public class Command {

        /// <summary>
        ///     读取全部输出时的结果
        /// </summary>
        public class ReadToEndResult {
            public bool IsSuccess { get; set; }
            public string Output { get; set; }
            public string Error { get; set; }
        }

        /// <summary>
        ///     上下文
        /// </summary>
        public Context Ctx { get; set; } = null;

        /// <summary>
        ///     运行路径
        /// </summary>
        public string Cwd { get; set; } = "";

        /// <summary>
        ///     参数
        /// </summary>
        public string Args { get; set; } = "";

        /// <summary>
        ///     是否忽略错误
        /// </summary>
        public bool DontRaiseError { get; set; } = false;

        /// <summary>
        ///     使用标准错误输出
        /// </summary>
        public bool TraitErrorAsOutput { get; set; } = false;

        /// <summary>
        ///     运行
        /// </summary>
        public bool Exec() {
            var start = new ProcessStartInfo();
            start.FileName = Models.Preference.Instance.Git.Path;
            start.Arguments = "--no-pager -c core.quotepath=off " + Args;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;

            if (!string.IsNullOrEmpty(Cwd)) start.WorkingDirectory = Cwd;

            var progressFilter = new Regex(@"\s\d+%\s");
            var errs = new List<string>();
            var proc = new Process() { StartInfo = start };
            var isCancelled = false;

            proc.OutputDataReceived += (o, e) => {
                if (Ctx != null && Ctx.IsCancelRequested) {
                    isCancelled = true;
                    proc.CancelErrorRead();
                    proc.CancelOutputRead();
                    if (!proc.HasExited) proc.Kill();
                    return;
                }

                if (e.Data == null) return;
                OnReadline(e.Data);
            };
            proc.ErrorDataReceived += (o, e) => {
                if (Ctx != null && Ctx.IsCancelRequested) {
                    isCancelled = true;
                    proc.CancelErrorRead();
                    proc.CancelOutputRead();
                    if (!proc.HasExited) proc.Kill();
                    return;
                }

                if (string.IsNullOrEmpty(e.Data)) return;
                if (TraitErrorAsOutput) OnReadline(e.Data);

                if (progressFilter.IsMatch(e.Data)) return;
                if (e.Data.StartsWith("remote: Counting objects:", StringComparison.Ordinal)) return;
                errs.Add(e.Data);
            };

            try {
                proc.Start();
            } catch (Exception e) {
                if (!DontRaiseError) OnException(e.Message);
                return false;
            }

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            int exitCode = proc.ExitCode;
            proc.Close();

            if (!isCancelled && exitCode != 0 && errs.Count > 0) {
                if (!DontRaiseError) OnException(string.Join("\n", errs));
                return false;
            } else {
                return true;
            }
        }

        /// <summary>
        ///     直接读取全部标准输出
        /// </summary>
        public ReadToEndResult ReadToEnd() {
            var start = new ProcessStartInfo();
            start.FileName = Models.Preference.Instance.Git.Path;
            start.Arguments = "--no-pager -c core.quotepath=off " + Args;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;

            if (!string.IsNullOrEmpty(Cwd)) start.WorkingDirectory = Cwd;

            var proc = new Process() { StartInfo = start };
            try {
                proc.Start();
            } catch (Exception e) {
                return new ReadToEndResult() {
                    Output = "",
                    Error = e.Message,
                    IsSuccess = false,
                };
            }

            var rs = new ReadToEndResult();
            rs.Output = proc.StandardOutput.ReadToEnd();
            rs.Error = proc.StandardError.ReadToEnd();

            proc.WaitForExit();
            rs.IsSuccess = proc.ExitCode == 0;
            proc.Close();

            return rs;
        }

        /// <summary>
        ///     调用Exec时的读取函数
        /// </summary>
        /// <param name="line"></param>
        public virtual void OnReadline(string line) {
        }

        /// <summary>
        ///     默认异常处理函数
        /// </summary>
        /// <param name="message"></param>
        public virtual void OnException(string message) {
            Models.Exception.Raise(message);
        }
    }
}
