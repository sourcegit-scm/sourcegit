using System;
using System.Diagnostics;
using System.IO;

namespace SourceGit.Commands
{
    public static class QueryFileContent
    {
        public static Stream Run(string repo, string revision, string file)
        {
            var starter = new ProcessStartInfo();
            starter.WorkingDirectory = repo;
            starter.FileName = Native.OS.GitExecutable;
            starter.Arguments = $"show {revision}:\"{file}\"";
            starter.UseShellExecute = false;
            starter.CreateNoWindow = true;
            starter.WindowStyle = ProcessWindowStyle.Hidden;
            starter.RedirectStandardOutput = true;

            var stream = new MemoryStream();
            try
            {
                var proc = new Process() { StartInfo = starter };
                proc.Start();
                proc.StandardOutput.BaseStream.CopyTo(stream);
                proc.WaitForExit();
                proc.Close();

                stream.Position = 0;
            }
            catch (Exception e)
            {
                App.RaiseException(repo, $"Failed to query file content: {e}");
            }

            return stream;
        }

        public static Stream FromLFS(string repo, string oid, long size)
        {
            var starter = new ProcessStartInfo();
            starter.WorkingDirectory = repo;
            starter.FileName = Native.OS.GitExecutable;
            starter.Arguments = "lfs smudge";
            starter.UseShellExecute = false;
            starter.CreateNoWindow = true;
            starter.WindowStyle = ProcessWindowStyle.Hidden;
            starter.RedirectStandardInput = true;
            starter.RedirectStandardOutput = true;

            var stream = new MemoryStream();
            try
            {
                var proc = new Process() { StartInfo = starter };
                proc.Start();
                proc.StandardInput.WriteLine("version https://git-lfs.github.com/spec/v1");
                proc.StandardInput.WriteLine($"oid sha256:{oid}");
                proc.StandardInput.WriteLine($"size {size}");
                proc.StandardOutput.BaseStream.CopyTo(stream);
                proc.WaitForExit();
                proc.Close();

                stream.Position = 0;
            }
            catch (Exception e)
            {
                App.RaiseException(repo, $"Failed to query file content: {e}");
            }

            return stream;
        }
    }
}
