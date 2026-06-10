# Lens Docs Studio Watcher/Conflict Manual Evidence

Status: Required evidence checklist for `v0.1.0-dev.1` certification.

This document records the human-only Windows picker pass for native workspace watcher and conflict behaviour. It must be completed against a packaged Windows shell or installed prerelease build before publishing `v0.1.0-dev.1`.

Do not mark items as passed unless a tester performed the action through the real Windows folder picker and observed the result manually. Automated browser tests and native smoke tests are supporting evidence, not a substitute for this checklist.

## Test Context

| Field | Value |
| --- | --- |
| Tester | Codex attempted packaged launch; human-only UI observation not completed. |
| Date | 2026-06-10 |
| Build or installer asset | `LensDocsStudio.Windows-0.1.0-dev.zip` from the published `v0.1.0-dev` prerelease |
| Commit SHA | `7f8d6f048312c338844ed75440861739c9b10992` |
| Windows version | Microsoft Windows 11 Home `10.0.26200` |
| Install method | Extracted ZIP app from the published prerelease |
| Workspace path type | Temporary local folder under `%TEMP%\LensDocsStudio-WatcherManual` |

## Phase 3U Attempt

Phase 3U used the existing published ZIP asset without modifying the `v0.1.0-dev` release, tags, or assets.

- Release checked: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev`
- Asset source: `LensDocsStudio.Windows-0.1.0-dev.zip`
- Extracted packaged app: `%TEMP%\LensDocsStudio-v0.1.0-dev-release\extracted\LensDocsStudio.Windows.exe`
- Manual workspace: `%TEMP%\LensDocsStudio-WatcherManual`
- Test file: `watcher-evidence.md`
- Packaged launch result: the Windows process started from the extracted prerelease ZIP.

The required human-only watcher/conflict observations were not completed in this environment. The app launch and workspace preparation are supporting setup evidence only; they do not satisfy the manual picker checklist below.

## Checklist

- [ ] Open Lens Docs Studio in the Windows shell.
- [ ] Use **File > Open folder** and choose a real Windows folder picker workspace.
- [ ] Confirm the workspace browser loads supported Markdown, Mermaid, or text files with relative paths only.
- [ ] Open a Markdown file from the selected workspace.
- [ ] With no local edits, modify that file externally in another editor.
- [ ] Confirm the external clean change marker or status appears.
- [ ] Use **Refresh active file**.
- [ ] Confirm the editor reloads the external clean change.
- [ ] Modify the same file externally again.
- [ ] Make a local dirty edit in Lens Docs Studio before refreshing.
- [ ] Confirm the dirty/external-conflict marker or status appears.
- [ ] Cancel **Refresh active file**.
- [ ] Confirm the local dirty edit remains in the editor.
- [ ] Use **Refresh active file** again and confirm the prompt.
- [ ] Confirm refresh.
- [ ] Confirm the editor reloads the external content and the local dirty edit is discarded only after confirmation.
- [ ] Delete the active workspace file externally.
- [ ] Confirm Lens Docs Studio indicates the deleted state and does not silently erase the editor content.
- [ ] Rename a workspace file externally.
- [ ] Confirm Lens Docs Studio indicates or handles the rename safely, without exposing absolute paths or discarding unsaved edits.
- [ ] Reopen or switch files as needed and confirm the workspace remains usable after changed, deleted, and renamed events.

## Verdict

Result: **MANUAL_WATCHER_CONFLICT_BLOCKED**

The manual human-only watcher/conflict pass remains blocked because the required observation steps were not completed through the real packaged Windows UI and folder picker. The `v0.1.0-dev.1` watcher/conflict certification blocker is not cleared.

Scenario results:

| Scenario | Result | Notes |
| --- | --- | --- |
| Clean external change | BLOCKED | Packaged app launch was prepared, but the external-change marker and explicit refresh behaviour were not manually observed. |
| Dirty conflict cancel | BLOCKED | The local dirty edit preservation path was not manually observed. |
| Dirty conflict confirm | BLOCKED | The explicit confirmation reload path was not manually observed. |
| External delete | BLOCKED | The deleted-state marker and content-preservation behaviour were not manually observed. |
| External rename | BLOCKED | Rename handling was not manually observed. |

## Notes

- Phase 3S added this checklist only; it did not fake manual evidence.
- Phase 3T carried this requirement forward in `docs/release/lens-docs-studio-v0.1.0-dev.1-certification-plan.md`; it still must not be marked passed without a real packaged Windows picker pass.
- Phase 3U confirmed the published ZIP could be downloaded, extracted, and launched, but did not complete the human-only UI evidence. A future tester must still perform the checklist and update this verdict to `MANUAL_WATCHER_CONFLICT_PASS`, `MANUAL_WATCHER_CONFLICT_PASS_WITH_NOTES`, or `MANUAL_WATCHER_CONFLICT_FAILED`.
- Supporting automated coverage remains the native bridge smoke and focused browser regressions for watcher/conflict behaviour.
- If any checklist item fails, capture exact steps, screenshots where useful, affected file state, and whether content was preserved before deciding whether the issue blocks `v0.1.0-dev.1`.
