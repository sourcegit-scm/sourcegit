# Compact folder paths

The **File System Tree** item in the Changes display-mode menu has a
**Compact single-child folders** submenu checkbox. Clicking the parent selects
tree mode; hovering or pressing Right Arrow opens the submenu. Toggling the
checkbox does not change the selected display mode. The three parent items
retain their original icons.
It uses the existing persisted preference (also available in Preferences > General),
so it applies consistently to working-copy changes, selected commits, comparisons
and stashes. Existing preferences and the default remain unchanged.

The historical **Files** tab now supports the same option through its tree button
beside Search. A path such as `src/main/java/example` becomes one folder row,
`src / main / java / example`; files stay separate. Branches and folders containing
both files and folders are not merged. Full-path tooltips, keyboard navigation and
file actions continue using actual Git object paths, not display labels.

Historical folders are loaded on demand. Compaction looks ahead only through
single-folder chains; it does not recursively enumerate branching subtrees.
Original nodes, loaded children and expansion state are retained when switching
between compact and regular views. Query results are shared for repeated loads.

## Verification

```sh
dotnet run --project tests/CompactFolders/CompactFolders.csproj
dotnet run --project tests/ChangeViewModeMenu -c Release -p:DisableAOT=true
dotnet build src/SourceGit.csproj -c Release -p:DisableAOT=true
```

Manual UI checks: toggle from each Changes display-mode menu; expand/collapse deep
chains with Left/Right keys; copy a compact folder's path; open/copy/save a file;
search for a deep file, toggle compaction, and clear search; switch revisions and
toggle repeatedly while files load. Framework-free tests exercise production tree
models. The Avalonia Headless harness checks menu pointer and keyboard input;
full historical-file navigation still requires runtime UI verification.
