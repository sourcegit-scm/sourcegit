﻿using System.Collections.Generic;

namespace SourceGit.Models
{
    public class ConventionalCommitType
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }

        public static readonly List<ConventionalCommitType> Supported = [
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
        ];

        public ConventionalCommitType(string name, string type, string description)
        {
            Name = name;
            Type = type;
            Description = description;
        }
    }
}
