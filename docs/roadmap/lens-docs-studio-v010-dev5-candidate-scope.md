# Lens Docs Studio v0.1.0-dev.5 Candidate Scope

Planning date: 2026-06-13

## Status

This document is candidate scope only for a possible future `v0.1.0-dev.5` cycle.

`v0.1.0-dev.5` is not approved. No release date is committed. This document does not authorise implementation, package rebuilds, installer rebuilds, release asset changes, new tags, new releases, a `main` merge, production readiness, stable-channel certification, stable/latest positioning, or go-live.

The current verified prerelease/dev release for new validation remains `v0.1.0-dev.4`.

Current release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.4`

Existing release assets remain unchanged. Any future `v0.1.0-dev.5` work requires separate approval, scoped implementation evidence, validation, publication approval, and post-publication verification if a release is later authorised.

## Baseline

The `v0.1.0-dev.4` baseline records:

- Phase 3BM closed `v0.1.0-dev.4` as the current verified prerelease/dev release.
- Phase 3BL post-publication verification passed for the release tag, prerelease state, expected six-asset set, downloaded ZIP SHA256, downloaded installer SHA256, downloaded ZIP smoke, payload hygiene, and contained installer smoke.
- Phase 3BE support bundle generation and Phase 3BG Diagnostics copy/export improvements are now available in the current prerelease.
- Phase 3BI payload hygiene guards protect future package and installer payloads from runtime-generated WebView2 data, browser storage, caches, and logs.
- Phase 3AW remains the referenced manual packaged sanity/remediation evidence for interactive native UI behaviours.
- True upgrade from `v0.1.0-dev.3` remains blocked by the already-published baseline installer rollback recorded in Phase 3BH.
- Production readiness and go-live remain separate approval gates.

## Candidate Themes

These themes may be considered for a future `v0.1.0-dev.5` cycle if separately approved.

### Manual Packaged Sanity Helper Implementation

Implement the Phase 3BF manual packaged sanity helper/checklist generator, if explicitly approved.

Candidate focus:

- Create a temporary synthetic workspace only.
- Generate or print checklist steps for human observation of packaged onboarding, Diagnostics, native Open folder select/cancel, support bundle preview/copy/export, support bundle privacy boundaries, watcher/conflict decisions, and installer checks when in scope.
- Output local evidence templates with `PASS`, `FAIL`, and `BLOCKED` fields.
- Avoid reading document contents, storing full private paths by default, bypassing the native picker, faking UI observation, collecting screenshots by default, or claiming manual sanity success automatically.

### Upgrade-Path Strategy

Define the next upgrade-path strategy after the `v0.1.0-dev.3` baseline installer rollback finding.

Candidate focus:

- Decide whether future upgrade evidence starts from a later cleanly installable prerelease baseline.
- Decide whether affected historical users should be guided to uninstall/reinstall, use ZIP extraction, or follow another explicit migration path.
- Preserve the fact that already-published baseline assets are unchanged.
- Define the evidence needed before any future upgrade success claim.

### Installer Shortcut And Task Behaviour Refinement

Review installer shortcut and optional task behaviour from prerelease feedback.

Candidate focus:

- Start Menu shortcut behaviour.
- Optional Desktop shortcut behaviour.
- Optional file association task clarity.
- Silent install flags such as `/NOICONS` and `/TASKS=`.
- Uninstall cleanup expectations for installer-owned files and shortcuts.
- WebView2 user data retention wording.

### Support Bundle UX Refinements

Refine the support bundle flow based on real prerelease feedback while preserving the Phase 3BE privacy model.

Candidate focus:

- Preview wording and field labels.
- Copy/export affordances after preview creation.
- Neutral export filename clarity.
- Bounded success/failure messages.
- Safe missing-field additions for troubleshooting.
- Continued exclusion of document contents, imported ZIP contents, full private paths by default, secrets, raw stacks, screenshots by default, browser storage, WebView2 user data, telemetry, and uploads.

### Diagnostics Export And Copy Refinements

Refine Diagnostics copy/export based on prerelease feedback.

Candidate focus:

- Concise operational summary fields.
- Bridge, route, native picker, package, installer, watcher, and prerequisite state labels.
- Redaction wording.
- Clear local-only/no-upload copy.
- Failure messages that remain bounded and useful.

### Release Provenance Simplification

Simplify future release provenance wording where possible.

Candidate focus:

- Make tag target, evidence commit, frozen source commit, publication documentation commit, package source commit, and asset hashes easier to follow.
- Preserve exact auditability and release evidence.
- Avoid implying that existing tags, releases, or assets were changed during documentation-only phases.

### Prerelease Guidance Maintenance

Keep prerelease guidance current as feedback and future planning evolve.

Candidate focus:

- Keep `v0.1.0-dev.4` identified as the current verified prerelease/dev release until a later prerelease is actually published and verified.
- Keep `v0.1.0-dev.5` labelled as candidate planning only until separately approved.
- Preserve older prerelease evidence historically.
- Avoid production readiness, stable/latest, go-live, or `main` promotion claims unless those gates are explicitly approved.

## Candidate Acceptance Questions

Before any `v0.1.0-dev.5` implementation work is approved:

- What prerelease feedback or monitoring signal justifies the cycle?
- Which candidate themes are approved, and which are explicitly deferred?
- Does the approved scope change runtime code, helper scripts, packaged static assets, Windows shell behaviour, installer output, or release documentation only?
- What validation is required for the approved blast radius?
- Is real manual packaged sanity required, and if so, who performs and records it?
- Does any installer or upgrade-path work require clean install, uninstall, or upgrade evidence?
- What source commit, package source commit, tag target, and asset hashes would need to be recorded if publication is later authorised?

## Validation Expectations

For this Phase 3BN planning gate:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Browser tests, package rebuilds, installer rebuilds, release preparation, release commands, tag operations, and manual packaged smoke are intentionally skipped because this planning gate changes documentation only.

For a later approved implementation cycle, validation should be chosen by blast radius:

- Documentation-only changes: static validation and documentation review.
- Helper/checklist script changes: static validation, script self-tests when added, Windows solution build if Windows packaging docs or scripts are touched, and packaged static asset validation when relevant.
- Runtime UI, Diagnostics, support bundle, import/export, or rendering changes: static validation plus browser smoke/regression coverage.
- Windows shell or native bridge changes: Windows solution build, packaged static asset validation, and native bridge smoke.
- Installer-affecting changes: installer build evidence and upgrade/uninstall evidence as explicitly approved.
- Native picker or watcher/conflict changes: targeted browser/fake WebView2 coverage plus real packaged manual evidence where the behaviour depends on interactive Windows UI.

## Release Artefact Expectations

This candidate scope creates no release artefacts and changes no published release assets.

If a later phase approves `v0.1.0-dev.5` publication, it must separately define:

- Release tag and target commit.
- Frozen source/package commit.
- ZIP and installer artefact expectations.
- Checksums, reports, release notes, and publication evidence.
- Manual or automated evidence required before upload.
- Post-publication verification expectations.
- Historical guidance for older prereleases.

## Candidate Verdict

`v0.1.0-dev.5` remains candidate planning only. It is not approved, has no committed release date, and does not claim production readiness or go-live.

Use `docs/roadmap/lens-docs-studio-post-v010-dev4-backlog.md` for the post-`v0.1.0-dev.4` backlog and `docs/release/lens-docs-studio-v010-dev4-release-closure.md` for the current release closure baseline.

