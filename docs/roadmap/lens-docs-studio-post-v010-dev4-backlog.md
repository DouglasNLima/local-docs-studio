# Lens Docs Studio Post-v0.1.0-dev.4 Backlog

Planning date: 2026-06-13

## Status

Phase 3BN opens a documentation/planning-only post-release backlog and `v0.1.0-dev.5` candidate planning gate after Phase 3BM closed the `v0.1.0-dev.4` evidence chain.

The current verified prerelease/dev release for new validation is `v0.1.0-dev.4`.

Current release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.4`

Verified published checksums:

| Artefact | SHA256 |
| --- | --- |
| ZIP package | `06BFA3896E1CCA48F1DC87D2F49D2387A8BCB93078E9F070764F22B048DD860C` |
| Unsigned Inno Setup installer | `E52DB8B71B34E269F1754E2CCB6AB1691F38E6172CD2C826299A5DCF4A6652FC` |

This backlog does not approve implementation, publication, package rebuilds, installer rebuilds, release asset changes, new tags, new releases, a merge to `main`, production readiness, stable-channel certification, stable/latest positioning, or go-live.

Existing release assets remain unchanged. No `v0.1.0-dev.4` asset, older prerelease asset, tag, or GitHub Release is edited, deleted, replaced, re-uploaded, rebuilt, republished, or reclassified by this planning gate.

## What v0.1.0-dev.4 Closed

`v0.1.0-dev.4` closed the prerelease evidence chain for support bundle generation, Diagnostics copy/export refinement, package and installer payload hygiene, publication, post-publication verification, and release closure.

- Phase 3BE implemented explicit-user-action Diagnostics support bundle generation with preview-first local copy/export, allowlisted operational metadata, bounded redaction, and no upload, telemetry, account, ticket, or external service path.
- Phase 3BG improved Diagnostics copy/export wording and local action feedback while preserving the support bundle privacy model and schema version 1.
- Phase 3BH recorded that true upgrade from the already-published `v0.1.0-dev.3` installer remains blocked by that baseline installer rollback while copying a deep runtime-generated WebView2 Service Worker cache path.
- Phase 3BI added package and installer payload hygiene guards so runtime-generated WebView2 user data, service-worker cache paths, browser storage, caches, and logs cannot enter Windows ZIP or Inno installer payloads.
- Phase 3BJ froze the local publication bundle for later approval.
- Phase 3BL verified the published prerelease, including tag state, expected six-asset set, downloaded ZIP SHA256, downloaded installer SHA256, downloaded ZIP smoke, payload hygiene, and contained installer smoke.
- Phase 3BM closed `v0.1.0-dev.4` as the current verified prerelease/dev release for new validation.

## Remaining Known Limitations

- `v0.1.0-dev.4` remains a prerelease/dev release, not a production release.
- Production readiness, go-live approval, stable-channel certification, stable/latest positioning, signing, and `main` promotion remain separate future gates.
- True upgrade from `v0.1.0-dev.3` remains blocked by the already-published baseline installer rollback recorded in Phase 3BH.
- Phase 3BL skipped real interactive native UI control; Phase 3AW remains the referenced manual packaged verification for interactive native UI behaviours.
- The unsigned installer can trigger operating-system, browser, or SmartScreen friction.
- Runtime prerequisites remain external and are not bootstrapped by the installer.
- WebView2 user data may remain after uninstall so browser-local state is not silently deleted.
- Manual packaged sanity helper implementation remains unimplemented and should be considered only after explicit approval.

## Feedback Areas To Monitor

Collect feedback against `v0.1.0-dev.4` before approving any `v0.1.0-dev.5` scope:

- First-run setup clarity and whether users understand the local/offline product position.
- Open folder pending guidance clarity, especially whether users can find and operate the native `Select Folder` window.
- Native picker cancellation, recovery, and browser fallback inactivity in packaged WebView2.
- Watcher/conflict wording after external file changes.
- ZIP versus installer selection and whether the unsigned prerelease installer path is understood.
- Runtime prerequisite recovery for .NET Desktop Runtime, Windows App SDK Runtime, and Evergreen WebView2 Runtime.
- Help guide, README, prerelease guidance, and release provenance gaps found by real prerelease users.

## Support Bundle And Diagnostics Feedback

Monitor the Phase 3BE and Phase 3BG Diagnostics work without adding telemetry or upload paths:

- Whether users understand that support bundle creation is local, explicit, preview-first, and optional.
- Whether **Copy summary** and **Export local JSON** labels are clear.
- Whether exported JSON and copied summaries provide enough operational context for troubleshooting.
- Whether users report missing safe fields for bridge, native picker, package, installer, or watcher/conflict state.
- Whether redaction, bounded messages, neutral filenames, and private-content exclusions remain understandable and effective.
- Whether any feedback suggests unsafe collection pressure, such as raw logs, private paths, document contents, screenshots, browser storage, or WebView2 user data.

## Payload Hygiene And Installer Monitoring

Monitor package, installer, download, and payload hygiene behaviour without changing published assets:

- Download failures, partial downloads, checksum mismatches, or confusion about the verified ZIP and installer SHA256 values.
- Installer launch, silent install, uninstall, shortcut cleanup, optional file association, and WebView2 user data retention reports.
- Reports of runtime-generated WebView2 data, browser storage, caches, or logs appearing in future local package or installer sources.
- Payload hygiene guard failures in local validation.
- Runtime-prerequisite failures on clean or lightly configured Windows machines.
- Whether historical prerelease assets cause users to validate against older releases.

## Upgrade Limitation From v0.1.0-dev.3

The `v0.1.0-dev.3` to `v0.1.0-dev.4` true upgrade path remains blocked because the already-published `v0.1.0-dev.3` baseline installer rolls back while copying a deep runtime-generated WebView2 Service Worker cache path.

This backlog does not alter the published `v0.1.0-dev.3` baseline asset and does not claim upgrade success. A future upgrade-path strategy should be scoped separately before any implementation or evidence run.

Candidate investigation questions:

- Should future upgrade evidence start from a cleanly installable prerelease baseline rather than the blocked `v0.1.0-dev.3` installer?
- Should guidance explicitly direct users from affected old installers to uninstall/reinstall or use the ZIP path?
- What evidence is needed before claiming upgrade success for a later prerelease?
- How should release notes explain the blocked historical baseline without implying old assets were changed?

## Manual Packaged Sanity Follow-Up

Phase 3AW remains the referenced manual packaged sanity/remediation evidence for interactive native UI behaviours. Phase 3BL did not execute real interactive packaged UI observation/control.

Future follow-up areas:

- Packaged onboarding rendering and local/offline copy.
- Diagnostics opening and retry bridge check visibility.
- Visible native Open folder picker selection.
- Native picker cancellation.
- Browser fallback inactivity in packaged WebView2.
- Support bundle preview, copy/export, and privacy-boundary observation.
- Watcher changed-on-disk copy and explicit refresh flow.
- Installer-launched app behaviour when a future upgrade path is explicitly authorised.

The Phase 3BF helper plan remains the design reference if a helper/checklist generator is explicitly approved. No helper script exists yet.

## Candidate Scope Ideas For v0.1.0-dev.5

These are candidate ideas only and do not approve implementation:

- Implement the manual packaged sanity helper/checklist generator from the Phase 3BF plan, if explicitly approved.
- Define an upgrade-path strategy after the `v0.1.0-dev.3` baseline rollback finding.
- Refine installer shortcut, Start Menu, Desktop shortcut, optional file association, and task behaviour based on prerelease feedback.
- Refine support bundle UX from real prerelease feedback while preserving the local, preview-first, privacy-bounded model.
- Refine Diagnostics copy/export text, fields, and success/failure feedback from real prerelease feedback.
- Simplify future release provenance wording where tag, evidence, source, package, and publication commits differ.
- Maintain prerelease guidance as newer planning or release evidence supersedes historical records.

## Explicitly Out Of Scope Until Separately Approved

- Runtime code changes.
- Helper script implementation.
- Package rebuilds.
- Installer rebuilds.
- Browser smoke, package RC, installer RC, release preparation, release publication, or manual packaged smoke.
- Editing, deleting, replacing, re-uploading, rebuilding, or republishing `v0.1.0-dev.4` or older prerelease assets.
- Creating, moving, or deleting tags.
- Creating, editing, publishing, unpublishing, or deleting GitHub Releases.
- Merging `develop` to `main`.
- Production readiness, go-live approval, stable-channel certification, stable/latest positioning, or public production rollout.
- Code signing, auto-update, runtime bootstrappers, WebView2 Fixed Version Runtime bundling, MSIX, Store publication, or winget publication.
- Automatic support uploads, telemetry, accounts, cloud sync, or direct integrations to other Lens tools.

## Planning Gate Verdict

Phase 3BN records the post-`v0.1.0-dev.4` backlog and candidate planning input for a possible `v0.1.0-dev.5` cycle.

Use `docs/roadmap/lens-docs-studio-v010-dev5-candidate-scope.md` for the candidate theme draft. `v0.1.0-dev.5` is not approved, has no committed release date, and does not claim production readiness or go-live.

