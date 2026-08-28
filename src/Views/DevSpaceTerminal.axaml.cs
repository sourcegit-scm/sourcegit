using System;

using Avalonia.Controls;
using Avalonia.Threading;

using Iciclecreek.Terminal;

namespace SourceGit.Views
{
    public partial class DevSpaceTerminal : UserControl, IDisposable
    {
        public DevSpaceTerminal()
        {
            InitializeComponent();
        }

        public void Start(SourceGit.DevSpaces.IDevSpaceSessionLauncher launcher)
        {
            if (_started || DataContext is not ViewModels.DevSpaceTerminal session)
                return;

            _started = true;
            session.StopRequested += OnStopRequested;

            try
            {
                var spec = launcher.Create(session.Command, session.WorkingDirectory);
                Terminal.ProcessExited += OnProcessExited;
                Terminal.LaunchProcess(spec.WorkingDirectory, spec.Process, spec.Arguments);
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

        private bool _started;
        private bool _stopped;
    }
}
