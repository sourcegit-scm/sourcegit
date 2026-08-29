# Microsoft Store publishing

DevBoard has an isolated Microsoft Store pipeline in `.github/workflows/store-msix.yml`. It builds Store-targeted MSIX packages for Windows x64 and ARM64 without changing the normal DevBoard release workflows.

## Partner Center product reservation

Reserve **DevBoard** in Microsoft Partner Center before configuring the repository. After the product exists, open its product identity page and copy the exact identity values shown by Partner Center.

## Product identity values

Configure these values in GitHub repository variables when possible (repository secrets are also accepted by the workflow):

- `STORE_PACKAGE_NAME` — Partner Center `Package/Identity/Name`
- `STORE_PUBLISHER` — Partner Center `Package/Identity/Publisher`
- `STORE_PUBLISHER_DISPLAY_NAME` — publisher display name used in the package manifest

Do not invent or normalize these values. They must match Partner Center exactly for Store ingestion.

## Partner Center submission credentials

Automatic Store submission additionally uses:

- `STORE_TENANT_ID`
- `STORE_CLIENT_ID`
- `STORE_APPLICATION_ID`
- `STORE_CLIENT_SECRET` — **GitHub Secret only**

The client secret must never be stored in the repository, workflow inputs, documentation examples, or repository variables.

## Manual Store build

Run the **Store MSIX** workflow with `workflow_dispatch` to validate packaging before enabling automatic submission. Optional inputs can override the package identity and Store version for a manual test.

A manual run performs these steps:

1. Validates Store identity and resolves a three-part version.
2. Runs Store contract tests.
3. Publishes DevBoard for `win-x64` and `win-arm64` using the Release NativeAOT path.
4. Packages the complete publish directories as unsigned MSIX files.
5. Verifies that both architecture packages exist.
6. Uploads the packages as workflow artifacts.

A manual workflow run **never submits to Partner Center**.

If the `version` input is omitted, the current repository `VERSION` is used. A two-part version such as `2026.18` is normalized to `2026.18.0` for Store packaging.

## Store release tag

Live Partner Center submission is enabled only for tags matching `vX.Y.Z-store`.

```bash
git tag v1.0.0-store
git push origin v1.0.0-store
```

The tag above creates MSIX version `1.0.0.0`. A malformed tag fails before packaging.

After both architecture packages pass verification, the workflow creates `store-upload.zip` and submits it with `scripts/store-submission.mjs` using the configured Partner Center credentials.

## Expected artifacts

For Store version `1.0.0`, the workflow produces exactly:

```text
DevBoard_1.0.0_x64.msix
DevBoard_1.0.0_arm64.msix
```

Each package contains the complete `dotnet publish` output, including the architecture-specific native terminal component staged by the existing project publish targets.

## Signing

Store packages produced by this repository are intentionally **unsigned**. Microsoft Store ingestion applies Store signing. This workflow does not require or store a private code-signing certificate.

## Manual package validation

A package can be produced on a Windows machine with the Windows SDK installed:

```powershell
dotnet publish src/DevBoard.csproj -c Release -r win-x64 -o store/publish/win-x64
./scripts/validate-store-assets.ps1
./scripts/build-store-msix.ps1 `
  -PackageName 'TEST.IDENTITY' `
  -Publisher 'CN=TEST' `
  -PublisherDisplayName 'Test Publisher' `
  -Version '1.0.0' `
  -Architecture x64 `
  -PublishDir 'store/publish/win-x64' `
  -OutputPath 'store/msix/DevBoard_1.0.0_x64.msix'
```

Use real Partner Center identity values for a package intended for Store ingestion.

## Troubleshooting

- **Missing Store identity** — copy the exact package name/publisher values from Partner Center or provide manual workflow inputs.
- **Invalid version** — manual Store versions must be `X.Y.Z`; Store tags must be `vX.Y.Z-store`.
- **Missing asset** — confirm all three PNGs under `store/assets/` exist and run `scripts/validate-store-assets.ps1`.
- **`makeappx.exe` not found** — install a Windows SDK that includes MakeAppx.
- **Only one architecture produced** — the verification job intentionally fails unless both x64 and ARM64 artifacts are present.
- **Partner Center authentication/upload/commit failure** — verify the Entra application credentials and application ID; the workflow reports the failed stage without printing secret values.

## License attribution

DevBoard is based on [SourceGit](https://github.com/sourcegit-scm/sourcegit) and remains distributed under the MIT License. Keep the upstream license/copyright notice and the repository's third-party license notices in distributed copies.
