# Repository bookmark branch color checks

Run on Windows with .NET 10:

```sh
dotnet run --project tests/BookmarkBranchColorChecks -p:UsedAvaloniaProducts=
```

These standalone checks cover every bookmark color and graph highlighting mode,
an older or detached HEAD, root commits, merged branches, uncolored bookmarks,
custom graph palettes and line thickness, multiple repository colors, and live
bookmark changes for managed and unmanaged tabs. They use isolated preferences
and leave the user's repositories and settings untouched.
