using System;
using System.Diagnostics;

using CommunityToolkit.Mvvm.ComponentModel;
using DevBoard.DevSpaces.Terminal;

namespace DevBoard.ViewModels
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

        public string DevSpaceId { get; }

        public string Title { get; }

        public string Terminal { get; }

        public string StartupCommand { get; }

        public string WorkingDirectory { get; }

        public TerminalTranscriptStore Transcript { get; } = new();

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

        public string BackendName
        {
            get => _backendName;
            private set => SetProperty(ref _backendName, value);
        }

        public event Action<DevSpaceTerminal> StopRequested;

        public DevSpaceTerminal(
            string title,
            string terminal,
            string workingDirectory,
            string startupCommand = null,
            string devSpaceId = null)
        {
            Title = title;
            Terminal = terminal;
            WorkingDirectory = workingDirectory;
            StartupCommand = startupCommand ?? string.Empty;
            DevSpaceId = string.IsNullOrWhiteSpace(devSpaceId) ? workingDirectory : devSpaceId;
        }

        public void MarkRunning(string backendName)
        {
            BackendName = backendName ?? string.Empty;
            ErrorMessage = string.Empty;
            State = DevSpaceTerminalState.Running;
            Trace.WriteLine($"DevSpaces terminal backend: {BackendName}");
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
        private string _backendName = string.Empty;
        private bool _disposed;
    }
}
