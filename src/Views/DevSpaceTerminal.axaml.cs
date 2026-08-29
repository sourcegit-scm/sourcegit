using System;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Threading;

namespace DevBoard.Views
{
    public partial class DevSpaceTerminal : UserControl, IDisposable
    {
        public DevSpaceTerminal()
        {
            InitializeComponent();
        }

        public void Start(DevBoard.DevSpaces.IDevSpaceSessionLauncher launcher)
        {
            if (_started || DataContext is not ViewModels.DevSpaceTerminal session)
                return;

            _started = true;
            session.StopRequested += OnStopRequested;

            try
            {
                var spec = launcher.Create(session.Terminal, session.WorkingDirectory, session.StartupCommand);
                var surface = CreatePreferredSurface(session);
                AttachSurface(surface);
                _ = StartSurfaceAsync(surface, spec, session);
            }
            catch (Exception ex)
            {
                session.MarkFailed(App.Text("DevSpaces.StartFailed", ex.Message));
            }
        }

        public void SetPageActive(bool active)
        {
            _pageActive = active;
            _surface?.SetPageActive(active);
        }

        public void Stop()
        {
            if (_stopped)
                return;

            _stopped = true;

            if (DataContext is ViewModels.DevSpaceTerminal session)
                session.StopRequested -= OnStopRequested;

            if (_surface == null)
                return;

            var surface = _surface;
            _surface = null;
            surface.Exited -= OnSurfaceExited;
            surface.SetPageActive(false);
            surface.Stop();

            if (ReferenceEquals(SurfaceHost.Child, surface.View))
                SurfaceHost.Child = null;

            surface.Dispose();
        }

        public void Dispose()
        {
            Stop();
        }

        private DevBoard.DevSpaces.IDevSpaceTerminalSurface CreatePreferredSurface(
            ViewModels.DevSpaceTerminal session)
        {
            if (Native.WindowsTerminal.IsSupported)
                return new DevBoard.DevSpaces.WindowsTerminalDevSpaceSurface(session.Transcript);

            return CreateFallbackSurface();
        }

        private DevBoard.DevSpaces.AvaloniaDevSpaceTerminalSurface CreateFallbackSurface()
        {
            if (!Resources.TryGetValue("DevSpaces.FallbackTerminalTemplate", out var resource) ||
                resource is not IControlTemplate template)
            {
                throw new InvalidOperationException("DevSpaces fallback terminal template was not found.");
            }

            return new DevBoard.DevSpaces.AvaloniaDevSpaceTerminalSurface(template, FontFamily);
        }

        private void AttachSurface(DevBoard.DevSpaces.IDevSpaceTerminalSurface surface)
        {
            _surface = surface;
            surface.Exited += OnSurfaceExited;
            surface.SetPageActive(_pageActive);
            SurfaceHost.Child = surface.View;
        }

        private async Task StartSurfaceAsync(
            DevBoard.DevSpaces.IDevSpaceTerminalSurface surface,
            DevBoard.DevSpaces.DevSpaceLaunchSpec spec,
            ViewModels.DevSpaceTerminal session)
        {
            try
            {
                await surface.StartAsync(spec);
            }
            catch (Exception ex)
            {
                if (_stopped || !ReferenceEquals(_surface, surface))
                    return;

                if (surface is DevBoard.DevSpaces.WindowsTerminalDevSpaceSurface)
                {
                    await TryFallbackAsync(surface, spec, session, ex);
                    return;
                }

                session.MarkFailed(App.Text("DevSpaces.StartFailed", ex.Message));
                return;
            }

            if (!_stopped && ReferenceEquals(_surface, surface))
                session.MarkRunning(surface.BackendName);
        }

        private async Task TryFallbackAsync(
            DevBoard.DevSpaces.IDevSpaceTerminalSurface failedSurface,
            DevBoard.DevSpaces.DevSpaceLaunchSpec spec,
            ViewModels.DevSpaceTerminal session,
            Exception nativeError)
        {
            DevBoard.DevSpaces.AvaloniaDevSpaceTerminalSurface fallback;
            try
            {
                fallback = CreateFallbackSurface();
            }
            catch (Exception fallbackCreationError)
            {
                session.MarkFailed(App.Text("DevSpaces.StartFailed", fallbackCreationError.Message));
                return;
            }

            failedSurface.Exited -= OnSurfaceExited;
            failedSurface.SetPageActive(false);
            failedSurface.Stop();
            if (ReferenceEquals(SurfaceHost.Child, failedSurface.View))
                SurfaceHost.Child = null;
            failedSurface.Dispose();

            if (_stopped)
            {
                fallback.Dispose();
                return;
            }

            AttachSurface(fallback);

            try
            {
                await fallback.StartAsync(spec);
                if (!_stopped && ReferenceEquals(_surface, fallback))
                    session.MarkRunning(fallback.BackendName);
            }
            catch (Exception fallbackError)
            {
                session.MarkFailed(App.Text("DevSpaces.StartFailed", fallbackError.Message));
                System.Diagnostics.Trace.WriteLine(
                    $"DevSpaces native terminal startup failed before fallback: {nativeError.Message}");
            }
        }

        private void OnStopRequested(ViewModels.DevSpaceTerminal _)
        {
            Stop();
        }

        private void OnSurfaceExited(object sender, DevBoard.DevSpaces.DevSpaceTerminalExitedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(sender, _surface) && DataContext is ViewModels.DevSpaceTerminal session)
                    session.MarkExited(e.ExitCode);
            });
        }

        private DevBoard.DevSpaces.IDevSpaceTerminalSurface _surface;
        private bool _started;
        private bool _stopped;
        private bool _pageActive;
    }
}
