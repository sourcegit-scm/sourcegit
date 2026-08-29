using System.Collections.Generic;

namespace DevBoard.DevSpaces.Terminal
{
    public sealed record TerminalReadResult(
        IReadOnlyList<DevSpaceTerminalEvent> Events,
        long OldestSequence,
        long NextSequence,
        bool Truncated);
}
