namespace SourceGit.Models
{
    public class MergeMode(string n, string d, string a)
    {
        public static readonly MergeMode Default =
            new MergeMode("Default", "Use git configuration", "");

        public static readonly MergeMode FastForward =
            new MergeMode("Fast-forward", "Refuse to merge when fast-forward is not possible", "--ff-only");

        public static readonly MergeMode NoFastForward =
            new MergeMode("No Fast-forward", "Always create a merge commit", "--no-ff");

        public static readonly MergeMode Squash =
            new MergeMode("Squash", "Squash merge", "--squash");

        public static readonly MergeMode DontCommit
            = new MergeMode("Don't commit", "Merge without commit", "--no-ff --no-commit");

        public static readonly MergeMode[] Supported =
        [
            Default,
            FastForward,
            NoFastForward,
            Squash,
            DontCommit,
        ];

        public string Name { get; set; } = n;
        public string Desc { get; set; } = d;
        public string Arg { get; set; } = a;
    }
}
