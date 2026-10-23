using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public static class QueryFileContent
    {
        public static async Task<Stream> RunAsync(string repo, string revision, string file)
        {
            var starter = Command.CreateStartInfo(repo, $"show {revision}:{file.Quoted()}");
            starter.WindowStyle = ProcessWindowStyle.Hidden;
            starter.RedirectStandardOutput = true;

            var stream = new MemoryStream();
            try
            {
                using var proc = Process.Start(starter)!;
                await proc.StandardOutput.BaseStream.CopyToAsync(stream).ConfigureAwait(false);
                await proc.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Models.Notification.Send(repo, $"Failed to query file content: {e}", true);
            }

            stream.Position = 0;
            return stream;
        }

        public static async Task<Stream> FromLFSAsync(string repo, string oid, long size)
        {
            var starter = Command.CreateStartInfo(repo, "lfs smudge");
            starter.WindowStyle = ProcessWindowStyle.Hidden;
            starter.RedirectStandardInput = true;
            starter.RedirectStandardOutput = true;

            var stream = new MemoryStream();
            try
            {
                using var proc = Process.Start(starter)!;
                await proc.StandardInput.WriteLineAsync("version https://git-lfs.github.com/spec/v1").ConfigureAwait(false);
                await proc.StandardInput.WriteLineAsync($"oid sha256:{oid}").ConfigureAwait(false);
                await proc.StandardInput.WriteLineAsync($"size {size}").ConfigureAwait(false);
                await proc.StandardOutput.BaseStream.CopyToAsync(stream).ConfigureAwait(false);
                await proc.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Models.Notification.Send(repo, $"Failed to query file content: {e}", true);
            }

            stream.Position = 0;
            return stream;
        }
    }
}
