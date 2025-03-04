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
        public List<InteractiveRebaseJob> Jobs { get; set; } = new List<InteractiveRebaseJob>();
    }
}
