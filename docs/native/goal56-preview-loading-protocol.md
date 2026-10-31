# Goal #56 — bounded preview loading lifecycle investigation

Fixed before hosted execution on 31 October 2026. No integration, merge, signing,
tag, release or SignPath application is authorised. PRs #69/#70 stay unchanged.

## Immutable sources

| Role | Commit | Tree |
|---|---|---|
| Baseline develop | `86d60322f6f1f1dec06974bf5f9e1738a3bb80c7` | `552158f43ebf2501c684d210dd9ebae80a3e6f8f` |
| Candidate PR #70 | `460be40d42c1c7248249e4b10a677078db474baa` | `127f9d879ff5eff8422186aa0f3fa402af5408a1` |
| Privacy PR #69, preserved only | `c85a51118ac0f175cb21c79c3f7503ee1bc8ecd0` | `9f578b8bc6ceb99dd58e5ab195f0170e8c3232db` |
| Hosted-verified inventory writer | `b7b4774a05428e279f1c5b96c957fa4b5cd020ee` | `440ec3eb6aa37564c078fe135cb88fb32ae6d0ff` |

The diagnostic source is the immutable workflow checkout commit/tree containing
this protocol and `scripts/native/diagnostics/goal56-preview/`. Each job records
those Git identities, every helper hash, original and overlaid validation hashes,
the complete patch, and untouched product-source diff. The same overlay applies
to both sources; no Core or Native product file is modified. Local compilation
is preparation only; actual shown UIA/High Contrast runs require clean disposable
GitHub-hosted Windows. No environment markers are forged on the developer host.

## Original failure and evidence gap

Run `41180694385`, attempt 1, job `124477488630`, required `complex-er-preview`,
fails `NativePreviewLoadingChecks.cs:76`: `Until(() => view.Visibility ==
Visibility.Visible)`, with `System.TimeoutException: Preview lifecycle check
exceeded its bounded wait.` The original 25-second bound is unchanged.
Artefact `13034554180` was downloaded and independently hashed as
`c7e074cdccce9e8d6f6331c370be5f3c0a9be651e853f36f93103d9f237da323`.

Its 25 successful Light/Continuous assertions end at active-document closure.
The next incoming document is persisted as active and preferences record Pages.
The latest recorded phase spans belong to the preceding scenario; there is no
failed-boundary task, request, cancellation, loading generation, visibility,
render gate, timer or dispatcher snapshot. Four Continuous captures exist;
missing Pages capture is not evidence of product failure. Persisted recovery
state was written asynchronously, and finally cleanup precedes the original
receipt, so neither is an atomic failed-boundary snapshot. The original blob is
identical in develop/PR #70; that alone proves no cause or attribution.

## Fixed execution budget and sequence

One initial paired comparison only: two new `windows-latest` jobs, SDK **10.0.403**,
one per immutable source, fail-fast disabled and no automatic retries. A strict
SDK selector lives in the workspace parent outside product checkouts; the script
rejects any selected SDK other than 10.0.403. Capture
actual image/version, OS/build, CPU class/core counts, SDK information, loaded
.NET/WPF version and source/test hashes. Do not compare timings causally between
materially different environments. Each job has a 15-minute overall bound.

Each job restores/builds once with locked dependencies, then executes:

1. Original shown `--preview-loading-ui` sequence once (Light/Continuous →
   Light/Pages → Dark/Continuous → Dark/Pages, retaining the same window/model).
2. Original actual Windows High Contrast sequence once, in a new process.
3. One separate never-completing diagnostic control, in a new shown process:
  hold the actual existing preview gate through the unchanged current-terminal wait.

Maximum **six validation-process invocations** in the initial pair. Continue
independent stages to preserve controls even if an ordinary stage fails; do not
replay any stage. The successful Save/UIA, inventory, Mermaid Creation and 24
installed-authoring experiments are preserved and are not repeated here.
No original ER fixture, renderer, theme, performance deadline or assertion is
modified. The preceding standalone complex ER acceptance is already successful;
this comparison investigates its separate failed lifecycle process.

## Instrumentation and controls

The disposable overlay uses the existing preview EventSource, WPF visibility
events and model changes. It keeps at most 8,192 events in memory, records the
discard count, and flushes before cleanup. Events carry a sequence allocated
under a lock, monotonic time, process/thread identities, stage request argument
and directly observed WPF/reflection state. Observations are sequential, not
atomic across threads. Worker events do not read dispatcher-owned objects.
Only explicit boundaries hash the actual source. Dispatcher hooks retain pending
operation identities/counts, with at most 64 pending status entries per snapshot;
their concurrent observations do not establish global idleness.

Snapshots occur before/after Hold and Render, at each scenario setup, and at the
unchanged timeout **before gate release or window/model cleanup**. Timeout JSON
and a WPF bitmap are attempted; diagnostic errors cannot swallow or replace the
original exception. Native WPF peers are labelled as such. Actual external UIA
ring visibility/bounds/status are captured by the existing shown acceptance on
its background client. Synchronous external UIA at a dispatcher timeout is
explicitly unsupported; no extra provider wait is inserted into that boundary.
The shell does not retain the timer task; current request phases and the captured
explicit task identity are reported separately, never conflated.

- **A, pending:** original held-gate assertions require busy, incoming masking,
  visible indeterminate ring/status and actual shown UIA. Request/generation,
  exact document/source, gate ownership and lack of acceptance are recorded.
- **B, released:** existing release requires current exact source/document,
  completed first layout, busy cleared and ring dismissed, with prepared media.
- **C, superseded:** original deterministic QueuePreview, rapid edit and three-tab
  controls retain all generation, cancellation and stale-content assertions.
- **D, transition:** retain the same original window across reading modes;
  record previous terminal state, new selection, mode, timer and incoming request.
- **E, never:** after the existing visible-pending assertions, keep the real gate
  held and call the existing bounded current-terminal wait (`!loading.Busy &&
  !queuedPreview.IsEnabled`). It must fail with the original TimeoutException,
  a FAIL product receipt, held gate, active busy generation and no accepted source.
  A captured superseded task may validly complete; the latest request's pending
  status is inferred from its queued stage and held gate, not a retained Task.
  The wrapper passes only
  as an expected negative control; it cannot certify successful rendering.

## Confirmation, stop conditions and retention

A causal classification requires matching raw failed-boundary state and a source
mechanism. Synthetic controls can demonstrate a mechanism but cannot recreate
missing historical observations. A passing pair does not erase the required
failure or establish an environmental cause. If inconclusive, make no speculative
fix and retain `PREVIEW_ROOT_CAUSE_INCONCLUSIVE`/integration NO-GO.

Stop a source stage on build/identity/overlay mismatch. Retain each failure,
exception, pre-cleanup state, screenshot, patch, helper/runtime identities, full
stdout and all receipts. Upload on failure, use 30-day retention, download and
independently hash outer archives and every inventory member. Do not overwrite
original evidence or rerun to obtain green. Any further execution requires a
committed amendment stating a specific new hypothesis/information gain, source,
maximum executions and stop conditions before dispatch.

Only a demonstrated defect permits a minimal correction/regression and one Draft
PR to develop. Its final-source mandatory checks must be verified; focused passes
cannot override a failed required context. Independent technical agent review
must assess source identities, raw boundaries, causal limits, assertion strength
and preservation of PR #70 before final publication under Goal #56. This is not
human approval. Preserve `PR69_INTEGRATION_NO_GO`,
`NATIVE_CONTINUOUS_250MS_FAIL`, `SIGNPATH_ELIGIBILITY_UNCONFIRMED` and
`NATIVE_STABLE_NO_GO`, paused PRs #38/#50 and all historical failures.

## Pre-execution diagnostic correction — fixed before replacement comparison

Initial protocol `e25d3d6ffcb99660bf874d8fc325b4c305fdf1ac`, tree
`ea400801e7f1fef00a19bd36afab3cd9ef931941`, ran once in **41186571384**.
Both jobs failed before build or validation invocation. The empty `git diff`
pipeline did not create `product-source.diff`; the following Get-Item threw.
This is an evidence-writer defect, not a preview failure. Exact SDK/source/helper
identities and overlays were captured, but **zero** validation processes ran.

The correction explicitly writes the captured diff even when empty, checks git's
exit code and retains the zero-diff product-source requirement. No C# overlay,
ordinary assertion, deadline, control or product source changes. The information
gain is reaching the first actual lifecycle boundary observations, which the
initial attempt never executed. Authorise exactly **one replacement paired
comparison**, same immutable sources, six validation invocations maximum, no
automatic retry. An additional failure stops this amendment; further execution
requires a new specific committed justification. Retain the initial FAILURE.

Independently rehashed original failed diagnostic archives:

| Source | Artefact | ZIP SHA-256 |
|---|---|---|
| Baseline | `13037255901` | `bee3d37036f376febb3b588fff90620cd91f1eff3a1718a350c9bfa133a08974` |
| Candidate | `13035924636` | `74cf0387c1edc1b89cfcc9cb8044c0c494323bddb6a3648e3049edd0c363a70f` |
