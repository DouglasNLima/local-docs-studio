# Independent High Contrast preview-loading source diagnosis

Reviewed the retained `final-complex-preview-4461591-attempt1` receipt/log, `NativePreviewLoadingChecks.cs`, `MainWindow.xaml.cs`, `MainWindow.PreviewDiagnostics.cs`, `MainWindow.PreviewLoading.cs`, `PreviewLoadingState.cs` and `NativeTestHighContrast.cs`. This was source/evidence inspection only; no product edit, shared build, private original access or local High Contrast change was performed.

## Finding and confidence

**The harness assumes too much from completion of its captured render Task.** At `NativePreviewLoadingChecks.cs:103–104`, it releases the held rendering gate, awaits `first`, then immediately asserts that global loading has finished. That is valid only if `first` still owns the current preview request.

Two inspected production paths make that assumption false:

1. `MainWindow.WindowsPreferenceChanged` posts asynchronously to the dispatcher and calls `ApplyTheme(); QueuePreview()` (`MainWindow.xaml.cs:90–100`). `QueuePreview` cancels the old token, transfers loading state and restarts the 300 ms preview timer (`:155`). A request waiting on the held gate observes cancellation; its catch/finally finish the Task, while `PreviewLoadingState.Advance` rejects its stale ticket and correctly leaves the newer generation busy.
2. `UpdatePreviewAsync` can itself detect that the current High Contrast resources have not caught up, queue another preview, and return before the ordinary pipeline (`:164–169`). Such a Task is complete without having rendered the requested document.

Cancellation is handled inside `UpdatePreviewAsync`, so `TaskStatus.RanToCompletion` is not proof of a successful visible preview. The loading generation and accepted current source are the ownership evidence.

The failed receipt's `OrdinaryPreviewMilliseconds=0.0017`, seven successful held/loading/UIA checks, still-busy assertion failure, and empty `Phases` array are consistent with the captured Task already having completed before any ordinary render preparation. **They do not uniquely establish that a Windows High Contrast broadcast caused it.** The receipt does not record the old/new task/request/loading generations or the early coalescing branch. Report the cause as inferred supersession/appearance coalescing, not a proven exact event. The retained CI failure must remain a failure, not be retroactively labelled a pass.

## Proposed validation-only correction

**Source-review approval, subject to these boundaries:** observe the captured Task, then wait for the current loading operation to reach a terminal state, retaining the original strict preview assertions. Do not change product cancellation, loading ownership, rendering deadlines or the 25-second validation budget.

- Use one deadline starting when the gate is released. A captured-task wait followed by a second independent 25-second wait could allow nearly 50 seconds and would weaken the original bound. Share the remaining time or use one combined bounded waiter.
- Require the successful current state: loading `Completed`, matching current document and source version/parsed source, no queued preview, and the expected visible/collapsed hosts. Keep the existing media count, warnings, expected text and later selection/zoom checks. Merely `!Busy` is not sufficient evidence of success.
- Never call another `Render` or reset loading as a recovery action inside the waiter. A stuck or failed current pipeline must still fail within the unchanged bound.
- Record `first.IsCompleted`/status before release, loading generation/phase/busy and diagnostic request before/after awaiting it, preview timer state, and current accepted document/source at terminal. Keep captured-task completion time separate from current preview completion time.
- Add deterministic supersession coverage after a successful stable initial preview: hold the gate; start a pending render; invoke the real `QueuePreview`; await the old request; assert the newer generation remains busy; release the gate and require its successful current completion within the same bound. This tests the ownership sequence without depending on OS broadcast timing.

The source supports this harness correction. A fresh normal/High Contrast validation run is still required before claiming the corrected harness passes, and no pending CI result is inferred here.

## Validation-only correction review

Reviewed the actual `NativePreviewLoadingChecks.cs` diff from `44615917`, including the subsequent stale-text-map correction. Reviewed file SHA-256: `2A725665FA3CFFBC263DADEE66ABF4E21085CBE8D9992E3F74DDB0350C71BB9E`.

**Source-review recommendation: GO for the corrected harness logic; runtime/High Contrast acceptance remains pending its actual runs.**

- `Await(first, watch)` and the current-terminal `Until(..., watch)` use the same stopwatch started at gate release. The 25-second limit is unchanged and is not converted into two sequential 25-second allowances.
- Terminal waiting requires no busy operation and no queued preview. The following strict check additionally requires `Completed`, the active document in the loading context and rendered result, exact parsed source and matching source version. Cancelled/failed terminal states cannot pass merely because `Busy` becomes false.
- Existing visible-host/ring, expected-content, media, warnings and subsequent selection/zoom tests remain present. No rendering restart, loading reset, OS-event suppression or product timeout change was introduced to force success.
- The deterministic case exercises the real `QueuePreview` while the rendering gate is held. It observes that the completed superseded Task leaves a newer loading generation busy, then releases the gate and requires the queued replacement to complete with current source under a bounded wait. This is a validation scenario, not a production retry.

One concrete defect in the initial test correction was identified independently: the queued replacement installs a new FlowDocument, but the following selection test still used `firstResult.TextMap` from the old FlowDocument. Its TextPointers belong to the old text container. The root agent reported that the first shown local run failed at this exact selection operation and retained that receipt/log. The reviewed correction refreshes `firstResult` from the current rendered result and rechecks media/warnings before obtaining selection pointers. Source inspection confirms this resolution; no product defect is implied by that test-reference failure.

Two non-blocking evidence improvements were communicated:

1. Append request/generation observations before waiting or from a `finally` path. In the reviewed version, the before/after snapshots are appended only after the current-terminal wait succeeds, so a new timeout would still omit them from its failure receipt.
2. Assert that the deterministic `superseded` task is pending before invoking `QueuePreview`, making the active-request cancellation premise explicit rather than allowing an already-coalesced Task to satisfy that part of the scenario.

No product source changes, builds or test executions were performed by this reviewer. The original failed High Contrast CI outcome and the root agent's first local stale-text-map failure must remain retained; source approval does not convert either into a pass or claim the next hosted High Contrast run has completed.

## Final source-readiness check

Final reviewed `NativePreviewLoadingChecks.cs` SHA-256: `9171AED4F007A8A98B772BBCCF5875F5AA0528664B2A4D82BA53B8C922333869`.

Both non-blocking evidence improvements are now source-verified. The before-release observation is appended before awaiting the captured task; the after-task observation is appended before waiting for current terminal state. A timeout at either point therefore retains the observations that were actually reached. The deterministic supersession scenario explicitly asserts that the held render Task is pending before calling the production queue method. The refreshed `firstResult` and media/warning checks remain present after replacement, so later selection pointers belong to the installed document.

**Final source-readiness recommendation: GO for this validation-only correction.** The shared 25-second deadline, current-document/source/version/Completed checks and existing strict content/view assertions are unchanged from the approved correction. No new false-pass path or product/deadline change was found. The root agent reported shown Light/Dark run 2 passing 102 checks and four supersession cases; that is a reported execution result, not an execution performed by this reviewer. Final run 3 and hosted High Contrast results must retain their own receipts and are not assumed successful here.
