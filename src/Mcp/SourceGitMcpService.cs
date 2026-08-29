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

        public static bool IsConfigurationProperty(string propertyName)
        {
            return propertyName is nameof(SourceGitMcpSettings.Enabled) or
                nameof(SourceGitMcpSettings.Port) or
                nameof(SourceGitMcpSettings.ShareDevSpaceTerminalOutput) or
                nameof(SourceGitMcpSettings.AuthToken);
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
            SourceGitMcpSettings settings;
            lock (_sync)
            {
                settings = _settings;
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
                settings?.UpdateRuntimeState(false, string.Empty, string.Empty);
            }
            catch (Exception ex)
            {
                settings?.UpdateRuntimeState(false, string.Empty, ex.Message);
            }
        }

        private static void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (IsConfigurationProperty(e.PropertyName))
                _ = ApplyAsync();
        }

        private static async Task ApplyAsync()
        {
            SourceGitMcpSettings settings = null;
            await _applyGate.WaitAsync().ConfigureAwait(false);
            try
            {
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
                    settings.UpdateRuntimeState(false, string.Empty, string.Empty);
                    return;
                }

                if (string.IsNullOrWhiteSpace(settings.AuthToken))
                    settings.RegenerateAuthToken();

                await host.StopAsync().ConfigureAwait(false);
                var started = await host.StartAsync(CreateOptions(settings)).ConfigureAwait(false);
                settings.UpdateRuntimeState(
                    started,
                    started ? host.SseEndpoint : string.Empty,
                    started ? string.Empty : host.LastError);
            }
            catch (Exception ex)
            {
                settings?.UpdateRuntimeState(false, string.Empty, ex.Message);
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
