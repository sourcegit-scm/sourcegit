# Changes tab commit-message checks

Run from the repository root:

```sh
dotnet run --project tests/ChangesMessageChecks
```

The checks use Avalonia Headless and a temporary Git repository. They cover
complete multiline messages and parsed formatting, one-line collapse, restoring
the expanded size, actual splitter dragging, saved layout preferences, commit
switching, scroll reset, tab changes, and preserving diff space in small windows.
User preferences are isolated inside the temporary fixture.

Pass an existing output directory to save light, dark, and collapsed previews:

```sh
dotnet run --project tests/ChangesMessageChecks -- /absolute/path/to/previews
```
