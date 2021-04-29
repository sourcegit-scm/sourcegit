using System.Collections.Generic;

namespace SourceGit.Models {
    /// <summary>
    ///     合并方式
    /// </summary>
    public class MergeOption {
        public string Name { get; set; }
        public string Desc { get; set; }
        public string Arg { get; set; }

        public static List<MergeOption> Supported = new List<MergeOption>() {
            new MergeOption("Default", "Fast-forward if possible", ""),
            new MergeOption("No Fast-forward", "Always create a merge commit", "--no-ff"),
            new MergeOption("Squash", "Use '--squash'", "--squash"),
            new MergeOption("Don't commit", "Merge without commit", "--no-commit"),
        };

        public MergeOption(string n, string d, string a) {
            Name = n;
            Desc = d;
            Arg = a;
        }
    }
}
