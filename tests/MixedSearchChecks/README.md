# Mixed commit-search checks

This standalone executable uses an isolated temporary Git repository to exercise
Mixed search against real Git output. It requires Git and the application build
prerequisites, and cleans up its own fixture on success or failure.

```powershell
dotnet build tests/MixedSearchChecks/MixedSearchChecks.csproj -o build/mixed-search-checks
./build/mixed-search-checks/MixedSearchChecks.exe
```

Use a separate output folder if a running SourceGit instance locks the normal
build output. In restricted environments, add `-p:UsedAvaloniaProducts=` to disable
the build-services telemetry task. Remove the temporary output after validation.

Mixed uses case-insensitive SHA prefix matching and literal substring matching
for author name/email and the full commit message, returning a commit if any field
matches. Results follow Git history order and are limited to 1,000 matches.
The current-branch option restricts searches to HEAD's reachable history;
otherwise local branches, remote branches, and HEAD are included.

Coverage includes default mode selection, SHA prefixes and rejection of interior SHA substrings,
author/message OR matching, multiline/long messages, Unicode and punctuation,
branch scoping and detached HEAD, deduplication, matches older than 1,000 commits,
the matching-result limit, cancellation, empty repositories, and existing Author
and Message modes.
