# Goal VIII-E — owned-window acceptance blocked at manual cleanup

Recorded 25 October 2026. This is a source-labelled blocked handoff for existing
[Draft PR #48](https://github.com/DouglasNLima/local-docs-studio/pull/48).
It records read-only investigation and preservation, without a harness exception
or a new local acceptance attempt. Local Markdown, Mermaid, and documentation
studio.

## Source and original acceptance

The independently checked starting PR head is
`c6bb3102fcb5c5c8fcb50ca4424b37789057bbb5`, tree
`c92d97bc7f4e1096abc333477f882edd2bc1343a`. Remote accepted develop remains
`9b1e73bbf564c53b48149e8c0dcd6fd7acbe308f`; the local develop ref differs and
was neither checked out nor changed. PR #38 remains OPEN / DRAFT / PAUSED at
`31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`.

The [Goal VIII-D evidence](goal-viii-d-operator-assisted-find.md) and its ignored
post-commit delivery receipt retain the actual Find correction: only FindText's
`PreviewKeyDown="FindKeyDown"` binding changed in product source. Historical .880
Enter failed while Next passed. Corrected .891 Enter selected exact UTF-16 `Café`
at start 2, length 4, caret 6 in both native AvalonEdit and independent UIA.
Editor/preview routing and focus checks passed. Independent .891 invalid Mermaid
error/source preservation and valid companion reopening also passed. Those
resolved product questions were not replayed in Goal VIII-E.

Original f05's `ui/shell/completion.json` is **BLOCKED** at `cancel-stale-owner`.
Its seven completed groups remain historical: first launch/Unicode/image/preview;
Quick Open; relative-link pointer; Command Palette; editor/preview Find; native
visual Apply with one-step Undo/Redo/Save; isolated template/snippet. Private
Cancel completed before the later transition, but its combined stale-owner group
did not finish. No external owner mutation or stale-owner Apply occurred.

Actual pane splitters, zoom/Fit/pan, invalid-source acceptance within the complete
sequence, close/restart/persisted panes, Close Workspace and normal final exit
remain incomplete in original f05. Earlier separate successes cannot complete it.

## Original f05 cleanup

At **14:21:20 UTC**, the read-only V2 receipt proved the original process identity:

| Identity | Observed value |
| --- | --- |
| PID / session | 19116 / 3 |
| Process start UTC | 2026-10-25T12:46:46.1911552Z, exact ticks matching original close receipt |
| Executable SHA-256 | `4d5e451ef71a60980c59854274b35cb2fbe63ace0caee87c3b127fa5848cde38` |
| Product version/source | `0.1.1-preview.891+c6bb3102fcb5c5c8fcb50ca4424b37789057bbb5` |
| Executable | Original f05 `payload/LensDocsStudio.Native.exe` |
| Explicit private state | Original f05 `state/ui-shell` |
| Explicit workspace/document | Original f05 `workspace` / `acceptance-lf.md` |
| Main HWND | 6037100; real, visible, enabled WPF window; no Win32 owner |
| Visual HWND | 29626154; real, visible WPF window, title `Mermaid Visual Editor •`; `GW_OWNER` = 6037100 |
| Confirmation HWND | 13570798; real, visible, enabled `#32770`, title `Unsaved Mermaid draft`; `GW_OWNER` = 29626154 |
| Foreground/focus | 13570798 / 41162088; PID 19116; visual editor disabled by the confirmation |

The active non-elevated interactive console and accessible Default input desktop
were observed. The only observed native application process was the exact f05
instance. Its workspace, state, binary identity and command line matched the
original synthetic session. UIA exposed the confirmation's **OK / Cancel**
buttons and exact application-close question. Bounded screenshots contain only
the synthetic visual editor and its confirmation.

The owner was asked to personally identify this exact synthetic window and:

1. Choose Cancel in the OK/Cancel confirmation to abort the requested app close.
2. Use the verified synthetic visual editor's own Cancel command to discard its
   private draft.
3. Close the remaining synthetic application normally.

**Manual confirmation/action remains pending. HOST CLEANUP: BLOCKED.** No
automated click, key, activation, close retry, discard or process termination was
sent. No exact process exit or zero-process cleanup is claimed. Existing f05
Complete requires zero native processes, so it was not invoked while occupied.
Its original shell verdict must remain BLOCKED even after later cleanup.

All 528 f05 payload paths still match. Observed associations including
ProgIDs/UserChoice, registrations, shortcuts and the four existing default
private-state file hashes match the original host-before snapshot. This is a
bounded observation, without an exhaustive filesystem trace or a new personal-file
preservation claim. Saved synthetic source SHA-256 is
`4a2d1b240d8ceb77e56038d2129c7939510a9517faecafe2185e5c7f0c2a8076`.

Three prior evidence indices independently retained all **140 + 88 + 222**
entries. The first 140 and 88 comprise the previously reported 228; these are
manifest entry counts, not a claim of distinct files across indices. Open f05 log
and live observer-stream exclusions remain explicit and unchanged.

## Stale-owner target and foreground boundary

Actual source was inspected in `NativeDevelopmentHostShellUiChecks.cs`,
`NativeHostInputGuard.cs`, `MainWindow.MermaidVisualEditor.cs` and
`MermaidVisualEditorWindow.xaml.cs`.

The app explicitly opens the visual editor with `window.Owner = this` and
`window.Show()`. Its private Cancel sets discard and closes only that editor;
owner-close confirmation uses `MessageBoxButton.OKCancel`.

The stale-owner harness first calls `FocusWindow(main)` and then a direct,
explicit-target UIA `ValuePattern.SetValue(external)`. `FocusWindow` is an
assertion, not activation. `AssertHostForeground` requires the target PID and,
when supplied, exact HWND/focus, active console and accessible Default desktop.
Historical f05 foreground 29626154 failed the required main HWND 6037100 before
the setter. This does not demonstrate a switch to another application.

Current Win32 ownership is proved, but current foreground is a **modal
confirmation**, not the eligible pre-mutation nonmodal visual-editor boundary.
There is no complete exact-boundary source/owner/draft-version proof. Same PID
and ownership alone do not authorise mutation. The safe result is fail closed.

**Narrow harness correction: NOT IMPLEMENTED. New positive/negative controls:
NOT RUN. Actual portable stale-owner Apply rejection: NOT RUN.** The exact-target
foreground guard remains unchanged for all actions. No focus stealing, same-PID
fallback, keyboard/mouse exception, retries, timeout increase or safety bypass was
introduced. Original 20-second observation and 25-second owner budgets remain.

After observed cleanup, a separately authorised continuation can prove the actual
pre-mutation boundary and add the requested direct-setter-only assertion and its
negative controls. It must still reject foreign/unrelated windows, absent or
forged ownership, disabled/closed/stale targets, mismatched process start/source/
state, elevated/wrong-console/non-Default desktop, ambiguous modals and any attempt
to apply the exception to keyboard/mouse. This document does not implement or
certify those controls.

## Fresh complete shell

**NOT RUN / BLOCKED by original f05 cleanup.** No new Plan/Prepare/RunUi/Shell
session was started. No synthetic owner restoration was attempted. No unfinished
stage was relabelled as successful and f05 was not replayed under its old name.

## Package provenance and CI

The immutable internal unsigned .891 portable package remains from native run
[40191941601](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40191941601),
artefact **12599126443**, expiring **8 November 2026, 12:34:29 UTC**. Current API
metadata still reports that source, digest and expiry; the original archive,
portable ZIP and inspect-only installer hashes were independently rechecked.

| Object | SHA-256 |
| --- | --- |
| Actions archive | `67114e351163c56527072e33bb97a29eb896e7d3d39aedc5f0bfb3f004c34043` |
| Portable ZIP | `2875a5d4ad8e37a05f1ccf50fac016460c7ad4651d4b0ccdf63f6b67d637e04c` |
| Installer, inspect only | `6cfee3cc2834ec9ab691df4a839348fac0e353721a165b5c65913e31ff2f9da0` |

The package source/tree remain the starting identities above. Self-contained
portable Core/WPF **10.0.13** is separate from the local framework-dependent
validation client's **10.0.12**. No rejected locally packaged 10.0.12 application
was executed.

Read-only GitHub re-observation confirms all eight starting-head workflows still
succeeded, attempt 1: **113 successful jobs, eight deliberate skips**. Native
push/PR each have 2,117 Core, 176 corpus/export, 25 package negatives, 24 installed
families and 72 same-source synthetic phases. Each web run has 135 Chromium and
135 Edge. Expanded 38/38 appearances and the original five specialist families
remain separate. Stock Continuous correctness does not certify 250 ms.

Original .853 ×2, historical upgrade ×2, Pages ×2, consolidated historical and
controlled 250 ms remain skips. Historical red CI is preserved. These existing
results certify only their actual starting SHA; any subsequent documentation head
must obtain a separate post-commit CI receipt. This document cannot contain its
own final committed hash. The PR body and ignored final delivery receipt record
the actual final SHA/tree, runs, attempts, counts and limitations after validation.

## Evidence and delivery boundaries

Paths are relative to the existing isolated PR worktree. Raw local host inventory
and private paths remain ignored rather than committed/public.

- Original f05: `artifacts/native/development-host/c6bb3102fcb5c5c8fcb50ca4424b37789057bbb5/f05/`.
- Original VIII-D delivery: `artifacts/native/g8d/c6bb3102fcb5c5c8fcb50ca4424b37789057bbb5/a01/`.
- VIII-E read-only investigation: `artifacts/native/g8e/c6bb3102fcb5c5c8fcb50ca4424b37789057bbb5/a01/`.
- Exact ownership/identity receipt: `observation-9c47a098e23146d2a55030b37a162f10.json`;
  retained first observer receipt refused exact identity after a date-to-string
  conversion lost fractional seconds. V2 parses the original raw timestamp and
  proves identical ticks; neither observer sends input.
- `pending-dialog-accessibility.json`, `pending-dialog-0.png` and
  `pending-dialog-1.png` retain only the inspected synthetic windows.
- `current-ci-runs.json`, `current-ci-summary.json`, eight `jobs-*.json`,
  `current-artifact.json`, and `immutable-package-hashes-v2.json` retain current
  remote metadata and independently rechecked package hashes. An initial package
  verifier used a missing parent-directory path; this was a path error, not a
  digest mismatch. The successful V2 uses the observed version subdirectory.

**HOST PORTABLE: BLOCKED. INTERNAL BETA: BLOCKED; recommend withholding GO until
the mandatory complete local shell and remaining owner acceptance criteria are
met. PUBLIC PRODUCTION: BLOCKED / NO-GO.** HOST INSTALLER remains NOT RUN. SDK-free
clean Windows, physical screen reader, alternate DPI/multi-monitor, GUI shared
export dialogues, inspector persistence, exhaustive visual review, historical
upgrade and production signing remain NOT RUN or NOT CERTIFIED. PR #48 remains
OPEN / DRAFT; PR #38 stays paused. No merge, release, tag, signing, Pages deployment,
installer invocation or association change is authorised or performed.
