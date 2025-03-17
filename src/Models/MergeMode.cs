namespace SourceGit.Models
{
    public class MergeMode
    {
        public static readonly MergeMode[] Supported =
        [
            new MergeMode("Default", "Fast-forward if possible", ""),
            new MergeMode("Fast-forward", "Refuse to merge when fast-forward is not possible", "--ff-only"),
            new MergeMode("No Fast-forward", "Always create a merge commit", "--no-ff"),
            new MergeMode("Squash", "Squash merge", "--squash"),
            new MergeMode("Don't commit", "Merge without commit", "--no-ff --no-commit"),
        ];

        public string Name { get; set; }
        public string Desc { get; set; }
        public string Arg { get; set; }

        public MergeMode(string n, string d, string a)
        {
            Name = n;
            Desc = d;
            Arg = a;
        }
    }
}
