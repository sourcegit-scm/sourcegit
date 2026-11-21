# Commit graph regression checks

Run with .NET 10 from the repository root:

```sh
dotnet run --project tests/CommitGraphChecks -p:UsedAvaloniaProducts=
```

The standalone executable checks stacked branch color boundaries, shared branch
bases, multiple names at one commit, tags, root commits, merged branch lanes,
small palettes, and every graph highlighting mode. It verifies that recoloring
preserves lane positions and grayed-out sections without opening a window or
changing Git history or user preferences.
