using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
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
                            writer.Write(Encoding.UTF8.GetBytes(cmd));
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
            _server?.Close();
        }

        private async void StartServer()
        {
            var buffer = new byte[1024];

            while (true)
            {
                try
                {
                    await _server.WaitForConnectionAsync(_cancellationTokenSource.Token);

                    using (var stream = new MemoryStream())
                    {
                        while (true)
                        {
                            var readed = await _server.ReadAsync(buffer.AsMemory(0, 1024), _cancellationTokenSource.Token);
                            if (readed == 0)
                                break;

                            stream.Write(buffer, 0, readed);
                        }

                        stream.Seek(0, SeekOrigin.Begin);
                        MessageReceived?.Invoke(Encoding.UTF8.GetString(stream.ToArray()).Trim());
                        _server.Disconnect();
                    }
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
