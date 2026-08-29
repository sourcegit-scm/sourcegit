using System.Collections.Generic;

namespace DevBoard.AI
{
    public static class Options
    {
        public static readonly string IgnoredReasoningEffortLevel = "unspecified";
        public static readonly List<string> ReasoningEffortLevels = [
            IgnoredReasoningEffortLevel,
            "none",
            "minimal",
            "low",
            "medium",
            "high",
        ];
    }
}
