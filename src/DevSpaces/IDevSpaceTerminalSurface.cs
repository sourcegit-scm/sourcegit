using System;
using System.Threading.Tasks;

using Avalonia.Controls;

namespace SourceGit.DevSpaces
{
    internal sealed class DevSpaceTerminalExitedEventArgs : EventArgs
    {
        internal DevSpaceTerminalExitedEventArgs(int exitCode)
        {
            ExitCode = exitCode;
        }

        internal int ExitCode { get; }
    }

    internal interface IDevSpaceTerminalSurface : IDisposable
    {
        Control View { get; }

        string BackendName { get; }

        event EventHandler<DevSpaceTerminalExitedEventArgs> Exited;

        Task StartAsync(DevSpaceLaunchSpec spec);

        void SetPageActive(bool active);

        void Stop();
    }
}
