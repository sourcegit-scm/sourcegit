using System;

namespace DevBoard.ViewModels
{
    public enum DevSpaceCapabilityState
    {
        Checking,
        Available,
        Unavailable,
        Failed,
    }

    public enum DevSpaceActivityKind
    {
        SessionStarted,
        SessionClosed,
        SessionExited,
        SessionFailed,
        FileOpened,
        AnalysisCompleted,
        DiagnosticsChanged,
    }

    public sealed record DevSpaceDashboardSessionRow(
        DevSpaceTerminal Terminal,
        string Title,
        DevSpaceTerminalState State,
        string WorkingDirectory);

    public sealed record DevSpaceGitSummary(
        int Total,
        int Added,
        int Modified,
        int Deleted,
        int Renamed,
        int Staged,
        int Unstaged)
    {
        public static DevSpaceGitSummary Empty { get; } = new(0, 0, 0, 0, 0, 0, 0);
    }

    public sealed record DevSpaceActivityEntry(
        DevSpaceActivityKind Kind,
        string Text,
        DateTimeOffset At);
}
