# Lens Docs Studio v0.1.0-dev.5 Implementation Readiness

Planning date: 2026-06-13

## Status

This document is the Phase 3BO implementation readiness gate for a possible future `v0.1.0-dev.5` prerelease/dev release.

It converts the Phase 3BN candidate scope into an ordered, reviewable implementation plan with explicit slices, acceptance gates, validation expectations, evidence requirements, and release boundaries.

This is an implementation readiness plan, not release approval. It does not authorise runtime implementation by itself, package rebuilds, installer rebuilds, release asset changes, new tags, GitHub Release changes, a merge to `main`, production readiness, stable-channel certification, stable/latest positioning, or go-live.

Production readiness, go-live approval, stable-channel certification, stable/latest positioning, public production rollout, and promotion to `main` remain separate and unclaimed.

## Current Baseline

`v0.1.0-dev.4` is the current verified prerelease/dev release for new validation.

Current release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.4`

Verified published checksums:

| Artefact | SHA256 |
| --- | --- |
| ZIP package | `06BFA3896E1CCA48F1DC87D2F49D2387A8BCB93078E9F070764F22B048DD860C` |
| Unsigned Inno Setup installer | `E52DB8B71B34E269F1754E2CCB6AB1691F38E6172CD2C826299A5DCF4A6652FC` |

Baseline evidence:

- Phase 3BM closed `v0.1.0-dev.4` as the current verified prerelease/dev release.
- Phase 3BL post-publication verification passed for the release tag, prerelease state, expected six-asset set, downloaded ZIP SHA256, downloaded installer SHA256, downloaded ZIP smoke, payload hygiene, and contained installer smoke.
- Phase 3BE implemented explicit-user-action Diagnostics support bundle generation.
- Phase 3BG improved Diagnostics copy/export wording and local action feedback.
- Phase 3BI added package and installer payload hygiene guards.
- Phase 3AW remains the referenced manual packaged sanity/remediation evidence for interactive native UI behaviours.
- Phase 3BL skipped real interactive manual packaged UI observation/control.
- True upgrade from the already-published `v0.1.0-dev.3` installer remains blocked by the baseline installer rollback recorded in Phase 3BH.
- Phase 3BN recorded the post-`v0.1.0-dev.4` backlog and `v0.1.0-dev.5` candidate scope as documentation/planning only.
- `v0.1.0-dev.3`, `v0.1.0-dev.2`, and `v0.1.0-dev.1` are superseded for new validation but preserved historically.

Repository gate at the start of Phase 3BO:

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Required Phase 3BN commit | `a373fcd5d7bb043b55f2cbf245ea6bd1d16aba50` |
| Baseline in history | PASS, the Phase 3BN commit is an ancestor of `HEAD` |
| Tracked status before edits | Clean, `## develop...origin/develop` |
| Tracked changes before edits | None |
| Ignored/generated paths | Not staged; expected local outputs such as `artifacts/`, `node_modules/`, `src/windows/.vs/`, `src/windows/LensDocsStudio.Windows/bin/`, `src/windows/LensDocsStudio.Windows/obj/`, `test-results/`, WebView2 runtime data, downloads, package output, installer output, and local logs remain outside source changes. |

`git status --ignored --short` can emit Windows long-path warnings while scanning generated WebView2 cache folders under ignored Windows build output. Those paths are generated runtime/build output, not tracked source.

## Implementation Readiness Verdict

`v0.1.0-dev.5` is ready for a small targeted implementation cycle only after an individual slice is explicitly selected and approved.

The recommended first implementation slice is **Manual packaged sanity helper implementation, if explicitly approved**.

Reason: the project now has strong automated smoke, package verification, installer smoke, payload hygiene, publication verification, and release closure evidence, but interactive packaged UI checks are still repeatedly skipped or referenced from Phase 3AW. A helper/checklist generator can reduce evidence friction without bypassing real native picker observation or claiming manual sanity success automatically.

This readiness verdict does not approve a release. It only says that the candidate scope is sufficiently organised to start the first selected implementation slice on `develop` with targeted validation.

## Proposed Delivery Sequence

1. Manual packaged sanity helper implementation, if explicitly approved.
2. Upgrade-path strategy after the `v0.1.0-dev.3` baseline rollback finding.
3. Installer shortcut/task behaviour refinement.
4. Support bundle UX refinements from prerelease feedback.
5. Diagnostics export/copy refinements from prerelease feedback.
6. Release provenance simplification for future tags/source commits.
7. Prerelease guidance maintenance.

The sequence favours evidence friction reduction first, then upgrade and installer strategy, then smaller support, Diagnostics, provenance, and guidance refinements.

## Dependencies And Risks

- Manual packaged sanity helper work depends on explicit approval of the Phase 3BF helper plan before any script or checklist implementation starts.
- The helper must preserve the distinction between automated native bridge smoke and real human observation of packaged onboarding, Diagnostics, native picker selection/cancellation, support bundle privacy, and watcher/conflict decisions.
- Helper output must avoid document contents, imported ZIP contents, screenshots by default, browser storage, WebView2 user data, raw logs, raw stack traces, secrets, tokens, connection strings, emails, usernames, machine names, private names, raw personally identifiable information, and full private paths.
- Upgrade-path strategy depends on the historical `v0.1.0-dev.3` baseline rollback and must not imply old published assets were changed.
- Installer shortcut/task refinements may require installer build, install, uninstall, shortcut, registry, and upgrade evidence if implementation is later approved.
- Support bundle and Diagnostics refinements must preserve the Phase 3BE/3BG local, explicit, preview-first, privacy-bounded model.
- Release provenance simplification must improve readability without losing exact auditability for tag targets, frozen source commits, package commits, publication evidence, post-publication verification commits, asset names, and hashes.
- Any future runtime, script, Windows shell, package, or installer slice may need broader validation than this planning gate.

## Explicit Non-Goals

- Do not implement runtime changes as part of this Phase 3BO gate.
- Do not implement helper scripts as part of this Phase 3BO gate.
- Do not rebuild packages or installers.
- Do not run package rebuilds, installer rebuilds, release preparation, release commands, tag commands, asset uploads, or manual packaged smoke for this planning gate.
- Do not edit, delete, replace, re-upload, rebuild, republish, or reclassify `v0.1.0-dev.4` or older prerelease assets.
- Do not create, move, or delete tags.
- Do not create, edit, publish, unpublish, or delete GitHub Releases.
- Do not merge `develop` to `main`.
- Do not claim production readiness, go-live approval, stable-channel certification, stable/latest positioning, or public production rollout.
- Do not add signing, auto-update, runtime prerequisite bootstrappers, WebView2 Fixed Version Runtime bundling, MSIX, Store publication, or winget publication.
- Do not add automatic support uploads, telemetry, accounts, cloud sync, external service calls, or direct integrations to other Lens tools.
- Do not make optional Lens artefact bundle support mandatory or infer evidence levels from imported metadata.

## Slice 1: Manual Packaged Sanity Helper Implementation

Goal: implement the Phase 3BF manual packaged sanity helper/checklist generator so a human operator can create a temporary synthetic workspace, follow manual packaged UI checks, and record bounded `PASS`, `FAIL`, or `BLOCKED` evidence without bypassing real native picker observation.

Expected files or areas:

- A focused helper script under `scripts/windows/` or another existing script location if that better matches local conventions.
- Documentation updates in `docs/testing/lens-docs-studio-manual-packaged-sanity-helper-plan.md`, README, or release/testing guidance.
- Targeted static validation coverage if a script is added, likely through `scripts/windows/Test-WindowsStaticAssets.ps1` or an adjacent script self-test.
- No runtime app modules unless a separately approved implementation slice expands scope.

Acceptance criteria:

- The helper creates only temporary synthetic workspaces with safe Markdown, Mermaid, and text fixtures.
- The helper generates or prints manual checklist steps for packaged onboarding, Diagnostics, native Open folder selection/cancellation, support bundle preview/copy/export, support bundle privacy boundaries, watcher/conflict checks, and installer checks when explicitly in scope.
- The helper outputs a local evidence template with scenario rows and `PASS`, `FAIL`, and `BLOCKED` fields.
- The helper does not read document contents from user workspaces.
- The helper does not record full private paths by default.
- The helper does not fake, automate, inject, or bypass native Windows folder picker selection.
- The helper does not claim manual packaged sanity success automatically.
- Generated evidence, support bundles, package downloads, installer outputs, logs, screenshots, WebView2 data, and temporary workspaces remain outside git.

Validation commands:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`
- Script self-test or focused script dry-run if helper tooling is added.
- Package/native validation if any script or package behaviour changes beyond documentation/checklist output.

Evidence to collect:

- Branch, baseline commit, tracked status, and ignored/generated path summary before edits.
- Helper command or output path.
- Temporary workspace path convention using a redacted basename or `TEMP_WORKSPACE_USED`, not a full private path in committed evidence.
- Example local evidence template showing the scenario matrix and bounded note fields.
- Static, Windows build, packaged static, and helper self-test/dry-run results.
- Skipped browser tests, package rebuilds, installer rebuilds, release commands, and manual smoke with planning/helper-only reason unless explicitly approved.

Risks:

- Helper wording could imply that automation replaces human observation.
- Checklist prompts could accidentally ask for private details.
- Helper output could be mistaken for release approval or completed manual sanity evidence.

Out of scope:

- Runtime product shortcuts, debug-only workspace injection, localStorage setup bypasses, command-line workspace bypasses, or native picker bypasses.
- Reading user document contents, collecting screenshots by default, uploading evidence, or storing full private paths by default.
- Package rebuilds, installer rebuilds, release publication, asset replacement, tags, releases, `main` merges, production readiness, or go-live.

## Slice 2: Upgrade-Path Strategy

Goal: define the upgrade-path strategy after the `v0.1.0-dev.3` baseline installer rollback finding, without changing historical assets or claiming upgrade success prematurely.

Expected files or areas:

- Release or roadmap documentation under `docs/release/` and `docs/roadmap/`.
- README or prerelease guidance if user-facing historical upgrade guidance changes.
- No runtime or installer code unless a later separate implementation slice is approved.

Acceptance criteria:

- The strategy explains that true upgrade from the published `v0.1.0-dev.3` installer remains blocked by the baseline installer rollback.
- The strategy decides whether future upgrade evidence starts from a later cleanly installable prerelease baseline.
- The strategy defines whether affected historical users should uninstall/reinstall, use the ZIP path, or follow another explicit migration path.
- The strategy defines evidence needed before any future upgrade success claim.
- Historical release assets remain unchanged and preserved.

Validation commands:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Evidence to collect:

- Documentation diff summary.
- Static, Windows build, and packaged static validation results.
- Explicit confirmation that old release assets, tags, releases, and `main` were untouched.

Risks:

- Guidance could be read as changing the historical `v0.1.0-dev.3` asset.
- Upgrade success wording could exceed available evidence.

Out of scope:

- Rebuilding, replacing, deleting, or republishing old installers.
- Running a true upgrade evidence pass without a separately authorised candidate.
- Release publication, signing, bootstrapping, MSIX, Store, winget, production readiness, or go-live.

## Slice 3: Installer Shortcut/Task Behaviour Refinement

Goal: refine installer shortcut, Start Menu, Desktop shortcut, optional file association, and task behaviour based on prerelease feedback and prior clean install observations.

Expected files or areas:

- `installer/inno/LensDocsStudio.iss` if implementation is approved.
- Windows installer scripts under `scripts/windows/`.
- Installer evidence documentation under `docs/release/`.
- README and Windows roadmap installer guidance.

Acceptance criteria:

- Start Menu shortcut behaviour is documented and, if changed, validated.
- Optional Desktop shortcut behaviour remains explicit and predictable.
- Optional file association task remains default-safe and per-user.
- Silent install flags such as `/NOICONS` and `/TASKS=` have documented expectations.
- Uninstall cleanup expectations for installer-owned files, shortcuts, and Lens-owned registry entries are clear.
- WebView2 user data retention remains documented so browser-local state is not silently deleted.

Validation commands:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`
- Installer build and clean install/uninstall evidence only if installer implementation is approved.

Evidence to collect:

- Installer source diff summary.
- Build, static asset, installer build, install, uninstall, shortcut, and file association observations when applicable.
- Explicit old-asset immutability confirmation.

Risks:

- Silent install flags may be misunderstood across Inno Setup behaviours.
- Installer checks can create local machine state requiring careful cleanup.

Out of scope:

- Production signing, machine-wide installation, runtime bootstrapping, auto-update, MSIX, Store, winget, release publication, or old asset replacement.

## Slice 4: Support Bundle UX Refinements

Goal: refine support bundle UX from real prerelease feedback while preserving the Phase 3BE local, explicit, preview-first, privacy-bounded model.

Expected files or areas:

- Diagnostics/support bundle UI services under `assets/scripts/ui/`.
- Safe support bundle builder or related utilities under `assets/scripts/`.
- Browser tests for support bundle preview, copy, export, redaction, and failure states.
- README, tool guide, release guidance, or support bundle evidence documentation.
- `service-worker.js` if new cacheable modules are added.

Acceptance criteria:

- Preview wording and field labels remain clear.
- Copy/export affordances stay disabled until a preview exists.
- Export filenames remain neutral and avoid private workspace names.
- Success/failure messages remain bounded.
- Safe missing fields may be added only through allowlisted operational metadata.
- Document contents, imported ZIP contents, full private paths by default, secrets, raw stacks, screenshots by default, browser storage, WebView2 user data, telemetry, and uploads remain excluded.

Validation commands:

- `npm run test:static`
- `npm run test:browser`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Evidence to collect:

- Static, browser, Windows build, and packaged static results.
- Before/after UX notes or screenshots where useful.
- Redaction and privacy-boundary test summary.
- Confirmation that no upload or telemetry path was added.

Risks:

- Small UX additions could expand data collection unintentionally.
- Users could misread local export as support submission.

Out of scope:

- Automatic upload, telemetry, ticket creation, accounts, external services, raw logs, screenshots by default, document content, release publication, or production support claims.

## Slice 5: Diagnostics Export/Copy Refinements

Goal: refine Diagnostics copy/export text, fields, and success/failure feedback from prerelease feedback without broad telemetry or unsafe data capture.

Expected files or areas:

- Diagnostics UI and copy/export actions under `assets/scripts/ui/`.
- Native bridge, native picker, package, installer, watcher, and prerequisite diagnostic state sources where already available.
- Browser tests for fake WebView2 and diagnostics states.
- README, tool guide, prerelease guidance, or release notes where user guidance changes.

Acceptance criteria:

- Copied summaries use concise safe operational fields.
- Bridge, route, native picker, package, installer, watcher, and prerequisite labels remain readable.
- Redaction wording is clear.
- Local-only/no-upload behaviour remains explicit.
- Failure messages are bounded and useful.
- Raw exceptions, full private paths, document contents, browser storage, and WebView2 user data remain excluded by default.

Validation commands:

- `npm run test:static`
- `npm run test:browser`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Evidence to collect:

- Static, browser, Windows build, and packaged static validation results.
- Before/after examples of safe copied/exported diagnostics.
- Privacy exclusion confirmation.

Risks:

- Diagnostics output can become log-like if fields are not bounded.
- Browser and packaged modes may require different wording to avoid misleading users.

Out of scope:

- Automatic telemetry, raw log export, support submission, installer rebuilds, release publication, production readiness, or go-live.

## Slice 6: Release Provenance Simplification

Goal: simplify future release provenance wording where tag, evidence, source, package, and publication commits differ, while preserving exact auditability.

Expected files or areas:

- Release templates and release guidance under `docs/release/`.
- README, roadmap, and prerelease guidance summaries where release evidence wording is repeated.
- No runtime code.

Acceptance criteria:

- Future release records can clearly identify tag target, frozen source/package commit, publication evidence commit, publication documentation commit, post-publication verification commit, asset names, checksums, and evidence status.
- Simplified wording does not hide commit differences.
- Historical release records remain intact unless a separately approved documentation clarification is made.
- Existing assets, tags, and releases are not changed.

Validation commands:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Evidence to collect:

- Documentation diff summary.
- Static, Windows build, and packaged static validation results.
- Confirmation that no tags, releases, release assets, or `main` merge were touched.

Risks:

- Over-simplified wording could reduce auditability.
- Historical records could be misread as changed release evidence.

Out of scope:

- Editing, deleting, replacing, re-uploading, rebuilding, or republishing historical assets.
- Creating tags, releases, release assets, or release notes for a new prerelease.

## Slice 7: Prerelease Guidance Maintenance

Goal: keep prerelease guidance current as feedback and future planning evolve, while preserving historical evidence and avoiding release approval claims.

Expected files or areas:

- `docs/release/lens-docs-studio-prerelease-guidance.md`
- README
- Roadmap/backlog documents under `docs/roadmap/`
- Release closure references under `docs/release/`

Acceptance criteria:

- `v0.1.0-dev.4` remains identified as the current verified prerelease/dev release until a later prerelease is actually published and verified.
- `v0.1.0-dev.5` remains candidate/readiness planning only until separately approved.
- Older prerelease evidence remains historically preserved.
- No production readiness, stable/latest, go-live, or `main` promotion claim is introduced.

Validation commands:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Evidence to collect:

- Documentation diff summary.
- Static, Windows build, and packaged static validation results.
- Explicit release asset, tag, release, and `main` immutability confirmation.

Risks:

- Guidance can drift from actual published release state.
- Users may misinterpret planning documents as release approval.

Out of scope:

- Publishing, editing, deleting, replacing, re-uploading, or republishing assets.
- Creating tags or releases.
- Stable/latest positioning, production readiness, or go-live.

## Validation Matrix

| Change type | Minimum validation | Additional validation |
| --- | --- | --- |
| Documentation-only planning | `npm run test:static` | Windows build and packaged static asset validation when planning touches Windows distribution guidance. |
| Helper/checklist script | `npm run test:static`, helper self-test or dry-run, `dotnet build src/windows/LensDocsStudio.Windows.sln`, packaged static asset validation when Windows scripts are touched | Package/native validation if script or package behaviour changes. |
| Runtime UI, Diagnostics, support bundle, import/export, or rendering changes | `npm run test:static`, `npm run test:browser` | Windows build and packaged static asset validation when packaged runtime files are affected. |
| Windows shell or native bridge behaviour | Static checks, browser smoke/regression coverage, Windows solution build, packaged static asset validation | Native bridge smoke and real packaged manual evidence where native picker or watcher UI must be observed. |
| Installer-affecting implementation | Static checks, Windows build, packaged static asset validation, installer build evidence as approved | Clean install, uninstall, shortcut, file association, upgrade, checksum, and report evidence before any publication decision. |
| Release publication | All approved implementation and artefact gates plus release preparation, checksum verification, release notes review, and explicit publication approval | Post-publication verification after upload. |

For Phase 3BO itself, run:

- `npm run test:static`
- `dotnet build src/windows/LensDocsStudio.Windows.sln`
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`

Browser tests, package rebuilds, installer rebuilds, release commands, tag commands, release asset operations, and manual packaged smoke are intentionally skipped for Phase 3BO because the work is documentation/planning only and does not change runtime code, helper scripts, package output, installer output, or release artefacts.

## Evidence Requirements

Each future implementation slice should record:

- Branch, baseline commit, and tracked/ignored repository state before edits.
- Files changed and whether runtime code changed.
- Acceptance criteria mapping.
- Validation commands, results, and skipped validation with reason.
- Script dry-run or local evidence-template output when helper tooling changes.
- Browser test evidence when runtime UI, support bundle, Diagnostics, import/export, rendering, layout, clipboard, theme, or app workflow changes.
- Package/native validation when packaged behaviour, Windows shell behaviour, installer scripts, or native bridge behaviour changes.
- Manual evidence only when a human actually performs and records the checks.
- Confirmation that release assets, tags, GitHub Releases, `main`, production readiness, and go-live approval were not changed unless a later phase explicitly authorises them.

## Release Artefact Expectations

This readiness gate creates no release artefacts and changes no published release assets.

If a later phase approves `v0.1.0-dev.5` publication, it must separately define:

- Release tag and target commit.
- Frozen source/package commit.
- ZIP and installer artefact expectations.
- Checksums, reports, release notes, and publication evidence.
- Manual or automated evidence required before upload.
- Post-publication verification expectations.
- Historical guidance for older prereleases.
- Supersedence wording for `v0.1.0-dev.4`, `v0.1.0-dev.3`, `v0.1.0-dev.2`, and `v0.1.0-dev.1`.

Routine supersedence documentation must not edit, delete, replace, re-upload, rebuild, republish, or reclassify old release assets.

## Rollback And Supersedence

For implementation slices:

- Prefer small commits that can be reverted independently.
- Keep documentation, tests, helper scripts, runtime changes, and installer changes separated where practical.
- If a helper script collects unsafe data, implies native picker automation, or overstates manual evidence, revert or supersede the slice on `develop` before considering any artefact build.
- If a runtime slice introduces unsafe diagnostics, privacy risk, misleading release wording, or user-facing confusion, revert or supersede the slice on `develop` before considering any artefact build.

For releases:

- A future `v0.1.0-dev.5` prerelease, if authorised, published, and post-publication verified, would supersede `v0.1.0-dev.4` for new validation through explicit release notes and documentation.
- Supersedence does not require editing or deleting `v0.1.0-dev.4`, `v0.1.0-dev.3`, `v0.1.0-dev.2`, or `v0.1.0-dev.1` assets.
- Any release asset correction, replacement, removal, or republishing requires a separate authorised release-maintenance decision.

## First-Slice Execution Prompt

Use this prompt only after manual packaged sanity helper implementation is explicitly approved:

```text
You are working in C:\Code\MarkdownReader on branch develop.

Objective:
Implement the first v0.1.0-dev.5 slice: approved manual packaged sanity helper/checklist generator for Lens Docs Studio packaged Windows verification.

Context:
- v0.1.0-dev.4 is the current verified prerelease/dev release for new validation.
- docs/testing/lens-docs-studio-manual-packaged-sanity-helper-plan.md defines the Phase 3BF design/planning-only helper approach.
- docs/roadmap/lens-docs-studio-v010-dev5-implementation-readiness.md recommends manual packaged sanity helper implementation as the first slice only if explicitly approved.
- The helper supports human verification of packaged onboarding, Diagnostics, native Open folder picker selection/cancellation, support bundle preview/copy/export, support bundle privacy boundaries, watcher/conflict UX, and installer install/launch/uninstall checks when relevant.
- This is helper/checklist tooling only. It is not runtime product implementation and not release approval.

Constraints:
- Work directly on develop.
- Do not add runtime product shortcuts, debug-only workspace injection, localStorage setup bypasses, command-line workspace bypasses, or native picker bypasses.
- Do not fake, automate, or bypass the real Windows folder picker. The helper may instruct the human to use it, but must not select folders for the app or claim picker observation automatically.
- Do not collect document contents, imported ZIP contents, screenshots by default, browser storage, WebView2 user data, raw logs, raw stack traces, secrets, tokens, connection strings, emails, usernames, machine names, private names, raw personally identifiable information, or full private paths.
- Create only temporary synthetic workspaces for checklist use.
- Output a local evidence template with PASS/FAIL/BLOCKED fields and bounded operator notes.
- Keep generated evidence, support bundles, package downloads, installer outputs, logs, screenshots, WebView2 data, and temporary workspaces out of git.
- Do not edit, delete, replace, re-upload, rebuild, or republish existing release assets.
- Do not create tags or releases.
- Do not merge to main.
- Do not claim production readiness, go-live approval, stable-channel certification, stable/latest positioning, or public production rollout.
- Use British English for first-party docs, comments, logs, generated copy, examples, templates, and snippets.

Tasks:
1. Confirm branch, Phase 3BN/3BO baseline history, tracked status, and ignored/generated paths before editing.
2. Inspect the helper plan, existing package/native smoke scripts, package RC checklist, installer evidence plan, support bundle implementation evidence, Diagnostics copy/export evidence, payload hygiene evidence, and dev.5 roadmap docs.
3. Implement the smallest approved helper shape:
   - create a temporary synthetic workspace only;
   - generate or print manual checklist steps;
   - output a local evidence template with scenario rows and PASS/FAIL/BLOCKED fields;
   - avoid reading workspace document contents;
   - avoid storing full private paths by default;
   - avoid native picker bypass or UI automation claims.
4. Add or update documentation for running the helper.
5. Add tests or static validation coverage if a helper script is added.
6. Run validation appropriate to the blast radius, at minimum:
   - npm run test:static
   - dotnet build src/windows/LensDocsStudio.Windows.sln
   - pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1
7. Run package/native validation if script/package behaviour changes.
8. Do not run browser tests, package rebuilds, installer rebuilds, release commands, or manual smoke unless the approved scope explicitly requires them.
9. Stage only intentional docs/script/test changes, commit, and push to origin/develop.

Suggested commit message:
docs: add manual packaged sanity helper

Final response must include:
- Branch
- Commit hash
- Push result
- Whether runtime code changed or helper/docs only
- Helper/checklist output path or command
- Docs/tests updated
- Validation results
- Skipped validation and reason
- Confirmation that no release assets, tags, releases, or main merges were changed
- Confirmation that existing prerelease assets were not changed
- Confirmation that production readiness/go-live was not claimed
```

## Readiness Verdict

Phase 3BO approves only this implementation readiness plan for a possible `v0.1.0-dev.5` cycle. It does not approve runtime implementation, helper implementation, release publication, package rebuilds, installer rebuilds, release asset changes, tags, releases, a merge to `main`, production readiness, or go-live.

The recommended first implementation slice is manual packaged sanity helper implementation, if explicitly approved.

`v0.1.0-dev.5` is not approved, has no committed release date, and remains separate from production readiness/go-live approval.
