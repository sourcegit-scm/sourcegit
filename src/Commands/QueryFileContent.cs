using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace SourceGit.Commands
{
    public static class QueryFileContent
    {
        public static Task<Stream> RunIndexAsync(string repo, string file)
        {
            // Read from index (staged content).
            return RunObjectSpecAsync(repo, $":{file.Quoted()}");
        }

        public static Task<Stream> RunAsync(string repo, string revision, string file)
        {
            // Read from a specific revision.
            return RunObjectSpecAsync(repo, $"{revision}:{file.Quoted()}");
        }

        private static async Task<Stream> RunObjectSpecAsync(string repo, string objectSpec)
        {
            // Shared git show runner for both index and revision reads.
            var starter = new ProcessStartInfo
            {
                WorkingDirectory = repo,
                FileName = Native.OS.GitExecutable,
                Arguments = $"show {objectSpec}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
            };

            var stream = new MemoryStream();
            var sw = Stopwatch.StartNew();
            try
            {
                using var proc = Process.Start(starter)!;
                await proc.StandardOutput.BaseStream.CopyToAsync(stream).ConfigureAwait(false);
                await proc.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                App.RaiseException(repo, $"Failed to query file content: {e}");
            }

            stream.Position = 0;
            return stream;
        }

        // Batch read file contents using git cat-file --batch.
        // maxBytesPerObject: if > 0, read only first N bytes of each object (for performance).
        public static async Task<Dictionary<string, byte[]>> RunBatchAsync(string repo, IReadOnlyList<string> objectSpecs, int maxBytesPerObject = 0)
        {
            var results = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (objectSpecs == null || objectSpecs.Count == 0)
                return results;

            var starter = new ProcessStartInfo
            {
                WorkingDirectory = repo,
                FileName = Native.OS.GitExecutable,
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

                // Write requests in background to avoid deadlock (pipe buffer full)
                var writeTask = Task.Run(async () =>
                {
                    await using var input = proc.StandardInput;
                    foreach (var spec in objectSpecs)
                    {
                        await input.WriteLineAsync(spec).ConfigureAwait(false);
                    }
                });

                await using var output = proc.StandardOutput.BaseStream;
                for (int i = 0; i < objectSpecs.Count; i++)
                {
                    var header = await ReadBatchHeaderLineAsync(output).ConfigureAwait(false);
                    if (header == null)
                        break;

                    if (header.EndsWith(" missing", StringComparison.Ordinal))
                        continue;

                    var size = ParseBatchObjectSize(header);
                    if (size > 0)
                    {
                        // If maxBytesPerObject is set, read only that many bytes and skip the rest.
                        var bytesToRead = (maxBytesPerObject > 0 && size > maxBytesPerObject)
                            ? maxBytesPerObject
                            : size;
                        var bytesToSkip = size - bytesToRead;

                        var data = await ReadExactBytesAsync(output, bytesToRead).ConfigureAwait(false);
                        if (data != null)
                        {
                            results[objectSpecs[i]] = data;
                        }

                        // Skip remaining bytes if we limited the read.
                        if (bytesToSkip > 0)
                        {
                            await SkipBytesAsync(output, bytesToSkip).ConfigureAwait(false);
                        }
                    }

                    // Consume trailing newline after object content (even for size 0).
                    _ = await ReadSingleByteAsync(output).ConfigureAwait(false);
                }

                // Ensure writing is finished (should be, or implies error)
                await writeTask.ConfigureAwait(false);
                await proc.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                App.RaiseException(repo, $"Failed to query batch file content: {e}");
            }

            return results;
        }

        private static int ParseBatchObjectSize(string header)
        {
            // Header format: "<sha1> <type> <size>" or "<spec> missing"
            var lastSpace = header.LastIndexOf(' ');
            if (lastSpace <= 0 || lastSpace == header.Length - 1)
                return 0;

            if (int.TryParse(header.AsSpan(lastSpace + 1), out var size))
                return size;

            return 0;
        }

        private static async Task<string?> ReadBatchHeaderLineAsync(Stream stream)
        {
            var buffer = new MemoryStream();
            while (true)
            {
                int value = await ReadSingleByteAsync(stream).ConfigureAwait(false);
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

        private static async Task<byte[]?> ReadExactBytesAsync(Stream stream, int length)
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
            // Use a small buffer to skip bytes efficiently.
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

        public static async Task<Stream> FromLFSAsync(string repo, string oid, long size)
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
                using var proc = Process.Start(starter)!;
                await proc.StandardInput.WriteLineAsync("version https://git-lfs.github.com/spec/v1").ConfigureAwait(false);
                await proc.StandardInput.WriteLineAsync($"oid sha256:{oid}").ConfigureAwait(false);
                await proc.StandardInput.WriteLineAsync($"size {size}").ConfigureAwait(false);
                await proc.StandardOutput.BaseStream.CopyToAsync(stream).ConfigureAwait(false);
                await proc.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                App.RaiseException(repo, $"Failed to query file content: {e}");
            }

            stream.Position = 0;
            return stream;
        }
    }
}
