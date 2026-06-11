# Lens Docs Studio v0.1.0-dev.2 Implementation Readiness

Planning date: 2026-06-11

## Status

This document is the Phase 3AJ implementation readiness gate for a possible `v0.1.0-dev.2` prerelease/dev release.

It converts the Phase 3AI candidate scope into an ordered, reviewable implementation plan. It is not a release approval, does not approve publication, does not authorise package or installer rebuilds by itself, and does not claim production readiness or go-live approval.

Production readiness, go-live approval, stable-channel certification, promotion to `main`, tag creation, GitHub Release creation, and release asset changes remain separate and unclaimed.

## Current Baseline

`v0.1.0-dev.1` is the current published prerelease/dev release.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.1`

Baseline evidence:

- Phase 3AA Stage A packaged diagnostics, native picker selection, and Stage B watcher/conflict evidence passed before publication.
- Phase 3AD froze the authoritative local publication bundle.
- Phase 3AF post-publication verification passed for the release tag, prerelease state, exact asset set, downloaded ZIP and installer hashes, downloaded ZIP smoke, and contained installer smoke.
- Phase 3AG closed the release evidence chain without runtime code changes, release asset changes, tag changes, release changes, or a merge to `main`.
- Phase 3AH opened the post-prerelease backlog.
- Phase 3AI recorded the `v0.1.0-dev.2` candidate scope and prerelease feedback intake process.

Repository gate at the start of Phase 3AJ:

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Required baseline commit | `50c303395f33239a544f76f6773d661f4c52a557` |
| Baseline in history | PASS, the commit is an ancestor of `HEAD` |
| Tracked status before edits | Clean, `## develop...origin/develop` |
| Generated or ignored paths | Not staged; expected local outputs such as `artifacts/`, `node_modules/`, `test-results/`, Playwright reports, Windows build output, and local logs remain outside source changes. |

## Implementation Readiness Verdict

`v0.1.0-dev.2` is ready for a small targeted implementation cycle only after individual slices are selected from this plan.

The recommended first slice is **Diagnostics visibility polish for bridge/folder-picker issues**.

Reason: `v0.1.0-dev.1` uncovered real diagnostics and picker-evidence friction. Improving this first should reduce future support cost and make prerelease feedback easier to triage.

This readiness verdict does not approve a release. It only says that the candidate scope is sufficiently organised to start the first implementation slice on `develop` with targeted validation.

## Proposed Delivery Sequence

1. Diagnostics visibility polish for bridge/folder-picker issues.
2. First-run/onboarding copy polish.
3. Watcher/conflict UX copy refinement.
4. Optional troubleshooting/support bundle design.
5. Installer upgrade/uninstall evidence planning.
6. Stale older prerelease guidance.

The sequence favours low-risk visibility and copy work before larger support-bundle or installer-evidence design. Any slice can be deferred if feedback shows it is not needed for a small dev prerelease.

Implementation notes as of 2026-06-11:

- Phase 3AK implemented the first slice, diagnostics visibility polish for bridge and folder-picker issues, on `develop`.
- Phase 3AL implements the second slice, first-run/onboarding copy polish, on `develop`.
- These implementation slices do not approve `v0.1.0-dev.2` publication, package upload, installer upload, tag creation, release creation, or a merge to `main`.

## Non-Goals

- Do not edit, delete, replace, re-upload, rebuild, or republish `v0.1.0-dev.1` release assets.
- Do not create new tags or GitHub Releases.
- Do not merge to `main`.
- Do not claim production readiness, go-live approval, stable-channel certification, or stable/latest positioning.
- Do not add signing, auto-update, runtime prerequisite bootstrappers, WebView2 Fixed Version Runtime bundling, MSIX, Store, or winget publication.
- Do not convert optional Lens artefact metadata into mandatory workflows or direct integrations.
- Do not collect secrets, tokens, customer data, private documents, unredacted local paths, machine names, usernames, browser-local storage dumps, or WebView2 user data folders.

## Slice 1: Diagnostics Visibility Polish

Goal: make native bridge, packaged-origin, WebView2 runtime, and folder-picker diagnostics easier to understand and easier to report without exposing private paths.

Expected files or areas:

- `assets/scripts/ui/` diagnostics, help, status, or setup UI services.
- `assets/scripts/files/` native open-folder routing and fallback status text if needed.
- `assets/scripts/state/config.js` only if existing user-facing constants own the wording.
- `assets/styles/app.css` only for compact diagnostics presentation polish.
- `tests/browser/app-smoke.spec.mjs` for fake WebView2 or diagnostics regression coverage.
- `docs/tool-guide.md`, README, or release notes guidance only where user-visible diagnostics wording changes need documentation.
- `service-worker.js` only if a new cacheable static asset or module is added.

Acceptance criteria:

- **Help > Check Windows bridge** clearly distinguishes native bridge unavailable, bridge present, unsupported browser mode, packaged origin, missing capability, and native folder-picker failure states.
- Folder-picker failure guidance avoids suggesting browser fallback while a native bridge is present and expected to handle the action.
- Status copy uses British English and keeps private local paths out of user-visible diagnostics by default.
- The diagnostics copy can be reported through the feedback intake guide without collecting sensitive data.
- Browser/GitHub Pages mode remains defensive and does not expose native-only actions as available.

Validation commands:

- `npm run test:static`
- `npm run test:browser`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`
- Packaged smoke only if the runtime or Windows package surface changes beyond copy/tests/docs: `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1`

Evidence to collect:

- Static validation result.
- Browser smoke result or targeted Playwright result when browser coverage is narrower.
- Windows solution build result.
- Packaged static asset validation result.
- Before/after notes or screenshots of diagnostics and folder-picker failure wording where practical.
- Any skipped packaged smoke rationale if changes are copy-only and do not affect package behaviour.

Risks:

- Copy could imply a browser fallback even when the native bridge is present.
- Diagnostics could expose private host details if new fields are not filtered.
- Fake WebView2 coverage could pass while real packaged picker wording remains unclear.

Out of scope:

- New native bridge capabilities.
- New picker implementation.
- Package or installer publication.
- Troubleshooting/support bundle export.
- Release asset replacement.

## Slice 2: First-Run/Onboarding Copy Polish

Goal: reduce confusion around prerequisites, ZIP versus installer selection, setup wizard recovery, and where to find the local feature guide.

Expected files or areas:

- Windows first-run setup UI service and related copy constants.
- `docs/tool-guide.md`
- README Windows shell guidance.
- Browser tests for setup wizard copy or mode detection where existing coverage applies.

Acceptance criteria:

- Onboarding remains generic: Local Markdown, Mermaid, and documentation studio.
- Prerequisite wording stays accurate for .NET Desktop Runtime, Windows App SDK Runtime, and Evergreen WebView2 Runtime.
- ZIP and installer positioning remains precise: ZIP is portable/fallback; unsigned installer is the easier Windows install path.
- Setup wizard recovery guidance does not imply installer bootstrapping, auto-update, signing, or production readiness.

Validation commands:

- `npm run test:static`
- `npm run test:browser` if UI copy or setup behaviour changes.
- `dotnet build src/windows/LensDocsStudio.Windows.sln` if Windows project or packaged setup expectations change.

Evidence to collect:

- Static validation result.
- Targeted browser smoke result if UI changed.
- Screenshot or note confirming first-run wording where practical.
- Documentation diff summary.

Risks:

- Copy may overstate installer readiness or production support.
- Setup guidance may drift from actual prerequisite behaviour.

Out of scope:

- New setup wizard steps.
- Runtime prerequisite bootstrapping.
- Installer rebuilds or release publication.

## Slice 3: Watcher/Conflict UX Copy Refinement

Goal: clarify dirty-file, external-change, external-deletion, recognised-rename, refresh, save, discard, and recovery wording without changing the non-destructive watcher model.

Expected files or areas:

- `assets/scripts/ui/` file-list/status rendering.
- `assets/scripts/files/` watcher/conflict state messages.
- `tests/browser/app-smoke.spec.mjs` fake WebView2 watcher/conflict coverage.
- `docs/release/lens-docs-studio-watcher-conflict-manual-evidence.md` only if evidence wording needs a planning update.

Acceptance criteria:

- Dirty editor content is never silently discarded.
- Refresh prompts explain the consequence before replacing local edits.
- Deleted-file guidance preserves recovery options.
- Rename wording stays non-destructive and does not imply automatic merge.
- Existing browser/PWA folder workflows remain unaffected.

Validation commands:

- `npm run test:static`
- `npm run test:browser`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- Packaged native smoke only if runtime bridge or packaged watcher behaviour changes.

Evidence to collect:

- Targeted fake WebView2 watcher/conflict test result.
- Browser smoke result.
- Notes for manual packaged evidence if wording affects human validation.

Risks:

- Copy-only changes may still change user decisions in conflict prompts.
- Tests may cover state transitions but not real user comprehension.

Out of scope:

- Automatic merge.
- Background dirty reload.
- Delete, rename, or move commands initiated from the app.
- Native watcher protocol changes unless separately approved.

## Slice 4: Optional Troubleshooting/Support Bundle Design

Goal: design an opt-in troubleshooting/support bundle that captures safe diagnostics by default and makes sensitive content boundaries explicit before any implementation.

Expected files or areas:

- New or updated planning documentation under `docs/architecture/` or `docs/release/`.
- Feedback intake guidance.
- No runtime implementation unless a later slice approves it.

Acceptance criteria:

- Bundle contents are listed by default-safe, user-reviewable, and prohibited categories.
- The design excludes document contents, private paths, secrets, personally identifiable information, browser-local storage dumps, and WebView2 user data by default.
- User consent and review boundaries are explicit.
- The design explains whether any future implementation would affect exports, Markdown Bundle, or artefact review pack flows.

Validation commands:

- `npm run test:static`

Evidence to collect:

- Static validation result.
- Design review notes.
- Privacy checklist mapping to the prerelease feedback intake guide.

Risks:

- A support bundle could accidentally become a data collection feature.
- Users may attach sensitive material if boundaries are unclear.

Out of scope:

- Runtime support bundle export implementation.
- Uploads, telemetry, external services, or accounts.
- Automatic evidence-level inference for artefact metadata.

## Slice 5: Installer Upgrade/Uninstall Evidence Planning

Goal: define the evidence required before any future installer-affecting `v0.1.0-dev.2` publication, including upgrade and uninstall behaviour.

Expected files or areas:

- `docs/architecture/windows-offline-distribution-roadmap.md`
- Installer release or smoke checklists under `docs/release/`
- README installer caveats if guidance changes.

Acceptance criteria:

- Evidence covers Start Menu shortcut cleanup, optional Desktop shortcut cleanup, Lens-owned registry cleanup, WebView2 data retention, prerequisite failure observations, default/custom install paths, and upgrade from an older prerelease where practical.
- The plan keeps WebView2 user data cleanup documented-only unless a separate decision authorises an opt-in cleanup path.
- The plan does not authorise package or installer rebuilds by itself.

Validation commands:

- `npm run test:static`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` if packaged-static guidance changes.

Evidence to collect:

- Static validation result.
- Checklist or matrix review notes.
- Explicit skipped-package rationale if no package or installer files changed.

Risks:

- Installer planning can be mistaken for installer publication approval.
- Upgrade/uninstall claims may exceed available evidence.

Out of scope:

- Rebuilding installers.
- Publishing release assets.
- Signing, bootstrapping, MSIX, Store, or winget.

## Slice 6: Stale Older Prerelease Guidance

Goal: clarify that `v0.1.0-dev.1` is the preferred verified prerelease/dev release unless a future release supersedes it, while the older `v0.1.0-dev` assets remain historical/stale.

Expected files or areas:

- README release guidance.
- `docs/release/lens-docs-studio-v0.1.0-dev-known-issues.md`
- Roadmap or release closure notes where cross-links need clarification.

Acceptance criteria:

- Guidance distinguishes historical prerelease assets from the current verified prerelease.
- No older release assets are edited, deleted, replaced, re-uploaded, or republished.
- No new tag or release is created.
- No stable/latest or production readiness claim is introduced.

Validation commands:

- `npm run test:static`

Evidence to collect:

- Static validation result.
- Documentation diff summary.
- Confirmation that release assets, tags, releases, and `main` were untouched.

Risks:

- Users may interpret stale guidance as a release disposition decision.
- Documentation could accidentally imply that the older release was modified.

Out of scope:

- Archiving, deleting, or editing older GitHub releases.
- New release publication.
- Stable-channel promotion.

## Validation Matrix

| Change type | Minimum validation | Additional validation |
| --- | --- | --- |
| Documentation-only planning | `npm run test:static` | Windows build and packaged static asset validation when the planning touches Windows distribution guidance. |
| Runtime UI copy or diagnostics | `npm run test:static`, `npm run test:browser` | Windows build and packaged static asset validation; native smoke if bridge/package behaviour changes. |
| Windows shell or native bridge behaviour | `npm run test:static`, `npm run test:browser`, `dotnet build src/windows/LensDocsStudio.Windows.sln`, packaged static asset validation | Native bridge smoke and manual packaged evidence where picker or watcher UI must be observed. |
| Installer-affecting implementation | Static checks, Windows build, packaged static asset validation, package RC validation, installer build evidence | Silent install/uninstall smoke, upgrade/uninstall manual evidence, checksum and report capture before publication. |
| Release publication | All approved implementation gates plus release preparation, checksum verification, release notes review, and explicit publication approval | Post-publication verification after upload. |

For Phase 3AJ itself, run:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Browser tests, package rebuilds, installer rebuilds, release preparation, and release publication are intentionally skipped for this gate because the work is documentation/planning only.

## Evidence Requirements

Each implementation slice should record:

- Branch, commit, and tracked git status before and after the slice.
- Files changed and whether runtime code changed.
- Validation commands, results, and any skipped validation with reason.
- Screenshots or manual notes when user-facing wording is the acceptance target.
- Confirmation that release assets, tags, releases, `main`, production readiness, and go-live approval were not changed unless a later phase explicitly authorises them.
- Feedback or known-risk mapping back to `docs/release/lens-docs-studio-prerelease-feedback-intake.md`.

## Release Artefact Expectations

This readiness gate creates no release artefacts.

If a later phase approves `v0.1.0-dev.2` publication, it must define:

- Target commit and tag.
- ZIP expectation.
- Installer expectation.
- Checksum and report expectations.
- Manual and automated evidence required before upload.
- Whether older prereleases remain visible, are documented as superseded, or need a separately authorised disposition decision.
- Post-publication verification steps.

No `v0.1.0-dev.1` release asset should be touched by the `v0.1.0-dev.2` implementation cycle.

## Rollback And Supersedence

For implementation slices:

- Prefer small commits that can be reverted independently.
- Keep documentation and runtime changes separated where practical.
- If a runtime slice introduces user-facing confusion, revert or supersede the slice on `develop` before considering any release artefact build.

For releases:

- A future `v0.1.0-dev.2` prerelease, if authorised and published, would supersede `v0.1.0-dev.1` only through explicit release notes and documentation.
- Supersedence does not require editing or deleting `v0.1.0-dev.1` assets.
- Any release asset correction, replacement, or removal requires a separate authorised release-maintenance decision.

## First-Slice Execution Prompt

Use this prompt for the recommended first implementation slice:

```text
You are working in C:\Code\MarkdownReader on branch develop.

Objective:
Implement the first v0.1.0-dev.2 slice: diagnostics visibility polish for native bridge and folder-picker issues.

Context:
- v0.1.0-dev.1 is published, post-publication verified, and release-closed.
- docs/roadmap/lens-docs-studio-v010-dev2-implementation-readiness.md recommends this as the first implementation slice.
- This is an implementation slice for develop, not a release approval.

Constraints:
- Work directly on develop.
- Preserve v0.1.0-dev.1 release asset boundaries.
- Do not edit, delete, replace, re-upload, rebuild, or republish v0.1.0-dev.1 release assets.
- Do not create tags or releases.
- Do not merge to main.
- Do not claim production readiness, go-live approval, stable-channel certification, or stable/latest positioning.
- Keep the product generic: Local Markdown, Mermaid, and documentation studio.
- Use British English for first-party UI, docs, tests, comments, and generated copy.
- Avoid browser fallback wording while the native bridge is present and expected to handle folder picking.
- Keep diagnostics safe: do not expose private local paths, usernames, machine names, secrets, tokens, document contents, or WebView2 user data by default.

Tasks:
1. Confirm branch, baseline history, and tracked/ignored repository state.
2. Inspect existing diagnostics, native bridge, folder-picker, status, setup, and test code before editing.
3. Improve diagnostics and folder-picker visibility copy with the smallest runtime changes needed.
4. Add or update targeted tests for the changed diagnostics/folder-picker states.
5. Update docs or evidence notes only where user-visible wording or validation expectations changed.
6. Update service-worker.js only if a new cacheable static asset or module is added.
7. Run targeted validation:
   - npm run test:static
   - npm run test:browser, or a focused Playwright command if the changed tests are narrower and the reason is recorded
   - dotnet build src/windows/LensDocsStudio.Windows.sln
   - pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1
8. Run packaged native smoke only if runtime/package-affecting changes require packaged evidence:
   - pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1
9. Record skipped validation with reason.
10. Stage only intentional source/docs/test changes, commit, and push to origin/develop.

Suggested commit message:
polish bridge diagnostics visibility

Final response must include:
- Branch
- Commit hash
- Push result
- Runtime code changed or docs/tests only
- Files changed
- Diagnostics/folder-picker behaviours updated
- Tests and validation results
- Skipped validation and reason
- Confirmation that no release assets, tags, releases, or main merges were changed
- Confirmation that production readiness/go-live was not claimed
```

## Readiness Verdict

Phase 3AJ approves starting the first implementation slice on `develop` with targeted validation. It does not approve `v0.1.0-dev.2` release publication.

`v0.1.0-dev.1` remains the current published prerelease/dev release. `v0.1.0-dev.2` is not released, not stable/latest, and not production-ready by this document.
