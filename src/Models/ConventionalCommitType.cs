using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SourceGit.Models
{
    public class ConventionalCommitType
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PrefillShortDesc { get; set; } = string.Empty;

        public ConventionalCommitType(string name, string type, string description)
        {
            Name = name;
            Type = type;
            Description = description;
        }

        public static List<ConventionalCommitType> Load(string storageFile)
        {
            try
            {
                if (!string.IsNullOrEmpty(storageFile) && File.Exists(storageFile))
                    return JsonSerializer.Deserialize(File.ReadAllText(storageFile), JsonCodeGen.Default.ListConventionalCommitType) ?? [];
            }
            catch
            {
                // Ignore errors.
            }

            return new List<ConventionalCommitType> {
                new("Features", "feat", "Adding a new feature"),
                new("Bug Fixes", "fix", "Fixing a bug"),
                new("Work In Progress", "wip", "Still being developed and not yet complete"),
                new("Reverts", "revert", "Undoing a previous commit"),
                new("Code Refactoring", "refactor", "Restructuring code without changing its external behavior"),
                new("Performance Improvements", "perf", "Improves performance"),
                new("Builds", "build", "Changes that affect the build system or external dependencies"),
                new("Continuous Integrations", "ci", "Changes to CI configuration files and scripts"),
                new("Documentations", "docs", "Updating documentation"),
                new("Styles", "style", "Elements or code styles without changing the code logic"),
                new("Tests", "test", "Adding or updating tests"),
                new("Chores", "chore", "Other changes that don't modify src or test files"),
            };
        }
    }
}
