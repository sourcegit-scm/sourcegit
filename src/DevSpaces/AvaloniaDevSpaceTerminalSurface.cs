using System;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;

using Iciclecreek.Terminal;

namespace SourceGit.DevSpaces
{
    internal sealed class AvaloniaDevSpaceTerminalSurface : IDevSpaceTerminalSurface
    {
        internal AvaloniaDevSpaceTerminalSurface(ControlTemplate template, FontFamily fontFamily)
        {
            _terminal = new Views.DevSpaceTerminalControl
            {
                Template = template,
                FontFamily = fontFamily,
                BufferSize = 3000,
                Process = string.Empty,
            };
            _terminal.ProcessExited += OnProcessExited;
        }

        public Control View => _terminal;

        public string BackendName => "Avalonia Terminal";

        public event EventHandler<DevSpaceTerminalExitedEventArgs> Exited;

        public Task StartAsync(DevSpaceLaunchSpec spec)
        {
            if (_started)
                throw new InvalidOperationException("The terminal surface has already been started.");

            _started = true;
            _terminal.StartingDirectory = spec.WorkingDirectory;
            _terminal.Process = spec.Process;
            _terminal.Args = spec.Arguments;
            return Task.CompletedTask;
        }

        public void SetPageActive(bool active)
        {
            // Keep the Avalonia fallback mounted and measured. Its renderer participates in
            // Avalonia opacity normally, so no explicit visibility toggle is required here.
        }

        public void Stop()
        {
            if (_stopped)
                return;

            _stopped = true;
            _terminal.ProcessExited -= OnProcessExited;
            try
            {
                _terminal.Kill();
            }
            catch
            {
                // The PTY may already have exited or may never have started.
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void OnProcessExited(object sender, ProcessExitedEventArgs e)
        {
            Exited?.Invoke(this, new DevSpaceTerminalExitedEventArgs(e.ExitCode));
        }

        private readonly Views.DevSpaceTerminalControl _terminal;
        private bool _started;
        private bool _stopped;
    }
}
