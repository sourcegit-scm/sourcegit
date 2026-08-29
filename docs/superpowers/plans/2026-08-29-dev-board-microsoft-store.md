# Dev Board Microsoft Store Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an isolated Microsoft Store pipeline that builds Dev Board x64 and ARM64 MSIX packages and optionally submits them to Partner Center from `vX.Y.Z-store` tags.

**Architecture:** Keep the existing SourceGit Avalonia project and normal release pipeline unchanged. A Store-only GitHub Actions workflow publishes the existing project for Windows, a PowerShell packager copies the full publish output into an MSIX layout and emits unsigned Store packages, and a small Node.js Partner Center client submits only on explicitly tagged Store releases.

**Tech Stack:** .NET 10, Avalonia, NativeAOT, PowerShell 7, Windows SDK `makeappx.exe`, GitHub Actions, Node.js 22 built-in test runner, Microsoft Partner Center submission API.

**Spec:** `docs/superpowers/specs/2026-08-29-dev-board-microsoft-store-design.md`

## Global Constraints

- Store-visible product name is exactly `Dev Board`.
- Do not rename `src/SourceGit.csproj`, `SourceGit` namespaces, current app-data paths, or ordinary GitHub release outputs.
- Keep the Store path additive and independent from `.github/workflows/package.yml` and `.github/workflows/release.yml`.
- Store architectures are exactly x64 (`win-x64`) and ARM64 (`win-arm64`); do not add x86.
- Store release tags must match `vX.Y.Z-store`; convert product version `X.Y.Z` to MSIX `X.Y.Z.0`.
- Current repository `VERSION` is two-part (`2026.18`), so manual builds that omit the workflow version input must normalize `X.Y` project metadata to `X.Y.0`; explicit Store inputs and Store tags remain strict three-part `X.Y.Z`.
- Required package identity names: `STORE_PACKAGE_NAME`, `STORE_PUBLISHER`, `STORE_PUBLISHER_DISPLAY_NAME`.
- Required live-submission names: `STORE_TENANT_ID`, `STORE_CLIENT_ID`, `STORE_CLIENT_SECRET`, `STORE_APPLICATION_ID`.
- `STORE_CLIENT_SECRET` must come from GitHub Secrets and must never be logged.
- Manual workflow dispatch builds and verifies packages but never submits.
- Only `vX.Y.Z-store` tags may run live Partner Center submission.
- Store packages are unsigned before ingestion; do not add a private code-signing certificate.
- MSIX must contain the complete `dotnet publish` output, including staged `native-terminal/<rid>/Microsoft.Terminal.Control.dll` files.
- Minimum Windows target is `10.0.19041.0` unless implementation verification proves the current app requires newer.

---

### Task 1: Store submission client and contract tests

**Files:**
- Create: `scripts/store-submission.mjs`
- Create: `tests/store-submission.test.mjs`

**Interfaces:**
- Produces: `storeVersionFromTag(tag: string): string`
- Produces: `selectStorePackages(fileNames: string[], version: string): string[]`
- Produces: `buildSubmissionUpdate(createdSubmission: object, packageNames: string[]): object`
- Produces: `runStoreSubmission(options): Promise<{ submissionId: string, status: string }>`
- Package filenames are exactly `DevBoard_<version>_x64.msix` and `DevBoard_<version>_arm64.msix`.

- [ ] **Step 1: Write failing tests for tag parsing and package selection**

Create `tests/store-submission.test.mjs` with Node's built-in test runner. Cover:

```js
import assert from 'node:assert/strict';
import test from 'node:test';
import {
  storeVersionFromTag,
  selectStorePackages,
  buildSubmissionUpdate,
  runStoreSubmission,
} from '../scripts/store-submission.mjs';

test('storeVersionFromTag accepts strict Store tags', () => {
  assert.equal(storeVersionFromTag('v1.2.3-store'), '1.2.3');
});

test('storeVersionFromTag rejects malformed tags', () => {
  assert.throws(() => storeVersionFromTag('v1.2-store'), /vX\.Y\.Z-store/);
  assert.throws(() => storeVersionFromTag('1.2.3-store'), /vX\.Y\.Z-store/);
});

test('selectStorePackages requires x64 and arm64 Dev Board packages', () => {
  assert.deepEqual(
    selectStorePackages(['DevBoard_1.2.3_x64.msix', 'DevBoard_1.2.3_arm64.msix'], '1.2.3'),
    ['DevBoard_1.2.3_x64.msix', 'DevBoard_1.2.3_arm64.msix'],
  );
  assert.throws(
    () => selectStorePackages(['DevBoard_1.2.3_x64.msix'], '1.2.3'),
    /arm64/,
  );
});
```

Also add tests that `buildSubmissionUpdate` marks existing packages `PendingDelete` and new Dev Board packages `PendingUpload`.

- [ ] **Step 2: Run tests to verify they fail**

Run:

```bash
node --test tests/store-submission.test.mjs
```

Expected: FAIL because `scripts/store-submission.mjs` does not exist yet.

- [ ] **Step 3: Implement the minimal submission helpers**

Create `scripts/store-submission.mjs` by adapting Quay's proven implementation. Keep the editable submission-field whitelist, failure/accepted status sets, token request, create submission, PUT submission update, Azure Blob upload, commit call, and status polling behavior. Change package expectations from `Quay_*` to `DevBoard_*`.

Use this strict tag parser:

```js
export function storeVersionFromTag(tag) {
  const match = /^v(\d+\.\d+\.\d+)-store$/.exec(tag ?? '');
  if (!match) {
    throw new Error(`Store tag '${tag ?? ''}' must match vX.Y.Z-store`);
  }
  return match[1];
}
```

Use this package contract:

```js
export function selectStorePackages(fileNames, version) {
  const expected = [
    `DevBoard_${version}_x64.msix`,
    `DevBoard_${version}_arm64.msix`,
  ];
  const files = new Set(fileNames);
  if (!expected.every((name) => files.has(name))) {
    throw new Error(`Expected Store MSIX artifacts: ${expected.join(', ')}`);
  }
  return expected;
}
```

The CLI entry point must require exactly:

```js
const requiredNames = [
  'STORE_TENANT_ID',
  'STORE_CLIENT_ID',
  'STORE_CLIENT_SECRET',
  'STORE_APPLICATION_ID',
  'STORE_TAG',
];
```

Only print sanitized success/failure information; never print token responses, authorization headers, client secret values, or upload SAS URLs.

- [ ] **Step 4: Add mocked Partner Center request-order tests**

Add a `fetchImpl` fake that records calls and returns deterministic responses for token, create, update, upload, commit, and status. Verify `runStoreSubmission`:

```js
assert.equal(result.submissionId, 'submission-123');
assert.equal(result.status, 'Certification');
assert.deepEqual(calls.map((call) => call.method), ['POST', 'POST', 'PUT', 'PUT', 'POST', 'GET']);
```

Add a failure-status case returning `CertificationFailed` and assert the function rejects with a message containing the status but no supplied `clientSecret` value.

- [ ] **Step 5: Run submission tests**

Run:

```bash
node --test tests/store-submission.test.mjs
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add scripts/store-submission.mjs tests/store-submission.test.mjs
git commit -m "feat: add Dev Board Store submission client"
```

---

### Task 2: Deterministic MSIX packager and Store manifest validation

**Files:**
- Create: `scripts/build-store-msix.ps1`
- Create: `tests/store-msix-script.test.mjs`

**Interfaces:**
- Consumes: full publish directory path, not one executable path.
- Produces: unsigned `.msix` at caller-supplied output path.
- Parameters:
  - `PackageName: string`
  - `Publisher: string`
  - `PublisherDisplayName: string`
  - `Version: X.Y.Z`
  - `Architecture: x64|arm64`
  - `PublishDir: directory`
  - `OutputPath: path`

- [ ] **Step 1: Write static contract tests for the packaging script**

Create `tests/store-msix-script.test.mjs` that reads `scripts/build-store-msix.ps1` as text and asserts the required safety contracts exist. The test should verify strings/regexes for:

```js
assert.match(script, /ValidateSet\('x64', 'arm64'\)/);
assert.match(script, /PublishDir/);
assert.match(script, /Dev Board/);
assert.match(script, /runFullTrust/);
assert.match(script, /10\.0\.19041\.0/);
assert.match(script, /Square44x44Logo\.png/);
assert.match(script, /Square150x150Logo\.png/);
assert.match(script, /StoreLogo\.png/);
assert.match(script, /makeappx\.exe/i);
```

Also assert that the script recursively copies publish contents rather than copying only `SourceGit.exe`.

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
node --test tests/store-msix-script.test.mjs
```

Expected: FAIL because the packager does not exist.

- [ ] **Step 3: Implement `scripts/build-store-msix.ps1`**

Start with:

```powershell
param(
  [Parameter(Mandatory = $true)][string]$PackageName,
  [Parameter(Mandatory = $true)][string]$Publisher,
  [Parameter(Mandatory = $true)][string]$PublisherDisplayName,
  [Parameter(Mandatory = $true)][string]$Version,
  [Parameter(Mandatory = $true)][ValidateSet('x64', 'arm64')][string]$Architecture,
  [Parameter(Mandatory = $true)][string]$PublishDir,
  [Parameter(Mandatory = $true)][string]$OutputPath
)
```

Validation rules:

```powershell
if (-not (Test-Path $PublishDir -PathType Container)) {
  throw "Dev Board publish directory not found: $PublishDir"
}

$exe = Join-Path $PublishDir 'SourceGit.exe'
if (-not (Test-Path $exe -PathType Leaf)) {
  throw "SourceGit.exe was not found in Dev Board publish directory: $PublishDir"
}

if ($Version -notmatch '^(\d+)\.(\d+)\.(\d+)$') {
  throw "Version '$Version' must be a numeric three-part version such as 1.2.3"
}
$msixVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"
```

Copy the entire publish directory into `store/msix-layout-$Architecture`:

```powershell
Copy-Item (Join-Path $PublishDir '*') $layout -Recurse -Force
```

Then create `Assets`, copy the three repository Store PNGs, and emit an `AppxManifest.xml` with:

```xml
<Properties>
  <DisplayName>Dev Board</DisplayName>
  <PublisherDisplayName>...</PublisherDisplayName>
  <Description>Git, worktrees, terminals, files, and AI agents in one development workspace.</Description>
  <Logo>Assets\StoreLogo.png</Logo>
</Properties>
```

Application settings:

```xml
<Application
  Id="DevBoard"
  Executable="SourceGit.exe"
  uap10:RuntimeBehavior="packagedClassicApp"
  uap10:TrustLevel="mediumIL">
```

Visual elements must use Dev Board and the two square assets. Add:

```xml
<rescap:Capability Name="runFullTrust" />
```

Find `makeappx.exe` under Windows Kits exactly as Quay does, preferring the runner's native SDK tool architecture, and propagate a non-zero exit code as a terminating error.

- [ ] **Step 4: Add explicit required-asset validation**

The script must check these repository paths before packaging:

```text
store/assets/Square44x44Logo.png
store/assets/Square150x150Logo.png
store/assets/StoreLogo.png
```

Each missing file must fail with `MSIX asset missing: <path>`.

- [ ] **Step 5: Run static packager contract tests**

Run:

```bash
node --test tests/store-msix-script.test.mjs
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add scripts/build-store-msix.ps1 tests/store-msix-script.test.mjs
git commit -m "feat: add Dev Board MSIX packager"
```

---

### Task 3: Dev Board Store assets

**Files:**
- Create: `store/assets/Square44x44Logo.png`
- Create: `store/assets/Square150x150Logo.png`
- Create: `store/assets/StoreLogo.png`
- Create: `scripts/validate-store-assets.ps1`
- Create: `tests/store-assets.test.mjs`

**Interfaces:**
- Consumes: Dev Board branding source already established in `docs/branding`.
- Produces: three PNG assets accepted by `build-store-msix.ps1`.

- [ ] **Step 1: Write failing asset-presence and PNG-dimension tests**

Create `tests/store-assets.test.mjs`. Use `node:fs` and parse the first 24 bytes of each PNG to read IHDR width/height as big-endian integers. Assert:

```text
Square44x44Logo.png      44 x 44
Square150x150Logo.png    150 x 150
StoreLogo.png            50 x 50
```

Also assert PNG signature bytes are `89 50 4E 47 0D 0A 1A 0A`.

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
node --test tests/store-assets.test.mjs
```

Expected: FAIL because Store assets do not yet exist.

- [ ] **Step 3: Create the PNG assets from the approved Dev Board icon**

Use the existing Dev Board icon concept: blue/cyan/purple D/workspace/terminal mark with orange/pink layered accents, transparent-safe background, no SourceGit wordmark. Generate/resample exact PNG dimensions above and commit only the derived PNG assets, not font files or temporary generation sources.

- [ ] **Step 4: Add PowerShell asset validation for CI/local use**

Create `scripts/validate-store-assets.ps1` that checks the three paths exist and have non-zero length. It should terminate with a distinct message naming the missing/empty asset.

- [ ] **Step 5: Run asset tests**

Run:

```bash
node --test tests/store-assets.test.mjs
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add store/assets scripts/validate-store-assets.ps1 tests/store-assets.test.mjs
git commit -m "feat: add Dev Board Store assets"
```

---

### Task 4: Store workflow with x64/ARM64 package verification

**Files:**
- Create: `.github/workflows/store-msix.yml`
- Create: `tests/store-workflow.test.mjs`

**Interfaces:**
- Consumes:
  - `scripts/build-store-msix.ps1`
  - `scripts/store-submission.mjs`
  - `scripts/validate-store-assets.ps1`
  - `VERSION`
- Produces artifacts:
  - `store-msix-x64` containing `DevBoard_<version>_x64.msix`
  - `store-msix-arm64` containing `DevBoard_<version>_arm64.msix`

- [ ] **Step 1: Write failing workflow contract tests**

Create `tests/store-workflow.test.mjs` and read the workflow as text. Assert it contains:

```text
workflow_dispatch
v*-store
win-x64
win-arm64
STORE_PACKAGE_NAME
STORE_PUBLISHER
STORE_PUBLISHER_DISPLAY_NAME
STORE_TENANT_ID
STORE_CLIENT_ID
STORE_CLIENT_SECRET
STORE_APPLICATION_ID
node --test tests/store-submission.test.mjs
node --test tests/store-msix-script.test.mjs
node --test tests/store-assets.test.mjs
```

Assert submission is guarded by both tag ref type and `-store` suffix.

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
node --test tests/store-workflow.test.mjs
```

Expected: FAIL because `.github/workflows/store-msix.yml` does not exist.

- [ ] **Step 3: Add workflow triggers and identity/version validation**

Create `.github/workflows/store-msix.yml` with:

```yaml
on:
  workflow_dispatch:
    inputs:
      package_name:
        required: false
        type: string
      publisher:
        required: false
        type: string
      publisher_display_name:
        required: false
        type: string
      version:
        required: false
        type: string
  push:
    tags:
      - "v*-store"
```

Identity values resolve from input first, then repository variables, then secrets. Fail early if any package identity value is blank.

Version resolution rules:

```powershell
if (tag) {
  require '^v(\d+\.\d+\.\d+)-store$'
  version = captured X.Y.Z
} elseif (input version exists) {
  require '^\d+\.\d+\.\d+$'
} else {
  projectVersion = (Get-Content VERSION -Raw).Trim()
  if (projectVersion -match '^(\d+)\.(\d+)$') { version = "$($Matches[1]).$($Matches[2]).0" }
  elseif (projectVersion -match '^\d+\.\d+\.\d+$') { version = projectVersion }
  else { fail }
}
```

- [ ] **Step 4: Add Store preflight tests job**

Use Ubuntu + Node 22 and run:

```bash
node --test tests/store-submission.test.mjs
node --test tests/store-msix-script.test.mjs
node --test tests/store-assets.test.mjs
node --test tests/store-workflow.test.mjs
```

- [ ] **Step 5: Add x64 and ARM64 Windows build matrix**

Use:

```yaml
strategy:
  fail-fast: false
  matrix:
    include:
      - os: windows-latest
        rid: win-x64
        arch: x64
      - os: windows-11-arm
        rid: win-arm64
        arch: arm64
```

For each matrix leg:

```powershell
dotnet restore src/SourceGit.csproj -r ${{ matrix.rid }}
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --configuration Release
dotnet publish src/SourceGit.csproj --configuration Release --runtime ${{ matrix.rid }} --output "store/publish/${{ matrix.rid }}"
./scripts/validate-store-assets.ps1
./scripts/build-store-msix.ps1 ... -PublishDir "store/publish/${{ matrix.rid }}" -OutputPath "store/msix/DevBoard_${version}_${{ matrix.arch }}.msix"
```

Do not pass `DisableAOT=true` for the Store package publish; exercise the same Release NativeAOT path used by the product.

- [ ] **Step 6: Add verification job requiring both architectures**

Download `store-msix-*` artifacts with `merge-multiple: true`. Verify exactly these two package names exist for the resolved version:

```text
DevBoard_<version>_x64.msix
DevBoard_<version>_arm64.msix
```

Fail if either is absent. Print only package filename and size.

- [ ] **Step 7: Add tag-only submission job**

Guard with:

```yaml
if: github.ref_type == 'tag' && endsWith(github.ref_name, '-store')
```

Set Partner Center env values and create `store-upload.zip` from the two MSIX files, then run:

```bash
node scripts/store-submission.mjs
```

- [ ] **Step 8: Run workflow contract tests**

Run:

```bash
node --test tests/store-workflow.test.mjs
```

Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add .github/workflows/store-msix.yml tests/store-workflow.test.mjs
git commit -m "ci: add Dev Board Microsoft Store workflow"
```

---

### Task 5: Store publishing documentation

**Files:**
- Create: `docs/store-publishing.md`
- Modify: `README.md`

**Interfaces:**
- Produces: operator instructions for Partner Center and GitHub configuration.

- [ ] **Step 1: Write `docs/store-publishing.md` with exact setup values**

Document these sections:

```text
Partner Center product reservation
Product identity values
GitHub repository variables
GitHub repository secrets
Manual Store build
Store release tag
Expected artifacts
Manual package validation
Troubleshooting
License attribution
```

State clearly:

```text
Variables/secrets:
STORE_PACKAGE_NAME
STORE_PUBLISHER
STORE_PUBLISHER_DISPLAY_NAME
STORE_TENANT_ID
STORE_CLIENT_ID
STORE_CLIENT_SECRET (secret only)
STORE_APPLICATION_ID
```

Manual build behavior: workflow builds and verifies, never submits.

Release example:

```bash
git tag v1.0.0-store
git push origin v1.0.0-store
```

Explain that Store packages are intentionally unsigned before Microsoft Store ingestion.

- [ ] **Step 2: Add a concise README pointer**

Add a Microsoft Store publishing link in the build/distribution documentation without changing the normal SourceGit release commands or implying Store publishing is required for ordinary releases.

- [ ] **Step 3: Verify no credentials or concrete Partner Center identity values were committed**

Run:

```bash
git grep -nE 'STORE_CLIENT_SECRET\s*[=:]\s*[^$<{ ]|Bearer [A-Za-z0-9._-]+' -- . ':!docs/superpowers'
```

Expected: no secret values.

- [ ] **Step 4: Commit**

```bash
git add docs/store-publishing.md README.md
git commit -m "docs: document Dev Board Store publishing"
```

---

### Task 6: End-to-end verification and PR preparation

**Files:**
- Verify all files created/modified by Tasks 1-5.

**Interfaces:**
- Produces: a reviewable PR whose live submission remains disabled except on Store tags.

- [ ] **Step 1: Run all Store-specific tests**

Run:

```bash
node --test tests/store-submission.test.mjs tests/store-msix-script.test.mjs tests/store-assets.test.mjs tests/store-workflow.test.mjs
```

Expected: all PASS.

- [ ] **Step 2: Run existing unit tests**

Run:

```bash
dotnet test tests/SourceGit.Tests/SourceGit.Tests.csproj --configuration Release
```

Expected: PASS.

- [ ] **Step 3: Run existing Release build**

Run:

```bash
dotnet build src/SourceGit.csproj --configuration Release -p:DisableAOT=true
```

Expected: 0 errors.

- [ ] **Step 4: Run Windows x64 Store publish/package locally or in Actions**

On a Windows runner/host with Windows SDK:

```powershell
dotnet publish src/SourceGit.csproj -c Release -r win-x64 -o store/publish/win-x64
./scripts/build-store-msix.ps1 -PackageName 'TEST.IDENTITY' -Publisher 'CN=TEST' -PublisherDisplayName 'Test Publisher' -Version '1.0.0' -Architecture x64 -PublishDir 'store/publish/win-x64' -OutputPath 'store/msix/DevBoard_1.0.0_x64.msix'
```

Expected: MSIX exists and `makeappx.exe` exits 0.

- [ ] **Step 5: Verify ARM64 through the GitHub Actions matrix**

The PR workflow must reach the ARM64 publish/package step on `windows-11-arm`. Treat the feature as incomplete if ARM64 is skipped or its MSIX is absent.

- [ ] **Step 6: Inspect MSIX package contents**

Use Windows SDK tooling or ZIP-compatible inspection to confirm:

```text
AppxManifest.xml
SourceGit.exe
Assets/Square44x44Logo.png
Assets/Square150x150Logo.png
Assets/StoreLogo.png
native-terminal/win-x64/Microsoft.Terminal.Control.dll (x64 package)
```

For ARM64, confirm `native-terminal/win-arm64/Microsoft.Terminal.Control.dll`.

- [ ] **Step 7: Confirm normal release workflows were not modified**

Run:

```bash
git diff master...HEAD -- .github/workflows/package.yml .github/workflows/release.yml
```

Expected: no diff.

- [ ] **Step 8: Prepare PR summary**

PR body must include:

```text
- isolated Store-only workflow
- x64 + ARM64 MSIX
- Dev Board Store-visible branding
- full publish-directory packaging
- manual builds do not submit
- vX.Y.Z-store tag is the only live submission trigger
- required Partner Center variables/secrets
- existing release pipeline unchanged
- test/build/package verification results
```

Do not claim live Partner Center submission was verified unless an actual Store tag with configured credentials was run successfully.
