using System;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

using Iciclecreek.Terminal;

namespace SourceGit.Views
{
    public partial class DevSpaceTerminal : UserControl, IDisposable
    {
        public DevSpaceTerminal()
        {
            InitializeComponent();

            Terminal.AddHandler(
                PointerPressedEvent,
                OnTerminalPointerPressed,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
        }

        public void Start(SourceGit.DevSpaces.IDevSpaceSessionLauncher launcher)
        {
            if (_started || DataContext is not ViewModels.DevSpaceTerminal session)
                return;

            _started = true;
            session.StopRequested += OnStopRequested;

            Terminal.Process = string.Empty;

            try
            {
                var spec = launcher.Create(session.Terminal, session.WorkingDirectory, session.StartupCommand);

                Terminal.ProcessExited += OnProcessExited;
                Terminal.StartingDirectory = spec.WorkingDirectory;
                Terminal.Process = spec.Process;
                Terminal.Args = spec.Arguments;

                _startupCommand = spec.StartupCommand?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(_startupCommand))
                    Terminal.ShellReady += OnShellReady;

                session.MarkRunning();
            }
            catch (Exception ex)
            {
                session.MarkFailed(App.Text("DevSpaces.StartFailed", ex.Message));
            }
        }

        public void Stop()
        {
            if (_stopped)
                return;

            _stopped = true;

            if (DataContext is ViewModels.DevSpaceTerminal session)
                session.StopRequested -= OnStopRequested;

            Terminal.ShellReady -= OnShellReady;
            Terminal.ProcessExited -= OnProcessExited;

            try
            {
                Terminal.Kill();
            }
            catch
            {
                // The PTY may already have exited.
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private async void OnShellReady(object sender, EventArgs e)
        {
            Terminal.ShellReady -= OnShellReady;

            if (_startupCommandSent || string.IsNullOrWhiteSpace(_startupCommand))
                return;

            _startupCommandSent = true;
            try
            {
                await Terminal.SendInputAsync(_startupCommand + "\r");
            }
            catch (Exception ex)
            {
                if (DataContext is ViewModels.DevSpaceTerminal session)
                    session.MarkFailed(App.Text("DevSpaces.StartFailed", ex.Message));
            }
        }

        private void OnTerminalPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(Terminal).Properties.IsRightButtonPressed ||
                Terminal.IsMouseReportingActive)
                return;

            var copy = new MenuItem
            {
                Header = "Copy",
                IsEnabled = Terminal.HasSelection,
            };
            var paste = new MenuItem { Header = "Paste" };
            var selectAll = new MenuItem { Header = "Select All" };

            copy.Click += async (_, _) => await TryClipboardAsync(async () => await Terminal.CopyAsync());
            paste.Click += async (_, _) => await TryClipboardAsync(Terminal.PasteAsync);
            selectAll.Click += (_, _) => Terminal.SelectAll();

            var menu = new ContextMenu
            {
                ItemsSource = new[] { copy, paste, selectAll },
            };

            menu.Open(Terminal);
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

        private void OnStopRequested(ViewModels.DevSpaceTerminal _)
        {
            Stop();
        }

        private void OnProcessExited(object sender, ProcessExitedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is ViewModels.DevSpaceTerminal session)
                    session.MarkExited(e.ExitCode);
            });
        }

        private string _startupCommand = string.Empty;
        private bool _startupCommandSent;
        private bool _started;
        private bool _stopped;
    }
}
