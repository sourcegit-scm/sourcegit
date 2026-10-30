# History quick search checks

Run from the repository root:

```sh
dotnet run --project tests/HistoryQuickSearchChecks
```

These checks use a temporary Git repository and Avalonia Headless. They cover
literal, case-insensitive Mixed matching (SHA prefix, author name/email, full
commit message), loaded-history scope, cancellation, streamed messages, navigation,
filter restoration, history refresh, selection preservation, keyboard input,
Ctrl+F focus and F3 match navigation, inactive-window caret/focus appearance,
and the active filter background. Tests leave user preferences untouched.

An optional PNG path saves light and dark theme previews:

```sh
dotnet run --project tests/HistoryQuickSearchChecks -- /absolute/path/preview.png
```

Quick search is limited to commits already loaded in the history view, honoring
its branch/date/commit-count constraints. It has no dependency on the separate
`mixed-commit-search` branch or changes to the Commit Search panel.

To reproduce filtered graph issues against a real repository without changing
its files or preferences, run:

```sh
dotnet run --project tests/HistoryQuickSearchChecks -- --repository /path/to/repo /absolute/path/preview.png
```

This searches the loaded branch/tag history for `fix:`, checks graph width,
and optionally saves previews at the first and last match. The fixture suite
also covers hidden merges that expose redundant edges to visible ancestors.
