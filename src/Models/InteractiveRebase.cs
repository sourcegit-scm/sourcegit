using System.Collections.Generic;

namespace SourceGit.Models
{
    public enum InteractiveRebaseAction
    {
        Pick,
        Edit,
        Reword,
        Squash,
        Fixup,
        Drop,
    }

    public enum InteractiveRebasePendingType
    {
        None = 0,
        Target,
        Pending,
        Ignore,
        Last,
    }

    public class InteractiveCommit
    {
        public Commit Commit { get; set; } = new Commit();
        public string Message { get; set; } = string.Empty;
    }

    public class InteractiveRebaseJob
    {
        public string SHA { get; set; } = string.Empty;
        public InteractiveRebaseAction Action { get; set; } = InteractiveRebaseAction.Pick;
        public string Message { get; set; } = string.Empty;
    }

    public class InteractiveRebaseJobCollection
    {
        public string OrigHead { get; set; } = string.Empty;
        public string Onto { get; set; } = string.Empty;
        public List<InteractiveRebaseJob> Jobs { get; set; } = new List<InteractiveRebaseJob>();
    }
}
