# Working-directory regression checks

This standalone executable exercises the actual Avalonia controls, view models,
commit graph, and Git status refresh pipeline without additional test packages.
It uses a temporary repository and temporary preferences, and removes its fixture
when it finishes. Run it on a desktop with the application prerequisites installed.

To avoid locking the running SourceGit application's output, build separately:

```powershell
dotnet build tests/WorkingDirectoryChecks/WorkingDirectoryChecks.csproj -o build/working-directory-checks
./build/working-directory-checks/WorkingDirectoryChecks.exe
```

Pass `--render` to save light/dark renders for both history orientations at 800 and
1100 pixels wide beside the executable. Pass `--ui` to leave the fixture window
open for inspection; close it to finish cleanup. Remove the separate output folder
after validation. In restricted environments, `-p:UsedAvaloniaProducts=` disables
the build-services telemetry task that otherwise writes outside the workspace.

Coverage includes status classification and deduplication, untracked-file settings,
staged additions edited again, renames with deletion, clean/dirty transitions,
draft preservation, keyboard navigation, exclusive selection, real-commit counts,
programmatic navigation, empty/filtered history, attached/detached HEAD edges, pinned leftmost first-parent ancestry,
shared editor lifecycle, collapsed details, all three Working Directory Head modes,
live preference changes, persisted mode values, original graph lanes in Hide mode,
bare repositories, the Appearance dropdown, and theme/orientation rendering.
Push and interactive merge/rebase workflows require additional manual testing.

Working Directory Head defaults to Hide. The tests opt into conditional display
for the existing graph-head scenarios, then exercise Hide and Always separately.
The `--render` option also saves the Appearance tab with the new dropdown.
