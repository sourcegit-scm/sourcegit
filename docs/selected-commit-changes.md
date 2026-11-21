# Selected-commit change summary

Select two or more commits in History (Ctrl/Cmd-click for individual commits,
Shift-click for a range). The Changes tab shows their joined file changes and
loads the selected file's diff. All three detail tabs remain available:

* **Information** lists exactly the selected commits in displayed history order,
  in bordered cards using the same full information view as a single commit:
  metadata, full message, signature, links and changed-file preview. Cards load
  information on demand with bounded concurrency; offscreen cards are virtualized.
* **Changes** keeps the joined summary for the entire history selection.
* **Files** reuses the single-commit historical file browser: full tree, search,
  previews, open/save and applicable existing file actions. It defaults to the
  topmost selected history commit, not a synthetic combined tree. Choose a card
  in Information or use the SHA/subject dropdown in Files to browse another
  selected revision; this never changes the outer history selection or summary.

The file browser is loaded lazily, and changing its revision replaces/disposes
the old detail context. Late tree/search/preview results cannot overwrite a
newer revision or file selection. Search also matches the original name of a
renamed file. Selecting one commit restores the existing commit detail view.

For the previous two-endpoint comparison, select exactly two commits and choose
**Compare these two revisions (not selected changes)** from the context menu.
This is intentionally distinct from the selected-commit summary.

## What is aggregated

The implementation follows the path-based “zip changes” model used by Rebased:

* Process only selected commits, in oldest-to-newest displayed history order.
  Clicking the same commits in a different order does not reverse their changes.
* Join a later change to the earliest existing change whose destination is the
  later change's source path. Keep the first before endpoint and last after
  endpoint. Rename chains therefore retain their initial and final names.
* Add then delete cancels. Delete then add remains two independent entries.
  Reverting to identical content keeps an entry, whose content diff is empty.
* A merge contributes only paths changed relative to **every** parent. Diff
  content uses the first parent's real before endpoint. Different-source merge
  renames are displayed as modifications while retaining a valid before source.

This is **not** a diff of the oldest parent's whole tree against the newest
commit, and it does **not** replay/cherry-pick patches into a synthetic tree.
Files changed only by skipped commits do not appear. When selected commits touch
the same file, their before/after comparison can include intervening unselected
edits to that file. Path-based joining across unrelated branches has the same
limitation; the result is not a hypothetical merge or cherry-pick result.

Rebased can additionally present specialized per-parent/three-way merge views.
This feature provides SourceGit's existing two-pane/unified diff presentation,
not a new three-way merge viewer.

## Safety and lifecycle

All summary queries are read-only Git commands. They do not change HEAD, refs,
the index or working files, and do not create temporary commits or objects.
Git output is NUL-delimited, preserving tabs, newlines, quotes and Unicode in
paths. Exact blob endpoints prevent unrelated occupants of old/new renamed paths
from appearing in a file diff. Absent endpoints never mean the working copy.

Changing selection or closing the repository cancels pending summary queries.
Disposed view models reject queued success/error callbacks. Filtering uses the
latest UI value after loading, and hiding a selected file clears/replaces its
content rather than keeping stale content on screen. Diff refreshes also use a
generation guard for repeated context/whitespace changes.

Summary context menus intentionally offer only external diff and copy path;
reset/check-out and aggregate patch export are not exposed because selected
changes do not share one pair of tree revisions. External diff is disabled for
Gitlinks (submodules); their internal historical revision display remains
available.

## Verification

Run the framework-free production-code regression harness:

```sh
dotnet run --project tests/SelectedCommitChanges/SelectedCommitChanges.csproj
```

Build the app (including compiled Avalonia bindings):

```sh
dotnet build src/SourceGit.csproj -c Debug -m:1 -p:UseSharedCompilation=false
```

Run the upstream formatting check:

```sh
dotnet format --verify-no-changes src/SourceGit.csproj
```

The harness exercises production query and aggregate view-model code, but stubs
Avalonia dispatch and individual commit details. It does not replace runtime UI
checks. Manually check Ctrl/Cmd and Shift selection, each detail tab, repeated
card/revision navigation, search, file actions, and selection changes while data
is loading. This feature adds no runtime NuGet dependencies.

## Known rendering edge case

Same-path endpoints retain Git's path-aware diff and `.gitattributes` behavior.
Renamed endpoints use exact blobs so unrelated files occupying either name cannot
leak into the diff. That blob comparison does not apply filename-based attribute
diff drivers/classification; for example an ASCII file explicitly marked `-diff`
may appear as text after a rename. Ordinary NUL-detected binary files remain
binary. This limitation does not change which selected paths are aggregated.
