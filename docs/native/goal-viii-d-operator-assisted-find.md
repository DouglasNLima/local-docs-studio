# Goal VIII-D — operator-assisted Find differential

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

