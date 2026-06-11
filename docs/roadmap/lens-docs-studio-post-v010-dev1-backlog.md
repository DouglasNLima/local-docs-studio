# Lens Docs Studio Post-v0.1.0-dev.1 Backlog

Planning date: 2026-06-11

## Status

`v0.1.0-dev.1` is closed as a published, post-publication-verified prerelease/dev release.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.1`

The closed release evidence confirms:

- The release is a GitHub prerelease, not a production or stable release.
- Stage A packaged diagnostics passed before publication.
- Stage B packaged watcher/conflict evidence passed before publication.
- The release was published from the frozen Phase 3AD bundle.
- Phase 3AF post-publication verification passed for release metadata, tag, exact asset set, downloaded hashes, downloaded ZIP smoke, and contained installer smoke.
- Phase 3AG closed the release evidence chain without changing runtime code, release assets, tags, releases, or `main`.

## What Is Complete

- `v0.1.0-dev.1` release publication is complete.
- Published ZIP and installer hashes match the frozen publication bundle.
- Downloaded ZIP smoke and contained installer smoke passed during Phase 3AF.
- Packaged native folder picker and watcher/conflict evidence passed during Phase 3AA.
- ZIP versus installer positioning is documented: ZIP is the portable/fallback package, and the unsigned Inno installer is the easier Windows install path.
- Known caveats are recorded for unsigned installer warnings, external runtime prerequisites, optional/default-safe file associations, and WebView2 user data retention after uninstall.
- The older `v0.1.0-dev` assets remain older/stale relative to `v0.1.0-dev.1` and were not modified.

## What Is Not Claimed

- Production readiness is not claimed.
- Go-live approval is not claimed.
- Stable-channel certification is not claimed.
- `main` has not been updated from this prerelease closure.
- The next release scope is not yet approved.
- No new tags, releases, or release assets are authorised by this backlog.
- No package rebuild, installer rebuild, asset replacement, or release republishing is implied.

## User Feedback Areas

Collect prerelease feedback before approving `v0.1.0-dev.2` scope:

- First-run clarity, including whether prerequisite and setup guidance is understandable.
- Installer and ZIP selection friction.
- Runtime prerequisite failure modes and user-facing recovery steps.
- Native bridge diagnostics comprehension.
- Folder picker reliability and wording when the native bridge cannot respond.
- Watcher/conflict UX wording around dirty files, external changes, and confirmation prompts.
- File association expectations, especially where users expect the installer to take ownership.
- Help guide gaps discovered by real prerelease users.

## Installer And Download Monitoring

Monitor without changing the closed `v0.1.0-dev.1` release assets:

- Download failures or checksum confusion for ZIP and installer assets.
- Windows SmartScreen, browser warning, or unsigned-installer friction.
- External runtime prerequisite install failures.
- Silent install/uninstall behaviour on real user machines.
- Uninstall evidence, including Start Menu shortcut cleanup, optional shortcut cleanup, Lens-owned registry cleanup, and documented WebView2 user data retention.
- Whether users can distinguish portable ZIP extraction from the installer path.
- Whether old `v0.1.0-dev` assets cause confusion now that `v0.1.0-dev.1` is the verified prerelease/dev release.

## Known Technical Risks

- The installer remains unsigned.
- Runtime prerequisites remain external and are not bootstrapped.
- WebView2 user data cleanup remains documented-only to avoid silently deleting browser-local state.
- Folder picker and native bridge behaviour depends on the packaged WebView2 host and installed runtime environment.
- Watcher/conflict UX has passed packaged evidence, but real user feedback may reveal wording or recovery gaps.
- Optional file associations may still be misunderstood as mandatory or automatic.
- GitHub release visibility may confuse users if older prerelease assets remain available.
- Connector-based commit verification is currently unreliable for this repository, so local git and GitHub CLI output remain the operational source of truth for this gate.

## Candidate Themes For v0.1.0-dev.2

These are candidate planning themes only. They do not approve implementation:

- Improve first-run and onboarding copy.
- Improve diagnostics visibility for bridge and folder-picker issues.
- Add an optional support bundle or troubleshooting export.
- Polish watcher/conflict UX copy.
- Review installer upgrade and uninstall evidence for future releases.
- Decide whether the older `v0.1.0-dev` release should remain, be superseded, or be documented as stale.
- Collect feedback from real prerelease users.

## Candidate Scope For v0.1.0-dev.2

Potential scope, pending separate approval:

- Documentation and UI-copy refinements that reduce setup, prerequisite, ZIP, installer, and native bridge confusion.
- Diagnostics visibility improvements that help users report bridge, folder-picker, and packaged-shell failures without exposing sensitive local paths.
- An opt-in troubleshooting/support export design that avoids collecting file contents unless the user explicitly chooses them.
- Watcher/conflict copy polish based on real prerelease observations.
- Installer upgrade/uninstall evidence planning, including what should be tested before any future installer asset is published.
- Release-disposition guidance for old prerelease assets, including whether `v0.1.0-dev` should stay as historical, be clearly superseded in documentation, or receive another approved disposition.

## Explicitly Out Of Scope Until Separately Approved

- Runtime code changes.
- Package rebuilds.
- Installer rebuilds.
- Editing, deleting, replacing, re-uploading, rebuilding, or republishing `v0.1.0-dev.1` release assets.
- Creating new tags or releases.
- Merging to `main`.
- Production readiness or go-live approval.
- Code signing.
- Auto-update.
- Runtime or WebView2 bootstrappers.
- MSIX, Store, or winget publication.
- Silent WebView2 user data deletion during uninstall.
- Direct links or integrations to other Lens tools.

## Planning Gate Verdict

Phase 3AH opens the post-prerelease backlog for the next development cycle. `v0.1.0-dev.1` remains closed, and `v0.1.0-dev.2` remains unapproved until candidate scope is reviewed against real feedback, installer/download monitoring, and the known technical risks above.

Phase 3AI records that candidate scope in `docs/roadmap/lens-docs-studio-v010-dev2-candidate-scope.md` and records the prerelease feedback intake process in `docs/release/lens-docs-studio-prerelease-feedback-intake.md`. These documents are planning-only and do not approve implementation, release publication, production readiness, or go-live.

Phase 3AJ records implementation readiness in `docs/roadmap/lens-docs-studio-v010-dev2-implementation-readiness.md`, including ordered slices, acceptance gates, validation expectations, evidence requirements, release boundaries, rollback/supersedence considerations, and a ready-to-use first-slice prompt. The recommended first slice is diagnostics visibility polish for native bridge and folder-picker issues.

Phase 3AJ remains planning-only. It does not release `v0.1.0-dev.2`, change runtime code, change release assets, create tags or releases, merge to `main`, or claim production readiness/go-live approval.
