using System;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

using Iciclecreek.Terminal;

namespace DevBoard.DevSpaces
{
    internal sealed class AvaloniaDevSpaceTerminalSurface : IDevSpaceTerminalSurface
    {
        internal AvaloniaDevSpaceTerminalSurface(IControlTemplate template, FontFamily fontFamily)
        {
            _terminal = new Views.DevSpaceTerminalControl
            {
                Template = template,
                FontFamily = fontFamily,
                BufferSize = 3000,
                Process = string.Empty,
            };
            _terminal.ProcessExited += OnProcessExited;
            _terminal.AddHandler(
                InputElement.PointerPressedEvent,
                OnTerminalPointerPressed,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
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
            _terminal.RemoveHandler(InputElement.PointerPressedEvent, OnTerminalPointerPressed);
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

        private void OnTerminalPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(_terminal).Properties.IsRightButtonPressed ||
                _terminal.IsMouseReportingActive)
                return;

            var copy = new MenuItem
            {
                Header = "Copy",
                IsEnabled = _terminal.HasSelection,
            };
            var paste = new MenuItem { Header = "Paste" };
            var selectAll = new MenuItem { Header = "Select All" };

            copy.Click += async (_, _) => await TryClipboardAsync(async () => await _terminal.CopyAsync());
            paste.Click += async (_, _) => await TryClipboardAsync(_terminal.PasteAsync);
            selectAll.Click += (_, _) => _terminal.SelectAll();

            var menu = new ContextMenu
            {
                ItemsSource = new[] { copy, paste, selectAll },
            };

            menu.Open(_terminal);
            e.Handled = true;
        }

        private static async Task TryClipboardAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch
            {
                // Clipboard access may be unavailable on the current platform/session.
            }
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
