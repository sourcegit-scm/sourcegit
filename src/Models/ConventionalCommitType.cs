using System.Collections.Generic;

namespace SourceGit.Models
{
    public class ConventionalCommitType
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public static readonly List<ConventionalCommitType> Supported = new List<ConventionalCommitType>()
        {
            new ConventionalCommitType("Features", "feat", "Adding a new feature"),
            new ConventionalCommitType("Bug Fixes", "fix", "Fixing a bug"),
            new ConventionalCommitType("Reverts", "revert", "Undoing a previous commit"),
            new ConventionalCommitType("Code Refactoring", "refactor", "Restructuring code without changing its external behavior"),
            new ConventionalCommitType("Performance Improvements", "pref", "Improves performance"),
            new ConventionalCommitType("Builds", "build", "Changes that affect the build system or external dependencies"),
            new ConventionalCommitType("Continuous Integrations", "ci", "Changes to CI configuration files and scripts"),
            new ConventionalCommitType("Documentations", "docs", "Updating documentation"),
            new ConventionalCommitType("Styles", "style", "Elements or code styles without changing the code logic"),
            new ConventionalCommitType("Tests", "test", "Adding or updating tests"),
            new ConventionalCommitType("Chores", "chore", "Other changes that don't modify src or test files"),

        };

        public ConventionalCommitType(string name, string type, string description)
        {
            Name = name;
            Type = type;
            Description = description;
        }
    }
}
