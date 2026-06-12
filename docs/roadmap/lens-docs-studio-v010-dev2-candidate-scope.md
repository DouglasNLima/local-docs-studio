# Lens Docs Studio v0.1.0-dev.2 Candidate Scope

Planning date: 2026-06-11

## Status

This document converts the post-`v0.1.0-dev.1` backlog into a candidate scope for a possible `v0.1.0-dev.2` prerelease/dev release.

This is a candidate scope only. It is not an approved release plan, does not authorise implementation, does not authorise package or installer rebuilds, and does not authorise publication.

Production readiness, go-live approval, stable-channel certification, promotion to `main`, and any wider release approval remain separate and unclaimed.

## Current Baseline

`v0.1.0-dev.1` is the current published prerelease/dev release.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.1`

The baseline evidence records:

- Stage A packaged diagnostics passed before publication.
- Stage B packaged watcher/conflict evidence passed before publication.
- The release was published from the frozen Phase 3AD bundle.
- Phase 3AF post-publication verification passed for release metadata, tag, exact asset set, downloaded hashes, downloaded ZIP smoke, and contained installer smoke.
- Phase 3AG closed the release evidence chain without changing runtime code, release assets, tags, releases, or `main`.
- Phase 3AH opened the post-prerelease backlog without approving next-release scope.
- Phase 3AO records installer upgrade/uninstall evidence planning for a future installer-affecting `v0.1.0-dev.2` gate. It is planning only and does not rebuild installers, publish release assets, or approve publication.
- Phase 3AP records stale older prerelease guidance in `docs/release/lens-docs-studio-prerelease-guidance.md`. It is documentation/planning only and does not edit older release assets, publish release assets, or approve publication.

## Already Closed

- `v0.1.0-dev.1` release publication is complete.
- Published ZIP and installer hashes match the frozen publication bundle.
- Downloaded ZIP smoke and contained installer smoke passed.
- ZIP versus installer positioning is documented.
- Unsigned installer, external runtime prerequisites, optional/default-safe file associations, and WebView2 user data retention are documented caveats.
- The older `v0.1.0-dev` assets remain older/stale relative to `v0.1.0-dev.1`, are superseded for new validation, and were not modified.

## Candidate Goals

`v0.1.0-dev.2` should be considered only if prerelease feedback or release monitoring shows that a small, targeted dev release would reduce real user friction.

Candidate goals:

- Make first-run and onboarding guidance clearer without adding a new positioning claim.
- Improve diagnostics visibility for native bridge and folder-picker issues.
- Design an optional troubleshooting/support bundle that avoids sensitive data by default.
- Refine watcher/conflict copy where prerelease users find the wording unclear.
- Plan stronger installer upgrade and uninstall evidence before any future installer publication.
- Clarify stale older prerelease guidance so users understand which prerelease is preferred.
- Establish a structured prerelease feedback triage loop for deciding what enters or leaves the candidate scope.

## Proposed Must-Have Items

These items should be treated as must-have only if `v0.1.0-dev.2` is approved as a targeted prerelease cycle:

- Publish a reviewed feedback triage summary from `v0.1.0-dev.1` usage, including installer, ZIP, first-run, native bridge, folder picker, watcher/conflict, and documentation feedback.
- Confirm that every accepted `v0.1.0-dev.2` item maps to an observed feedback signal, a known limitation, or a release-monitoring risk.
- Keep the release notes and README wording precise: `v0.1.0-dev.1` is the current published prerelease/dev release, while `v0.1.0-dev.2` remains candidate scope until separately approved.
- Define validation expectations before implementation begins, including the minimum static, Windows build, packaged static asset, smoke, and manual evidence gates required by any runtime or installer-affecting change.
- Preserve the `v0.1.0-dev.1` release assets unchanged unless a separate release maintenance decision explicitly authorises action.
- Keep production readiness and go-live approval outside the `v0.1.0-dev.2` candidate scope.

## Proposed Should-Have Items

These items are candidates for inclusion if they stay small and are backed by feedback:

- First-run/onboarding copy polish for prerequisites, ZIP versus installer selection, setup wizard recovery, and where to find the local feature guide.
- Diagnostics copy and status-message improvements for native bridge, folder-picker, packaged-origin, WebView2 runtime, and unsupported browser-mode paths.
- Optional troubleshooting/support bundle design covering safe operational metadata, diagnostic status, explicit user consent boundaries, no automatic upload, and strict exclusions for private document content, secrets, tokens, raw stack traces, full private paths, screenshots by default, and unbounded logs.
- Watcher/conflict UX copy refinement for dirty files, external changes, external deletions, recognised renames, refresh prompts, and recovery wording.
- Installer upgrade/uninstall evidence planning, now recorded by Phase 3AO in `docs/release/lens-docs-studio-installer-upgrade-uninstall-evidence-plan.md`, including Start Menu shortcut cleanup, optional shortcut cleanup, Lens-owned registry cleanup, WebView2 data retention, silent install/uninstall, upgrade from `v0.1.0-dev.1`, and release asset immutability checks.
- Stale older prerelease documentation guidance for `v0.1.0-dev`, now recorded in `docs/release/lens-docs-studio-prerelease-guidance.md`, making clear that `v0.1.0-dev.1` is the preferred verified prerelease/dev release unless a future release supersedes it.
- Feedback intake templates and triage labels for repeatable prerelease issue handling.

## Deferred Or Out Of Scope

Deferred until separately approved:

- Runtime code changes.
- Package rebuilds.
- Installer rebuilds.
- Editing, deleting, replacing, re-uploading, rebuilding, or republishing `v0.1.0-dev.1` release assets.
- Creating new tags or releases.
- Merging to `main`.
- Production readiness, go-live approval, stable-channel certification, or stable/latest release positioning.
- Code signing.
- Auto-update.
- Runtime prerequisite bootstrappers.
- WebView2 Fixed Version Runtime bundling.
- MSIX, Store, or winget publication.
- Silent WebView2 user data deletion during uninstall.
- Direct links or integrations to other Lens tools.
- Collecting secrets, tokens, customer data, private documents, or personally identifiable information through feedback or troubleshooting artefacts.

## Acceptance Gates

Before approving implementation scope:

- Feedback from `v0.1.0-dev.1` users or release monitoring has been triaged.
- Each proposed item has a category, severity, owner or decision maker, and release decision bucket.
- Must-have items are small enough for a dev prerelease and do not imply production readiness.
- Any runtime-affecting item has a validation plan that covers browser/static compatibility and Windows shell behaviour.
- Any installer-affecting item has an installer evidence plan before publication is considered.
- Documentation clearly distinguishes candidate scope from an approved release plan.

Before approving any future `v0.1.0-dev.2` release:

- Static validation passes.
- Windows solution build passes.
- Packaged static asset validation passes.
- Browser smoke, native bridge smoke, package RC, installer build, or manual evidence is run only as required by the approved change type.
- Release notes identify the release as a prerelease/dev release.
- The exact release artefact set and checksums are recorded before publication.
- No production readiness or go-live claim is introduced.

## Validation Expectations

For this Phase 3AI planning gate:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Browser tests, package rebuilds, installer rebuilds, release preparation, and release publication are intentionally skipped for this gate because the work is documentation/planning only.

For a later approved `v0.1.0-dev.2` implementation cycle, validation should be selected by blast radius:

- Documentation-only changes: static validation and documentation review.
- Runtime UI or export/import changes: browser smoke coverage plus static validation.
- Windows shell or native bridge changes: Windows build, packaged static asset validation, and native bridge smoke.
- Installer changes: installer build evidence, silent install/uninstall smoke where safe, and manual upgrade/uninstall evidence planning.
- Watcher/conflict changes: targeted browser/fake WebView2 coverage plus real packaged manual evidence when the behaviour depends on the Windows picker or host watcher.

## Release Artefact Expectations

No release artefacts are created, rebuilt, uploaded, edited, replaced, or deleted by this candidate scope.

If a later phase approves `v0.1.0-dev.2` publication, it must define:

- Release tag and target commit.
- ZIP package expectation.
- Installer expectation.
- Checksum files and reports.
- Manual or automated evidence required before upload.
- Whether older prereleases remain visible, are documented as superseded, or require a separately authorised disposition decision.

## Feedback Intake Link

Use `docs/release/lens-docs-studio-prerelease-feedback-intake.md` for `v0.1.0-dev.1` feedback collection and triage before deciding whether this candidate scope should become an approved `v0.1.0-dev.2` implementation plan.

## Support Bundle Design Link

Phase 3AN records the optional troubleshooting/support bundle contract in `docs/architecture/lens-docs-studio-support-bundle-design.md`. It is design-only, does not implement bundle generation, does not approve a release, does not change runtime code, and does not change release assets, tags, releases, or `main`.

## Installer Evidence Plan Link

Phase 3AO records the installer upgrade/uninstall evidence plan in `docs/release/lens-docs-studio-installer-upgrade-uninstall-evidence-plan.md`. It is documentation/planning only, does not build or publish installer artefacts, does not change `v0.1.0-dev.1` assets, and does not approve `v0.1.0-dev.2` publication.

## Prerelease Guidance Link

Phase 3AP records stale older prerelease guidance in `docs/release/lens-docs-studio-prerelease-guidance.md`. It keeps `v0.1.0-dev.1` as the current recommended verified prerelease/dev release for new validation and marks `v0.1.0-dev` as stale/superseded for new installation, validation, and support evidence unless explicitly investigating historical behaviour.

## Candidate Verdict

`v0.1.0-dev.2` is a candidate planning target only. The next decision is feedback triage and scope approval, not release publication.

`v0.1.0-dev.1` remains the current published prerelease/dev release. No release assets, tags, releases, `main` merges, production readiness claims, or go-live approvals are changed by this planning document.

Phase 3AJ follows this candidate scope with `docs/roadmap/lens-docs-studio-v010-dev2-implementation-readiness.md`, an implementation readiness plan that orders candidate slices and recommends diagnostics visibility polish as the first slice. That readiness plan still does not approve `v0.1.0-dev.2` publication, production readiness, go-live approval, release asset changes, tags, releases, or `main` merges.
