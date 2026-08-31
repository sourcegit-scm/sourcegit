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
        public bool IsFirstInstance { get; private set; }

        public event Action<string> MessageReceived;

        public IpcChannel()
        {
            IsFirstInstance = false;

            var lockFile = GetLockFilePath();
            _lockFilePath = lockFile.Path;

            if (OperatingSystem.IsLinux() && lockFile.NeedChangePermissions)
            {
                // On Linux, if the lock file is created in the XDG_RUNTIME_DIR, we need to set the permissions to 700 (rwx------) to ensure that only the current user can access it.
                try
                {
                    _singletonLock = new FileStream(_lockFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                    File.SetUnixFileMode(_lockFilePath, File.GetUnixFileMode(_lockFilePath) | UnixFileMode.StickyBit);
                    IsFirstInstance = true;
                }
                catch
                {
                    try
                    {
                        _singletonLock = new FileStream(_lockFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        IsFirstInstance = true;
                    }
                    catch
                    {
                        // Just ignore the exception and assume that another instance is running.
                    }
                }
            }
            else
            {
                try
                {
                    _singletonLock = File.Open(_lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    IsFirstInstance = true;
                }
                catch
                {
                    // Just ignore the exception and assume that another instance is running.
                }
            }

            if (IsFirstInstance)
            {
                _server = new NamedPipeServerStream(
                    GetPipeName(),
                    PipeDirection.In,
                    -1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(StartServer);
            }
        }

        public void SendToFirstInstance(string cmd)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", GetPipeName(), PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
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

            if (IsFirstInstance && File.Exists(_lockFilePath))
                File.Delete(_lockFilePath);
        }

        private LockFile GetLockFilePath()
        {
            // On Windows and macOS, we can use the cache directory for the lock file.
            if (!OperatingSystem.IsLinux())
                return new LockFile(Path.Combine(Native.OS.BasicDirectories.CacheDir, "process.lock"), false);

            // On Linux, we should first check if the app is running in portable mode.
            var appImage = Environment.GetEnvironmentVariable("APPIMAGE");
            if (!string.IsNullOrEmpty(appImage) && File.Exists(appImage))
            {
                var portableDir = Path.Combine(Path.GetDirectoryName(appImage)!, "data");
                if (Directory.Exists(portableDir))
                    return new LockFile(Path.Combine(portableDir, "process.lock"), false);
            }

            // If not in portable mode, we should use the XDG_RUNTIME_DIR environment variable for the lock file.
            var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            if (string.IsNullOrEmpty(runtimeDir) || !Directory.Exists(runtimeDir))
                return new LockFile(Path.Combine(Native.OS.BasicDirectories.CacheDir, "process.lock"), false);

            return new LockFile(Path.Combine(runtimeDir, "sourcegit.instance.lock"), true);
        }

        private string GetPipeName()
        {
            // SourceGit does not support multiple instances on macOS, so we can use a fixed pipe name for macOS.
            if (OperatingSystem.IsMacOS())
                return "SourceGit";

            // Windows and Linux can have multiple instances of SourceGit running (portable-mode), so we need to generate a unique pipe name based on the data directory.
            var dataDir = Native.OS.BasicDirectories.CacheDir.Replace('\\', '/').TrimEnd('/');
            var hashStr = $"{Environment.UserName}_{dataDir}";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashStr))).Substring(0, 10);
            return $"SG_{hash}";
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

        private record LockFile(string Path, bool NeedChangePermissions);

        private string _lockFilePath = null;
        private FileStream _singletonLock = null;
        private NamedPipeServerStream _server = null;
        private CancellationTokenSource _cancellationTokenSource = null;
    }
}
