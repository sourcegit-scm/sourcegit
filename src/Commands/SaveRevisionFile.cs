using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public static class SaveRevisionFile
    {
        public static void Run(string repo, string revision, string file, string saveTo)
        {
            var dir = Path.GetDirectoryName(saveTo);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var isLFSFiltered = new IsLFSFiltered(repo, revision, file).Result();
            if (isLFSFiltered)
            {
                var pointerStream = QueryFileContent.Run(repo, revision, file);
                ExecCmd(repo, "lfs smudge", saveTo, pointerStream);
            }
            else
            {
                ExecCmd(repo, $"show {revision}:\"{file}\"", saveTo);
            }
        }

        private static void ExecCmd(string repo, string args, string outputFile, Stream input = null)
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
                    proc.Close();
                }
                catch (Exception e)
                {
                    App.RaiseException(repo, "Save file failed: " + e.Message);
                }
            }
        }

        public static async Task RunAsync(string repo, string revision, string file, string saveTo)
        {
            var dir = Path.GetDirectoryName(saveTo);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var isLFSFiltered = await new IsLFSFiltered(repo, revision, file).ResultAsync();
            if (isLFSFiltered)
            {
                var pointerStream = await QueryFileContent.RunAsync(repo, revision, file);
                await ExecCmdAsync(repo, "lfs smudge", saveTo, pointerStream);
            }
            else
            {
                await ExecCmdAsync(repo, $"show {revision}:\"{file}\"", saveTo);
            }
        }

        private static async Task ExecCmdAsync(string repo, string args, string outputFile, Stream input = null)
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

            await using (var sw = File.OpenWrite(outputFile))
            {
                try
                {
                    var proc = new Process() { StartInfo = starter };
                    proc.Start();
                    if (input != null)
                        await proc.StandardInput.WriteAsync(await new StreamReader(input).ReadToEndAsync());
                    await proc.StandardOutput.BaseStream.CopyToAsync(sw);
                    await proc.WaitForExitAsync();
                    proc.Close();
                }
                catch (Exception e)
                {
                    App.RaiseException(repo, "Save file failed: " + e.Message);
                }
            }
        }
    }
}
