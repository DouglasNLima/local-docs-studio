# Lens Docs Studio Watcher/Conflict Manual Evidence

Status: Required evidence checklist for `v0.1.0-dev.1` certification.

This document records the human-only Windows picker pass for native workspace watcher and conflict behaviour. It must be completed against a packaged Windows shell or installed prerelease build before publishing `v0.1.0-dev.1`.

Do not mark items as passed unless a tester performed the action through the real Windows folder picker and observed the result manually. Automated browser tests and native smoke tests are supporting evidence, not a substitute for this checklist.

## Test Context

| Field | Value |
| --- | --- |
| Tester | _Not run._ |
| Date | _Not run._ |
| Build or installer asset | _Not run._ |
| Commit SHA | _Not run._ |
| Windows version | _Not run._ |
| Install method | ZIP / installer / development run |
| Workspace path type | Temporary local folder recommended |

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

Result: **NOT RUN**

The manual human-only watcher/conflict pass has not been executed in Phase 3S. It remains required evidence for `v0.1.0-dev.1` certification.

## Notes

- Phase 3S added this checklist only; it did not fake manual evidence.
- Supporting automated coverage remains the native bridge smoke and focused browser regressions for watcher/conflict behaviour.
- If any checklist item fails, capture exact steps, screenshots where useful, affected file state, and whether content was preserved before deciding whether the issue blocks `v0.1.0-dev.1`.
