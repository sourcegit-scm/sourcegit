# DevBoard Identity Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the application, build, runtime, packaging, and current documentation identity from SourceGit / Dev Board to `DevBoard`, while preserving upstream attribution and migrating existing user data safely.

**Architecture:** Perform one coordinated branch-wide identity migration. First lock the desired identity with regression tests, then rename .NET solution/project/namespaces, add an isolated data-directory resolver that can migrate legacy SourceGit data, and finally update every platform package/workflow/documentation surface. Historical `SourceGit` references are permitted only by an explicit allowlist for upstream attribution, licensing, and legacy-data migration.

**Tech Stack:** .NET 10, C#, Avalonia 11, xUnit, Node.js contract tests, PowerShell packaging scripts, GitHub Actions, MSIX/AppImage/DEB/Flatpak/macOS packaging.

**Spec:** `docs/superpowers/specs/2026-08-29-devboard-identity-migration-design.md`

## Global Constraints

- Canonical visible and technical product name is exactly `DevBoard`.
- Repository remains `dhhieu113pro/dev-board`.
- Main solution becomes `DevBoard.slnx`; main project becomes `src/DevBoard.csproj`; executable/assembly becomes `DevBoard` / `DevBoard.exe`.
- Root C# namespace becomes `DevBoard` and XAML CLR/type references must match it.
- Windows data path becomes `%APPDATA%\DevBoard`; Linux becomes `~/.devboard`; macOS becomes `~/Library/Application Support/DevBoard`.
- Existing SourceGit data must be preserved; legacy directories are never deleted automatically.
- Portable `data` beside the executable/AppImage takes precedence and bypasses migration.
- Do not ship a `SourceGit.exe` compatibility alias.
- Lowercase ecosystem identifiers use `devboard`.
- `SourceGit` is allowed only for upstream `sourcegit-scm/sourcegit` attribution/license/history and explicit legacy migration code/tests.
- Microsoft Store Partner Center identity values remain externally configured; do not guess or hard-code them.
- Existing Store safety remains: PR/manual builds do not submit; only `vX.Y.Z-store` may submit; x64 and ARM64 packages are mandatory.

---

### Task 1: Add the stale-identity regression contract

**Files:**
- Create: `scripts/check-devboard-identity.mjs`
- Create: `tests/devboard-identity.test.mjs`
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Produces: `scanIdentity(rootDir: string): Array<{ path: string, line: number, text: string }>` from `scripts/check-devboard-identity.mjs`.
- Produces: a CLI exit code of `0` when only allowlisted historical SourceGit references remain, otherwise `1` with offending file/line output.
- Consumes: repository working tree only; no network access.

- [ ] **Step 1: Write failing Node contract tests**

Create `tests/devboard-identity.test.mjs` with fixtures/assertions that require the scanner to reject current-product uses such as `namespace SourceGit`, `src/SourceGit.csproj`, `SourceGit.exe`, `dhhieu113pro/sourcegit`, and `Product>Dev Board`, while allowing `sourcegit-scm/sourcegit`, `LICENSE`, the migration design/plan, and explicitly marked legacy-migration code.

```js
import assert from 'node:assert/strict';
import test from 'node:test';
import { scanText } from '../scripts/check-devboard-identity.mjs';

test('rejects stale current-product identity', () => {
  const hits = scanText('src/Foo.cs', 'namespace SourceGit.Models;\nvar exe = "SourceGit.exe";');
  assert.equal(hits.length, 2);
});

test('allows upstream attribution', () => {
  assert.deepEqual(scanText('README.md', 'Based on SourceGit (https://github.com/sourcegit-scm/sourcegit).'), []);
});
```

- [ ] **Step 2: Run the tests and verify RED**

Run: `node --test tests/devboard-identity.test.mjs`

Expected: FAIL because `scripts/check-devboard-identity.mjs` does not exist.

- [ ] **Step 3: Implement the minimal scanner**

Implement `scanText(path, text)` and `scanIdentity(rootDir)` using Node built-ins. Scan current-product source/build/workflow/docs files, skip `.git`, `bin`, `obj`, generated screenshots/binaries, and allow only explicit historical contexts. The CLI must print each violation as `path:line: text` and set `process.exitCode = 1` when violations exist.

- [ ] **Step 4: Run the focused tests**

Run: `node --test tests/devboard-identity.test.mjs`

Expected: PASS.

- [ ] **Step 5: Wire the contract into CI**

Add to `.github/workflows/ci.yml` after checkout/setup:

```yaml
- name: Verify DevBoard identity
  run: node scripts/check-devboard-identity.mjs
```

During this task, invoke the scanner in a test mode/fixture mode rather than requiring the existing tree to be clean; the repository-wide CLI is expected to fail until later rename tasks complete.

- [ ] **Step 6: Commit**

```bash
git add scripts/check-devboard-identity.mjs tests/devboard-identity.test.mjs .github/workflows/ci.yml
git commit -m "test: lock DevBoard identity contract"
```

### Task 2: Rename the .NET solution, projects, assembly, namespaces, and XAML types

**Files:**
- Rename: `SourceGit.slnx` -> `DevBoard.slnx`
- Rename: `src/SourceGit.csproj` -> `src/DevBoard.csproj`
- Rename: `tests/SourceGit.Tests/SourceGit.Tests.csproj` -> `tests/DevBoard.Tests/DevBoard.Tests.csproj`
- Move: all files under `tests/SourceGit.Tests/` -> `tests/DevBoard.Tests/`
- Modify: all `src/**/*.cs`, `src/**/*.axaml`, `tests/DevBoard.Tests/**/*.cs`
- Modify: `src/DevBoard.csproj`
- Modify: `DevBoard.slnx`

**Interfaces:**
- Produces: assembly/executable `DevBoard` / `DevBoard.exe`.
- Produces: root namespace `DevBoard`.
- Produces: test project `tests/DevBoard.Tests/DevBoard.Tests.csproj` referencing `../../src/DevBoard.csproj`.
- Consumes: existing application source without feature behavior changes.

- [ ] **Step 1: Add failing project identity assertions**

Extend `tests/devboard-identity.test.mjs` to assert that `DevBoard.slnx`, `src/DevBoard.csproj`, and `tests/DevBoard.Tests/DevBoard.Tests.csproj` exist in the final tree and that the main project declares:

```xml
<AssemblyName>DevBoard</AssemblyName>
<RootNamespace>DevBoard</RootNamespace>
<Product>DevBoard</Product>
<RepositoryUrl>https://github.com/dhhieu113pro/dev-board.git</RepositoryUrl>
<TrimmerRootAssembly Include="DevBoard" />
```

- [ ] **Step 2: Run the identity tests and verify RED**

Run: `node --test tests/devboard-identity.test.mjs`

Expected: FAIL because the old solution/project paths and identity still exist.

- [ ] **Step 3: Rename solution/project/test paths and metadata**

Use `git mv` for the solution, main project, and test directory/project. Update all solution project entries and project references. In `src/DevBoard.csproj`, set `AssemblyName`, `RootNamespace`, `Product`, `PackageProjectUrl`, and `RepositoryUrl` to DevBoard values, change `TrimmerRootAssembly Include="SourceGit"` to `DevBoard`, while retaining upstream copyright/license attribution where required.

- [ ] **Step 4: Rename namespaces and XAML CLR references mechanically**

Replace current application type identity `SourceGit` -> `DevBoard` across `src/**/*.cs`, `src/**/*.axaml`, and tests. This includes `namespace`, `using`, `x:Class`, `xmlns:*="using:SourceGit..."`, compiled binding/type references, and fully-qualified reflection/type strings. Do not alter strings whose sole purpose is upstream attribution or legacy migration.

- [ ] **Step 5: Compile to expose missed type references**

Run: `dotnet build DevBoard.slnx -c Debug`

Expected: PASS. Any compiler/XAML error mentioning `SourceGit.*` must be corrected before proceeding.

- [ ] **Step 6: Run .NET tests**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj -c Release`

Expected: PASS.

- [ ] **Step 7: Run focused identity tests**

Run: `node --test tests/devboard-identity.test.mjs`

Expected: project/namespace assertions PASS; repository-wide scan may still report packaging/docs references scheduled for later tasks.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "refactor: rename application identity to DevBoard"
```

### Task 3: Add safe legacy user-data migration

**Files:**
- Create: `src/Native/DataDirectoryResolver.cs`
- Create: `tests/DevBoard.Tests/Native/DataDirectoryResolverTests.cs`
- Modify: `src/Native/OS.cs`
- Modify: `src/Native/Windows.cs`
- Modify: `src/Native/Linux.cs`
- Modify: `src/Native/MacOS.cs`

**Interfaces:**
- Produces: `DataDirectoryResolver.Resolve(string portablePath, string devBoardPath, string legacySourceGitPath, Action<string>? log = null): string`.
- Behavior: portable path wins when present; otherwise non-empty DevBoard path wins; otherwise legacy SourceGit directory is recursively copied to DevBoard; on migration failure legacy path is returned for that run; legacy data is never deleted.
- Platform backends provide both canonical and legacy paths; `OS.SetupDataDir()` owns resolution before settings initialization.

- [ ] **Step 1: Write failing xUnit migration tests**

Create tests using temporary directories for all required cases:

```csharp
[Fact]
public void LegacyOnly_IsCopiedToDevBoard_AndLegacyRemains() { /* assert files in both */ }

[Fact]
public void ExistingDevBoardData_WinsWithoutOverwrite() { /* assert legacy ignored */ }

[Fact]
public void PortableData_WinsAndBypassesMigration() { /* assert returned portable path */ }

[Fact]
public void MigrationFailure_FallsBackToLegacy() { /* inject/cause copy failure and assert legacy */ }
```

Also cover no-existing-data -> DevBoard path.

- [ ] **Step 2: Run migration tests and verify RED**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj -c Release --filter DataDirectoryResolverTests`

Expected: FAIL because `DataDirectoryResolver` does not exist.

- [ ] **Step 3: Implement the resolver**

Implement a focused internal/static resolver. A directory counts as existing DevBoard data when it exists and contains at least one file or directory. Copy recursively with `Directory.CreateDirectory`, `Directory.EnumerateDirectories`, and `File.Copy(..., overwrite: false)`. Catch migration exceptions, log a sanitized message, and return the legacy path. Never delete or move legacy data.

- [ ] **Step 4: Update platform path providers**

Windows canonical: `%APPDATA%\DevBoard`; legacy: `%APPDATA%\SourceGit`.

Linux canonical: `~/.devboard`; legacy: `~/.sourcegit`; preserve AppImage portable `data` precedence.

macOS canonical: `ApplicationData/DevBoard`; legacy: `ApplicationData/SourceGit`.

Refactor the backend interface so `OS.SetupDataDir()` has the canonical, legacy, and portable candidates required by the resolver rather than embedding migration separately in each backend.

- [ ] **Step 5: Run focused migration tests**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj -c Release --filter DataDirectoryResolverTests`

Expected: PASS.

- [ ] **Step 6: Run the complete .NET test suite**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj -c Release`

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Native tests/DevBoard.Tests
git commit -m "feat: migrate legacy SourceGit user data to DevBoard"
```

### Task 4: Rename Linux and macOS packaging identity

**Files:**
- Rename: `build/resources/_common/applications/sourcegit.desktop` -> `build/resources/_common/applications/devboard.desktop`
- Rename: `build/resources/_common/icons/sourcegit.png` -> `build/resources/_common/icons/devboard.png`
- Rename: `build/resources/appimage/sourcegit.appdata.xml` -> `build/resources/appimage/devboard.appdata.xml`
- Rename: `build/resources/flatpak/sourcegit.desktop` -> `build/resources/flatpak/devboard.desktop`
- Modify: `build/resources/deb/DEBIAN/control`
- Modify: `build/resources/deb/DEBIAN/postinst`
- Modify: `build/resources/deb/DEBIAN/preinst`
- Modify: `build/resources/deb/DEBIAN/prerm`
- Modify: `build/resources/app/App.plist`
- Modify: packaging references discovered under `build/**`
- Modify: `tests/devboard-identity.test.mjs`

**Interfaces:**
- Produces: Linux package/desktop/AppStream identifiers using lowercase `devboard` and visible labels `DevBoard`.
- Produces: macOS bundle metadata identifying `DevBoard` and launching the DevBoard executable.

- [ ] **Step 1: Add failing package metadata assertions**

Extend `tests/devboard-identity.test.mjs` to assert the new filenames exist and their textual metadata contains `DevBoard`/`devboard`, not current-product `SourceGit`/`sourcegit`.

- [ ] **Step 2: Run focused tests and verify RED**

Run: `node --test tests/devboard-identity.test.mjs`

Expected: FAIL on old package resource names/metadata.

- [ ] **Step 3: Rename packaging resources and textual metadata**

Use `git mv` for named desktop/icon/AppStream resources. Update desktop `Name`, `Exec`, `Icon`; Debian `Package` and installed paths/scripts; Flatpak IDs/commands; AppImage AppStream IDs/executable references; macOS `CFBundleName`, `CFBundleDisplayName`, executable/bundle identifiers, and any package scripts that refer to old paths. Use `devboard` where lowercase identifiers are required and `DevBoard` for visible labels/executable.

- [ ] **Step 4: Run package identity tests**

Run: `node --test tests/devboard-identity.test.mjs`

Expected: packaging assertions PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "build: rename platform packages to DevBoard"
```

### Task 5: Rename Microsoft Store and release automation

**Files:**
- Modify: `scripts/build-store-msix.ps1`
- Modify: `scripts/store-submission.mjs`
- Modify: `tests/store-msix-script.test.mjs`
- Modify: `tests/store-submission.test.mjs`
- Modify: `tests/store-workflow.test.mjs`
- Modify: `.github/workflows/store-msix.yml`
- Modify: `.github/workflows/build.yml`
- Modify: `.github/workflows/package.yml`
- Modify: `.github/workflows/release.yml`
- Modify: `.github/workflows/devspaces-screenshots.yml`
- Modify: any remaining workflow/script references to old solution/project/executable paths

**Interfaces:**
- Produces: Store package contents containing `DevBoard.exe`.
- Produces: package filenames `DevBoard_<version>_x64.msix` and `DevBoard_<version>_arm64.msix` (existing filename convention retained).
- Consumes: externally configured `STORE_PACKAGE_NAME`, `STORE_PUBLISHER`, `STORE_PUBLISHER_DISPLAY_NAME`, `STORE_TENANT_ID`, `STORE_CLIENT_ID`, `STORE_APPLICATION_ID`, `STORE_CLIENT_SECRET`.

- [ ] **Step 1: Change Store/workflow contract tests first**

Update tests so they require `src/DevBoard.csproj`, `DevBoard.exe`, visible `DevBoard`, and renamed test project paths. Add assertions that no current workflow invokes `SourceGit.csproj`, `SourceGit.Tests`, or validates `SourceGit.exe`.

- [ ] **Step 2: Run Store contract tests and verify RED**

Run:

```bash
node --test tests/store-submission.test.mjs tests/store-msix-script.test.mjs tests/store-assets.test.mjs tests/store-workflow.test.mjs
```

Expected: FAIL on old project/executable/workflow references.

- [ ] **Step 3: Update Store packager and workflow**

In `scripts/build-store-msix.ps1`, require `DevBoard.exe` and generate `Executable="DevBoard.exe"`, `DisplayName="DevBoard"`. Update `.github/workflows/store-msix.yml` to restore/test/publish `DevBoard` paths while retaining submodule checkout, NativeAOT, x64/ARM64 verification, and tag-only submission.

- [ ] **Step 4: Update normal build/package/release/screenshot workflows**

Replace old solution/project/test/executable paths in `.github/workflows/build.yml`, `package.yml`, `release.yml`, `devspaces-screenshots.yml`, and any helper scripts they invoke. Rename output/archive/install names to DevBoard/devboard where they represent the current product.

- [ ] **Step 5: Run all Node automation contracts**

Run:

```bash
node --test tests/devboard-identity.test.mjs tests/store-submission.test.mjs tests/store-msix-script.test.mjs tests/store-assets.test.mjs tests/store-workflow.test.mjs
```

Expected: PASS for focused automation contracts.

- [ ] **Step 6: Commit**

```bash
git add .github scripts tests
git commit -m "ci: publish DevBoard across release pipelines"
```

### Task 6: Make current documentation consistently DevBoard

**Files:**
- Modify: `README.md`
- Modify: `docs/store-publishing.md`
- Modify: `build/README.md`
- Modify: `TRANSLATION.md` where it describes the current application rather than historical upstream content
- Modify: other current-product Markdown/docs found by the identity scanner
- Preserve: `LICENSE`, upstream attribution in `THIRD-PARTY-LICENSES.md`, design/spec historical compatibility explanations

**Interfaces:**
- Produces: current documentation whose canonical product name is `DevBoard` and whose repo/build commands point to `dhhieu113pro/dev-board`, `DevBoard.slnx`, `src/DevBoard.csproj`, and `DevBoard` executable/data paths.

- [ ] **Step 1: Run the repository identity scanner and capture remaining documentation failures**

Run: `node scripts/check-devboard-identity.mjs`

Expected: FAIL only on docs/current-product text scheduled in this task (plus explicitly allowlisted historical/migration references, which must not fail).

- [ ] **Step 2: Update README current identity**

Ensure header/logo alt text, all product prose, build commands, executable examples, application-data paths, Store section, badges, and clone instructions use `DevBoard`. Replace the transition note with a short attribution/migration note; do not describe SourceGit as the current executable or repository.

- [ ] **Step 3: Update Store/build/translation documentation**

Change current commands and package examples to DevBoard paths/names. Retain `SourceGit` only when crediting upstream or explaining legacy migration.

- [ ] **Step 4: Run the repository identity scanner**

Run: `node scripts/check-devboard-identity.mjs`

Expected: PASS with zero non-allowlisted stale identity hits.

- [ ] **Step 5: Run Node identity tests**

Run: `node --test tests/devboard-identity.test.mjs`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add README.md docs build/README.md TRANSLATION.md tests/devboard-identity.test.mjs
git commit -m "docs: standardize product name as DevBoard"
```

### Task 7: Full verification and PR readiness

**Files:**
- Modify only if verification exposes a concrete rename defect.

**Interfaces:**
- Produces: fresh evidence that the coordinated rename builds, tests, and packages under DevBoard identity.

- [ ] **Step 1: Run stale identity verification**

Run: `node scripts/check-devboard-identity.mjs`

Expected: PASS.

- [ ] **Step 2: Run all Node contracts**

Run:

```bash
node --test tests/*.test.mjs
```

Expected: all tests PASS.

- [ ] **Step 3: Run all .NET tests**

Run: `dotnet test tests/DevBoard.Tests/DevBoard.Tests.csproj -c Release`

Expected: PASS.

- [ ] **Step 4: Build the renamed solution**

Run: `dotnet build DevBoard.slnx -c Release`

Expected: PASS.

- [ ] **Step 5: Publish Windows x64 and ARM64**

Run:

```powershell
dotnet publish src/DevBoard.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
dotnet publish src/DevBoard.csproj -c Release -r win-arm64 --self-contained true -o artifacts/publish/win-arm64
Test-Path artifacts/publish/win-x64/DevBoard.exe
Test-Path artifacts/publish/win-arm64/DevBoard.exe
```

Expected: both `Test-Path` calls return `True`.

- [ ] **Step 6: Build unsigned Store MSIX packages locally on Windows**

Run `scripts/validate-store-assets.ps1`, then `scripts/build-store-msix.ps1` for x64 and ARM64 using harmless test identity values and the publish directories from Step 5.

Expected: both `DevBoard_<version>_x64.msix` and `DevBoard_<version>_arm64.msix` are created and `makeappx unpack` shows `DevBoard.exe` as the application executable.

- [ ] **Step 7: Inspect Linux/macOS package metadata contracts**

Run the identity/package Node tests and inspect the renamed desktop/AppStream/App.plist files for `devboard` identifiers and `DevBoard` executable/display names.

Expected: no current package metadata points at SourceGit.

- [ ] **Step 8: Review branch diff for accidental attribution removal**

Run:

```bash
git diff master...HEAD -- LICENSE THIRD-PARTY-LICENSES.md README.md src build .github scripts tests docs
```

Expected: upstream SourceGit attribution/license remains; current product identity is DevBoard.

- [ ] **Step 9: Commit any verification-only fixes**

If Step 1-8 exposed a concrete defect, fix it with the smallest change, rerun the failing command, then:

```bash
git add -A
git commit -m "fix: complete DevBoard identity migration"
```

If no fixes were needed, do not create an empty commit.

- [ ] **Step 10: Push and open the coordinated PR**

Push `refactor/devboard-identity` and open a PR to `master` summarizing the technical rename, legacy-data migration, package/workflow changes, and fresh verification evidence. Do not merge until required CI is green.
