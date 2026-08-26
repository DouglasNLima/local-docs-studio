# Lens Docs Studio Prerelease Feedback Intake

Planning date: 2026-06-11

Use this guide to collect, classify, and triage feedback from `v0.1.0-dev.1` prerelease users before deciding whether any item belongs in the candidate `v0.1.0-dev.2` scope.

`v0.1.0-dev.1` is a published prerelease/dev release. It is not a stable release, not production readiness approval, and not go-live approval.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.1`

## Collection Channels

Collect feedback through repository issues, direct tester notes, release-monitoring notes, or structured manual smoke reports.

Prefer one issue or record per distinct problem. When one report includes several independent symptoms, split them during triage so installer, runtime prerequisite, bridge, folder-picker, watcher/conflict, and documentation issues can be evaluated separately.

## Recommended Issue Categories

- Installation
- ZIP package
- Installer
- Runtime prerequisites
- Launch
- First-run setup
- Native bridge diagnostics
- Folder picker
- File open/save
- Workspace open/save
- Watcher/conflict UX
- File associations
- Uninstall
- Documentation/release notes
- Stale older prerelease guidance
- Troubleshooting/support bundle request
- Security/privacy concern
- Other

## Minimum Reproduction Details

Ask reporters to provide:

- Short summary.
- Category.
- Severity.
- Exact release or asset used.
- Whether SHA256 was checked.
- Install method: ZIP, installer, development run, browser, GitHub Pages, or local static server.
- Steps to reproduce.
- Expected behaviour.
- Actual behaviour.
- Whether the issue happened once or is repeatable.
- Whether restarting the app or reinstalling changed the result.
- Whether the issue blocks prerelease adoption.

## Required Environment Details

Ask for only the environment details needed to reproduce or classify the issue:

- Windows version and edition where relevant.
- Browser name and version for browser/PWA reports.
- Install path type: default installer path, custom installer path, extracted ZIP folder, or browser mode.
- .NET Desktop Runtime installed: yes, no, or unsure.
- Windows App SDK Runtime installed: yes, no, or unsure.
- Evergreen WebView2 Runtime installed: yes, no, or unsure.
- Asset names used, especially ZIP versus installer.
- Whether the older `v0.1.0-dev` or current `v0.1.0-dev.1` prerelease was used.

Avoid asking for machine names, usernames, absolute private folder paths, customer names, private repository names, or document contents unless a maintainer explicitly determines that a redacted value is required.

## Expected Logs And Screenshots

Useful supporting evidence:

- Screenshot of the visible error, warning, setup wizard state, diagnostics result, or conflict prompt.
- Copy of safe status text shown by Lens Docs Studio.
- Checksum command and output for the downloaded asset.
- Installer log excerpt with private paths redacted.
- Windows bridge diagnostics result from **Help > Check Windows bridge**.
- File association script output with private paths redacted.
- Uninstall observations, including whether shortcuts, Lens-owned registry entries, or install folders remain.
- Minimal test Markdown or Mermaid content when the issue depends on a small reproducible file.

For watcher/conflict issues, capture the file state sequence:

- Initial clean or dirty state.
- External action: changed, created, deleted, or renamed.
- Lens Docs Studio marker/status observed.
- Refresh, save, discard, or cancel action taken.
- Whether editor content was preserved.

## What Not To Collect

Do not collect:

- Secrets.
- Access tokens.
- API keys.
- Passwords.
- Customer data.
- Private documents.
- Private repository contents.
- Personally identifiable information.
- Full local folder trees.
- Machine names.
- Usernames.
- Absolute private paths unless explicitly redacted.
- Browser-local storage dumps.
- WebView2 user data folders.
- Full installer logs containing unredacted private paths.
- Imported artefact bundle contents that include confidential evidence or candidate findings.

Troubleshooting/support bundle work, if later approved, must be opt-in and should collect safe metadata by default. It must not include document contents, private paths, secrets, or personally identifiable information unless the user explicitly selects and reviews those contents.

## Triage Labels

Recommended type labels:

- `type:bug`
- `type:documentation`
- `type:diagnostics`
- `type:installer`
- `type:feedback`
- `type:question`

Recommended area labels:

- `area:first-run`
- `area:bridge`
- `area:folder-picker`
- `area:watcher-conflict`
- `area:installer`
- `area:zip`
- `area:uninstall`
- `area:file-associations`
- `area:release-docs`
- `area:privacy`

Recommended severity labels:

- `severity:blocker`
- `severity:high`
- `severity:medium`
- `severity:low`

Recommended release-decision labels:

- `decision:must-have-candidate`
- `decision:should-have-candidate`
- `decision:defer`
- `decision:documentation-only`
- `decision:accepted-caveat`
- `decision:not-reproducible`

## Triage Statuses

- New: reported but not reviewed.
- Needs details: cannot classify without missing reproduction or environment details.
- Reproducing: maintainer is attempting to reproduce.
- Confirmed: behaviour is reproduced or supported by enough evidence.
- Candidate for v0.1.0-dev.2: may enter candidate scope after review.
- Deferred: valid but outside the next small prerelease cycle.
- Documentation only: handled through guidance, notes, or known caveats.
- Accepted caveat: known prerelease limitation with no current fix planned.
- Closed: resolved, duplicate, invalid, or no further action.

## Mapping To v0.1.0-dev.2 Candidate Scope

Map feedback into `docs/roadmap/lens-docs-studio-v010-dev2-candidate-scope.md` only after triage.

Candidate mapping rules:

- First-run confusion maps to onboarding copy polish.
- Native bridge or folder-picker ambiguity maps to diagnostics visibility.
- Repeated requests for shareable diagnostics map to optional troubleshooting/support bundle design.
- Watcher/conflict misunderstanding maps to UX copy refinement or manual evidence requirements.
- Installer upgrade, uninstall, shortcut, file association, or prerequisite issues map to installer evidence planning.
- Reports about older `v0.1.0-dev` confusion map to stale older prerelease documentation guidance.
- Data-loss, data-corruption, installer failure, bridge failure, or watcher/conflict correctness concerns may require escalation before scope approval.

Do not add an item to `v0.1.0-dev.2` solely because it is interesting. It should have a feedback signal, known-risk basis, or release-monitoring need.

## Escalation Criteria

Escalate before ordinary scope triage when feedback indicates:

- Possible data loss, document overwrite, silent discard, or corrupted saved content.
- Watcher/conflict behaviour that erases dirty editor content or misleads the user into losing work.
- Native bridge operations crossing the selected workspace boundary.
- Folder picker failure that prevents normal Windows shell usage.
- Installer cannot install, cannot uninstall, leaves unexpected Lens-owned registry state, or breaks an existing installation.
- File associations take ownership unexpectedly, write machine-wide keys, or affect unrelated applications.
- Runtime prerequisite guidance is materially wrong or sends users to an unsafe path.
- Checksum mismatch for a published asset.
- Reported release asset mismatch, missing asset, or suspicious download behaviour.
- Any secret, token, customer data, private document, or personally identifiable information is accidentally attached.

Escalated items should be isolated from normal feedback records, redacted where necessary, and reviewed before any public triage summary is written.

## Suggested Issue Body

```markdown
## Summary

## Environment

- Release used: v0.1.0-dev.1 / other:
- Asset used: ZIP / installer / browser / GitHub Pages / local static server:
- SHA256 checked: yes / no / unsure:
- Windows version:
- Browser and version, if browser/PWA:
- .NET Desktop Runtime installed: yes / no / unsure:
- Windows App SDK Runtime installed: yes / no / unsure:
- Evergreen WebView2 Runtime installed: yes / no / unsure:

## Category

Installation / ZIP package / Installer / Runtime prerequisites / Launch / First-run setup / Native bridge diagnostics / Folder picker / File open-save / Workspace open-save / Watcher-conflict UX / File associations / Uninstall / Documentation-release notes / Stale older prerelease guidance / Troubleshooting-support bundle request / Security-privacy concern / Other

## Severity

Blocker / High / Medium / Low / Question-feedback

## Steps To Reproduce

1.
2.
3.

## Expected Behaviour

## Actual Behaviour

## Attachments

Screenshots, safe status text, checksum output, or redacted logs if available.

## Privacy Check

I have removed secrets, tokens, customer data, private documents, private paths, and personally identifiable information: yes / no / unsure

## Adoption Impact

Does this block prerelease adoption? yes / no / unsure
```

## Intake Verdict

This intake process supports feedback triage for a possible `v0.1.0-dev.2` prerelease/dev release. It does not approve a new release, create or modify release assets, create tags or releases, merge to `main`, or claim production readiness/go-live approval.
