using System;
using System.Reflection;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace SourceGit.Models {
    /// <summary>
    ///     崩溃日志生成
    /// </summary>
    public class CrashInfo {
        public static void Create(Exception e) {
            var builder = new StringBuilder();
            builder.Append("Crash: ");
            builder.Append(e.Message);
            builder.Append("\n\n");
            builder.Append("----------------------------\n");
            builder.Append($"Windows OS: {Environment.OSVersion}\n");
            builder.Append($"Version: {Assembly.GetExecutingAssembly().GetName().Version}\n");
            builder.Append($"Platform: {AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName}\n");
            builder.Append($"Source: {e.Source}\n");
            builder.Append($"---------------------------\n\n");
            builder.Append(e.StackTrace);

            var time = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var file = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            file = Path.Combine(file, $"sourcegit_crash_{time}.log");
            File.WriteAllText(file, builder.ToString());
        }
    }
}
