using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using SourceGit.DevSpaces.Terminal;

namespace SourceGit.Mcp
{
    public static class SourceGitMcpService
    {
        public static bool IsRunning => _host?.IsRunning == true;

        public static string LastError => _host?.LastError ?? string.Empty;

        public static string SseEndpoint => _host?.SseEndpoint ?? string.Empty;

        public static SourceGitMcpOptions CreateOptions(SourceGitMcpSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return new SourceGitMcpOptions
            {
                Port = settings.Port,
                ShareDevSpaceTerminalOutput = settings.ShareDevSpaceTerminalOutput,
                AuthToken = settings.AuthToken,
                MaxConcurrentToolCalls = SourceGitMcpOptions.DefaultMaxConcurrentToolCalls,
            };
        }

        public static void Initialize(SourceGitMcpSettings settings = null)
        {
            settings ??= SourceGitMcpSettings.Instance;

            lock (_sync)
            {
                if (ReferenceEquals(_settings, settings))
                    return;

                if (_settings != null)
                    _settings.PropertyChanged -= OnSettingsChanged;

                _settings = settings;
                _settings.PropertyChanged += OnSettingsChanged;
                _host ??= new SourceGitMcpHost(DevSpaceTerminalRegistry.Instance);
            }

            _ = ApplyAsync();
        }

        public static async Task ShutdownAsync()
        {
            SourceGitMcpHost host;
            lock (_sync)
            {
                if (_settings != null)
                    _settings.PropertyChanged -= OnSettingsChanged;

                _settings = null;
                host = _host;
            }

            if (host == null)
                return;

            try
            {
                await host.StopAsync().ConfigureAwait(false);
            }
            catch
            {
                // MCP is optional and must never interfere with SourceGit shutdown.
            }
        }

        private static void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            _ = ApplyAsync();
        }

        private static async Task ApplyAsync()
        {
            await _applyGate.WaitAsync().ConfigureAwait(false);
            try
            {
                SourceGitMcpSettings settings;
                SourceGitMcpHost host;
                lock (_sync)
                {
                    settings = _settings;
                    host = _host;
                }

                if (settings == null || host == null)
                    return;

                if (!settings.Enabled)
                {
                    await host.StopAsync().ConfigureAwait(false);
                    return;
                }

                if (string.IsNullOrWhiteSpace(settings.AuthToken))
                    settings.RegenerateAuthToken();

                await host.StopAsync().ConfigureAwait(false);
                await host.StartAsync(CreateOptions(settings)).ConfigureAwait(false);
            }
            catch
            {
                // Start/stop failures remain visible through SourceGitMcpHost.LastError.
            }
            finally
            {
                _applyGate.Release();
            }
        }

        private static readonly object _sync = new();
        private static readonly SemaphoreSlim _applyGate = new(1, 1);
        private static SourceGitMcpSettings _settings;
        private static SourceGitMcpHost _host;
    }
}
