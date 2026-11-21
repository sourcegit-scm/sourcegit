# Selected-commit changes regression harness

Run with the .NET 10 SDK and Git on `PATH`:

```sh
dotnet run --project tests/SelectedCommitChanges/SelectedCommitChanges.csproj
```

This framework-free console harness requires no NuGet packages. It links the
production `CommitChange`, `Change`, `DiffOption`, `CommitChanges` view-model, and
`QuerySelectedCommitChanges` files rather
than reimplementing the aggregation algorithm. App process setup, minimal commit metadata, and UI infrastructure are stubbed,
avoiding Avalonia and app initialization. A queued dispatcher makes late-success
and late-error disposal races deterministic. Fixtures run real Git commands in disposable temporary
repositories; they never change the developer's worktree or Git configuration.

Coverage includes root commits, immutable before/after revisions, contiguous and
non-contiguous selections (including skipped same-file edits), rename chains and
path reuse, file lifetimes, reverted content with empty diffs, preservation of refs,
index, working files and object database, combined merge behavior with first-parent
resolution endpoints, unusual path characters,
binary objects, executable mode-only changes, symlinks, literal pathspecs, SHA-256
repositories, submodule configuration, malformed output,
pre-cancelled and in-flight cancellation, Git error propagation, latest-filter application, selection clearing, and
disposed view-model callback suppression. Multi-selection detail tests cover the
exact defensive commit list, all three tab indices, lazy historical-file context,
revision switching without changing the aggregate, repeated navigation, invalid
selections and disposed file-detail ownership. Card regressions verify reuse of the
single-commit Information control (including message, signature, links and file
preview), explicit virtualization and borders, deferred per-card ownership, a
shared bounded query gate, card/Files selection synchronization, and disposal of
all card contexts. Historical-file and card detail infrastructure
is stubbed in this harness; actual Avalonia interaction and file previews require
runtime UI verification.

The fixtures supply commits in newest-first history order, as required by the
production query. Windows skips the Unix process-cancellation, symlink and
invalid-filename pathspec checks.
This is not a substitute for compiling Avalonia bindings, exercising the real
app's process configuration, or manually checking the UI's selection lifecycle.
