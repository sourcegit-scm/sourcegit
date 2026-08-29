using System.Collections.Generic;

namespace SourceGit.DevSpaces.Terminal
{
    public sealed record TerminalReadResult(
        IReadOnlyList<DevSpaceTerminalEvent> Events,
        long OldestSequence,
        long NextSequence,
        bool Truncated);
}
