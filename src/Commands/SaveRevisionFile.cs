using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public static class SaveRevisionFile
    {
        public static async Task RunAsync(string repo, string revision, string file, string saveTo)
        {
            var dir = Path.GetDirectoryName(saveTo);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var isLFSFiltered = await new IsLFSFiltered(repo, revision, file).GetResultAsync().ConfigureAwait(false);
            if (isLFSFiltered)
            {
                var pointerStream = await QueryFileContent.RunAsync(repo, revision, file).ConfigureAwait(false);
                await ExecCmdAsync(repo, "lfs smudge", saveTo, pointerStream).ConfigureAwait(false);
            }
            else
            {
                await ExecCmdAsync(repo, $"show {revision}:\"{file}\"", saveTo).ConfigureAwait(false);
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
                    using var proc = Process.Start(starter);
                    if (input != null)
                        await proc.StandardInput.WriteAsync(await new StreamReader(input).ReadToEndAsync());
                    await proc.StandardOutput.BaseStream.CopyToAsync(sw);
                    await proc.WaitForExitAsync();
                }
                catch (Exception e)
                {
                    App.RaiseException(repo, "Save file failed: " + e.Message);
                }
            }
        }
    }
}
