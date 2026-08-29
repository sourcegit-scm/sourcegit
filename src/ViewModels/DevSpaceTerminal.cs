using System;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public enum DevSpaceTerminalState
    {
        Created,
        Running,
        Exited,
        Failed,
        Stopping,
    }

    public sealed class DevSpaceTerminal : ObservableObject, IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();

        public string Title { get; }

        public string Terminal { get; }

        public string StartupCommand { get; }

        public string WorkingDirectory { get; }

        public DevSpaceTerminalState State
        {
            get => _state;
            private set => SetProperty(ref _state, value);
        }

        public int ExitCode
        {
            get => _exitCode;
            private set => SetProperty(ref _exitCode, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public event Action<DevSpaceTerminal> StopRequested;

        public DevSpaceTerminal(string title, string terminal, string workingDirectory, string startupCommand = null)
        {
            Title = title;
            Terminal = terminal;
            WorkingDirectory = workingDirectory;
            StartupCommand = startupCommand ?? string.Empty;
        }

        public void MarkRunning()
        {
            State = DevSpaceTerminalState.Running;
        }

        public void MarkExited(int exitCode)
        {
            ExitCode = exitCode;
            State = DevSpaceTerminalState.Exited;
        }

        public void MarkFailed(string message)
        {
            ErrorMessage = message;
            State = DevSpaceTerminalState.Failed;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            State = DevSpaceTerminalState.Stopping;
            StopRequested?.Invoke(this);
        }

        private DevSpaceTerminalState _state = DevSpaceTerminalState.Created;
        private int _exitCode;
        private string _errorMessage = string.Empty;
        private bool _disposed;
    }
}
