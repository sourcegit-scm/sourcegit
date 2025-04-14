using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Models
{
    public class IpcChannel : IDisposable
    {
        public bool IsFirstInstance
        {
            get => _server != null;
        }

        public event Action<string> MessageReceived;

        public IpcChannel()
        {
            try
            {
                _server = new NamedPipeServerStream("SourceGitIPCChannel", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(StartServer);
            }
            catch
            {
                // IGNORE
            }
        }

        public void SendToFirstInstance(string cmd)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", "SourceGitIPCChannel", PipeDirection.Out))
                {
                    client.Connect(1000);
                    if (client.IsConnected)
                    {
                        using (var writer = new StreamWriter(client))
                        {
                            writer.WriteLine(cmd);
                            writer.Flush();
                        }
                    }
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
        }

        private async void StartServer()
        {
            using var reader = new StreamReader(_server);

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    await _server.WaitForConnectionAsync(_cancellationTokenSource.Token);
                    
                    var line = (await reader.ReadLineAsync(_cancellationTokenSource.Token))?.Trim();
                    MessageReceived?.Invoke(line);
                    _server.Disconnect();
                }
                catch
                {
                    // IGNORE
                }
            }
        }

        private NamedPipeServerStream _server = null;
        private CancellationTokenSource _cancellationTokenSource = null;
    }
}
