# DevBoard Identity Migration Design

**Date:** 2026-08-29
**Status:** Proposed for implementation after review
**Repository:** `dhhieu113pro/dev-board`

## Goal

Make the fork consistently use **DevBoard** as its product, technical, packaging, repository-facing, and runtime identity while preserving required upstream SourceGit attribution and existing user data.

This migration replaces the current mixed state where the repository is `dev-board`, the visible product is sometimes `Dev Board`, and many technical identifiers still use `SourceGit`.

## Naming Policy

After this migration, the canonical project identity is:

- Product/display name: `DevBoard`
- Repository: `dhhieu113pro/dev-board`
- Solution: `DevBoard.slnx`
- Main project: `src/DevBoard.csproj`
- Main assembly/executable: `DevBoard` / `DevBoard.exe`
- Root namespace: `DevBoard`
- Store package filenames: `DevBoard_<version>_<arch>.msix`
- App data directory: `DevBoard`
- Linux desktop/appstream/package identifiers: DevBoard-specific identifiers
- macOS bundle metadata: DevBoard-specific identifiers

The string `SourceGit` remains only where it is intentionally historical or required for attribution, including:

- references to upstream `sourcegit-scm/sourcegit`
- MIT copyright/license attribution
- third-party/upstream credits
- one-time migration code that recognizes legacy SourceGit data/install locations
- compatibility notes describing migration from SourceGit

No user-facing current-product copy should call the application `SourceGit` after migration.

## Scope

### 1. .NET solution and project identity

Rename solution/project files and their references:

- `SourceGit.slnx` -> `DevBoard.slnx`
- `src/SourceGit.csproj` -> `src/DevBoard.csproj`
- test project references updated to point to `DevBoard.csproj`
- assembly name, root namespace, product name, description, application title, and executable output standardized to `DevBoard`

All C# namespaces currently under `SourceGit` will migrate to `DevBoard`, including models, view models, commands, converters, controls, views, services, and test namespaces.

XAML `x:Class`, CLR namespace declarations, compiled bindings, converters, design-time namespaces, and any reflection/type-name references must be updated in the same change so no stale `SourceGit.*` runtime type names remain.

### 2. Runtime application identity and user-data migration

New installs/runs use DevBoard data locations:

- Windows: `%APPDATA%\DevBoard`
- Linux: `~/.devboard`
- macOS: `~/Library/Application Support/DevBoard`

A one-time compatibility migration preserves existing user configuration.

Migration behavior:

1. Determine the new DevBoard data path.
2. If the new path already exists and is non-empty, use it and do not overwrite it.
3. If the new path does not exist but the legacy SourceGit path exists, migrate/copy the legacy directory to the DevBoard path before normal settings initialization.
4. Preserve the legacy directory rather than deleting it automatically. This gives users a rollback path and avoids destructive migration behavior.
5. Log migration success/failure without exposing secrets.
6. If migration fails, continue with the legacy path only for that run rather than losing configuration or preventing startup; retry migration on a later run when safe.

Portable `data` folders beside the executable remain supported and take precedence over both legacy and new roaming paths, matching current behavior.

### 3. Packaging and OS integration

All package/build assets will be renamed consistently.

Current repository packaging includes SourceGit-named resources such as:

- `build/resources/_common/applications/sourcegit.desktop`
- `build/resources/_common/icons/sourcegit.png`
- `build/resources/appimage/sourcegit.appdata.xml`
- Flatpak desktop metadata
- Debian package scripts/metadata
- macOS `App.plist`
- Store MSIX scripts/workflow

The migration will update package IDs, desktop filenames, executable commands, icons/assets references, install locations, bundle metadata, and package output filenames to DevBoard.

Where an ecosystem requires lowercase identifiers, use `devboard` rather than `sourcegit` (for example Linux desktop/appstream/package identifiers). Visible labels remain `DevBoard`.

### 4. Microsoft Store pipeline

The recently added Store pipeline is part of this rename and must no longer package `SourceGit.exe`.

Update:

- publish project path to `src/DevBoard.csproj`
- packager validation to require `DevBoard.exe`
- generated AppxManifest executable to `DevBoard.exe`
- Store description/display name to `DevBoard`
- tests to assert no current technical Store identity relies on SourceGit

Partner Center identity variables remain externally configurable because Microsoft assigns them. The workflow must not hard-code a guessed Store package identity.

Existing Store safety behavior remains unchanged:

- PR/manual build does not submit
- only `vX.Y.Z-store` tag flow may submit
- x64 + ARM64 packages remain mandatory

### 5. CI, scripts, tests, and automation

Update every workflow/script reference that targets the solution/project/executable/repository identity, including:

- build workflow
- CI/test workflow
- release/package workflows
- Store MSIX workflow
- screenshot automation where repo/executable paths are named
- build/publish scripts
- test project references
- path-based workflow triggers

CI must include a dedicated identity regression test that scans current-product files for forbidden stale identifiers.

The regression rule distinguishes legitimate historical references from accidental current identity usage. Allowed SourceGit references are limited to an explicit allowlist such as license/upstream attribution and legacy migration code. New production/package/current documentation references outside that allowlist fail CI.

### 6. README and documentation

README and docs will consistently use `DevBoard`:

- title/logo alt text/product copy
- badges for `dhhieu113pro/dev-board`
- clone command and `cd dev-board`
- build commands use `src/DevBoard.csproj`
- executable examples use `DevBoard`
- data-path documentation uses DevBoard paths
- Store publishing docs use the renamed project/executable
- screenshots/docs describing the current app use DevBoard

Upstream credit remains explicit:

> DevBoard is based on SourceGit (`sourcegit-scm/sourcegit`) and retains the upstream MIT license and attribution.

Historical SourceGit naming should not be presented as the current executable or repository identity after migration.

### 7. Branding assets

Existing DevBoard branding becomes canonical. SourceGit-named icon files used by packaging should be renamed to DevBoard/devboard equivalents and package manifests updated to those paths.

No new visual redesign is required in this migration; this is an identity consistency migration using the already approved DevBoard branding.

## Compatibility Constraints

This is intentionally a breaking technical rename with compatibility protection for user data.

We will **not** preserve `SourceGit.exe` as a second executable alias. Doing so would keep the mixed identity indefinitely and complicate package/update behavior.

We will preserve:

- legacy user data through migration
- portable `data` behavior
- upstream Git history and attribution
- repository fork relationship

Existing shortcuts pointing directly to `SourceGit.exe` may need recreation after installation/update. Package installers should create the new DevBoard shortcuts and desktop entries.

## Testing Strategy

### Identity tests

Add deterministic tests/scripts that verify:

- solution/project filenames are DevBoard
- project output is `DevBoard.exe` on Windows
- namespaces and XAML class names no longer use `SourceGit.*`
- current README/docs/build scripts do not reference `dhhieu113pro/sourcegit`
- current package metadata does not identify the product as SourceGit
- only allowlisted historical/migration references contain `SourceGit`

### Data migration tests

Cover at minimum:

1. no legacy data -> new DevBoard path initialized normally
2. legacy SourceGit data + no new data -> data copied/migrated to DevBoard
3. both legacy and new data exist -> new data wins; legacy is untouched
4. migration failure -> startup falls back safely without destroying legacy data
5. portable `data` directory -> migration is bypassed

### Build/package verification

Before merge, require:

- .NET unit tests green
- Release build green
- Windows x64 publish produces `DevBoard.exe`
- Windows ARM64 publish produces `DevBoard.exe`
- Store MSIX x64 and ARM64 build successfully and contain `DevBoard.exe`
- Linux package metadata/desktop files point to DevBoard
- macOS bundle metadata points to DevBoard
- normal release/package workflows remain functional after path renames

## Migration Order

Implementation should be performed in an order that keeps failures understandable:

1. Add identity/migration tests that describe the desired final state.
2. Rename solution/project and update build references.
3. Rename namespaces/XAML types and restore compilation.
4. Implement user-data migration and its tests.
5. Rename platform packaging resources and metadata.
6. Update Store/release/CI automation.
7. Update README/docs/current product copy.
8. Run stale-identity scan and full cross-platform/package verification.

The work belongs in one coordinated PR because intermediate states are intentionally inconsistent and should not land independently on `master`.

## Non-Goals

This migration does not:

- redesign DevBoard UI/branding
- remove upstream attribution
- rewrite Git history
- change application features or DevSpace behavior
- change the Store Partner Center account/identity values assigned externally
- delete legacy SourceGit user-data directories automatically

## Success Criteria

The migration is complete when a clean checkout of `dhhieu113pro/dev-board` builds and packages as **DevBoard** everywhere, the application runs with DevBoard namespaces/executable/data paths, existing SourceGit user settings are preserved through migration, and any remaining `SourceGit` text is demonstrably limited to upstream attribution or legacy compatibility code.