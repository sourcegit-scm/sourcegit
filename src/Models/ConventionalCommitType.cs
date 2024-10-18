using System.Collections.Generic;

namespace SourceGit.Models
{
    public class ConventionalCommitType
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public static readonly List<ConventionalCommitType> Supported = new List<ConventionalCommitType>()
        {
            new ConventionalCommitType("feat", "Adding a new feature"),
            new ConventionalCommitType("fix", "Fixing a bug"),
            new ConventionalCommitType("docs", "Updating documentation"),
            new ConventionalCommitType("style", "Elements or code styles without changing the code logic"),
            new ConventionalCommitType("test", "Adding or updating tests"),
            new ConventionalCommitType("chore", "Making changes to the build process or auxiliary tools and libraries"),
            new ConventionalCommitType("revert", "Undoing a previous commit"),
            new ConventionalCommitType("refactor", "Restructuring code without changing its external behavior")
        };

        public ConventionalCommitType(string type, string description)
        {
            Type = type;
            Description = description;
        }
    }
}
