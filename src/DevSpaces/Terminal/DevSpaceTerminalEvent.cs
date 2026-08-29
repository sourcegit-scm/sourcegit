using System;

namespace DevBoard.DevSpaces.Terminal
{
    public enum TerminalEventKind
    {
        Output,
        Exit,
    }

    public sealed record DevSpaceTerminalEvent(
        long Sequence,
        DateTimeOffset Timestamp,
        TerminalEventKind Kind,
        string Text,
        int? ExitCode);
}
