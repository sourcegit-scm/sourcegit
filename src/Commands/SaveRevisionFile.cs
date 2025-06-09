using System;
using System.Diagnostics;
using System.IO;

using Avalonia.Threading;

namespace SourceGit.Commands
{
    public static class SaveRevisionFile
    {
        public static void Run(string repo, string revision, string file, string saveTo)
        {
            var isLFSFiltered = new IsLFSFiltered(repo, revision, file).Result();
            if (isLFSFiltered)
            {
                var pointerStream = QueryFileContent.Run(repo, revision, file);
                ExecCmd(repo, $"lfs smudge", saveTo, pointerStream);
            }
            else
            {
                ExecCmd(repo, $"show {revision}:\"{file}\"", saveTo);
            }
        }

        private static bool ExecCmd(string repo, string args, string outputFile, Stream input = null)
        {
            var starter = new ProcessStartInfo();
            starter.WorkingDirectory = repo;
            starter.FileName = Native.OS.GitExecutable;
            starter.Arguments = args;
            starter.UseShellExecute = false;
            starter.CreateNoWindow = true;
            starter.WindowStyle = ProcessWindowStyle.Hidden;
            starter.RedirectStandardInput = true;
            starter.RedirectStandardOutput = true;
            starter.RedirectStandardError = true;

            using (var sw = File.OpenWrite(outputFile))
            {
                try
                {
                    var proc = new Process() { StartInfo = starter };
                    proc.Start();
                    if (input != null)
                        proc.StandardInput.Write(new StreamReader(input).ReadToEnd());
                    proc.StandardOutput.BaseStream.CopyTo(sw);
                    proc.WaitForExit();
                    var rs = proc.ExitCode == 0;
                    proc.Close();

                    return rs;
                }
                catch (Exception e)
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        App.RaiseException(repo, "Save file failed: " + e.Message);
                    });
                    return false;
                }
            }
        }
    }
}
