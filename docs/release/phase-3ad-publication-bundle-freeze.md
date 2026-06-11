# Lens Docs Studio v0.1.0-dev.1 Phase 3AD Publication Bundle Freeze

## Context

- Phase: `3AD`
- Purpose: freeze one authoritative local publication bundle for `v0.1.0-dev.1`.
- Repository: `DouglasNLima/local-docs-studio`
- Branch checked: `develop`
- Freeze recorded: `2026-06-11 18:47:28 +01:00`
- Source commit used for frozen artefacts: `c776fffdc8364c2c978f8106869a2d6bf50477ff`
- Previous Phase 3AC commit: `c776fffdc8364c2c978f8106869a2d6bf50477ff`

This is a local artefact freeze and handoff evidence checkpoint only. No publication was performed. No GitHub release assets, tags, releases, or `main` merges were created, updated, uploaded, deleted, replaced, or otherwise changed. Production readiness and go-live approval are not claimed.

## Repository State Before Changes

- Branch: `develop`
- `develop` contained `c776fffdc8364c2c978f8106869a2d6bf50477ff`.
- `git status --short --branch` before edits:

```text
## develop...origin/develop
```

- Tracked changes before edits: none.
- Ignored/generated paths observed separately: `artifacts/`, `node_modules/`, `src/windows/.vs/`, Windows `bin/` and `obj/` outputs, and `test-results/`.
- A long-path warning was observed while enumerating ignored paths under generated WebView2 cache output. This was not a tracked source change.

## Final Frozen Publication Bundle

Frozen local bundle path:

- `artifacts/windows/publication-bundles/v0.1.0-dev.1/`

Frozen files:

| Purpose | Frozen path | SHA256 |
| --- | --- | --- |
| ZIP package | `artifacts/windows/publication-bundles/v0.1.0-dev.1/LensDocsStudio.Windows-0.1.0-dev.zip` | `3C9C343B8ABB06655B6A2DD55CACEEFF2542ED64BCF4FF943AFC7A5957C79A7A` |
| Inno installer | `artifacts/windows/publication-bundles/v0.1.0-dev.1/LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | `4C2A79C7DF446957DE6F6D2E41271D0C7563178BB47ECACC09E001752C930745` |

Checksum, report, and metadata files:

- Installer checksum: `artifacts/windows/publication-bundles/v0.1.0-dev.1/LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256`
- Installer report: `artifacts/windows/publication-bundles/v0.1.0-dev.1/LensDocsStudio.Windows-0.1.0-dev-Setup-report.md`
- Release-candidate report: `artifacts/windows/publication-bundles/v0.1.0-dev.1/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T174119Z.md`
- Release-candidate metadata: `artifacts/windows/publication-bundles/v0.1.0-dev.1/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T174119Z.json`

The frozen installer checksum file contains:

```text
4C2A79C7DF446957DE6F6D2E41271D0C7563178BB47ECACC09E001752C930745  LensDocsStudio.Windows-0.1.0-dev-Setup.exe
```

## Build Commands Used For Freeze

Commands run before freezing:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1 -NoPackageBuild
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1 -NoPackageBuild
```

The release-candidate metadata command rebuilt the ignored working ZIP and produced the final release-candidate metadata. The installer was rebuilt afterwards from that package output so the frozen ZIP, installer, checksum, installer report, and release-candidate metadata form one self-consistent local bundle.

## Frozen File Verification

Frozen file verification was performed from the copied files under `artifacts/windows/publication-bundles/v0.1.0-dev.1/`.

| Check | Result |
| --- | --- |
| Frozen ZIP SHA256 recomputed as `3C9C343B8ABB06655B6A2DD55CACEEFF2542ED64BCF4FF943AFC7A5957C79A7A` | PASS |
| Frozen installer SHA256 recomputed as `4C2A79C7DF446957DE6F6D2E41271D0C7563178BB47ECACC09E001752C930745` | PASS |
| Frozen installer `.sha256` file matches the installer hash | PASS |
| Frozen release-candidate JSON references source commit `c776fffdc8364c2c978f8106869a2d6bf50477ff` | PASS |
| Frozen release-candidate JSON references ZIP hash `3C9C343B8ABB06655B6A2DD55CACEEFF2542ED64BCF4FF943AFC7A5957C79A7A` | PASS |
| Frozen installer report references source commit `c776fffdc8364c2c978f8106869a2d6bf50477ff` | PASS |
| Frozen installer report references installer hash `4C2A79C7DF446957DE6F6D2E41271D0C7563178BB47ECACC09E001752C930745` | PASS |

## Read-Only GitHub Release State

Read-only commands used:

```powershell
git ls-remote --tags origin refs/tags/v0.1.0-dev.1
gh release view v0.1.0-dev.1 --json tagName,name,isDraft,isPrerelease,url,createdAt,publishedAt,targetCommitish,assets
gh release view v0.1.0-dev --json tagName,name,isDraft,isPrerelease,url,createdAt,publishedAt,targetCommitish,assets
```

Observed state for `v0.1.0-dev.1`:

- Tag exists: no.
- Release exists: no.
- Draft status: not applicable because the release does not exist.
- Prerelease status: not applicable because the release does not exist.
- Attached asset names: none.
- Attached asset sizes or hashes: none available.
- If later authorised, publication would be **create-new**, not update-existing.

Context-only observation for the existing `v0.1.0-dev` prerelease:

- Release exists: yes.
- Draft: no.
- Prerelease: yes.
- Target commit: `8b215d039188af94d32857239e6829916f4b74fc`.
- Published assets include the earlier ZIP, installer, checksum files, and reports.
- The existing `v0.1.0-dev` ZIP digest is `sha256:8cd380c42c33aab8fe02c82184ff0a6ffde6fc0c6b077be40e0cf6a51c7e6188`, which does not match the Phase 3AD frozen ZIP hash.
- The existing `v0.1.0-dev` installer digest is `sha256:152051f7cdab5a33f8d8e5c219f937a54777687b8980d750b486be572883fa8e`, which does not match the Phase 3AD frozen installer hash.
- Those `v0.1.0-dev` assets belong to a different published prerelease and were not changed.

## Future Publication Actions If Authorised

If a later phase explicitly authorises publication, an operator would need to:

1. Verify the selected source commit is `c776fffdc8364c2c978f8106869a2d6bf50477ff` or intentionally superseded by a later approved commit.
2. Verify the frozen bundle exists at `artifacts/windows/publication-bundles/v0.1.0-dev.1/`.
3. Verify the frozen ZIP SHA256 is `3C9C343B8ABB06655B6A2DD55CACEEFF2542ED64BCF4FF943AFC7A5957C79A7A`.
4. Verify the frozen installer SHA256 is `4C2A79C7DF446957DE6F6D2E41271D0C7563178BB47ECACC09E001752C930745`.
5. Verify the installer `.sha256`, installer report, release-candidate report, and release-candidate JSON from the frozen bundle.
6. Create a new `v0.1.0-dev.1` prerelease only if publication is explicitly authorised.
7. Upload the frozen ZIP, installer, checksum, installer report, and release-candidate metadata only if explicitly authorised.
8. Mark `v0.1.0-dev.1` as a prerelease, not a stable/latest release.
9. Avoid production-readiness and go-live wording.
10. Verify the final release page, tag target, prerelease state, asset names, asset sizes, and published asset hashes after publication.

## Validation

Validation for this Phase 3AD evidence update:

- [x] `npm run test:static` - PASS. Static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans.
- [x] `npm run test:browser` - PASS. Completed 222 Playwright tests across Chromium and Edge.
- [x] `dotnet build src/windows/LensDocsStudio.Windows.sln` - PASS. Build completed with 0 errors and 5 known pre-existing PRI qualifier warnings for older dotted release-document filenames.
- [x] `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` - PASS. Verified 53 service-worker assets and 150 vendor assets in packaged `StaticApp`.
- [x] `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` - PASS. Generated ignored validation artefacts at `artifacts/windows/release-candidates/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T180204Z.md` and `.json`; rebuilt the ignored working ZIP at `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev.zip` with SHA256 `1816DDB346FB01D568B1B30336ECB9E6E023B368F7BF38E3B224D26B3D4288B3`.
- [x] `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` - PASS. Windows native bridge smoke completed successfully.

Post-validation frozen-bundle check:

- Frozen ZIP remained `3C9C343B8ABB06655B6A2DD55CACEEFF2542ED64BCF4FF943AFC7A5957C79A7A`.
- Frozen installer remained `4C2A79C7DF446957DE6F6D2E41271D0C7563178BB47ECACC09E001752C930745`.
- The validation-generated working ZIP hash is not the authoritative Phase 3AD frozen ZIP hash.

## Explicit Non-Actions

- No publication was performed.
- No release assets were uploaded, deleted, or replaced.
- No tags were created, moved, or deleted.
- No GitHub releases were created, edited, published, or deleted.
- No merge to `main` was performed.
- No generated artefacts were staged.
- No production readiness or go-live approval is claimed.
