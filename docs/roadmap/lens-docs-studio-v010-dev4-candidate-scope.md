# Lens Docs Studio v0.1.0-dev.4 Candidate Scope

Planning date: 2026-06-12

## Status

This document is candidate scope only for a possible `v0.1.0-dev.4` prerelease/dev release.

`v0.1.0-dev.4` is not approved. No release date is committed. No implementation, package rebuild, installer rebuild, tag, GitHub Release, release asset change, `main` merge, production readiness, or go-live approval is authorised by this document.

Current verified prerelease/dev release for new validation: `v0.1.0-dev.3`.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.3`

Phase 3BD adds `docs/roadmap/lens-docs-studio-v010-dev4-implementation-readiness.md` as a documentation/planning-only implementation readiness gate. It organises this candidate scope into proposed slices and recommends support bundle implementation as the first slice only if explicitly approved. It does not approve runtime work, release publication, production readiness, or go-live.

## Baseline

The `v0.1.0-dev.3` baseline records:

- Phase 3AW remains the referenced manual packaged sanity/remediation evidence for interactive native UI behaviours.
- Phase 3BA post-publication verification passed for the published tag, prerelease state, expected six-asset set, downloaded ZIP SHA256, downloaded installer SHA256, downloaded ZIP smoke, and contained installer smoke.
- Phase 3BB closed `v0.1.0-dev.3` as the current verified prerelease/dev release.
- `v0.1.0-dev.2` and `v0.1.0-dev.1` are superseded for new validation but preserved historically.
- Existing release assets remain unchanged unless a future release maintenance decision explicitly authorises action.

## Candidate Themes

These candidate themes may be considered for a future `v0.1.0-dev.4` cycle if separately approved.

### Support Bundle Implementation

Implement the optional troubleshooting/support bundle only if the design in `docs/architecture/lens-docs-studio-support-bundle-design.md` is explicitly approved for runtime work.

The implementation would need to remain local and user-initiated, show the exact included fields before copy/export, use allowlisted safe operational metadata, and exclude document content, raw logs, raw stack traces, screenshots by default, secrets, tokens, full private paths, browser storage, WebView2 user data, automatic telemetry, and network upload.

### Manual Packaged Sanity Automation Or Helper Design

Design helper tooling or checklists that make manual packaged sanity easier to repeat without pretending that native UI observation has been automated.

Candidate focus areas:

- Evidence capture templates for onboarding, diagnostics, Open folder selection, cancellation, browser fallback inactivity, and watcher changed-on-disk flows.
- Safer operator prompts for recording pass/fail/blocked results.
- Clear separation between automated native bridge smoke and real interactive packaged UI observation.

### Diagnostics Export And Copy Improvements

Improve safe diagnostics sharing without implementing broad telemetry or automatic uploads.

Candidate focus areas:

- Copyable bridge, route, capability, Open folder attempt, package, and installer state summaries.
- Bounded diagnostic messages that avoid private paths and raw exceptions.
- Alignment with the support bundle allowlist if that feature is approved.

### Installer Upgrade Evidence

Plan and, if separately approved, execute installer upgrade evidence from `v0.1.0-dev.3` to a future prerelease candidate.

Candidate evidence areas:

- Clean install and uninstall.
- Upgrade install over a `v0.1.0-dev.3` installation.
- Shortcut cleanup and optional file association behaviour.
- Installed executable smoke against the upgraded app.
- WebView2 user data retention caveat.
- Release asset immutability checks before and after the evidence run.

### Onboarding And Troubleshooting Refinements

Refine first-run, diagnostics, Help, README, and prerelease guidance from real prerelease feedback.

Candidate focus areas:

- Runtime prerequisite wording.
- ZIP versus installer choice.
- Open folder pending guidance recovery.
- Packaged WebView2 and browser fallback expectations.
- Watcher/conflict recovery wording.

### Stale Prerelease Guidance Maintenance

Keep prerelease guidance accurate as future prereleases supersede older ones.

Candidate focus areas:

- Preserve historical evidence for `v0.1.0-dev.1`, `v0.1.0-dev.2`, and `v0.1.0-dev.3`.
- Avoid implying old assets were unsafe, deleted, edited, rebuilt, or republished merely because they are superseded.
- Make the current recommended prerelease/dev release easy to identify.

### Release Provenance Simplification

Simplify future release provenance wording where possible, especially when tag targets, source commits, publication evidence commits, and package build commits differ.

Any simplification must preserve exact auditability for release tags, frozen source commits, published asset hashes, publication records, post-publication verification, and closure evidence.

## Candidate Acceptance Questions

Before any `v0.1.0-dev.4` implementation cycle is approved:

- Which feedback or monitoring signal justifies the cycle?
- Which candidate themes are in scope, and which are explicitly deferred?
- Does any item change runtime code, packaged static assets, Windows shell behaviour, installer output, or release assets?
- What validation is required for the approved blast radius?
- Does the work require fresh manual packaged sanity, package RC, installer RC, or upgrade/uninstall evidence?
- How will release notes distinguish prerelease/dev status from production readiness?
- What source commit, tag target, package source commit, and asset hashes would need to be recorded if publication is later approved?

## Validation Expectations

For this Phase 3BC planning gate:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Browser tests, package rebuilds, installer rebuilds, release preparation, release publication, tag operations, and manual packaged smoke are intentionally skipped for this planning gate because no runtime code or release artefacts are changed.

For a later approved implementation cycle, validation should be chosen by blast radius:

- Documentation-only changes: static validation and documentation review.
- Runtime UI, diagnostics, support bundle, import/export, or rendering changes: static validation and browser smoke/regression coverage.
- Windows shell or native bridge changes: Windows solution build, packaged static asset validation, and native bridge smoke.
- Installer-affecting changes: installer build evidence and upgrade/uninstall evidence as approved.
- Native picker or watcher/conflict changes: targeted browser/fake WebView2 coverage plus real packaged manual evidence where the behaviour depends on interactive Windows UI.

## Release Artefact Expectations

This candidate scope creates no release artefacts and changes no published release assets.

If a later phase approves `v0.1.0-dev.4` publication, it must separately define:

- Release tag and target commit.
- Frozen source/package commit.
- ZIP and installer artefact expectations.
- Checksums, reports, release notes, and publication evidence.
- Manual or automated evidence required before upload.
- Post-publication verification expectations.
- Historical guidance for older prereleases.

## Candidate Verdict

`v0.1.0-dev.4` remains a candidate planning target only. It is not an approved release plan, has no committed date, and does not claim production readiness or go-live approval.

Existing `v0.1.0-dev.1`, `v0.1.0-dev.2`, and `v0.1.0-dev.3` assets remain unchanged.

Use `docs/roadmap/lens-docs-studio-v010-dev4-implementation-readiness.md` for the Phase 3BD implementation readiness sequence. That readiness plan is still planning-only and is not release approval.
