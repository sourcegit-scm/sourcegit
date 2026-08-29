using System.Collections.Generic;

namespace DevBoard.DevSpaces
{
    public sealed record DevSpaceAgent(string Name, string Command)
    {
        public static IReadOnlyList<DevSpaceAgent> BuiltIn { get; } =
        [
            new("Copilot", "copilot"),
            new("Codex", "codex"),
            new("Antigravity", "agy"),
        ];
    }
}
