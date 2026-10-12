# Hierarchical submodule history

Select a commit in the parent repository's history to reveal the first actual commit of each direct submodule whose gitlink changed. Each first commit has a **▸ / ▾** disclosure control on its own log row. Its message, SHA, author and date remain visible when the area is collapsed. No extra summary row is inserted for a readable submodule. Display-only edges connect the parent commit to these submodule commits; expanded areas also connect previous pointers back to the earlier parent commit that recorded them (when visible). Real Git parents and repository history remain unchanged.

In **Preferences → Submodule history**:

- **Changed range** (default): commits reachable from the new gitlink but not the old gitlink(s), plus explicit previous-pointer boundaries. Rewinds and divergent updates retain both endpoints.
- **Full history**: the ancestry reachable from the new and previous gitlinks, including all merge parents.
- **Nesting scope**: **Direct submodules only** (default) keeps navigation at the first level. **Recursive exploration** lets you select a commit inside an expanded area to reveal the next level of changed gitlinks. Each click explores one level; descendants are not loaded automatically. Nested labels remain relative to the top repository.
- **Maximum expanded submodule areas**: defaults to 3; configurable from 1 to 20. This limits areas across all depths and selected parent commits, not the number of internal merge lanes. The history commit limit also bounds each query; truncation is reported.

- **Automatically expand histories when selecting a commit**: off by default, so selection reveals only first-commit previews. Enable it to also load and expand the changed areas up to the area limit.
- **Automatically collapse other submodule histories**: off by default, so opening a new area keeps existing ones. Enable it to replace other expanded areas under the same immediate parent repository, including their descendants. Their first commits remain visible; ancestors of the newly opened area stay expanded. Replacement works even at the area limit. A failed expansion leaves the existing area open. If both automatic options are enabled, selection opens the first available changed area.

These two interaction settings apply to subsequent selections and expansions.

| Action | Result |
| --- | --- |
| Select a parent commit | Show one real log per direct changed submodule by default. |
| Click **▸** on a submodule's first commit | Expand that area's remaining history. |
| Click **▾** on a submodule's first commit | Keep the first log and hide the remaining commits and all nested areas. |
| Click the parent commit again | Hide all rows belonging to its submodules, including first logs that were already collapsed. Other parent commits' areas are unaffected. |
| Click the hidden area's parent commit again | Reveal its direct first logs again, or auto-expand them if enabled. |
| **Collapse all** | Reduce visible top-level areas to their first logs. |
| Click a retained commit's body | Show that commit's details without reopening the area. Use **Compare gitlink change** separately to compare pointers. |

**Space / Enter** on a selected first commit also toggles its area. In recursive mode, selecting a commit inside an expanded area reveals first logs for its changed nested submodules; their arrows expand the next level. First-commit previews do not consume the expanded-area limit. Collapsed first logs display **Collapsed**; blocked expansion shows **Expansion limit reached · N / N**. If the repository or commit objects cannot be read, an explicit error placeholder appears instead of fabricating a commit. Selecting an unrelated commit keeps previously explored areas; refreshing history or changing scope/mode rebuilds the view.

Labels use paths relative to the top repository. **Preferences → Submodule history → Submodule label** offers `[relative path:branch]` (default), `[repository name:relative path:branch]`, and `[relative path]`. Repository names come from the local origin URL's final component (without credentials or `.git`); without origin, the submodule directory name is used. Branch labels describe local refs currently pointing exactly at the selected gitlink: the current branch is preferred, then local branches, then remote branches. When no ref matches, the label is `detached@<short SHA>`. A gitlink does not record historical branch names, so they are not guessed from the parent repository's branch. Changing label format updates already expanded rows immediately.

Local initialization and the referenced Git objects are required; the viewer never initializes, fetches, checks out, or modifies a submodule. Deleted, moved, or uninitialized checkouts and missing objects are reported. Historical branch names cannot be inferred from a gitlink itself. The existing first-parent-only display option is temporarily superseded while areas are expanded so their merge connections remain visible.

**Compare gitlink change** opens a read-only comparison in the same detail panel, for changed submodules or the selected child area. Choose a previous pointer when the owner has multiple parents. The view lists commits added and removed from reachability (bounded by the history limit), file statistics and a unified patch between the two trees. Additions and deletions use an empty tree for the absent endpoint when local objects remain available. Missing checkouts or objects report an error; no fetch or checkout runs.

Child commit details and gitlink comparisons display the recorded gitlink and current local HEAD. Status distinguishes matching, different, removed, and unavailable pointers. This compares the area's recorded endpoint, not an arbitrary historical row or the working tree's uncommitted contents. Use **Refresh pointer status** after an external checkout to refresh this read-only snapshot.

For a child commit or gitlink comparison, a clickable path above the graph shows the top repository and each nested submodule. **Go to owner commit** selects the immediate parent commit that opened the area; a path button selects its corresponding ancestor. These navigation actions preserve expanded areas and never auto-expand siblings. A comparison for a collapsed area still shows its path; unavailable graph targets are disabled.

The toolbar provides **Collapse all**, an expanded-area counter, and menus for history and comparison. Individual controls are on each area's first real commit. Double-clicking a parent-repository commit retains SourceGit's checkout behavior; use single clicks for history visibility. Child-commit and unavailable-placeholder double-clicks never check out a commit. Expanding, collapsing, comparing, and navigating submodule history never move HEAD.
