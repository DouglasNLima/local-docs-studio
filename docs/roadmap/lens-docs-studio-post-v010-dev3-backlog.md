# Lens Docs Studio Post-v0.1.0-dev.3 Backlog

Planning date: 2026-06-12

## Status

`v0.1.0-dev.3` is the current verified prerelease/dev release for new validation.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.3`

Verified published checksums:

| Artefact | SHA256 |
| --- | --- |
| ZIP package | `8EDD6AF39590E28FB24412177145C36C85E6174B8AFEDC0EF5DBE2A9A61437E9` |
| Unsigned Inno Setup installer | `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB` |

Phase 3BC opens a documentation/planning-only post-release backlog and candidate planning gate after Phase 3BB closed the `v0.1.0-dev.3` evidence chain. This backlog does not approve implementation, publication, package rebuilds, installer rebuilds, release asset changes, new tags, new releases, a merge to `main`, production readiness, or go-live.

## What v0.1.0-dev.3 Closed

`v0.1.0-dev.3` closed the remediation and publication chain for the native Open folder pending guidance work:

- Phase 3AW investigated the packaged `v0.1.0-dev.2` Open folder picker failure, could not reproduce the exact no-dialog/no-return condition in the retry environment, and hardened the app so a non-returning native Open folder request shows bounded operator guidance.
- Phase 3AX froze a local `v0.1.0-dev.3` candidate bundle.
- Phase 3AY audited that frozen bundle as a no-publication preflight.
- Phase 3BA verified the published `v0.1.0-dev.3` prerelease, including tag state, expected six-asset inventory, downloaded ZIP SHA256, downloaded installer SHA256, downloaded ZIP smoke, and contained installer smoke.
- Phase 3BB closed the release as the current verified prerelease/dev release.

No release assets, tags, GitHub Releases, packages, installers, or runtime code are changed by this backlog. The `v0.1.0-dev.1`, `v0.1.0-dev.2`, and `v0.1.0-dev.3` assets remain unchanged.

## Remaining Known Limitations

- `v0.1.0-dev.3` remains a prerelease/dev release, not a production release.
- Production readiness, go-live approval, stable-channel certification, stable/latest positioning, signing, and `main` promotion remain separate future gates.
- Phase 3BA did not repeat real interactive packaged UI observation/control; Phase 3AW remains the referenced manual packaged sanity/remediation evidence.
- The exact Phase 3AV no-dialog/no-return condition was not reproduced in Phase 3AW.
- The unsigned installer can still trigger operating-system, browser, or SmartScreen friction.
- Runtime prerequisites remain external and are not bootstrapped by the installer.
- WebView2 user data retention after uninstall remains documented to avoid silently deleting browser-local state.
- Support bundle generation remains design-only until explicitly approved.
- Installer upgrade evidence from `v0.1.0-dev.3` to a future prerelease has not yet been executed.

## Feedback Areas To Monitor

Collect feedback against `v0.1.0-dev.3` before approving any `v0.1.0-dev.4` scope:

- First-run setup clarity and whether users understand local/offline positioning.
- Windows shell diagnostics usefulness for bridge, native picker, and packaged mode issues.
- Open folder pending guidance clarity, especially whether users can find and operate the native `Select Folder` window.
- Cancellation and recovery wording for folder picker issues.
- Watcher/conflict wording after external file changes.
- ZIP versus installer selection and whether the easier install path is understood as unsigned prerelease/dev tooling.
- Runtime prerequisite recovery steps for .NET Desktop Runtime, Windows App SDK Runtime, and Evergreen WebView2 Runtime.
- Help guide, README, and prerelease guidance gaps found by real prerelease users.

## Installer And Download Monitoring

Monitor without changing published release assets:

- Download failures, partial downloads, or checksum mismatches for the ZIP and installer.
- Confusion about the verified ZIP SHA256 and installer SHA256 values.
- Installer launch, silent install, uninstall, shortcut cleanup, optional file association, and WebView2 user data retention reports.
- Upgrade or reinstall behaviour from `v0.1.0-dev.3` to any future candidate release, once a future candidate is explicitly authorised.
- Runtime-prerequisite failures on clean or lightly configured Windows machines.
- Whether historical prerelease assets cause users to validate against older releases.

## Open Folder Pending Guidance Feedback

The Phase 3AW remediation should be monitored through user reports and manual packaged sanity follow-up:

- Whether the pending guidance appears when a native Open folder request does not complete promptly.
- Whether the guidance avoids activating browser fallback in packaged WebView2.
- Whether users understand that they should look for a visible `Select Folder` window, select a folder, or cancel.
- Whether later native success or cancellation still resolves cleanly after the pending state appears.
- Whether any new no-dialog/no-return report includes enough environment and timing detail to reproduce the condition safely.

## Manual Packaged Sanity Follow-Up

Phase 3AW remains the current referenced manual packaged sanity/remediation evidence for interactive native UI behaviours. A future follow-up should be considered when a candidate release is explicitly authorised or when feedback warrants it.

Candidate follow-up areas:

- Onboarding rendering in the packaged app.
- Diagnostics opening and bridge capability visibility.
- Visible native Open folder picker selection.
- Native picker cancellation.
- Browser fallback inactivity in packaged WebView2.
- Watcher changed-on-disk copy and explicit refresh flow.
- Installer-launched app behaviour after an upgrade from `v0.1.0-dev.3`.

## Support Bundle Implementation Decision

`docs/architecture/lens-docs-studio-support-bundle-design.md` remains the design-only contract for a possible future local troubleshooting/support bundle.

Implementation remains unapproved. If approved separately, the feature should stay local, user-initiated, previewed before export/copy, and limited to allowlisted safe operational metadata. It must not collect document contents, raw logs, raw stack traces, screenshots by default, secrets, tokens, full private paths, browser storage dumps, WebView2 user data, automatic telemetry, or network uploads.

## Candidate Scope Ideas For v0.1.0-dev.4

These are candidate ideas only and do not approve implementation:

- Implement the optional support bundle feature if the Phase 3AN design is explicitly approved.
- Design automation or helper tooling for manual packaged sanity evidence without bypassing real native picker observation.
- Improve diagnostics export/copy paths for safe bridge, picker, package, and installer state summaries.
- Record installer upgrade evidence from `v0.1.0-dev.3` to a future prerelease candidate.
- Refine onboarding, troubleshooting, and prerelease guidance from observed feedback.
- Keep stale prerelease guidance current as newer prereleases supersede older assets.
- Simplify future release provenance wording where tag/source/package commits differ, while preserving exact evidence.

## Explicitly Out Of Scope Until Separately Approved

- Runtime code changes.
- Package rebuilds.
- Installer rebuilds.
- Browser smoke, package RC, installer RC, or manual packaged smoke unless a later approved task requires them.
- Editing, deleting, replacing, re-uploading, rebuilding, or republishing `v0.1.0-dev.1`, `v0.1.0-dev.2`, or `v0.1.0-dev.3` release assets.
- Creating, moving, or deleting tags.
- Creating, editing, publishing, unpublishing, or deleting GitHub Releases.
- Merging `develop` to `main`.
- Production readiness, go-live approval, stable-channel certification, stable/latest positioning, or public production rollout.
- Code signing, auto-update, runtime bootstrappers, WebView2 Fixed Version Runtime bundling, MSIX, Store publication, or winget publication.
- Automatic support uploads, telemetry, accounts, cloud sync, or direct integrations to other Lens tools.

## Planning Gate Verdict

Phase 3BC opens the post-`v0.1.0-dev.3` backlog and records candidate planning inputs for a possible `v0.1.0-dev.4` cycle. `v0.1.0-dev.4` is not approved, has no committed release date, and has no authorised implementation scope.

Phase 3BD adds `docs/roadmap/lens-docs-studio-v010-dev4-implementation-readiness.md` as a documentation/planning-only readiness gate. It recommends support bundle implementation as the first candidate slice only if explicitly approved, and records validation, evidence, release artefact, rollback, and supersedence expectations without changing runtime code or release assets.

Production readiness and go-live remain separate approval gates.
