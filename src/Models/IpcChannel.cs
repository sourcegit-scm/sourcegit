using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Models
{
    public class IpcChannel : IDisposable
    {
        public bool IsFirstInstance { get; }

        public event Action<string> MessageReceived;

        public IpcChannel()
        {
            try
            {
                _singletonLock = File.Open(Path.Combine(Native.OS.DataDir, "process.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                IsFirstInstance = true;
                _server = WithShortTmpDir(() => new NamedPipeServerStream(
                    GetPipeName(),
                    PipeDirection.In,
                    -1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly));
                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(StartServer);
            }
            catch
            {
                IsFirstInstance = false;
            }
        }

        public void SendToFirstInstance(string cmd)
        {
            try
            {
                using (var client = WithShortTmpDir(() => new NamedPipeClientStream(".", GetPipeName(), PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly)))
                {
                    client.Connect(1000);
                    if (!client.IsConnected)
                        return;

                    using (var writer = new StreamWriter(client))
                    {
                        writer.WriteLine(cmd);
                        writer.Flush();
                    }

                    if (OperatingSystem.IsWindows())
                        client.WaitForPipeDrain();
                    else
                        Thread.Sleep(1000);
                }
            }
            catch
            {
                // IGNORE
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _singletonLock?.Dispose();
        }

        private static string GetPipeName()
        {
            var dataDir = Native.OS.DataDir.Replace('\\', '/').TrimEnd('/');
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(dataDir));
            var hashStr = Convert.ToHexString(hash)[..16];
            return $"SourceGitIPCChannel{Environment.UserName}_{hashStr}";
        }

        // .NET's named pipes on non-Windows platforms are backed by a Unix domain
        // socket created under Path.GetTempPath() (i.e. $TMPDIR). macOS assigns each
        // user a long per-session TMPDIR (/var/folders/xx/<27 random chars>/T/), which
        // combined with our pipe name routinely exceeds the 104-byte sun_path limit for
        // AF_UNIX addresses. When that happens, NamedPipeServerStream/NamedPipeClientStream
        // throw, IsFirstInstance silently becomes false, and the app exits immediately
        // with no diagnostics - it looks like SourceGit simply refuses to start.
        // Forcing a short TMPDIR just for the pipe path keeps the socket path short
        // regardless of the platform's default temp directory.
        private static T WithShortTmpDir<T>(Func<T> factory)
        {
            if (OperatingSystem.IsWindows())
                return factory();

            var original = Environment.GetEnvironmentVariable("TMPDIR");
            try
            {
                Environment.SetEnvironmentVariable("TMPDIR", "/tmp");
                return factory();
            }
            finally
            {
                Environment.SetEnvironmentVariable("TMPDIR", original);
            }
        }

        private async void StartServer()
        {
            using var reader = new StreamReader(_server);

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    await _server.WaitForConnectionAsync(_cancellationTokenSource.Token);

                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        var line = await reader.ReadToEndAsync(_cancellationTokenSource.Token);
                        MessageReceived?.Invoke(line.Trim());
                    }

                    _server.Disconnect();
                }
                catch
                {
                    if (!_cancellationTokenSource.IsCancellationRequested && _server.IsConnected)
                        _server.Disconnect();
                }
            }
        }

        private FileStream _singletonLock = null;
        private NamedPipeServerStream _server = null;
        private CancellationTokenSource _cancellationTokenSource = null;
    }
}
