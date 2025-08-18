using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Collections;

namespace SourceGit.Models
{
    public static class IssueTrackerMigrator
    {
        public static IEnumerable<IssueTracker> Migrate(string gitDir)
        {
            var settingsFileVersion202526 = Path.Combine(gitDir, "sourcegit.settings");
            var rules = ReadIssueTrackerSettings202526(settingsFileVersion202526);

            var additionalIssueTrackers = rules.Select(rule => new IssueTracker
            {
                IsShared = false,
                Name = rule.Name,
                RegexString = rule.RegexString,
                URLTemplate = rule.URLTemplate,
            });

            return additionalIssueTrackers;
        }

        private static IEnumerable<IssueTrackerRuleVersion202526> ReadIssueTrackerSettings202526(
            string settingsFileVersion202526
        )
        {
            if (!File.Exists(settingsFileVersion202526))
                return [];

            try
            {
                using var stream = File.OpenRead(settingsFileVersion202526);
                var jsonTypeInfo = JsonCodeGen.Default.IssueTrackerRepositorySettingsVersion202526;
                var settings = JsonSerializer.Deserialize(stream, jsonTypeInfo);
                var rules = settings.IssueTrackerRules;
                return rules ?? [];
            }
            catch
            {
                return [];
            }
        }

        public record IssueTrackerRepositorySettingsVersion202526(
            AvaloniaList<IssueTrackerRuleVersion202526> IssueTrackerRules
        );

        public record IssueTrackerRuleVersion202526(
            string Name,
            string RegexString,
            string URLTemplate
        );
    }
}
