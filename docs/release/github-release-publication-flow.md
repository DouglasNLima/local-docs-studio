# GitHub Release ZIP Publication Flow

Phase 3G prepares a repeatable GitHub Release publication flow for the certified Windows folder/ZIP package. It operationalises the Phase 3F decision to use GitHub Releases for ZIP artefacts while MSIX, classic installers, signing, prerequisite bootstrapping, and auto-update remain deferred.

The default path is safe: it creates local release artefacts and prints the GitHub CLI command, but it does not create a GitHub release and does not upload files.

## Prepare A Dry Run

Run from the repository root on `develop` with a clean worktree:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Prepare-WindowsGitHubRelease.ps1 -DryRun
```

By default the script:

1. Confirms the current branch is `develop`.
2. Confirms the worktree is clean.
3. Resolves `-TargetCommit` and requires it to match the checked-out `HEAD`.
4. Runs `scripts/windows/Test-WindowsPackageReleaseCandidate.ps1`, which builds the Windows folder/ZIP package, validates packaged static assets, runs the packaged native bridge smoke harness, computes package metadata, and writes the RC report.
5. Copies the certified ZIP into `artifacts/releases/<tag>/`.
6. Writes `LensDocsStudio.Windows-<version>.zip.sha256`.
7. Generates release notes from `docs/release/templates/github-release-notes.md`.
8. Writes and prints the exact `gh release create` command.

The default values are:

```powershell
-Version 0.1.0-dev
-Tag v0.1.0-dev
-TargetCommit HEAD
-Configuration Release
-Draft
-Prerelease
```

The generated artefacts are ignored source outputs and must not be committed.

## Artefact Set

The default dry run prepares:

```text
artifacts/releases/v0.1.0-dev/
  LensDocsStudio.Windows-0.1.0-dev.zip
  LensDocsStudio.Windows-0.1.0-dev.zip.sha256
  LensDocsStudio.Windows-0.1.0-dev-rc-report.md
  LensDocsStudio.Windows-0.1.0-dev-release-notes.md
  LensDocsStudio.Windows-0.1.0-dev-gh-release-command.ps1.txt
```

The source package build and timestamped RC metadata may also exist under `artifacts/windows/`.

## Reusing An Existing Certified Package

Use `-NoBuild` only when a successful RC metadata JSON already exists for the requested version and target commit:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Prepare-WindowsGitHubRelease.ps1 -NoBuild -DryRun
```

The script refuses to reuse a package when the metadata version, commit SHA, validation result, or ZIP path does not match. This keeps stale ZIPs from being republished accidentally.

## Publish Behaviour

`-Publish` is required before the script runs `gh release create`:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Prepare-WindowsGitHubRelease.ps1 -Publish
```

Publishing requires the GitHub CLI to be installed, authenticated, and authorised for `DouglasNLima/local-docs-studio`. The script passes `--draft` and `--prerelease` by default. If the tag does not already exist, `gh release create --target <commit>` may create the release tag on GitHub as part of release creation. The script does not create or move local tags.

Supplying both `-Publish` and `-DryRun` keeps the run dry.

## Generated GitHub CLI Command

Dry-run output prints a command in this form:

```powershell
gh release create v0.1.0-dev `
  artifacts/releases/v0.1.0-dev/LensDocsStudio.Windows-0.1.0-dev.zip `
  artifacts/releases/v0.1.0-dev/LensDocsStudio.Windows-0.1.0-dev.zip.sha256 `
  artifacts/releases/v0.1.0-dev/LensDocsStudio.Windows-0.1.0-dev-rc-report.md `
  --repo DouglasNLima/local-docs-studio `
  --target <commit-sha> `
  --title "Lens Docs Studio v0.1.0-dev" `
  --notes-file artifacts/releases/v0.1.0-dev/LensDocsStudio.Windows-0.1.0-dev-release-notes.md `
  --draft `
  --prerelease
```

Do not run the command manually unless the release notes, checksum, and RC report have been reviewed.

## Checksum Validation

The generated `.sha256` file contains:

```text
<SHA256>  LensDocsStudio.Windows-<version>.zip
```

Release notes include a PowerShell verification command:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath "LensDocsStudio.Windows-<version>.zip"
```

The checksum verifies ZIP integrity only. It is not a substitute for code signing.

## Tag Strategy

Recommended tag shapes:

- Internal RC tags: `v0.1.0-rc.1`
- Development prerelease tags: `v0.1.0-dev`
- Stable release tags: `v0.1.0`

Phase 3G defaults to the dry-run prerelease tag `v0.1.0-dev`. It does not merge to `main`, does not create local tags by default, and does not move existing tags.

## Non-Claims

This flow does not add:

- Code signing.
- MSIX.
- A classic installer.
- Auto-update.
- WebView2 bootstrapper or Fixed Version Runtime bundling.
- Machine-wide file associations.
- CI/CD release automation.
- Automatic upload unless `-Publish` is supplied.
- Automatic merge to `main`.

## Validation

For Phase 3G script and documentation changes, run:

```powershell
npm run test:static
dotnet build src/windows/LensDocsStudio.Windows.sln
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1
pwsh -NoLogo -NoProfile -File scripts/windows/Prepare-WindowsGitHubRelease.ps1 -DryRun
```

If runtime, project, UI, import/export, clipboard, or browser workflow files change, also run:

```powershell
npm run test:browser
pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1
```
