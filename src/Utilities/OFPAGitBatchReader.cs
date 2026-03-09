using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Utilities
{
    internal static class OFPAGitBatchReader
    {
        public static async Task<Dictionary<string, byte[]>> ReadAsync(string repo, IReadOnlyList<string> objectSpecs, int maxBytesPerObject)
        {
            var results = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (objectSpecs.Count == 0)
                return results;

            var gitExecutable = string.IsNullOrEmpty(Native.OS.GitExecutable) ? "git" : Native.OS.GitExecutable;
            var starter = new ProcessStartInfo
            {
                WorkingDirectory = repo,
                FileName = gitExecutable,
                Arguments = "cat-file --batch",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };

            try
            {
                using var proc = Process.Start(starter)!;
                var writeTask = Task.Run(async () =>
                {
                    await using var input = proc.StandardInput;
                    foreach (var spec in objectSpecs)
                        await input.WriteLineAsync(spec).ConfigureAwait(false);
                });

                await using var output = proc.StandardOutput.BaseStream;
                for (var i = 0; i < objectSpecs.Count; i++)
                {
                    var header = await ReadHeaderLineAsync(output).ConfigureAwait(false);
                    if (header == null)
                        break;

                    if (header.EndsWith(" missing", StringComparison.Ordinal))
                        continue;

                    var size = ParseObjectSize(header);
                    if (size > 0)
                    {
                        var bytesToRead = maxBytesPerObject > 0 && size > maxBytesPerObject
                            ? maxBytesPerObject
                            : size;
                        var bytesToSkip = size - bytesToRead;
                        var data = await ReadExactBytesAsync(output, bytesToRead).ConfigureAwait(false);
                        if (data != null)
                            results[objectSpecs[i]] = data;

                        if (bytesToSkip > 0)
                            await SkipBytesAsync(output, bytesToSkip).ConfigureAwait(false);
                    }

                    _ = await ReadSingleByteAsync(output).ConfigureAwait(false);
                }

                await writeTask.ConfigureAwait(false);
                await proc.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                App.RaiseException(repo, $"Failed to query OFPA batch file content: {e}");
            }

            return results;
        }

        private static int ParseObjectSize(string header)
        {
            var lastSpace = header.LastIndexOf(' ');
            if (lastSpace <= 0 || lastSpace == header.Length - 1)
                return 0;

            return int.TryParse(header.AsSpan(lastSpace + 1), out var size) ? size : 0;
        }

        private static async Task<string> ReadHeaderLineAsync(Stream stream)
        {
            var buffer = new MemoryStream();
            while (true)
            {
                var value = await ReadSingleByteAsync(stream).ConfigureAwait(false);
                if (value == -1)
                    break;

                if (value == '\n')
                    break;

                buffer.WriteByte((byte)value);
            }

            if (buffer.Length == 0)
                return null;

            var line = Encoding.ASCII.GetString(buffer.ToArray());
            return line.EndsWith('\r') ? line[..^1] : line;
        }

        private static async Task<byte[]> ReadExactBytesAsync(Stream stream, int length)
        {
            var buffer = new byte[length];
            var totalRead = 0;
            while (totalRead < length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(totalRead, length - totalRead)).ConfigureAwait(false);
                if (read <= 0)
                    return null;

                totalRead += read;
            }

            return buffer;
        }

        private static async Task SkipBytesAsync(Stream stream, int length)
        {
            var buffer = new byte[Math.Min(length, 8192)];
            var remaining = length;
            while (remaining > 0)
            {
                var toRead = Math.Min(remaining, buffer.Length);
                var read = await stream.ReadAsync(buffer.AsMemory(0, toRead)).ConfigureAwait(false);
                if (read <= 0)
                    break;

                remaining -= read;
            }
        }

        private static async Task<int> ReadSingleByteAsync(Stream stream)
        {
            var buffer = new byte[1];
            var read = await stream.ReadAsync(buffer.AsMemory(0, 1)).ConfigureAwait(false);
            return read == 0 ? -1 : buffer[0];
        }
    }
}
