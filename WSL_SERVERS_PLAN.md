# Implementation Plan: WSL Repositories via "Servers"

## Goal

Make SourceGit on Windows work with repositories that live inside WSL distros, without installing SourceGit inside WSL. Inspired by OpenCode's connections: there is always a default **"This PC"** server (local Windows), and users can add more servers, e.g. a WSL distro.

## Problem today

Opening `\\wsl.localhost\<distro>\...` from Windows runs **Windows git over the 9P file share**:

- very slow on anything but tiny repos
- `safe.directory` / "dubious ownership" errors (files are owned by the Linux user)
- CRLF and filemode mismatches against what Linux tooling sees
- hooks, GPG, SSH config of the distro are not used
- `FileSystemWatcher` on the share does not reliably fire for changes made inside Linux

## Core design

1. **The app keeps using Windows UNC paths everywhere** (`//wsl.localhost/<distro>/home/...`). Repo list, bookmarks, file reads, `.git/*` state files, images and diffs keep working unchanged.
2. **Linux paths exist only at the git process boundary.** For a WSL repo, git runs inside the distro:
   `wsl.exe -d <distro> --cd <linux path> --exec git <args>`
   Paths going into git are translated to Linux form, absolute paths coming back are translated to UNC.
3. **The server is derived from the path prefix** (`//wsl.localhost/<distro>/` or `//wsl$/<distro>/`, case-insensitive, either slash). No per-repo field, no migration of existing preferences.

Every git call already goes through `Command.CreateGitStartInfo` (`src/Commands/Command.cs`); only five call sites bypass it. That is where the change lives.

---

## Phase 0 — Spike (≈0.5 day)

Verify the assumptions before writing production code. Each item gets a yes/no answer and a measured number where relevant.

- [x] **Argument quoting:** `wsl.exe --exec git ...` keeps SourceGit's args intact — quoted paths with spaces, `-c core.editor="\"…\" --core-editor"`, `%`/`$` in `--format=` strings. If not: fall back to `bash -c '<sh-quoted args>'` (more code, decide here).
- [x] **Overhead:** per-call cost of `wsl.exe` with ~20 parallel `git status` / `git log` calls (a normal refresh). Compare against Windows git over `\\wsl.localhost` on a mid-size repo.
- [x] **Cold start:** time of the first call when the WSL VM is stopped.
- [x] **FileSystemWatcher:** does it fire on `\\wsl.localhost\...` for edits made inside Linux? (Expected: no / unreliable.)
- [~] **Interop callbacks:** Linux git can launch `/mnt/c/.../SourceGit.exe --core-editor /home/...`; `SSH_ASKPASS` + `DISPLAY` reach Linux via `WSLENV` and ssh actually uses the askpass.
- [x] **Encoding:** git's UTF-8 stdout passes through `wsl.exe` untouched (only `wsl.exe`'s own output, e.g. `wsl -l -q`, is UTF-16LE).
- [~] **Credential helper:** GCM for Windows works from inside WSL (`/mnt/c/Program Files/Git/mingw64/bin/git-credential-manager.exe`).

---

## Phase 0 results (measured 2026-10-23, Ubuntu-26.04, WSL 2)

| Check | Result |
|-------|--------|
| Argument quoting via `wsl.exe --exec git` | **OK.** `%`, `$`, `"`, spaces in args and in `--cd` path, quoted `core.editor`, and UTF-8 filenames all arrive intact. No `bash -c` fallback needed. |
| `--cd` | Takes a **Linux path only**; a UNC path gives `E_INVALIDARG`. A quoted value with spaces works from .NET. |
| `-d <distro>` | **Must stay unquoted**: `wsl.exe` does not strip quotes there (`WSL_E_DISTRO_NOT_FOUND`). Distro names with spaces therefore fail (unverified whether WSL allows them at all). |
| Overhead | Warm call about 140 ms. 20 parallel `git status` about 530 ms total. Cold start (VM stopped) about 4 s. |
| Windows git on the UNC path | Confirms the bug: `fatal: detected dubious ownership`. |
| `FileSystemWatcher` on `\\wsl.localhost` | **No event** within 8 s for edits made inside Linux. Phase 2 is required. |
| `inotifywait` | **Not installed** in this distro. The polling fallback is not optional. |
| Linux git launching a Windows exe as editor | **OK**, and it receives a **Linux path** (`/home/.../.git/COMMIT_EDITMSG`). The path mapping in 1.6 is required. |
| `WSLENV` | Works. The `/p` flag translates a path variable (`C:\...` becomes `/mnt/c/...`). Use it for `SSH_ASKPASS`. |
| `git-credential-manager.exe` path | Exists under `/mnt/c/Program Files/Git/mingw64/bin/`. Not yet tested end to end with a real remote. |
| Encoding | git output is UTF-8. **`wsl.exe`'s own errors and `wsl -l` are UTF-16LE** (and localised), so detect a `wsl.exe` failure separately instead of showing its text raw. |

Not yet verified: askpass through a real SSH remote, GCM against a real HTTPS remote, `inotifywait` behaviour (not installed here), cancellation killing the Linux git process.

Gotcha for anyone testing from Git Bash: set `MSYS_NO_PATHCONV=1`, or `/home/...` arguments get rewritten to `C:/Program Files/Git/home/...`. This does not affect SourceGit itself.

---

## Phase 1 — Run git inside WSL (≈2–3 days)

After this phase every feature works on WSL repos (except live refresh, see Phase 2).

### 1.1 Path mapping — new `src/Native/WSL.cs`

- [x] `bool TryParse(string path, out string distro, out string linuxPath)` — handles `//wsl.localhost/`, `//wsl$/`, `\\` and `/` separators, case-insensitive host.
- [x] `string ToLinux(string distro, string path)` — UNC → `/…`; `C:\x` → `/mnt/c/x` (shortcut: assumes default automount root `/mnt/`; read `/etc/wsl.conf` if someone changed it).
- [x] `string ToWindows(string distro, string linuxPath)` — `/mnt/c/x` → `C:\x`; anything else → `//wsl.localhost/<distro>/x`.
- [x] Small test covering (`tests/SourceGit.Tests`, 23 cases, `dotnet test`) both prefixes, spaces in distro/path, drive paths, round trips.

### 1.2 Process start — `Command.CreateGitStartInfo`

When `WorkingDirectory` resolves to a WSL distro:

- [x] `FileName = "wsl.exe"`, arguments `-d <distro> --cd <linux cwd> --exec git <existing args>`.
- [x] Editor config uses the `/mnt/c/...` path of `Environment.ProcessPath`.
- [x] Credential helper from the server settings (default: GCM path above; empty = use the distro's own git config, i.e. don't pass `-c credential.helper=`).
- [x] Append to `WSLENV`: `SSH_ASKPASS/p:SSH_ASKPASS_REQUIRE:SOURCEGIT_LAUNCH_AS_ASKPASS:DISPLAY:GIT_SSH_COMMAND:LANG:LC_ALL:SOURCEGIT_WSL_DISTRO`.
- [x] Set `LANG=C` / `LC_ALL=C` (Linux git, output must stay parseable).
- [x] Set `SOURCEGIT_WSL_DISTRO=<distro>` so callbacks can map paths back.
- [x] `SSHKey` path → Linux form inside `GIT_SSH_COMMAND`.
- [ ] No `setsid` wrapping (that is a Unix-host feature); cancellation kills `wsl.exe`, verify the Linux git dies with it.

### 1.3 Route the bypassing call sites through `CreateGitStartInfo`

These set `FileName = Native.OS.GitExecutable` directly:

- [ ] `src/Commands/QueryFileContent.cs` (`RunAsync`, `FromLFSAsync`)
- [ ] `src/Commands/SaveChangesAsPatch.cs`
- [ ] `src/Commands/SaveRevisionFile.cs`
- [ ] `src/Commands/UpdateIndexInfo.cs`

### 1.4 Paths coming back from git → UNC

Translate inside the commands so callers stay unchanged:

- [ ] `QueryRepositoryRootPath` (7 callers: App, Launcher, OpenLocalRepository, ScanRepositories, TrustRepository, Welcome)
- [ ] `QueryGitDir`
- [ ] `QueryGitCommonDir`
- [ ] `Worktree` list parsing
- [ ] Audit: submodule paths, `rev-parse --show-superproject-working-tree` or similar if used

### 1.5 Paths going into git → Linux

Add `protected string ToGitPath(string path)` on `Command` (identity for local repos) and use it in:

- [ ] `Commit` (`-F` temp file)
- [ ] `Tag` (message temp file)
- [ ] `Discard` (pathspec file)
- [ ] `Archive` (output file)
- [ ] `FormatPatch` (output dir)
- [ ] `SaveChangesAsPatch`, `SaveRevisionFile`
- [ ] `Worktree` add (target path)
- [ ] `Clone`, `Init` (target dir)
- [ ] `Apply` (patch file)
- [ ] Audit remaining commands for `Path.GetTempFileName()` / absolute paths in `Args`

### 1.6 Callbacks into SourceGit (`src/App.axaml.cs`)

- [ ] `TryLaunchAsCoreEditor`, `TryLaunchAsRebaseTodoEditor`, `TryLaunchAsRebaseMessageEditor` receive Linux paths → map with `SOURCEGIT_WSL_DISTRO` before `File.Exists`.
- [ ] `TryLaunchAsAskpass`: verify it works unchanged (it only prints the answer to stdout).

### 1.7 Git version per server

`Native.OS.GitVersion` is global today.

- [ ] Cache version per distro (`wsl.exe -d <distro> --exec git --version`).
- [ ] Replace the checks with a per-repo lookup: `ViewModels/Merge.cs`, `ViewModels/Rebase.cs`, `ViewModels/StashChanges.cs` (2×).
- [ ] Git flow detection per server (or skip for WSL in the first version).

### Side effect

`safe.directory` prompts (`TrustRepository` / `AddSafeDirectory`) no longer trigger for WSL repos, because Linux git owns the files.

---

## Phase 2 — Live refresh (≈1 day)

`src/Models/Watcher.cs` uses `FileSystemWatcher`, which does not work reliably on the 9P share.

- [ ] Refactor the event handlers (`OnRepositoryChanged`, `OnWorkingCopyChanged`, `OnGitDirChanged`) to a shared `OnPathChanged(string path)` so a non-FSW source can feed them.
- [ ] For WSL repos start a long-running
      `wsl.exe -d <distro> --exec inotifywait -mrq -e modify,create,delete,move --format %w%f <linux root>`
      and map each output line to UNC → `OnPathChanged`.
- [ ] Kill the process on `Watcher.Dispose`.
- [ ] If `inotifywait` is missing (`command -v inotifywait` fails): fallback timer (≈5 s) marking working copy dirty, plus a one-time notification "install inotify-tools for live refresh".
- [ ] Watch for large repos: inotify watch limit (`fs.inotify.max_user_watches`) — surface the error, don't crash.

---

## Phase 3 — Servers UI (≈2 days)

### 3.1 Model and storage

- [ ] `Models.GitServer { Name, Kind (Local | WSL), Distro, CredentialHelper }`.
- [ ] `Preferences.Servers` list, persisted in `preferences.json`; register the type in `App.JsonCodeGen` (source-generated JSON context).
- [ ] Default entry "This PC" (Local), always present, not removable.
- [ ] Repos in a distro without a matching server entry still work (routing is path-based); offer "Add server for <distro>?" once.

### 3.2 Preferences → Git tab

- [ ] Server list: add / remove / test.
- [ ] "Add" lists distros from `wsl.exe -l -q` (decode as UTF-16LE).
- [ ] "Test" shows the git version inside the distro, or the error.
- [ ] Per-server credential helper field.

### 3.3 Repository entry points

- [ ] Open / Clone / Init / Scan dialogs: server picker that seeds the folder browser at `\\wsl.localhost\<distro>\home\<user>` (`wsl.exe -d <distro> --exec sh -c 'echo $HOME'`).
- [ ] Welcome page: distro badge on WSL repos. (Group-by-server: later.)
- [ ] Open terminal on a WSL repo → `wsl.exe -d <distro> --cd <linux path>` (inside Windows Terminal when that is the configured terminal).
- [ ] Localization keys for all new strings (`src/Resources/Locales`, en_US at minimum).

---

## Out of scope (first version)

- External diff/merge tools on WSL repos (`git difftool` would start the Linux tool; Windows tools would get Linux paths).
- Custom actions on WSL repos (they run with the Windows shell; UNC `${REPO}` still works for simple cases).
- Per-server global git config page (user.name / email shown are the Windows ones).
- Remote servers over SSH (the server model leaves room for it, but nothing is built).

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| `wsl.exe --exec` splits/mangles arguments | Phase 1 grows | Decided in Phase 0; fallback `bash -c` with sh quoting |
| Per-call `wsl.exe` overhead | Slower refresh on small repos | Measured in Phase 0; still far faster than 9P on real repos |
| WSL VM not running | First call takes seconds | Timeout + clear notification |
| Non-default automount root | Wrong `/mnt/c` paths | Read `/etc/wsl.conf` `[automount] root` if needed |
| Distro names with spaces / `wsl$` vs `wsl.localhost` | Routing misses | Covered by WSL.cs tests |
| inotify not installed / watch limit | No live refresh | Polling fallback + notification |

## Estimate

| Phase | Effort |
|-------|--------|
| 0 Spike | 0.5 d |
| 1 Run git inside WSL | 2–3 d |
| 2 Live refresh | 1 d |
| 3 Servers UI | 2 d |
| **Total** | **≈6 d** |
