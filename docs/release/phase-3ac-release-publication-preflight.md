# Lens Docs Studio v0.1.0-dev.1 Phase 3AC Publication Preflight

## Context

- Phase: `3AC`
- Purpose: release publication preflight and operator handoff for `v0.1.0-dev.1`
- Repository: `DouglasNLima/local-docs-studio`
- Branch checked: `develop`
- Preflight recorded: `2026-06-11 17:42:47 +01:00`
- Starting commit: `73e5ab1f3f9f59cab2478a29f43176f43d39fbf4`
- Phase 3AB source commit used for fresh artefacts: `055e51dedacd28ea277a480b60bd0333feec6004`
- Phase 3W artefacts from `e2026d728c131cf2e203928487b9aeb09b744da7` are superseded and stale.

This is an evidence and handoff checkpoint only. No GitHub release assets, tags, releases, or `main` merges were changed. No publication was performed. Production readiness and go-live approval are not claimed.

## Repository State Before Changes

- Branch: `develop`
- `develop` contains `73e5ab1f3f9f59cab2478a29f43176f43d39fbf4`.
- `git status --short --branch` before edits:

```text
## develop...origin/develop
```

- Tracked changes before edits: none.
- Ignored/generated paths observed separately: `artifacts/`, `node_modules/`, `src/windows/.vs/`, Windows `bin/` and `obj/` outputs, and `test-results/`.
- A long-path warning was observed while enumerating ignored paths under generated WebView2 cache output. This was not a tracked source change.

## Local Release-Candidate Artefacts

The following local files were present during preflight:

| File | Size |
| --- | ---: |
| `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev.zip` | `33966582` bytes |
| `artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | `29469676` bytes |
| `artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | `110` bytes |
| `artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | `158183` bytes |
| `artifacts/windows/release-candidates/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T162341Z.md` | `8430` bytes |
| `artifacts/windows/release-candidates/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T162341Z.json` | `4939` bytes |

Recomputed SHA256 values:

| Artefact | Path | SHA256 | Result |
| --- | --- | --- | --- |
| ZIP | `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev.zip` | `DF29869E85AADC3C79525D65CC9DC224B7B23FCF67F9C1CF423D31923A328B03` | Match |
| Installer | `artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | `29A4A985DABB8D7991E27C6F0E611A42EC7B31CA5B122B361ACA2B16B9893EDE` | Match |

Release-candidate metadata:

- `artifacts/windows/release-candidates/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T162341Z.md`
- `artifacts/windows/release-candidates/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T162341Z.json`

These are local release-candidate artefacts, not tracked publication assets.

Validation note: `Test-WindowsPackageReleaseCandidate.ps1` rebuilds the ignored local Windows package as part of its check. Before that validation command ran, the ZIP at `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev.zip` matched the Phase 3AB hash above. After the validation rebuild, the same ignored ZIP path had SHA256 `A8F6EAB0087B498AEEB70D9787B10DB43DF8C3B2AB54DA5AD41798B7927776CF`; this validation-generated package was not staged and was not published. The installer hash remained `29A4A985DABB8D7991E27C6F0E611A42EC7B31CA5B122B361ACA2B16B9893EDE`.

## Read-Only GitHub Release State

Read-only commands used:

```powershell
git ls-remote --tags origin refs/tags/v0.1.0-dev.1
gh release view v0.1.0-dev.1 --json tagName,name,isDraft,isPrerelease,url,createdAt,publishedAt,targetCommitish,assets
```

Observed state for `v0.1.0-dev.1`:

- Tag exists: no.
- Release exists: no.
- Draft status: not applicable because the release does not exist.
- Prerelease status: not applicable because the release does not exist.
- Attached asset names: none.
- Attached asset sizes or hashes: none available.
- Asset alignment relative to Phase 3AB artefacts: missing. There are no `v0.1.0-dev.1` published assets to classify as aligned or stale.

Context-only observation for the existing `v0.1.0-dev` prerelease:

- Release exists: yes.
- Draft: no.
- Prerelease: yes.
- Target commit: `8b215d039188af94d32857239e6829916f4b74fc`.
- Published assets include the earlier ZIP, installer, checksum files, and reports.
- The existing `v0.1.0-dev` ZIP digest is `sha256:8cd380c42c33aab8fe02c82184ff0a6ffde6fc0c6b077be40e0cf6a51c7e6188`, which does not match the Phase 3AB local ZIP hash.
- The existing `v0.1.0-dev` installer digest is `sha256:152051f7cdab5a33f8d8e5c219f937a54777687b8980d750b486be572883fa8e`, which does not match the Phase 3AB local installer hash.
- Those `v0.1.0-dev` assets belong to a different published prerelease and were not changed.

## Publication Decision State

- `v0.1.0-dev.1` release assets are missing because no `v0.1.0-dev.1` release exists.
- Existing `v0.1.0-dev` assets are stale relative to the Phase 3AB local candidate artefacts, but they are assets for the older `v0.1.0-dev` prerelease and should not be replaced as part of this preflight.
- Publication is not authorised in Phase 3AC.
- No GitHub release assets, tags, releases, or `main` merges were created, updated, uploaded, deleted, replaced, or otherwise changed.

## Future Publication Actions If Authorised

If a later phase explicitly authorises publication, an operator would need to:

1. Verify the selected `develop` commit and confirm it contains Phase 3AB or later approved evidence.
2. Verify `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev.zip` has SHA256 `DF29869E85AADC3C79525D65CC9DC224B7B23FCF67F9C1CF423D31923A328B03`.
3. Verify `artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe` has SHA256 `29A4A985DABB8D7991E27C6F0E611A42EC7B31CA5B122B361ACA2B16B9893EDE`.
4. Verify the release-candidate metadata files:
   - `artifacts/windows/release-candidates/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T162341Z.md`
   - `artifacts/windows/release-candidates/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T162341Z.json`
5. Decide whether to create or update `v0.1.0-dev.1`.
6. Upload or replace the ZIP only if explicitly authorised.
7. Upload or replace the installer only if explicitly authorised.
8. Upload or replace checksum and report assets only if explicitly authorised.
9. Mark `v0.1.0-dev.1` as a prerelease, not a stable/latest release.
10. Avoid production-readiness and go-live wording.
11. Verify the final release page, tag target, prerelease state, asset names, asset sizes, and published asset hashes after publication.

## Operator Handoff Checklist

- [ ] Verify the `develop` commit selected for publication.
- [ ] Verify the ZIP SHA256.
- [ ] Verify the installer SHA256.
- [ ] Verify release-candidate metadata.
- [ ] Decide whether to create or update `v0.1.0-dev.1`.
- [ ] Upload or replace the ZIP only if explicitly authorised.
- [ ] Upload or replace the installer only if explicitly authorised.
- [ ] Upload or replace checksums and reports only if explicitly authorised.
- [ ] Mark the release as a prerelease.
- [ ] Keep wording clear that this is not stable/latest.
- [ ] Avoid production-readiness and go-live claims.
- [ ] Confirm no unrelated published release, tag, or asset was changed.

## Validation

Validation for this Phase 3AC evidence update:

- [x] `npm run test:static` - PASS. Static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans.
- [x] `npm run test:browser` - PASS on retry with a longer command timeout. The first run timed out after about 604 seconds with no returned test output. The retry completed 222 Playwright tests across Chromium and Edge.
- [x] `dotnet build src/windows/LensDocsStudio.Windows.sln` - PASS. A first run before the evidence filename was shortened passed with warnings, including one warning caused by the initial dotted `v0.1.0-dev.1` filename. The evidence file was renamed to avoid adding that Windows resource qualifier warning, and the final direct build passed with 0 warnings and 0 errors.
- [x] `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` - PASS. Verified 53 service-worker assets and 150 vendor assets in packaged `StaticApp`; only pre-existing versioned release-doc PRI warnings were observed during its internal build.
- [x] `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` - PASS. Generated ignored validation artefacts at `artifacts/windows/release-candidates/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T165739Z.md` and `.json`; rebuilt ignored ZIP SHA256 `A8F6EAB0087B498AEEB70D9787B10DB43DF8C3B2AB54DA5AD41798B7927776CF`.
- [x] `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` - PASS. Windows native bridge smoke completed successfully.

## Explicit Non-Actions

- No publication was performed.
- No release assets were uploaded, deleted, or replaced.
- No tags were created, moved, or deleted.
- No GitHub releases were created, edited, published, or deleted.
- No merge to `main` was performed.
- No generated artefacts were staged.
- No production readiness or go-live approval is claimed.
