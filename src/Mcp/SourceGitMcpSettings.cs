using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Mcp
{
    public sealed class SourceGitMcpSettings : ObservableObject
    {
        public static SourceGitMcpSettings Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = Load();
                return _instance;
            }
        }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (value && string.IsNullOrWhiteSpace(_authToken))
                {
                    _authToken = GenerateToken();
                    OnPropertyChanged(nameof(AuthToken));
                }

                if (SetProperty(ref _enabled, value))
                    Save();
            }
        }

        public int Port
        {
            get => _port;
            set
            {
                if (SetProperty(ref _port, value))
                {
                    OnPropertyChanged(nameof(Endpoint));
                    OnPropertyChanged(nameof(DisplayEndpoint));
                    Save();
                }
            }
        }

        public bool ShareDevSpaceTerminalOutput
        {
            get => _shareDevSpaceTerminalOutput;
            set
            {
                if (SetProperty(ref _shareDevSpaceTerminalOutput, value))
                    Save();
            }
        }

        public string AuthToken
        {
            get => _authToken;
            set
            {
                if (SetProperty(ref _authToken, value ?? string.Empty))
                    Save();
            }
        }

        public string Endpoint => $"http://127.0.0.1:{_port}/sse";

        public string DisplayEndpoint => _runtimeRunning && !string.IsNullOrWhiteSpace(_runtimeEndpoint)
            ? _runtimeEndpoint
            : Endpoint;

        public string RuntimeStatus => _runtimeRunning
            ? "Running"
            : string.IsNullOrWhiteSpace(_runtimeError) ? "Stopped" : "Error";

        public string RuntimeEndpoint => _runtimeEndpoint;

        public string RuntimeError => _runtimeError;

        public void RegenerateAuthToken()
        {
            AuthToken = GenerateToken();
        }

        public void UpdateRuntimeState(bool running, string endpoint, string error)
        {
            var oldStatus = RuntimeStatus;
            var oldEndpoint = _runtimeEndpoint;
            var oldDisplayEndpoint = DisplayEndpoint;
            var oldError = _runtimeError;

            _runtimeRunning = running;
            _runtimeEndpoint = endpoint ?? string.Empty;
            _runtimeError = error ?? string.Empty;

            if (!string.Equals(oldStatus, RuntimeStatus, StringComparison.Ordinal))
                OnPropertyChanged(nameof(RuntimeStatus));
            if (!string.Equals(oldEndpoint, _runtimeEndpoint, StringComparison.Ordinal))
                OnPropertyChanged(nameof(RuntimeEndpoint));
            if (!string.Equals(oldDisplayEndpoint, DisplayEndpoint, StringComparison.Ordinal))
                OnPropertyChanged(nameof(DisplayEndpoint));
            if (!string.Equals(oldError, _runtimeError, StringComparison.Ordinal))
                OnPropertyChanged(nameof(RuntimeError));
        }

        private static SourceGitMcpSettings Load()
        {
            var path = GetStoragePath();
            var settings = new SourceGitMcpSettings();

            try
            {
                if (File.Exists(path))
                {
                    using var stream = File.OpenRead(path);
                    using var document = JsonDocument.Parse(stream);
                    var root = document.RootElement;

                    if (root.TryGetProperty("enabled", out var enabled) &&
                        enabled.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        settings._enabled = enabled.GetBoolean();
                    }

                    if (root.TryGetProperty("port", out var port) && port.TryGetInt32(out var parsedPort))
                        settings._port = parsedPort;

                    if (root.TryGetProperty("shareDevSpaceTerminalOutput", out var share) &&
                        share.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        settings._shareDevSpaceTerminalOutput = share.GetBoolean();
                    }

                    if (root.TryGetProperty("authToken", out var token) && token.ValueKind == JsonValueKind.String)
                        settings._authToken = token.GetString() ?? string.Empty;
                }
            }
            catch
            {
                // Invalid or unreadable optional MCP settings fall back to safe defaults.
            }

            settings._storagePath = path;

            if (settings._port is < 1 or > 65535)
                settings._port = SourceGitMcpOptions.DefaultPort;

            if (settings._enabled && string.IsNullOrWhiteSpace(settings._authToken))
            {
                settings._authToken = GenerateToken();
                settings.Save();
            }

            return settings;
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_storagePath))
                return;

            var temporaryPath = _storagePath + ".tmp";
            try
            {
                var directory = Path.GetDirectoryName(_storagePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                using (var stream = CreateSecureSettingsFile(temporaryPath))
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteBoolean("enabled", _enabled);
                    writer.WriteNumber("port", _port);
                    writer.WriteBoolean("shareDevSpaceTerminalOutput", _shareDevSpaceTerminalOutput);
                    writer.WriteString("authToken", _authToken);
                    writer.WriteEndObject();
                }

                File.Move(temporaryPath, _storagePath, true);

                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(
                        _storagePath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }
            catch
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch
                {
                    // Ignore cleanup failures for optional settings persistence.
                }
            }
        }

        private static FileStream CreateSecureSettingsFile(string path)
        {
            if (File.Exists(path))
                File.Delete(path);

            var options = new FileStreamOptions
            {
                Access = FileAccess.Write,
                Mode = FileMode.CreateNew,
                Share = FileShare.None,
                Options = FileOptions.WriteThrough,
            };

            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

            return new FileStream(path, options);
        }

        private static string GetStoragePath()
        {
            var dataDir = Native.OS.DataDir;
            if (string.IsNullOrWhiteSpace(dataDir))
                return string.Empty;

            return Path.Combine(dataDir, "mcp.json");
        }

        private static string GenerateToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        }

        private static SourceGitMcpSettings _instance;

        private bool _enabled;
        private int _port = SourceGitMcpOptions.DefaultPort;
        private bool _shareDevSpaceTerminalOutput = true;
        private string _authToken = string.Empty;
        private string _storagePath = string.Empty;
        private bool _runtimeRunning;
        private string _runtimeEndpoint = string.Empty;
        private string _runtimeError = string.Empty;
    }
}
