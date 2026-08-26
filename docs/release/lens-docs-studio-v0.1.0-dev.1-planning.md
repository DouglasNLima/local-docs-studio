# Lens Docs Studio v0.1.0-dev.1 Planning

## Context

- Current prerelease: `Lens Docs Studio v0.1.0-dev.1`
- Current release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.1`
- Current tag: `v0.1.0-dev.1`
- Frozen source commit: `c776fffdc8364c2c978f8106869a2d6bf50477ff`
- Post-publication verification commit: `256a7af04e4f8d6775c42da855d5732fa574a31b`
- Planning date: `2026-06-10`
- Repository: `DouglasNLima/local-docs-studio`
- Branch policy: work directly on `develop`; do not modify published release assets unless a future release is explicitly authorised.

## Feedback Sources Reviewed

- GitHub Issues: `gh issue list --repo DouglasNLima/local-docs-studio --state open --limit 100` returned `0` open issues.
- GitHub prerelease search: prerelease, installer, ZIP, WebView2, setup, workspace, and watcher search returned `0` matching issues.
- Known issues: `docs/release/lens-docs-studio-v0.1.0-dev-known-issues.md`.
- Feedback intake template: `.github/ISSUE_TEMPLATE/lens-docs-studio-prerelease-feedback.md`.
- Phase 3K/3O notes: draft release review, installer RC asset plan, and publication record.
- Local release docs: `docs/release/lens-docs-studio-v0.1.0-dev-feedback-intake.md`, `docs/release/lens-docs-studio-v0.1.0-dev-publication-record.md`, and related Windows release notes.

No public issue feedback was available at planning time, so this triage treats the documented prerelease caveats and prior RC notes as the current input set.

## Triage Summary

| Item | Source | Category | Severity | Decision | Notes |
| --- | --- | --- | --- | --- | --- |
| No public prerelease issues filed | GitHub Issues | Feedback intake | Low | Accepted caveat | Continue monitoring after prerelease publication. |
| WebView2 user data may remain after uninstall | Known issues, Phase 3M/3Q notes | Uninstall | Medium | Documentation-only for `v0.1.0-dev.1` | Phase 3S chose `WEBVIEW2_UNINSTALL_CLEANUP_DOCUMENTED_ONLY` because the WebView2 folder may contain browser-local user/session state. |
| Installer is unsigned | Known issues, publication record | Installer | Medium | Accepted caveat | Signing remains out of scope for the next dev prerelease. Keep warnings explicit. |
| Runtime prerequisites are external | Known issues, publication record | Runtime prerequisites | Medium | Documentation-only | Clarify ZIP versus installer expectations and prerequisite ownership; do not add bootstrappers in this cycle. |
| File associations are optional/default-safe | Known issues, setup notes | File associations | Low | Accepted caveat | Keep opt-in behaviour. Revalidate documentation wording before the next publication. |
| Watcher/conflict UX had `PASS_WITH_NOTES` | Phase 3K draft release review | Workspace watcher | Medium | Addressed for `v0.1.0-dev.1` | Phase 3AA completed packaged Stage A and Stage B evidence before publication. |
| ZIP versus installer wording may need clearer separation | Publication record, known issues | Documentation | Low | Documentation-only | Release notes and docs should distinguish portable fallback from unsigned installer path. |
| Issue template uses `prerelease-feedback` label | Issue template | Feedback triage | Low | Defer | If issue volume appears, add a fuller label taxonomy in a separate GitHub hygiene step. |

## Decision Buckets

### Must fix for v0.1.0-dev.1

No must-fix runtime or packaging items are identified from available feedback.

Before Phase 3S starts, repeat the GitHub issue search. Any new blocker, checksum mismatch, installer launch failure, data-loss report, or install/uninstall regression should become a must-fix item.

### Should fix for v0.1.0-dev.1

- Document the WebView2 uninstall caveat and manual clean-uninstall path without silently deleting local browser state.
- Add a fully manual human-only watcher/conflict UX evidence checklist for a packaged Windows shell, covering changed, deleted, renamed, dirty, and dirty-external-conflict states.
- Tighten release-note wording that helps testers choose between the ZIP fallback and unsigned installer.

### Documentation-only

- Keep external runtime prerequisite guidance prominent for both ZIP and installer users.
- State that the installer is unsigned and may trigger Windows SmartScreen or browser warnings.
- State that file associations are optional/default-safe and should remain unchecked unless the user opts in.
- State that ZIP remains the portable fallback and is still a prerelease asset.

### Defer to v0.1.0-rc.1 or later

- Code signing.
- Auto-update.
- MSIX.
- Store or winget publication.
- Runtime bootstrapper.
- WebView2 bootstrapper or Fixed Version Runtime.
- Major setup or installer redesign.
- Broad issue label automation or active GitHub project triage.
- Main branch merge or stable/latest release positioning.

### Accepted caveats after publication

- `v0.1.0-dev.1` is published and remains a prerelease.
- Installer remains unsigned.
- Runtime prerequisites remain external.
- ZIP remains the fallback package.
- File associations remain optional/default-safe.
- Post-publication verification passed.
- Stage A and Stage B packaged evidence passed.
- The older `v0.1.0-dev` assets remain older/stale relative to `v0.1.0-dev.1` and were not modified.
- No release assets require further action unless a future release is authorised.

## Recommended Scope for v0.1.0-dev.1

Proceed with a small targeted `v0.1.0-dev.1` cycle only if Phase 3S can stay low risk:

- Keep WebView2 uninstall cleanup documented-only unless the data ownership boundary becomes clear enough for an explicit safe cleanup option.
- Prefer documentation clarifications over runtime changes where feedback shows confusion rather than defects.
- Use manual packaged-shell evidence to decide whether watcher/conflict UX needs code changes or only release confidence notes.
- Avoid expanding scope beyond the documented prerelease caveats unless new GitHub feedback appears.

## Phase 3V Runtime Fix Planning Note

Phase 3V found that the interactive **Open folder** UI could fall through to browser fallback picker behaviour in a packaged WebView2 shell when the `workspace.openFolder` capability probe was missing, stale, or timed out. That explains why automated native smoke could pass while manual packaged UI evidence was blocked at the picker route.

The fix changes packaged static runtime code:

- Open folder now probes `workspace.openFolder` immediately before routing.
- In WebView2, Open folder calls the native bridge only when the capability is available.
- In WebView2, missing capability or bridge failure produces clear operator guidance instead of opening the browser folder/file-input fallback.
- Windows shell diagnostics now show the same route decision and include **Retry bridge check**.

Phase 3AA later completed packaged diagnostics, real native picker selection, and Stage B watcher/conflict evidence. Because runtime code changed, `v0.1.0-dev.1` was not docs-only and required fresh ZIP and installer artefacts before publication.

## Explicitly Out of Scope for v0.1.0-dev.1

- Stable release.
- Signing.
- Auto-update.
- MSIX.
- Store/winget.
- Runtime bootstrapper.
- WebView2 bootstrapper.
- Major UI redesign.
- Main branch merge.
- Modifying the published `v0.1.0-dev` release.
- Replacing, deleting, or uploading assets for `v0.1.0-dev`.
- Creating or moving tags outside an authorised release phase.

## Acceptance Criteria

- Existing static tests pass.
- Windows package RC gate passes if package/release artefacts change.
- Inno installer build passes if installer artefacts change.
- Native smoke passes if Windows shell or packaged artefacts change.
- Manual watcher/conflict evidence is recorded if watcher confidence remains part of the scope.
- Release notes/checksums are updated if new assets are published.
- No regression in ZIP fallback.
- No regression in installer install/uninstall path.
- `v0.1.0-dev.1` remains prerelease and does not become stable/latest.

## Post-v0.1.0-dev.1 Next Work

- Collect user feedback from prerelease usage.
- Monitor installer and download issues.
- Plan the next dev release scope from observed feedback and known limitations.
- Decide later whether any release should be promoted, superseded, or archived.
- Keep production readiness as a separate approval gate.

## Proposed Labels

Do not create labels during Phase 3R. If issue volume starts, use this candidate set:

- `prerelease-feedback`
- `installer`
- `zip-package`
- `windows-shell`
- `native-bridge`
- `workspace`
- `watcher`
- `setup-wizard`
- `documentation`
- `blocked`
- `v0.1.0-dev.1`

## Proposed Phase Sequence

- Phase 3S: Implement `v0.1.0-dev.1` targeted fixes.
- Phase 3T: Create the `v0.1.0-dev.1` certification plan and decide whether new ZIP/installer assets are needed.
- Phase 3U/3AF: Publish and verify `v0.1.0-dev.1` prerelease.
- Phase 3AG: Close release documentation and rebaseline the roadmap.

## Recommendation

`v0.1.0-dev.1` is now published as a prerelease and passed post-publication verification. Continue with feedback collection, installer/download monitoring, and next dev-release planning. Keep production readiness, promotion, supersession, or archival decisions separate from this prerelease closure.
