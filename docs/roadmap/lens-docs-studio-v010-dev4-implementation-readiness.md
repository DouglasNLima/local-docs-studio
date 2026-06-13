# Lens Docs Studio v0.1.0-dev.4 Implementation Readiness

Planning date: 2026-06-12

## Status

This document is the Phase 3BD implementation readiness gate for a possible `v0.1.0-dev.4` prerelease/dev release. Phase 3BF later adds the manual packaged sanity helper design in `docs/testing/lens-docs-studio-manual-packaged-sanity-helper-plan.md` as planning only.

It converts the Phase 3BC candidate scope into an ordered, reviewable implementation plan with explicit slices, acceptance gates, validation expectations, evidence requirements, and release boundaries.

This is an implementation readiness plan, not release approval. It does not approve publication, package rebuilds, installer rebuilds, release asset changes, tag creation, GitHub Release changes, a merge to `main`, production readiness, or go-live.

Production readiness, go-live approval, stable-channel certification, stable/latest positioning, and promotion to `main` remain separate and unclaimed.

## Current Baseline

`v0.1.0-dev.3` is the current verified prerelease/dev release for new validation.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.3`

Verified published checksums:

| Artefact | SHA256 |
| --- | --- |
| ZIP package | `8EDD6AF39590E28FB24412177145C36C85E6174B8AFEDC0EF5DBE2A9A61437E9` |
| Unsigned Inno Setup installer | `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB` |

Baseline evidence:

- Phase 3AW remains the referenced manual packaged sanity/remediation evidence for interactive native UI behaviours after the Open folder pending guidance remediation.
- Phase 3BA post-publication verification passed for the published `v0.1.0-dev.3` tag, prerelease state, expected six-asset set, downloaded ZIP SHA256, downloaded installer SHA256, downloaded ZIP smoke, and contained installer smoke.
- Phase 3BB closed `v0.1.0-dev.3` as the current verified prerelease/dev release.
- Phase 3BC recorded the post-`v0.1.0-dev.3` backlog and candidate `v0.1.0-dev.4` scope as documentation/planning only.
- `v0.1.0-dev.2` and `v0.1.0-dev.1` are superseded for new validation but preserved historically.
- Existing release assets remain unchanged unless a future release-maintenance decision explicitly authorises action.

Repository gate at the start of Phase 3BD:

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Required baseline commit | `96284983a19708a2f93c5b1b137879f13a546063` |
| Baseline in history | PASS, the commit is an ancestor of `HEAD` |
| Tracked status before edits | Clean, `## develop...origin/develop` |
| Generated or ignored paths | Not staged; expected local outputs such as `artifacts/`, `node_modules/`, `test-results/`, Playwright reports, WebView2 user data, Windows build output, and installer/package artefacts remain outside source changes. |

## Implementation Readiness Verdict

`v0.1.0-dev.4` is ready for a small targeted implementation cycle only after an individual slice is explicitly selected and approved for runtime work.

The recommended first implementation slice is **Support bundle implementation, if approved**.

Reason: the design is already documented in `docs/architecture/lens-docs-studio-support-bundle-design.md`, and it directly supports future troubleshooting for bridge, folder picker, watcher/conflict, package, and installer issues without collecting private document content.

This readiness verdict does not approve a release. It only says that the candidate scope is sufficiently organised to start the first selected implementation slice on `develop` with targeted validation.

## Proposed Delivery Sequence

1. Support bundle implementation, if approved.
2. Manual packaged sanity helper/design.
3. Diagnostics export/copy improvements.
4. Installer upgrade evidence from `v0.1.0-dev.3` to the next candidate.
5. Onboarding/troubleshooting refinements from prerelease feedback.
6. Release provenance simplification for future tags/source commits.

The sequence favours privacy-safe troubleshooting infrastructure before broader evidence capture and guidance refinement. Any slice can be deferred if feedback shows it is not needed for a small dev prerelease.

## Dependencies And Risks

- Support bundle implementation depends on explicit approval of the Phase 3AN design contract before runtime work starts.
- Diagnostics and support-bundle slices must reuse derived safe labels and avoid raw host data, raw exceptions, document contents, browser storage, WebView2 user data, and private paths by default.
- Manual packaged sanity helper work must not claim to automate real native picker observation unless an approved tool genuinely observes that UI path.
- Installer upgrade evidence depends on a separately authorised future candidate package and installer; this readiness plan does not rebuild either.
- Release provenance simplification must preserve exact auditability for tag targets, source commits, package commits, checksums, reports, publication evidence, and post-publication verification.
- Any runtime slice may require service-worker cache updates, browser tests, Windows build validation, and packaged static asset validation.

## Explicit Non-Goals

- Do not implement runtime changes as part of this Phase 3BD gate.
- Do not edit, delete, replace, re-upload, rebuild, or republish `v0.1.0-dev.1`, `v0.1.0-dev.2`, or `v0.1.0-dev.3` release assets.
- Do not create, move, or delete tags.
- Do not create, edit, publish, unpublish, or delete GitHub Releases.
- Do not merge `develop` to `main`.
- Do not claim production readiness, go-live approval, stable-channel certification, stable/latest positioning, or public production rollout.
- Do not add signing, auto-update, runtime prerequisite bootstrappers, WebView2 Fixed Version Runtime bundling, MSIX, Store publication, or winget publication.
- Do not add automatic support uploads, telemetry, accounts, cloud sync, external service calls, or direct integrations to other Lens tools.
- Do not make optional Lens artefact bundle support mandatory or infer evidence levels from imported metadata.

## Slice 1: Support Bundle Implementation

Goal: implement the approved optional troubleshooting/support bundle so users can create a local, inspectable bundle of allowlisted operational metadata for bridge, folder picker, watcher/conflict, package, and installer issues.

Expected files or areas:

- Diagnostics UI and related services under `assets/scripts/ui/`.
- Safe diagnostics/native bridge state sources under `assets/scripts/files/` and existing app state services where needed.
- A pure support-bundle builder in an owning service or focused module under `assets/scripts/`.
- `assets/scripts/utils/` only for reusable redaction or bounded-text helpers.
- `tests/browser/app-smoke.spec.mjs` for preview, cancel, copy/export, redaction, and no-upload coverage.
- `service-worker.js` if new cacheable modules are added.
- README, `docs/tool-guide.md`, support-bundle design/evidence notes, and roadmap guidance where user-visible behaviour changes.

Acceptance criteria:

- Bundle creation requires explicit user action.
- The app previews exactly what will be included before copy/export.
- The user can cancel without creating or exporting a bundle.
- Copy/export is local only and never uploads automatically.
- The bundle excludes document content, rendered previews, imported ZIP contents, session images, screenshots by default, secrets, tokens, connection strings, raw stack traces, unbounded logs, browser storage, WebView2 user data, full private paths, usernames, machine names, and email addresses by default.
- Free-text diagnostic fields are bounded and redacted before inclusion.
- Generated filenames avoid private workspace names.
- Browser/GitHub Pages compatibility and offline packaged operation remain intact.

Validation commands:

- `npm run test:static`
- `npm run test:browser`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`
- Package/native validation if runtime changes affect packaged behaviour: `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1`

Evidence to collect:

- Static, browser, Windows build, and packaged static validation results.
- Redaction test summary for paths, emails, secrets, tokens, raw stack traces, long strings, screenshots-by-default exclusion, document-content exclusion, and untrusted ZIP metadata exclusion.
- Notes or screenshots of the Diagnostics entry point, preview, cancel path, copy/export result, and bounded error copy where practical.
- Confirmation that no network upload or automatic submission path was added.
- Documentation update summary and package/release artefact expectations.

Risks:

- New fields could accidentally expose private data if they are not allowlisted.
- Users could misread support-bundle creation as automatic support submission.
- Bundle ZIP generation could be confused with Markdown Bundle, Docs Site, or artefact review pack exports.

Out of scope:

- Automatic upload, telemetry, ticket creation, accounts, or external service calls.
- Document-content, screenshot, log, browser-storage, WebView2 user-data, or full-path collection.
- Release publication, package rebuilds, installer rebuilds, asset replacement, tags, releases, `main` merges, production readiness, or go-live.

## Slice 2: Manual Packaged Sanity Helper/Design

Phase 3BF design status: `docs/testing/lens-docs-studio-manual-packaged-sanity-helper-plan.md` now defines the safe manual packaged sanity helper/checklist approach, including the scenario matrix, evidence fields, privacy rules, cleanup expectations, and future implementation prompt. No helper script, runtime product shortcut, package rebuild, installer rebuild, release asset change, tag, release, `main` merge, production readiness, or go-live approval was added.

Goal: design or implement helper guidance for repeatable manual packaged sanity evidence without bypassing real native picker observation.

Expected files or areas:

- Documentation under `docs/release/` for packaged sanity templates or evidence capture.
- Optional helper scripts only if they record operator-entered pass/fail/blocked notes without controlling or faking native UI.
- README or Windows roadmap links if the manual evidence process changes.

Acceptance criteria:

- Evidence templates cover onboarding, diagnostics, visible Open folder selection, cancellation, browser fallback inactivity, watcher changed-on-disk copy, and upgrade-launched app checks.
- The helper distinguishes automated native bridge smoke from real interactive packaged UI observation.
- Operator prompts avoid collecting private paths, document content, raw logs, screenshots by default, or personal data.
- The design does not claim manual packaged sanity passed unless the user actually executes it.

Validation commands:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln` if Windows guidance or scripts are affected.
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` if packaged-static guidance changes.

Evidence to collect:

- Static validation result.
- Design/checklist review notes.
- Any script dry-run output if helper tooling is added.
- Explicit skipped manual smoke rationale when no real packaged manual pass is executed.

Risks:

- Helper wording could overstate automation.
- Evidence prompts could accidentally ask for sensitive local details.

Out of scope:

- Fake workspace injection or hidden native picker bypasses.
- Package or installer publication.
- Claims that native UI observation has been automated.

## Slice 3: Diagnostics Export/Copy Improvements

Goal: improve safe diagnostics sharing for bridge, route, capability, Open folder, package, and installer state summaries without broad telemetry.

Expected files or areas:

- Diagnostics UI and copy/export actions under `assets/scripts/ui/`.
- Native bridge/folder-picker diagnostic state under `assets/scripts/files/`.
- Browser tests for fake WebView2 diagnostics states.
- Documentation where user-facing diagnostic sharing guidance changes.

Acceptance criteria:

- Diagnostics copy/export uses bounded safe labels rather than raw exceptions or full paths.
- Bridge, capability, route, browser fallback, Open folder attempt, package, and installer state summaries are readable and reportable.
- Copy/export does not upload automatically or imply production support.
- Support-bundle allowlist alignment is preserved if Slice 1 has landed.

Validation commands:

- `npm run test:static`
- `npm run test:browser`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Evidence to collect:

- Static/browser/Windows validation results.
- Before/after notes for diagnostic copy/export output.
- Confirmation that private paths and raw errors remain excluded.

Risks:

- Copyable diagnostics can become de facto logs if not bounded.
- Browser and packaged modes may need different wording to avoid misleading users.

Out of scope:

- Automatic telemetry or support submission.
- Raw log export.
- Installer rebuild or release publication.

## Slice 4: Installer Upgrade Evidence

Goal: execute installer upgrade evidence from `v0.1.0-dev.3` to a future authorised candidate after package and installer artefacts exist.

Expected files or areas:

- Evidence documentation under `docs/release/`.
- README or prerelease guidance only if user-facing installer guidance changes.
- No runtime source changes unless a separately discovered defect requires an approved fix.

Acceptance criteria:

- Evidence covers clean install, upgrade over `v0.1.0-dev.3`, uninstall, shortcut cleanup, optional file association behaviour, installed executable smoke, WebView2 user data retention caveat, and release asset immutability checks.
- Source candidate, package path, installer path, checksums, and validation results are recorded.
- Any defects are classified separately from the evidence run.

Validation commands:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`
- Candidate-specific installer/package validation only when a future approved artefact set exists.

Evidence to collect:

- Installer version and source candidate identity.
- SHA256 values for candidate ZIP and installer.
- Install, upgrade, uninstall, shortcut, association, and smoke observations.
- Confirmation that old release assets were not edited or republished.

Risks:

- Upgrade observations may be environment-specific.
- Running installer evidence can create local machine state that must be cleaned up carefully.

Out of scope:

- Creating the future candidate artefacts as part of this readiness plan.
- Publishing, uploading, replacing, or deleting release assets.
- Signing, bootstrapping, MSIX, Store, winget, or production installer claims.

## Slice 5: Onboarding/Troubleshooting Refinements

Goal: refine first-run, diagnostics, Help, README, and prerelease guidance from observed `v0.1.0-dev.3` feedback.

Expected files or areas:

- Windows first-run/setup UI services if runtime copy changes are approved.
- `docs/tool-guide.md`
- README, prerelease guidance, feedback intake, or roadmap files.
- Browser tests when UI behaviour or visible copy changes.

Acceptance criteria:

- Wording remains generic: Local Markdown, Mermaid, and documentation studio.
- Runtime prerequisite, ZIP versus installer, Open folder pending guidance, packaged WebView2/browser fallback, and watcher/conflict recovery guidance stay accurate.
- Copy does not imply signing, bootstrapping, auto-update, stable/latest, production readiness, or go-live approval.

Validation commands:

- `npm run test:static`
- `npm run test:browser` if UI behaviour or visible app copy changes.
- `dotnet build src/windows/LensDocsStudio.Windows.sln` and packaged static validation if Windows runtime/package expectations change.

Evidence to collect:

- Static and targeted browser validation results.
- Feedback item mapping and wording summary.
- Any skipped validation rationale.

Risks:

- Guidance can drift from actual packaged behaviour.
- Copy-only changes can still alter user expectations in recovery paths.

Out of scope:

- Runtime prerequisite bootstrapping.
- Installer rebuilds or publication.
- Production support or go-live claims.

## Slice 6: Release Provenance Simplification

Goal: simplify future release provenance wording where tag targets, source commits, publication evidence commits, and package build commits differ, while preserving auditability.

Expected files or areas:

- Release templates and release guidance under `docs/release/`.
- README and roadmap references where release evidence wording is summarised.
- No runtime code.

Acceptance criteria:

- Future release records can identify tag target, frozen source/package commit, publication evidence commit, post-publication verification commit, asset names, checksums, and evidence status without ambiguity.
- Simplified wording does not hide commit differences.
- Historical release records remain intact unless separately authorised for documentation clarification.

Validation commands:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Evidence to collect:

- Documentation diff summary.
- Static/Windows validation results.
- Confirmation that no tags, releases, or release assets were touched.

Risks:

- Over-simplified wording could reduce auditability.
- Historical release notes could be misread as changed release evidence.

Out of scope:

- Editing, deleting, replacing, re-uploading, rebuilding, or republishing historical assets.
- New tags, releases, release asset uploads, or `main` merges.

## Validation Matrix

| Change type | Minimum validation | Additional validation |
| --- | --- | --- |
| Documentation-only planning | `npm run test:static` | Windows build and packaged static asset validation when the planning touches Windows distribution guidance. |
| Runtime UI, diagnostics, support bundle, import/export, or rendering changes | `npm run test:static`, `npm run test:browser` | Windows build and packaged static asset validation when packaged runtime files are affected. |
| Windows shell or native bridge behaviour | Static checks, browser smoke/regression coverage, `dotnet build src/windows/LensDocsStudio.Windows.sln`, packaged static asset validation | Native bridge smoke and real packaged manual evidence where native picker or watcher UI must be observed. |
| Installer-affecting evidence or implementation | Static checks, Windows build, packaged static asset validation, candidate package/installer evidence as approved | Upgrade/uninstall evidence, checksums, reports, and release asset immutability checks before any publication decision. |
| Release publication | All approved implementation and artefact gates plus release preparation, checksum verification, release notes review, and explicit publication approval | Post-publication verification after upload. |

For Phase 3BD itself, run:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Browser tests, package rebuilds, installer rebuilds, release commands, tag commands, asset uploads, and manual smoke are intentionally skipped for Phase 3BD because the work is documentation/planning only and does not change runtime code or release artefacts.

For Phase 3BF planning-only helper design, run the same documentation-oriented validation:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Browser tests, package rebuilds, installer rebuilds, release commands, tag commands, asset uploads, and manual smoke are intentionally skipped for Phase 3BF because it adds only planning documentation and no runtime/helper implementation.

## Evidence Requirements

Each future implementation slice should record:

- Branch, baseline commit, and tracked/ignored repository state before edits.
- Files changed and whether runtime code changed.
- Acceptance criteria mapping.
- Validation commands, results, and skipped validation with reason.
- Screenshots, copied output, or manual notes when user-facing wording or troubleshooting output is the acceptance target.
- Package/native validation results when packaged behaviour is affected.
- Confirmation that release assets, tags, GitHub Releases, `main`, production readiness, and go-live approval were not changed unless a later phase explicitly authorises them.

## Release Artefact Expectations

This readiness gate creates no release artefacts and changes no published release assets.

If a later phase approves `v0.1.0-dev.4` publication, it must separately define:

- Release tag and target commit.
- Frozen source/package commit.
- ZIP and installer artefact expectations.
- Checksums, reports, release notes, and publication evidence.
- Manual or automated evidence required before upload.
- Post-publication verification expectations.
- Historical guidance for older prereleases.
- Supersedence wording for `v0.1.0-dev.3`, `v0.1.0-dev.2`, and `v0.1.0-dev.1`.

Routine supersedence documentation must not edit, delete, replace, re-upload, rebuild, or republish old release assets.

## Rollback And Supersedence

For implementation slices:

- Prefer small commits that can be reverted independently.
- Keep documentation, tests, and runtime changes separated where practical.
- If a runtime slice introduces unsafe diagnostics, privacy risk, misleading release wording, or user-facing confusion, revert or supersede the slice on `develop` before considering any artefact build.

For releases:

- A future `v0.1.0-dev.4` prerelease, if authorised and published, would supersede `v0.1.0-dev.3` for new validation through explicit release notes and documentation.
- Supersedence does not require editing or deleting `v0.1.0-dev.3`, `v0.1.0-dev.2`, or `v0.1.0-dev.1` assets.
- Any release asset correction, replacement, removal, or republishing requires a separate authorised release-maintenance decision.

## First-Slice Execution Prompt

Use this prompt only after the support bundle implementation slice is explicitly approved:

```text
You are working in C:\Code\MarkdownReader on branch develop.

Objective:
Implement the first v0.1.0-dev.4 slice: approved optional troubleshooting/support bundle generation from docs/architecture/lens-docs-studio-support-bundle-design.md.

Context:
- v0.1.0-dev.3 is the current verified prerelease/dev release for new validation.
- docs/roadmap/lens-docs-studio-v010-dev4-implementation-readiness.md recommends support bundle implementation as the first slice only if explicitly approved.
- This is an implementation slice for develop, not a release approval.

Constraints:
- Work directly on develop.
- Require explicit user action before creating or exporting a support bundle.
- Do not automatically upload, submit telemetry, create tickets, call external services, or perform network submission.
- Do not include document contents, rendered previews, imported ZIP contents, session images, full private paths, secrets, tokens, connection strings, raw stack traces, unbounded logs, email addresses, usernames, machine names, WebView2 user data, localStorage dumps, IndexedDB, service-worker cache contents, screenshots by default, or raw personally identifiable information.
- Keep generated support bundles out of git.
- Preserve GitHub Pages/static hosting compatibility and the buildless app architecture.
- Keep product wording generic: Local Markdown, Mermaid, and documentation studio.
- Use British English for first-party UI, docs, comments, tests, logs, generated copy, examples, templates, and snippets.
- Do not edit, delete, replace, re-upload, rebuild, or republish v0.1.0-dev.1, v0.1.0-dev.2, or v0.1.0-dev.3 release assets.
- Do not create tags or releases.
- Do not merge to main.
- Do not claim production readiness, go-live approval, stable-channel certification, or stable/latest positioning.

Tasks:
1. Confirm branch, Phase 3BC/3BD baseline history, tracked status, and ignored/generated paths before editing.
2. Inspect current Diagnostics, native bridge, folder-picker, watcher/conflict, package/static validation, installer guidance, and support-bundle design before editing.
3. Implement a pure allowlisted support bundle builder that accepts already-derived safe diagnostics and produces bounded JSON/text content.
4. Add redaction and bounded-error tests for private paths, usernames from paths, email addresses, secrets, tokens, connection strings, raw stack traces, long strings, screenshots-by-default exclusion, document-content exclusion, and untrusted ZIP metadata exclusion.
5. Add a Diagnostics UI flow:
   - user opens Diagnostics;
   - user chooses Create support bundle;
   - app shows exactly what will be included;
   - user can cancel;
   - user can copy a safe summary or export a local bundle;
   - generated filename avoids private workspace names;
   - bundle generation errors use bounded safe copy.
6. Keep automatic upload out of scope and test that no upload/network submission control is introduced.
7. Update docs and evidence notes for the implemented feature, including package-affecting artefact expectations if runtime code changes.
8. Update service-worker.js if new cacheable static assets or modules are added.
9. Run validation:
   - npm run test:static
   - npm run test:browser
   - dotnet build src/windows/LensDocsStudio.Windows.sln
   - pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1
10. Run package/native validation if runtime changes affect packaged behaviour, and record any skipped validation with the reason.
11. Stage only intentional source/docs/test changes, commit, and push to origin/develop.

Suggested commit message:
add diagnostics support bundle export

Final response must include:
- Branch
- Commit hash
- Push result
- Runtime code changed or docs/tests only
- Files changed
- Support bundle UX and safety behaviours implemented
- Redaction and browser test coverage
- Static, browser, Windows build, packaged static, and any package/native validation results
- Skipped validation and reason
- Confirmation that no release assets, tags, releases, or main merges were changed
- Confirmation that v0.1.0-dev.1, v0.1.0-dev.2, and v0.1.0-dev.3 assets were not changed
- Confirmation that production readiness/go-live was not claimed
```

## Readiness Verdict

Phase 3BD approves only the implementation readiness plan for a possible `v0.1.0-dev.4` cycle. It does not approve runtime implementation, release publication, package rebuilds, installer rebuilds, asset changes, tags, releases, a merge to `main`, production readiness, or go-live.

The recommended first implementation slice is support bundle implementation, if explicitly approved.

`v0.1.0-dev.4` remains unreleased and unapproved. `v0.1.0-dev.3` remains the current verified prerelease/dev release for new validation.
