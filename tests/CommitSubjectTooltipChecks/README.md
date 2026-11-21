# Commit subject tooltip checks

Run `dotnet run --project tests/CommitSubjectTooltipChecks/CommitSubjectTooltipChecks.csproj`.

These headless UI checks use a deliberately narrow subject presenter inside a wider SUBJECT cell, matching layouts with badges after the subject. They verify delayed full-message tooltips over blank cell space, issue-link URL tooltips, dismissal when entering another column, and cleanup when a row is recycled or detached.
