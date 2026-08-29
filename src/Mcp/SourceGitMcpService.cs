using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using DevBoard.DevSpaces.Terminal;

namespace DevBoard.Mcp
{
    public static class DevBoardMcpService
    {
        public static bool IsRunning => _host?.IsRunning == true;

        public static string LastError => _host?.LastError ?? string.Empty;

        public static string SseEndpoint => _host?.SseEndpoint ?? string.Empty;

        public static DevBoardMcpOptions CreateOptions(DevBoardMcpSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return new DevBoardMcpOptions
            {
                Port = settings.Port,
                ShareDevSpaceTerminalOutput = settings.ShareDevSpaceTerminalOutput,
                AuthToken = settings.AuthToken,
                MaxConcurrentToolCalls = DevBoardMcpOptions.DefaultMaxConcurrentToolCalls,
            };
        }

        public static bool IsConfigurationProperty(string propertyName)
        {
            return propertyName is nameof(DevBoardMcpSettings.Enabled) or
                nameof(DevBoardMcpSettings.Port) or
                nameof(DevBoardMcpSettings.ShareDevSpaceTerminalOutput) or
                nameof(DevBoardMcpSettings.AuthToken);
        }

        public static void Initialize(DevBoardMcpSettings settings = null)
        {
            settings ??= DevBoardMcpSettings.Instance;

            lock (_sync)
            {
                if (ReferenceEquals(_settings, settings))
                    return;

                if (_settings != null)
                    _settings.PropertyChanged -= OnSettingsChanged;

                _settings = settings;
                _settings.PropertyChanged += OnSettingsChanged;
                _host ??= new DevBoardMcpHost(DevSpaceTerminalRegistry.Instance);
            }

            _ = ApplyAsync();
        }

        public static async Task ShutdownAsync()
        {
            DevBoardMcpHost host;
            DevBoardMcpSettings settings;
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
            DevBoardMcpSettings settings = null;
            await _applyGate.WaitAsync().ConfigureAwait(false);
            try
            {
                DevBoardMcpHost host;
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
        private static DevBoardMcpSettings _settings;
        private static DevBoardMcpHost _host;
    }
}
