# Lens Docs Studio v0.1.0-dev.3 Publication Preflight

Phase: 3AY

Date: 2026-06-12

## Summary

Phase 3AY is a no-publication frozen bundle audit and publication preflight for the local `v0.1.0-dev.3` candidate. It verifies the Phase 3AX frozen bundle contents, hashes, metadata, and read-only GitHub tag/release state before any possible later publication approval.

No publication was performed. No release assets, tags, GitHub Releases, or `main` merges were created, edited, deleted, replaced, re-uploaded, rebuilt, republished, or changed. Existing `v0.1.0-dev.1` and `v0.1.0-dev.2` release assets were not changed. Production readiness, go-live approval, stable-channel certification, and stable/latest release status are not claimed.

## Repository State

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Starting HEAD | `4175b720c24827a7af9c4a869c8f7acfb3bee237` |
| Phase 3AX commit in history | PASS, `4175b720c24827a7af9c4a869c8f7acfb3bee237` is an ancestor of `HEAD` |
| Tracked status before Phase 3AY edits | Clean, `## develop...origin/develop` |
| Ignored/generated paths present | `artifacts/`, `node_modules/`, `src/windows/.vs/`, `src/windows/LensDocsStudio.Windows/bin/`, `src/windows/LensDocsStudio.Windows/obj/`, `test-results/` |

`git status --short --ignored` reported existing Windows long-path warnings while scanning ignored WebView2 cache folders under generated Windows build output. The tracked working tree was clean before the Phase 3AY documentation edits.

## Frozen Bundle

Frozen bundle path:

```text
C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3
```

The frozen bundle contains the expected publishable files:

| Artefact | Frozen path | Verification |
| --- | --- | --- |
| Windows ZIP package | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3\LensDocsStudio.Windows-0.1.0-dev.zip` | Present, 34054203 bytes |
| Unsigned Inno Setup installer | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3\LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | Present, 29689155 bytes |
| Installer checksum | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3\LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | Present, 110 bytes |
| Installer report | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3\LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | Present, 155006 bytes |
| RC Markdown metadata | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3\LensDocsStudio.Windows-0.1.0-dev-rc-20260612T144833Z.md` | Present, 8432 bytes |
| RC JSON metadata | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3\LensDocsStudio.Windows-0.1.0-dev-rc-20260612T144833Z.json` | Present, 4940 bytes |

Publication handoff must use the frozen paths above. Live artefact paths under `artifacts\windows\`, `artifacts\windows\release-candidates\`, and `artifacts\installers\inno\` are not the Phase 3AY publication handoff paths.

## Frozen Hash Verification

Hashes were recomputed from the files inside the frozen bundle folder.

| Check | Result |
| --- | --- |
| Frozen ZIP SHA256 | PASS, `8EDD6AF39590E28FB24412177145C36C85E6174B8AFEDC0EF5DBE2A9A61437E9` |
| Frozen installer SHA256 | PASS, `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB` |
| Installer `.sha256` file | PASS, contains `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB  LensDocsStudio.Windows-0.1.0-dev-Setup.exe` |

## RC Metadata Verification

| Check | Result |
| --- | --- |
| Frozen RC Markdown references source commit `ea75e13bf72518eb417cf11e4f62d7730738c8cd` | PASS |
| Frozen RC JSON references source commit `ea75e13bf72518eb417cf11e4f62d7730738c8cd` | PASS |
| Frozen RC Markdown references ZIP SHA256 `8EDD6AF39590E28FB24412177145C36C85E6174B8AFEDC0EF5DBE2A9A61437E9` | PASS |
| Frozen RC JSON references ZIP SHA256 `8EDD6AF39590E28FB24412177145C36C85E6174B8AFEDC0EF5DBE2A9A61437E9` | PASS |
| Frozen installer report references installer SHA256 `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB` | PASS |
| Frozen RC Markdown states no merge to `main`, public release publication, or stable-channel certification is performed | PASS |
| Frozen installer report states it does not publish a release, upload assets, create tags, move tags, or merge to `main` | PASS |

The RC JSON metadata is machine-readable package metadata and does not include publication or go-live prose fields. The no-publication and no-stable-channel boundary is recorded by the frozen RC Markdown metadata and frozen installer report, and is repeated in this Phase 3AY preflight.

## Read-Only GitHub State

Read-only commands used:

```powershell
git ls-remote origin "refs/tags/v0.1.0-dev.3" "refs/tags/v0.1.0-dev.3^{}"
gh release view v0.1.0-dev.3 --repo DouglasNLima/local-docs-studio --json tagName,isPrerelease,isDraft,isImmutable,name,url,targetCommitish,createdAt,publishedAt,assets
gh api repos/DouglasNLima/local-docs-studio/git/ref/tags/v0.1.0-dev.3
```

| Check | Result |
| --- | --- |
| Remote tag `v0.1.0-dev.3` | Missing, `git ls-remote` returned no refs |
| GitHub release `v0.1.0-dev.3` | Missing, `gh release view` returned `release not found` |
| GitHub tag API ref | Missing, `gh api` returned HTTP 404 |
| Attached assets | None, because the release is missing |
| Later publication disposition | Create-new if explicitly authorised later, not update-existing |

No read-only GitHub command modified GitHub state.

## Later Publication Plan

Only if a future publication is explicitly authorised:

1. Reconfirm the frozen bundle still exists at `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3`.
2. Recompute the frozen ZIP and installer hashes from that bundle.
3. Reconfirm `v0.1.0-dev.3` tag and release state.
4. Create the missing tag/release and upload only the frozen bundle assets intentionally.
5. Record the publication evidence separately.

This plan is not authorisation to publish and Phase 3AY did not execute it.

## Validation

| Command | Result | Notes |
| --- | --- | --- |
| `npm run test:static` | PASS | Static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans. |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS | Build succeeded with 6 existing PRI qualifier warnings and 0 errors. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS | Debug Windows shell build succeeded with 0 warnings and 0 errors; packaged `StaticApp/` verified 53 service-worker assets and 150 vendor assets. |

Browser tests, package rebuilds, installer rebuilds, release commands, publication commands, tag creation, release creation, asset uploads, and manual smoke were skipped because Phase 3AY is a no-publication frozen bundle audit and the requested validation list did not include those broader checks.

## Publication Boundary

Phase 3AY:

- did not publish `v0.1.0-dev.3`;
- did not create, move, or delete tags;
- did not create, edit, publish, unpublish, or delete GitHub Releases;
- did not create, edit, delete, replace, re-upload, rebuild, or republish release assets;
- did not merge to `main`;
- did not change runtime code;
- did not stage generated package, installer, frozen bundle, downloaded, or ignored artefacts as source;
- did not change `v0.1.0-dev.1` or `v0.1.0-dev.2` release assets;
- did not claim production readiness, go-live approval, stable-channel certification, or stable/latest release status.
