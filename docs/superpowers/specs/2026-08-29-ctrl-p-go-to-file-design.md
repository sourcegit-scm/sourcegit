# Ctrl+P Go to File Design

## Goal

Add a Visual Studio-style **Go to File** experience to SourceGit. Pressing **Ctrl+P** while a repository/worktree tab is active opens a centered search modal. The user can search by file name/path and by text content, navigate results with the keyboard, and press Enter to open the chosen file in **DevSpaces → Files**.

## User Experience

- `Ctrl+P` opens the modal only when the active launcher page is a repository.
- `Ctrl+T` remains unchanged and continues to create a new tab.
- The search box receives focus immediately.
- Results update while typing.
- File-name/path matches are shown immediately and rank ahead of content-only matches.
- Content matches show a short matching-line preview beneath the path.
- `Up`/`Down` changes the selected result.
- `Enter` opens the selected file and closes the modal.
- `Escape` closes the modal without changing the current DevSpaces state.
- Closing and reopening the modal starts with an empty query; modal search state is not persisted.
- Opening a result activates DevSpaces Files for the current repository/worktree, expands ancestors, selects the file, and uses the existing preview/diff behavior.

## Architecture

### Launcher shortcut and modal lifetime

`Views/Launcher.axaml.cs` remains the owner of application-level shortcuts. It handles Ctrl/Cmd-style modifiers consistently with existing shortcuts, but this feature is specifically bound to Ctrl+P on Windows/Linux and Cmd+P on macOS through the existing `cmdKey` abstraction.

The launcher view model gains a nullable `GoToFileSearch` property that represents modal visibility and state. Opening the modal creates a new `GoToFileSearch` instance for the active `Repository`. Closing sets the property back to null.

### Search model

Add a focused `ViewModels/GoToFileSearch` model. It receives the current repository and a `DevSpaceFiles` instance from the repository-path-scoped `DevSpaces` object. It owns:

- query text;
- result collection;
- selected result;
- cancellation/version token for in-flight content searches;
- opening the selected result.

The model must not own a second file tree or second preview implementation.

### Search source

`DevSpaceFiles` becomes the single source of searchable workspace paths. It exposes an immutable snapshot of file relative paths derived from the same tracked + non-ignored untracked path set already used by the Files explorer. `.git`, ignored paths, deleted rename-source paths, binary-only entries, and absent paths are handled consistently with the existing explorer behavior.

### Search behavior

Search is split into two stages:

1. **Path search** runs synchronously/in-memory against the current file snapshot and ranks exact/starts-with/name matches before generic path contains matches.
2. **Content search** runs asynchronously and is cancelled/invalidated whenever the query changes.

Content search rules:

- search current workspace file contents only;
- skip deleted files that do not exist on disk;
- skip files larger than 1 MiB;
- skip binary files detected by NUL bytes in an initial sample;
- use case-insensitive ordinal matching;
- return only the first matching line per file;
- cap combined displayed results to 100;
- filename/path matches remain ahead of content-only results;
- never block the UI thread while reading file contents.

No persistent full-text index or external search dependency is introduced in this version.

### Result model

Add `GoToFileSearchResult` with:

- `RelativePath`;
- `FileName`;
- `MatchKind` (`Path` or `Content`);
- optional `PreviewText`;
- integer `Rank` used for stable ordering.

Duplicate files are collapsed: if a file already appears as a path match, a content match for the same file may enrich its preview but must not produce a second row.

### Opening a result

`DevSpaceFiles` adds `OpenFile(string relativePath)` that:

1. clears the Files filter;
2. finds the file node in `_nodesByPath`;
3. expands every ancestor folder;
4. rebuilds visible rows;
5. assigns `SelectedNode` so the existing unchanged-file preview or existing `DiffContext` is reused.

`DevSpaces` adds `OpenFile(string relativePath)` that calls `ActivateFiles()` and delegates to `Files.OpenFile(relativePath)`.

The repository’s path-scoped DevSpaces instance is resolved through the existing `DevSpaceRegistry`, preserving worktree isolation.

## UI

Add `Views/GoToFileSearch.axaml` and code-behind. The modal is rendered as an overlay in `Launcher.axaml`, matching the existing command-palette visual language rather than creating a native child window.

Layout:

- centered panel, approximately 680 px wide with a sensible max-height;
- search TextBox at top;
- virtualized result ListBox below;
- each row shows file name prominently, relative path secondarily, and optional content preview;
- selected row follows keyboard navigation;
- lightweight footer hints: `↑↓ Navigate`, `Enter Open`, `Esc Close`.

## Error Handling

Unreadable files are skipped during content search. A single inaccessible file must never fail the whole query. If the workspace snapshot has not finished loading, the modal may initially show no results and refresh from the latest available snapshot; it must remain responsive.

## Performance

- Path filtering is in-memory.
- Content reads run off the UI thread.
- Each query invalidates the previous asynchronous search.
- Maximum file size searched: 1 MiB.
- Maximum displayed results: 100.
- No ignored-directory recursive scan is introduced; reuse the Git-aware path set from DevSpaceFiles.

## Testing and Verification

Because SourceGit currently has no dedicated unit-test project, implementation verification will use focused pure helper methods where practical plus repository build/format checks. Search ranking, path matching, binary/large-file skipping, cancellation/version behavior, and OpenFile ancestor expansion should be kept deterministic and separable so a test project can be added later without redesign.

Required verification:

- `dotnet format src/SourceGit.csproj --verify-no-changes`
- `dotnet build src/SourceGit.csproj -c Release`
- manual keyboard check for Ctrl+P, Up/Down, Enter, Escape;
- manual check across two worktree tabs to ensure selected file opens in the correct worktree’s DevSpaces state.

## Non-goals

- symbol/type/member search;
- fuzzy semantic search;
- persistent indexing database;
- regex search;
- replacing the existing Files explorer search box;
- changing Ctrl+T behavior.
