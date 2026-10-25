# Goal VIII-D — operator-assisted Find differential

## Corrected routing continuation — 25 October 2026

The continuation retains the clean local observer commit
`32017ffe611ba7f65515dc747f840b2dfc3b9be2`, directly based on reviewed remote
`6ed1be931fbc74666b7968fda7a4287f91f291c4`. The refreshed remote tree is
`6ad9a31f79c83819cbe6c0941c6529238c80149f`; develop and paused PR #38 still
match the identities below. Eight existing attempt-1 workflows retain 113
successful jobs and eight deliberate skips, attributable only to `6ed1be9`.

The product correction changes only FindText's XAML event binding from
`KeyDown="FindKeyDown"` to `PreviewKeyDown="FindKeyDown"`. The existing search,
preview and Escape handlers are unchanged. Compiled-XAML synthetic event checks
exercise exact Unicode selection, one invocation per Return, separate subsequent
navigation, editor routing with Find visible, ordinary typing and Escape.
These framework-dependent tests are not native portable GUI acceptance.

The validation-only observer remains child-process opt-in and excluded from the
portable payload. Additional read-only caret/source and preview-selection records
support the separate editor-newline and preview scenarios. Only the explicitly
selected synthetic newline variant permits its two known buffer forms; it never
changes an input, source, selection, focus or handled flag.

Focused portable modes now include `FindEditorEnterObserved`, `FindPreviewObserved`
and independent `InvalidMermaid`. Each requires its own new source-labelled
Plan → Prepare → RunUi → Complete session. Find Enter requires native and UIA
`Café`, UTF-16 start 2, length 4, caret 6, one Return, exact query and source,
and preserved selection after Close Find. Foreground guards require the actual
candidate HWND and Default desktop without activation. The shell stops on a
required Find failure and asserts real preview selection. Original 20-second
observation and 25-second owner budgets remain unchanged.

The first continuation preflight passed at 11:54:01 UTC: normal non-elevated
active console, Default input desktop accessible, no existing native process.
228 previously pinned evidence entries were rechecked without alteration.
The working-source Core run passed 2,117 tests on installed .NET 10.0.12;
working-source event tests also passed on 10.0.12. A helper-name compile failure
and subsequent local compile failure remain in their original logs. These are
pre-commit development checks, not results attributed to the observer commit or
to a packaged 10.0.13 application.

New-source portable, invalid-source GUI, complete shell and hosted CI results
remain **PENDING** until the exact committed-source delivery receipt records
them. A successful source-check push cannot establish local foreground access.
Only a verified new self-contained Core/WPF 10.0.13 package may be executed.
Internal beta remains BLOCKED pending acceptance and the separate owner decision;
public production remains NO-GO. Installer, physical reader, other DPI, shared
file pickers, inspector persistence, incomplete visual review and historical
migration remain NOT RUN. Earlier `.749`/`.853`, High Contrast/regex hypotheses
and the unaccepted 250 ms objective are not resolved by later green checks.

Continuation preparation evidence:
`artifacts/native/g8d/32017ffe611ba7f65515dc747f840b2dfc3b9be2/a02/`.
Subsequent package/GUI/CI receipts must retain their own exact source identities.

## Frozen earlier investigation

PR #48 stays OPEN / DRAFT. Develop remains `9b1e73bbf564c53b48149e8c0dcd6fd7acbe308f`.
PR #38 remains paused at `31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`;
its controlled 250 ms objective is INCOMPLETE. No merge or release is authorised.

## Preserved baseline and fresh Next control

On 25 October 2026, GitHub and the clean existing worktree matched review head
`6ed1be931fbc74666b7968fda7a4287f91f291c4`, tree
`6ad9a31f79c83819cbe6c0941c6529238c80149f`. All eight attempt-1 workflows
still passed: 113 successful jobs and eight intentional skips.
The raw d02/d03 observations, prior delivery receipt, Find source and guard
implementation were inspected. All 99 original and 36 diagnostic file hashes
matched their existing manifests; five further receipt/index files were pinned.

The read-only host preflight passed on the normal non-elevated Windows 11 Home
26300.9711 console, with accessible input desktop and no existing native process.
The operator was asked to leave the tested window in front during input.
No OS focus permissions, settings or shared state were changed.

Fresh session `development-host/11957e294a6f87b9729913b174b919ff23d176ed/e01`
independently verified the original Actions .880 package and all 528 payload files.
Validation client source was the review head above; portable source remains
`11957e294a6f87b9729913b174b919ff23d176ed`, tree
`ced9a65224b5ae00be2100112aaf27093a745511`.
The actual process was PID 82896 / HWND 22747820, DPI 144.
One existing Next-button click selected exact `Café`, UTF-16 start 2 / length 4,
in both native AvalonEdit and UI Automation at 11:27:29 UTC.
Native SelectionChanged and the final caret offset 6 were recorded.
Query, editor context, foreground and source were independently verified.
The source hash remained
`990c0ec966d38842f5ab666c67c3d7a9d6c40f1a216d8cf450bc8701454fdb42`.
Normal closure and the separate bounded host-preservation receipt passed.

This is a **Next differential control only**, not an Enter or full-shell pass.
Original d02 Enter remains FAIL; original d03 remains BLOCKED before input.

## Read-only observation boundary

The existing validation-only StartupHook now also observes KeyDown after the
UIElement class handlers but before the FindText instance handlers, filtering
strictly to the prepared synthetic FindText. It records the real window and
foreground HWND and defers one read-only Return snapshot until event dispatch
completes. It never writes focus, selection, Handled, query or source and never
sends a command. The child-only identity/readiness checks and original 20-second
observation / 25-second owner deadlines remain unchanged.

[WPF class-handling documentation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/events/marking-routed-events-as-handled-and-class-handling)
establishes that class handlers precede instance handlers and ordinary XAML
handlers do not receive handled events. A handled observation after all instance
handlers alone cannot attribute the cause. The new class-phase observation is
intended to distinguish that boundary; no product correction is claimed yet.

Fresh Enter, independent portable invalid-source GUI and the complete original
shell still require their own receipts. Stop local GUI work at any foreground
refusal. Installer, alternate DPI, physical reader and clean-machine certification
remain outside this host experiment. Internal beta still requires the separate
owner decision; public production remains blocked.

Ignored evidence begins at
`artifacts/native/g8d/6ed1be931fbc74666b7968fda7a4287f91f291c4/a01/`.
Final post-commit outcomes must retain exact source attribution and all earlier
failures rather than changing this pre-experiment record.

