using System;
using System.IO;
using System.IO.Pipes;
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
                _server = new NamedPipeServerStream(
                    "SourceGitIPCChannel" + Environment.UserName,
                    PipeDirection.In,
                    -1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
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
                using (var client = new NamedPipeClientStream(".", "SourceGitIPCChannel" + Environment.UserName, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
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
