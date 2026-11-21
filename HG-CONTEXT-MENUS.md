# Hg context-menu terminology inventory

The Appearance preference is off by default, persists in preference.json, and is read when a menu is built. Reopening a menu applies a changed preference immediately. Existing translated labels and arguments stay intact; only mapped menu labels gain ` { Hg: ... }`. The same preference also extends selected tooltips. Tooltip bindings respond to preference and locale changes, preserving shortcuts, formatting, literal flags, and dynamic descriptions. Dialog titles, native application menus, and user-defined labels are unchanged.

## Candidates implemented

Git branches are compared with Mercurial bookmarks (movable pointers), rather than permanent named branches. These hints describe corresponding functionality, not commands executed by SourceGit. Histedit requires the Mercurial histedit extension; it edits linear history and is not identical to every Git rebase scenario.

| Resource key | Existing English label | Hg postfix |
| --- | --- | --- |
| `BranchCM.Checkout` | Checkout ${0}$... | `{ Hg: update }` |
| `CommitCM.Checkout` | Checkout Commit... | `{ Hg: update }` |
| `TagCM.Checkout` | Checkout Tag... | `{ Hg: update }` |
| `CreateBranch` | Create Branch... | `{ Hg: bookmark }` |
| `BranchCM.Rename` | Rename ${0}$... | `{ Hg: rename bookmark }` |
| `BranchCM.Delete` | Delete ${0}$... | `{ Hg: delete bookmark }` |
| `BranchCM.DeleteMultiBranches` | Delete selected {0} branches... | `{ Hg: delete bookmark }` |
| `BranchCM.ResetToSelectedCommit` | Reset ${0}$ to ${1}$... | `{ Hg: move bookmark }` |
| `BranchCM.FastForward` | Fast-Forward to ${0}$ | `{ Hg: update bookmark }` |
| `RemoteCM.Fetch` | Fetch | `{ Hg: pull }` |
| `BranchCM.FetchInto` | Fetch ${0}$ into ${1}$ | `{ Hg: pull }` |
| `RemoteCM.EnableAutoFetch` | Enable Auto-Fetch | `{ Hg: pull }` |
| `CommitCM.CherryPick` | Cherry-Pick Commit... | `{ Hg: graft }` |
| `CommitCM.CherryPickMultiple` | Cherry-Pick... | `{ Hg: graft }` |
| `CommitCM.Revert` | Revert Commit... | `{ Hg: backout }` |
| `CommitCM.InteractiveRebase` | Interactive Rebase | `{ Hg: histedit }` |
| `CommitCM.InteractiveRebase.Manually` | Interactively Rebase ${0}$ on ${1}$ | `{ Hg: histedit }` |
| `BranchCM.InteractiveRebase.Manually` | Interactively Rebase ${0}$ on ${1}$ | `{ Hg: histedit }` |
| `CommitCM.InteractiveRebase.Reword` | Reword... | `{ Hg: histedit mess }` |
| `CommitCM.InteractiveRebase.Squash` | Squash into Parent... | `{ Hg: histedit fold }` |
| `CommitCM.InteractiveRebase.Fixup` | Fixup into Parent... | `{ Hg: histedit roll }` |
| `ChangeCM.CheckoutThisRevision` | Reset to This Revision | `{ Hg: revert }` |
| `ChangeCM.CheckoutFirstParentRevision` | Reset to Parent Revision | `{ Hg: revert }` |
| `ChangeCM.ResetFileTo` | Reset File(s) to ${0}$ | `{ Hg: revert }` |
| `FileCM.Discard` | Discard... | `{ Hg: revert }` |
| `FileCM.DiscardMulti` | Discard {0} files... | `{ Hg: revert }` |
| `FileCM.Stash` | Stash... | `{ Hg: shelve }` |
| `FileCM.StashMulti` | Stash {0} files... | `{ Hg: shelve }` |
| `StashCM.Apply` | Apply | `{ Hg: unshelve --keep }` |
| `StashCM.Drop` | Drop | `{ Hg: shelve --delete }` |
| `Blame` | Blame | `{ Hg: annotate }` |
| `WorkingCopy.AddToGitIgnore` | Git Ignore | `{ Hg: .hgignore }` |

The interactive-rebase action flyout also adds the Reword/mess, Squash/fold, and Fixup/roll hints to its structured descriptions. Pick, Edit, and Drop retain their shared action names. Working-copy blame keeps its HEAD-only qualifier before the postfix.

## Candidates deliberately left unchanged

| Candidates | Reason |
| --- | --- |
| Push, pull, merge, rebase, tag, archive, patch export, file history | Terminology already exists in Hg. |
| Stage / unstage; assume unchanged | Hg has no Git index or direct equivalent of assume-unchanged. In particular, hg add/forget would misrepresent staging tracked edits. |
| Discard untracked/added files or a selection including them | Removes files; hg revert would misrepresent the action. Only tracked-file discard gets the revert hint. |
| Reset current branch to a commit | The dialog offers soft, mixed, and hard modes; no single Hg operation describes all three. Moving a non-current branch pointer can be described as moving a bookmark. |
| Checkout as worktree; switch/open/lock/unlock/remove worktree | Hg share is related but has different lifecycle and state; no direct operation mapping. |
| Remote prune, tracking branches, push URL; Git Flow; Git LFS | Git-specific configuration, workflows, or extension behavior without a reliable universal mapping. |
| Submodule add/remove/update/deinit/move/configure | Hg subrepositories differ in lifecycle and configuration; avoid claiming identical commands. |
| Apply selected stash file changes; checkout branch from stash | Compositions or partial operations; a plain unshelve hint would omit important behavior. |
| Resolve using / use mine / use theirs | Resolution concepts are shared; Git rebase can swap ours/theirs roles, making a direct Hg local/other command hint misleading. |
| Compare with HEAD / worktree; SHA and commit metadata | General comparisons or metadata labels, rather than different operation names; retain the existing wording. |
| Ignore-file pattern submenu entries | Ignore terminology is shared; the parent identifies .hgignore. |
| Copy, cut, paste, open, save, reveal, sort, filter, layouts, browser links, tabs/workspaces, AI, avatars, logs, custom actions/templates | Application/editor actions or user-supplied labels, not differing Git/Hg operations. |

## Complete menu source inventory

This includes context menus and button-triggered menu flyouts. Shared factories cover multiple selection and single selection, local/current/remote branches, graph labels, and file/folder variants. Each source below was reviewed; only sources with mapped actions changed.

| Source | Mapped resource keys present |
| --- | --- |
| `src/Resources/Styles.axaml` | None |
| `src/Views/AIAssistant.axaml.cs` | None |
| `src/Views/Avatar.cs` | None |
| `src/Views/Blame.axaml.cs` | None |
| `src/Views/BranchTree.axaml.cs` | `BranchCM.Checkout`, `CreateBranch`, `BranchCM.Rename`, `BranchCM.Delete`, `BranchCM.DeleteMultiBranches`, `BranchCM.ResetToSelectedCommit`, `BranchCM.FastForward`, `RemoteCM.Fetch`, `BranchCM.FetchInto`, `RemoteCM.EnableAutoFetch`, `BranchCM.InteractiveRebase.Manually` |
| `src/Views/ChangeViewModeSwitcher.axaml` | None |
| `src/Views/CommitBaseInfo.axaml` | None |
| `src/Views/CommitBaseInfo.axaml.cs` | None |
| `src/Views/CommitChanges.axaml.cs` | `ChangeCM.CheckoutThisRevision`, `ChangeCM.CheckoutFirstParentRevision` |
| `src/Views/CommitDetail.axaml.cs` | `ChangeCM.CheckoutThisRevision`, `ChangeCM.CheckoutFirstParentRevision`, `Blame` |
| `src/Views/CommitMessagePresenter.cs` | None |
| `src/Views/CommitMessageToolBox.axaml.cs` | None |
| `src/Views/Compare.axaml.cs` | `CommitCM.CherryPickMultiple`, `ChangeCM.ResetFileTo` |
| `src/Views/Conflict.axaml` | None |
| `src/Views/FileHistories.axaml.cs` | None |
| `src/Views/FilterModeSwitchButton.axaml.cs` | None |
| `src/Views/Histories.axaml.cs` | `BranchCM.Checkout`, `CommitCM.Checkout`, `CreateBranch`, `BranchCM.Rename`, `BranchCM.Delete`, `BranchCM.FastForward`, `CommitCM.CherryPick`, `CommitCM.CherryPickMultiple`, `CommitCM.Revert`, `CommitCM.InteractiveRebase`, `CommitCM.InteractiveRebase.Manually`, `CommitCM.InteractiveRebase.Reword`, `CommitCM.InteractiveRebase.Squash`, `CommitCM.InteractiveRebase.Fixup` |
| `src/Views/InteractiveRebase.axaml.cs` | `CommitCM.InteractiveRebase.{Reword,Squash,Fixup}` |
| `src/Views/Launcher.axaml` | None |
| `src/Views/Launcher.axaml.cs` | None |
| `src/Views/LauncherTabBar.axaml.cs` | None |
| `src/Views/MergeConflictEditor.axaml` | None |
| `src/Views/MergeConflictEditor.axaml.cs` | None |
| `src/Views/RemoteProtocolSwitcher.axaml.cs` | None |
| `src/Views/Repository.axaml.cs` | None |
| `src/Views/RepositoryConfigure.axaml` | None |
| `src/Views/RepositoryToolbar.axaml.cs` | None |
| `src/Views/RevisionCompare.axaml.cs` | `ChangeCM.ResetFileTo` |
| `src/Views/RevisionFileContentViewer.axaml.cs` | None |
| `src/Views/RevisionFileTreeView.axaml.cs` | `ChangeCM.CheckoutThisRevision`, `Blame` |
| `src/Views/StashesPage.axaml.cs` | `ChangeCM.CheckoutThisRevision`, `StashCM.Apply`, `StashCM.Drop` |
| `src/Views/SubmoduleRevisionCompare.axaml.cs` | None |
| `src/Views/SubmodulesView.axaml.cs` | None |
| `src/Views/TagsView.axaml.cs` | `TagCM.Checkout`, `CreateBranch` |
| `src/Views/TextDiffView.axaml.cs` | None |
| `src/Views/ViewLogs.axaml.cs` | None |
| `src/Views/Welcome.axaml` | None |
| `src/Views/Welcome.axaml.cs` | None |
| `src/Views/WorkingCopy.axaml` | None |
| `src/Views/WorkingCopy.axaml.cs` | `FileCM.Discard`, `FileCM.DiscardMulti`, `FileCM.Stash`, `FileCM.StashMulti`, `Blame`, `WorkingCopy.AddToGitIgnore` |

## Tooltip candidates implemented

| Source | Tooltip | Hg postfix |
| --- | --- | --- |
| `RepositoryToolbar.axaml` | Fetch | `{ Hg: pull }` |
| `RepositoryToolbar.axaml` | Stash | `{ Hg: shelve }` |
| `RepositoryToolbar.axaml` | Create Branch (shortcut retained) | `{ Hg: bookmark }` |
| `RepositoryToolbar.axaml` | Apply Patch | `{ Hg: import --no-commit }` |
| `Repository.axaml` | Stashes (shortcut retained) | `{ Hg: shelves }` |
| `StashesPage.axaml` | Clear all stashes | `{ Hg: shelve --cleanup }` |
| `Blame.axaml` | Blame on Previous Revision | `{ Hg: annotate }` |
| `StashChanges.axaml` | `--include-untracked` | `{ Hg: shelve --unknown }` |
| `CreateBranch.axaml` | Overwrite existing branch (bound `OverrideTip`) | `{ Hg: bookmark --force }` |

The checkbox tooltip explains that this option applies to tooltips as well as context menus. `HgTextExtension` binds the original label (dynamic resource, literal text, or an existing binding) and the preference to the shared formatter. Only opted-in tooltip fields use this extension; translated labels are never scanned for Git words.

## Tooltip candidates left unchanged

- Push, pull, bisect, commit/amend, merge/rebase and their shared command options already use Hg terminology.
- Stage/unstage, staged-only stash, assume-unchanged, Git index restoration, Git Flow, Git LFS, worktrees, submodules, remote tracking and Git-specific configuration/flags lack a reliable direct equivalent.
- Discard-all can include deletion of untracked/ignored files, so `revert` would not describe every mode.
- GC/prune cleanup, force-with-lease, signoff, reset-author, hook bypass, cherry-pick mainline selection and autostash have Git-specific semantics; no misleading command substitutions are added.
- Blame instructions, search hints, author/commit metadata, branch/tag/worktree details, paths, URLs, logs, keyboard shortcuts, AI/custom-action descriptions and editor/display controls retain their content.

### Complete tooltip source inventory

The following files contain tooltip assignments or tooltip content. All were reviewed, including literal flags, resource labels, bound data, structured controls and programmatic `ToolTip.SetTip` calls. Most present application help or user/repository data and do not need terminology hints.

- `src/Resources/Styles.axaml`
- `src/Views/About.axaml`
- `src/Views/Apply.axaml`
- `src/Views/ApplyStash.axaml`
- `src/Views/Blame.axaml`
- `src/Views/Blame.axaml.cs`
- `src/Views/BlameCommandPalette.axaml`
- `src/Views/BranchTree.axaml`
- `src/Views/ChangeCollectionView.axaml.cs`
- `src/Views/ChangeViewModeSwitcher.axaml`
- `src/Views/Chart.cs`
- `src/Views/CherryPick.axaml`
- `src/Views/Clone.axaml`
- `src/Views/CommitBaseInfo.axaml`
- `src/Views/CommitBaseInfo.axaml.cs`
- `src/Views/CommitDetail.axaml`
- `src/Views/CommitMessagePresenter.cs`
- `src/Views/CommitMessageToolBox.axaml`
- `src/Views/CommitSearchView.axaml`
- `src/Views/CommitSubjectPresenter.cs`
- `src/Views/Compare.axaml`
- `src/Views/ConventionalCommitMessageBuilder.axaml`
- `src/Views/CreateBranch.axaml`
- `src/Views/DeinitSubmodule.axaml`
- `src/Views/DeleteBranch.axaml`
- `src/Views/DeleteMultipleBranches.axaml`
- `src/Views/DiffView.axaml`
- `src/Views/DirHistories.axaml.cs`
- `src/Views/ExecuteCustomAction.axaml`
- `src/Views/Fetch.axaml`
- `src/Views/FileHistories.axaml`
- `src/Views/FileHistories.axaml.cs`
- `src/Views/FileHistoryCommandPalette.axaml`
- `src/Views/FileModeChange.cs`
- `src/Views/FilterModeSwitchButton.axaml`
- `src/Views/GitFlowFinish.axaml`
- `src/Views/Histories.axaml`
- `src/Views/InteractiveRebase.axaml`
- `src/Views/Launcher.axaml`
- `src/Views/LauncherPage.axaml`
- `src/Views/LauncherPagesCommandPalette.axaml`
- `src/Views/LauncherTabBar.axaml`
- `src/Views/LFSLocks.axaml`
- `src/Views/Merge.axaml`
- `src/Views/MergeConflictEditor.axaml`
- `src/Views/OpenFileCommandPalette.axaml`
- `src/Views/Preferences.axaml`
- `src/Views/Pull.axaml`
- `src/Views/Push.axaml`
- `src/Views/PushRevision.axaml`
- `src/Views/Rebase.axaml`
- `src/Views/Repository.axaml`
- `src/Views/RepositoryConfigure.axaml`
- `src/Views/RepositoryToolbar.axaml`
- `src/Views/RevisionCompare.axaml`
- `src/Views/RevisionFiles.axaml`
- `src/Views/StashChanges.axaml`
- `src/Views/StashesPage.axaml`
- `src/Views/Statistics.axaml`
- `src/Views/SubmoduleRevisionCompare.axaml`
- `src/Views/SubmodulesView.axaml`
- `src/Views/TagsView.axaml`
- `src/Views/UpdateSubmodules.axaml`
- `src/Views/WelcomeToolbar.axaml`
- `src/Views/WorkingCopy.axaml`

## References

- [Mercurial glossary](https://www.mercurial-scm.org/help/topics/glossary): bookmarks and working-directory updates.
- [Graft](https://www.mercurial-scm.org/help/commands/graft) and [backout](https://www.mercurial-scm.org/help/commands/backout): copying and reversing changesets.
- [Histedit](https://www.mercurial-scm.org/help/commands/histedit): mess, fold, and roll actions.
- [Revert](https://www.mercurial-scm.org/help/commands/revert) and [annotate](https://www.mercurial-scm.org/help/commands/annotate): restoring files and line attribution.
- [Shelve](https://www.mercurial-scm.org/help/commands/shelve) and [unshelve](https://www.mercurial-scm.org/help/commands/unshelve): saving, keeping, and deleting shelved changes.

- [Import](https://mercurial-scm.org/help/commands/import): `--no-commit` applies the patch without committing.
