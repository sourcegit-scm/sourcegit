# Compact-folder regression harness

Run with .NET 10:

```sh
dotnet run --project tests/CompactFolders/CompactFolders.csproj
```

This framework-free console harness links the production `ChangeTreeNode`,
`RevisionFileTreeNode`, Git object, change and sorting types. Only the UI property
notification infrastructure is stubbed; the tree algorithms are not reimplemented.
A deterministic asynchronous directory loader substitutes for Git process I/O.

Coverage includes deep single-directory chains, branching and mixed file/folder
siblings, separate file rows, Unicode and spaces, preservation of real paths and
file action objects, folded ancestors and descendants, filtered tree rebuilds,
disabled compaction, lazy directory lookahead, cached and empty directories,
submodule leaves, repeated compact/ordinary mode changes, and stable operation
paths/labels during recursive re-preparation notifications. Source-structure checks
also cover the three-mode menu, Tree-only compaction submenu, explicit mode
selection structure, persisted two-way toggle binding, original mode icons, and a visible checkbox icon.

This harness does not exercise Avalonia keyboard/focus behavior, bindings,
view-level generation races, or real Git process invocation. Compile the app and
run tests/ChangeViewModeMenu for the actual menu input paths.
