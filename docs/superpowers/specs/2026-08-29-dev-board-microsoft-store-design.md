# Dev Board Microsoft Store Publishing Design

Date: 2026-08-29
Status: Approved design

## Goal

Add a Microsoft Store publishing path for Dev Board by adapting the proven Store/MSIX automation already used in `dhhieu113pro/quay`, while preserving the existing SourceGit technical identity and normal release workflows until the later repository/package rename.

## Scope

This change adds a Windows Store-specific build, packaging, verification, and optional Partner Center submission flow. It must not rename namespaces, the project file, application-data paths, or other internal SourceGit identifiers.

The user-facing Store identity is Dev Board.

## Architecture

The Store path is isolated from the existing build/package/release workflows.

New components:

- `.github/workflows/store-msix.yml`
  - manual `workflow_dispatch` for building Store packages without submitting
  - tag trigger `vX.Y.Z-store` for build, verification, and Partner Center submission
  - Windows x64 and Windows ARM64 jobs
  - final verification job that requires both packages
  - submission job that runs only for Store tags
- `scripts/build-store-msix.ps1`
  - prepares an MSIX layout from the complete `dotnet publish` output
  - writes a Store-compatible `AppxManifest.xml`
  - copies Store assets
  - packages an unsigned MSIX with Windows SDK `makeappx.exe`
- Store submission client and tests, adapted from Quay
  - uploads the package archive through Partner Center APIs
  - validates required configuration and failure cases
- Store assets for the Dev Board identity
  - Store logo
  - 44x44 logo
  - 150x150 logo
- `docs/store-publishing.md`
  - Partner Center setup
  - required GitHub variables/secrets
  - manual build instructions
  - Store-tag release instructions

## Build Strategy

Use the existing Avalonia/.NET project as the source of truth:

```text
dotnet publish src/SourceGit.csproj -c Release -r win-x64

dotnet publish src/SourceGit.csproj -c Release -r win-arm64
```

The workflow should follow the project's current publish/AOT requirements rather than introducing a separate Windows application project.

The MSIX package must include the complete publish directory, not only the executable. This preserves any runtime/native files required by Avalonia, NativeAOT, Git integration, terminal support, or other current app dependencies.

The packaged executable may remain `SourceGit.exe` during this phase. Store-visible names are `Dev Board`.

## MSIX Manifest

The generated package manifest uses Partner Center values for the immutable identity fields:

- `Identity/Name`: `STORE_PACKAGE_NAME`
- `Identity/Publisher`: `STORE_PUBLISHER`
- `PublisherDisplayName`: `STORE_PUBLISHER_DISPLAY_NAME`

Visible application metadata:

- Display name: `Dev Board`
- Description: `Git, worktrees, terminals, files, and AI agents in one development workspace.`
- Windows Desktop target
- classic packaged desktop application runtime
- `runFullTrust` capability

Minimum supported Windows version should match the current practical Windows baseline for the app; the initial Store manifest will use Windows 10 2004 (`10.0.19041.0`) unless the current SourceGit packaging already requires newer Windows.

## Versioning

Store builds use a semantic three-part product version `X.Y.Z`, converted to MSIX version `X.Y.Z.0`.

Supported triggers:

- Manual workflow: explicit version input; if omitted, resolve from the existing project version metadata.
- Store release tag: `vX.Y.Z-store`.

Examples:

- `v1.0.0-store` -> `1.0.0.0`
- `v1.2.3-store` -> `1.2.3.0`

Invalid Store tag/version formats fail before packaging.

## Architectures

Build and package both:

- `x64` / `win-x64`
- `arm64` / `win-arm64`

The verification stage fails unless both MSIX files are present.

Initial scope does not add x86.

## Store Identity Configuration

Reuse the Quay naming convention so Store configuration is consistent across repositories.

Required package identity values:

- `STORE_PACKAGE_NAME`
- `STORE_PUBLISHER`
- `STORE_PUBLISHER_DISPLAY_NAME`

Required for automatic Partner Center submission:

- `STORE_TENANT_ID`
- `STORE_CLIENT_ID`
- `STORE_CLIENT_SECRET`
- `STORE_APPLICATION_ID`

Non-secret values may be repository variables or secrets. `STORE_CLIENT_SECRET` must be a secret.

Manual workflow inputs may override package identity and version for validation/testing without editing the workflow.

## Submission Flow

### Manual workflow dispatch

1. Validate Store identity.
2. Resolve version.
3. Run tests/build prerequisites required by the Store flow.
4. Publish x64 and ARM64 binaries.
5. Package unsigned MSIX files.
6. Upload artifacts.
7. Verify both packages exist.
8. Stop. Do not submit to Partner Center.

This allows safe package validation before Store credentials are configured.

### `vX.Y.Z-store` tag

Perform the same build and verification steps, then:

1. Download both MSIX artifacts.
2. Create the Partner Center upload archive.
3. Authenticate using the configured tenant/client credentials.
4. Create or update a Microsoft Store submission for `STORE_APPLICATION_ID`.
5. Upload the package archive.
6. Commit/finalize the submission through Partner Center.

Submission failures must fail the workflow and leave diagnostic output that identifies the failed Partner Center stage without printing secrets.

## Signing

The repository creates unsigned Store-targeted MSIX packages. Microsoft Store ingestion handles Store signing.

This flow does not add a private code-signing certificate or secret to GitHub.

## Assets

Store assets should use the new Dev Board icon concept already established for the rebrand.

Required repository PNGs:

- Square44x44Logo.png
- Square150x150Logo.png
- StoreLogo.png

The workflow/package script validates that every required asset exists before invoking `makeappx.exe`.

Asset generation is implementation work; no SourceGit logo should be presented as the Store identity after this feature lands.

## Existing Release Compatibility

The current SourceGit workflows remain unchanged unless a minimal shared adjustment is required for compilation.

Specifically, this feature does not change:

- `src/SourceGit.csproj` filename
- `SourceGit` namespaces
- current app-data locations
- normal GitHub release artifacts
- macOS/Linux packaging
- Homebrew integration
- upstream SourceGit links that are deliberately retained during the staged rebrand

The Store workflow is additive and can be removed independently without affecting ordinary builds.

## Testing and Verification

Implementation must include tests or deterministic validation for the Store-specific logic, following the pattern used by Quay.

Minimum verification:

- Store submission client tests pass.
- Store version conversion rejects malformed versions.
- Missing identity values fail early with actionable messages.
- MSIX packaging script validates executable/publish directory and assets.
- Windows x64 Store package builds successfully.
- Windows ARM64 Store package builds successfully.
- Verification job detects missing architecture artifacts.
- Existing unit tests continue to pass.
- Existing Release build continues to pass.

Automatic Partner Center submission is not required to run in a pull request because credentials and an actual Store application are external resources. The PR must prove the submission client behavior through tests and keep the live submission job tag-gated.

## Error Handling

Failures should be explicit and early:

- missing Partner Center identity -> fail before build/package
- malformed version -> fail before build/package
- missing Store asset -> fail before `makeappx`
- missing publish output -> fail before `makeappx`
- `makeappx` failure -> propagate exit code
- missing x64/ARM64 output -> fail verification stage
- Partner Center authentication/upload/commit failure -> fail submission stage with sanitized diagnostics

No secret values may be echoed to workflow logs.

## Security

- Store submission credentials exist only in GitHub repository secrets/variables.
- `STORE_CLIENT_SECRET` is never accepted from source-controlled files.
- The package build itself does not require Store submission credentials.
- Manual Store builds never submit automatically.
- Live submission only occurs from an explicitly named `vX.Y.Z-store` tag.

## Documentation

`docs/store-publishing.md` will document:

- reserving Dev Board in Partner Center
- where to copy Package/Identity/Name and Publisher
- how to configure the GitHub variables/secrets
- running a manual MSIX build
- downloading and manually testing artifacts
- publishing with a `vX.Y.Z-store` tag
- expected x64 and ARM64 outputs
- troubleshooting common identity/version/submission failures

## Out of Scope

- repository rename from `sourcegit`
- namespace/project rename from `SourceGit`
- executable rename if doing so creates compatibility risk
- changing app-data directories
- x86 packages
- macOS App Store publishing
- Linux store/package publishing
- private signing certificates
- modifying the existing normal release process to depend on Microsoft Store publishing

## Success Criteria

The feature is complete when a developer can manually dispatch the Store workflow and receive valid Dev Board x64 and ARM64 MSIX artifacts, and when a `vX.Y.Z-store` tag can use configured Partner Center credentials to submit those packages automatically, without altering the normal SourceGit/Dev Board release pipeline.