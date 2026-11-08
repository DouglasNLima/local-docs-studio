# Native acceptance — bounded qualification preparation

Goal #77 / Draft PR #78 · 7 November 2026 · **PREPARED; execution NOT RUN**

8 November tooling continuation: [implementation, evidence schema and execution
boundary](qualification/tooling.md). The original preparation below and its
manifest remain the approved scope; tooling validation does not execute or certify it.

The [owner decision](https://github.com/DouglasNLima/local-docs-studio/issues/77#issuecomment-6714211040)
approves D01–D13 at **design level**, reviewed commit
`bc092512c30e152cd1e4384c1eab4f8ba30e059f`, tree
`90f2dc1480a5eaed7b6628d4a41d6cce9e2265aa`. This plan makes the authorised preparation
concrete. It neither activates a new acceptance policy nor claims any risk closed.
The [ADR](../adr/007-native-document-rendering-performance.md),
[contracts](native-acceptance-decision-package.md) and
[R01–R06 register](native-acceptance-risk-register.md) remain the governing design.

## Fixed inputs and execution boundary

Live refs were reverified before preparation: develop `99349d82eb06960eb1f9785cf384db2373aed4fa`,
#76 `7b1334b09a02ad2fbfd4a0234798e6c387d3e634`, #80
`22c223c6f56fa41fda2a36102e1eed0af44974ec`; all three PRs remain Draft.
#78 started this continuation at the owner-reviewed `bc092512`. The
[machine-readable plan](qualification/goal77-preparation.json) pins sources, fixture
hashes, finite cells, limits and missing configuration fields. These are preparation
records, not results. Null budgets/margins mean **unapproved**, never unlimited.

Preparation consists of source inspection, a fixed collection plan, evidence schema,
stop rules and hardware/corpus worksheets. No new app process, installer, system
appearance change, physical calibration or hosted campaign runs here. Existing
workflows are unchanged. A collection implementation must fulfil the prerequisites
below before dispatch; do not run a wider existing workflow to bypass missing scope
controls. No second approval of the already-approved design is needed; separate
operational activation remains explicitly unauthorised.

Use disposable authorised Windows hosts for foreground/HC work. Keep copied public
fixtures and isolated state, exact source/tree/product/Core/harness hashes, actual
loaded runtime/OS/image/DPI, QPC frequency and input/foreground ownership.
R01's existing 30-second observation, 240-second child, 2 GiB observed private-memory
and 16 MiB child-log bounds remain **collection safety limits**, not SLOs. R02 must
retain its original per-step deadlines, including the 20-second main-window bound;
a proposed 240-second outer supervisor must not extend any earlier deadline. Preserve
partial output on failure. No prewarming, forced GC/layout, cache reset, artificial
message pumping, pixel-dependent retries, deadline extension or production changes.

## R01 — one visible-completion journey

Question: can the observer distinguish current logical/layout readiness from the
actual fixed captured frame, and attribute ordering when the trace supports it?
This is observer/correctness qualification, not P95/P99, physical scan-out or a
new timing acceptance claim. The candidate product stays exact #76 `7b1334b0`.
Do not add baseline/products/modes/zoom/resize/interruption matrices to this work.

| Cell | Appearance | Trace | Product processes | Fixed journey |
|---|---|---|---:|---|
| V1 | Normal | Off | 1 | Pages, 100% application zoom, original mixed fixture, primary/secondary Find and current source positions. |
| V2 | Normal | On | 1 | Same exact actions/geometry; passive mutation/channel chronology. |
| V3 | Actual OS HC | Off | 1 | Same original journey; actual appearance and source ownership required. |
| V4 | Actual OS HC | On | 1 | Same actions; no extra waits other than the existing bounded observation path. |

Each process retains the existing seven boundaries: primary Find, primary Find
all-readers, secondary Find, secondary Find all-readers, returned primary, rapid
switch, closed comparison. Each boundary has exactly three fixed frames: at most
**84 full frames and 252 label crops** across four completed processes. Partial
collection stays partial; never replace a failed/early frame. Keep normal/HC and
trace-on/off distinct. There is no statistical sampling claim from four processes.

Evidence must contain before/after owner/version/generation, Ready/Busy/status/page
values, source-map/selection geometry, expected/visible text, required-reader set,
input QPC, visual-mutation events, available WPF media events, DwmFlush/BitBlt
intervals, full PNG/crop hashes and exact source/dirty/file preservation. Record
missing events, crops or ownership explicitly. Inspect full frames for OCR errors
or clipping; an OCR mismatch alone does not establish an application defect.

**Prerequisites and existing implementation:** the exact #76
[CompletionJourney](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/src/native/LensDocsStudio.Native.UxProbe/CompletionJourney.cs)
requires `LENS_NATIVE_COMPLETION_JOURNEY=1`, `LENS_NATIVE_TRANSITION_TRACE=0`,
`LENS_NATIVE_STARTUP_TRACE=0`, `LENS_NATIVE_COMPARISON_JOURNEY=0`,
`LENS_NATIVE_COMPLETION_SCENARIO=pixels`, `LENS_NATIVE_COMPLETION_READER=primary`,
`LENS_NATIVE_COMPARISON_MODE=Pages`, and explicitly matched
`LENS_NATIVE_COMPLETION_TRACE` / `LENS_NATIVE_COMPOSITION_TRACE` values (`0` or `1`
per cell), one iteration and the mixed fixture. Do not inherit ambient flags.
Without the completion-journey flag the host takes its ordinary UX path. Its seven
boundaries/three frames can be reused unchanged. However,
[Test-PreviewCompletion.ps1](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/scripts/native/Test-PreviewCompletion.ps1)
also launches interruption and controlled scenarios: it is **not** a four-cell launcher.
Prepare a narrowly scoped validation wrapper with a four-cell allowlist, inherited
supervision, exact product/harness verification and a fresh output root; do not
dispatch the full completion or targeted-regression workflow. #78 does not contain
the later #76 completion implementation; source-pin it, do not silently merge #76.

[CompletionTrace](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/src/native/LensDocsStudio.Native.UxProbe/CompletionTrace.cs)
uses `RenderComplete` in its commit waiter. Treat that label as an observer event,
not proof of presented pixels or of a particular window's surface. Validate the
observer correlation before claiming a frame is causally ordered after the relevant
update. Trace-free cells cannot establish channel attribution. The existing
[five audit controls](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/tests/unit/native-completion-audit.test.mjs)
are useful but do not certify a new visible-completion classifier.

Before collection, independently review offline negative cases for wrong owner or
version, missing/blank required text, stale required labels, invalid selection,
Busy secondary versus required all-readers boundaries, missing/truncated trace,
capture before submission and clipped/OCR-only mismatch. Use copied data, retain
the original bytes, and never reclassify an old FAIL as PASS.

Keep three output columns: **collection validity**, **unchanged correctness
assertion result**, **ordering attribution** (supported / unsupported / inconclusive).
Any proposed shadow interpretation is an additional labelled column, not a replacement
for today's assertion. Report latency as observed only; no finite UX budget has been
approved. Stop after V1–V4 or a safety breach; do not add repetitions. If chronology
is insufficient, retain INCONCLUSIVE with the exact missing evidence. Wrong-owner
Ready, lost source or demonstrated stale/blank required output is FAIL regardless
of a favourable duration. An independently reviewed explanation can inform a later
disposition; this preparation closes no original frame or risk.

## R02 — separate pre-assertion propagation from actual app controls

Question: does the original colour mismatch arise in the preflight process, in a
stock WPF control, or in an actual application control/transition? Retain the
historical **2.542596:1 FAIL** and the exact **4.5:1** requirement. Passing later
colours, a baseline failure or a stock reproduction cannot establish candidate safety.

Four cells only: packaged .NET/WPF **10.0.13** and recorded hosted **10.0.14**, each
in baseline-first and candidate-first order. Each cell has one fresh develop
`99349d82` and one fresh candidate `7b1334b0` observation. Total planned ceilings:
**eight preflight processes, eight application processes and two stock-WPF controls**
(one per runtime). No second OS image, third runtime or repeat-until-failure axis.
Loaded runtime identity must match; a moving `10.0.x` setup is insufficient. These
historical versions are diagnostic pins, not approved deployment configurations.

Source inspection refines the earlier preparation wording: the unchanged
[NativeUxScreenshots pre-assertion](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/src/native/LensDocsStudio.Native.Validation/NativeUxScreenshots.cs)
constructs `NativeTestHighContrast` **inside the validation process**, queries WPF
HC, creates theme resources and asserts the pair **before product launch**. It then
starts four application sizes if successful. Launching a process with HC already
active is not the same startup sequence. Preserve this distinction rather than
calling an already-HC launch an exact replay of the failing pre-assertion.

For each product slot, use two independent observations:

1. A narrow preflight adapter preserves the original startup/HC/resource/assertion
   order and fails normally on the unchanged contrast assertion. Inspect entry OS
   state through Win32 only. Read WPF/SystemColors/resource values at the original
   diagnostic point after theme-resource evaluation, never earlier: an added first
   WPF read can initialise the colour cache. Do not create a WPF window or prime
   resources for notification logging before that sequence. Record available passive
   notifications, runtime and PID; missing early chronology remains missing evidence.
   Preserve/restore the actual initial OS state. No assertion bypass.
2. A separately labelled application observation starts with HC active and captures
   the actual pressed control, text/icon/focus/selection and one HC→normal→HC
   transition. Keep a separate PID/result and source/runtime hashes. A preflight
   failure remains failed even if this independent diagnostic observation succeeds.

The fixed control is the toolbar Button named `New document (Ctrl+N)`, using
`Desktop.CommandButton` and its `PressedBrush`/`PressedTextBrush` trigger. Require
one matching enabled control. Use real pointer down, capture the pressed state,
then move outside and release to cancel the command; verify unchanged tab/source/
dirty/selection state and restore keyboard focus. Missing control, accidental
activation or unavailable resolved brush evidence is a failed/incomplete cell,
not permission to substitute another control. The original 20-second UI waits remain.

The two stock controls preserve the preflight ordering using stock WPF system
colour pairs without product resources, recording the same notifications/ratios.
No inference that a shared symptom absolves application accessibility is allowed.
Use actual system changes on the disposable host, not synthetic theme messages;
restore the initial setting in cleanup. If colour/foreground/restore state is
unknown, retain an invalid cell and stop further state-dependent collection.

**Prerequisites:** the existing
[recovery workflow](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/.github/workflows/native-acceptance-recovery.yml)
includes performance jobs and a multi-size app harness. It does not implement this
18-process protocol. Prepare/test a bounded validation-only preflight entry, one-size
application observer, real-notification chronology, process correlation and two
stock controls. Review any reuse of original assertion logic against its exact
source; keep the original gate untouched. No production ThemeService change is
permitted by this preparation. Unknown app brush values cannot be filled from the
preflight PID; raster antialiasing is not the resolved text/background pair.

Use one existing suite viewport, **1280×800**, record actual geometry and do not
multiply sizes. The failing preflight had no application viewport to reproduce.
Independently verify
RGB parsing/ratio calculation, original failing/passing pairs and detection of a
wrong-PID record before collection. Evidence requires cold initial pairs, pressed
control/frame, transition notifications and colours, keyboard/selection checks,
loaded runtime/OS, input timing, source hashes and cleanup outcome. No prewarming,
colour substitution, cache invalidation or relaxed assertion.

Stop after the four cells/two stock controls. A product failure is a correctness
blocker needing a demonstrated fix; preflight-only or stock-only reproduction
narrows attribution but does not close R02. If nothing reproduces, record
INCONCLUSIVE and stop. No automatic follow-on matrix or risk waiver.

## Hardware and corpus calibration preparation

The plan approves a **method**, not a supported floor. Hardware slots H0 (proposed
four physical cores, 8 GiB, SSD, integrated GPU, 1920×1080/60 Hz) and H1 (typical
16 GiB control) are worksheets. Exact CPU/GPU/driver, RAM/storage, power/thermal
state, OS edition/build/servicing status, runtime/package hashes, physical display,
scaling/refresh and operator remain null until nominated and verified. Do not
populate them from a hosted VM or the development desktop by assumption.

Preserve Windows 10/11 qualification obligations without asserting current support
for an unselected edition/build. Physical 100/125/150/200% and mixed-DPI checks
remain the release plan; OS DPI and application zoom are separate. No OS/runtime
upgrade, installation, procurement or display change occurs during preparation.

Seven existing exact-byte synthetic anchors are copied as metadata into the plan,
with their original manifest SHA-256 and asset hash; source files remain unchanged.
Table counts include header rows: table **1,001** and mixed **101**, distinct from
1,000 and 100 data rows. Record UTF-8 bytes/line endings, semantic element counts,
nesting, graph nodes/edges and decoded pixels; do not use bytes alone as complexity.

Prepare three consented real-corpus slots: ordinary prose/operating guide,
table/list-heavy technical reference, and mixed local-media/diagram documentation.
Each needs origin/permission, scrub review, exact file/asset hashes, structural
counts, intended task and human tolerability assessment. No private document is
copied or uploaded here. One boundary, one oversize and one recovery case remain
unnominated until finite product limits are selected; do not invent “oversize”
relative to the current candidate or silently extend an envelope after failure.

Proposed first **calibration pilot**, not executed: the seven anchors on H0/H1,
exact historical baseline `0237b08390d5d1d946c0b19b81d671ccd54fb951` and develop
`99349d82`, both starting orders, one fresh process per product/fixture/cell and
three labelled cycles: **56 process launches maximum**. Use Continuous only, normal
appearance and 100% application zoom; all actual OS/display/runtime strata remain
recorded separately. This tests baseline feasibility/observer overhead, not Pages,
Automatic, HC or P95/P99 compliance. Candidate #76 does not set the budgets.
The three real-corpus slots allow at most six separately recorded human journeys
(one per document/machine), not another automatic cross-product. Boundary/oversize
execution and other modes require their own fixed follow-on scope after nomination.

Do not dispatch the pilot until exact machines/corpus/OS/runtime, a physical-host
runner boundary and independent sampling review are complete. The current UX
runner is guarded for disposable GitHub-hosted Windows and cannot certify physical
machines; do not defeat its guard with environment variables. Statistical acceptance
implementation is not supplied by this worksheet. Retain all samples and observer
overhead; predeclare blocks and ordinals. Report medians/maxima and missing cells,
withhold tails; do not convert a 30-second safety bound or measured candidate time
into an SLO. Future budgets/margins require separate owner decisions before acceptance.

## R03–R06 and completion of this preparation

R03 shutdown and R04 pre-binding startup stay open with retained controls and
single-recurrence escalation; no new matrix. R05 selection stays under its existing
targeted controls, with any new wrong selection blocking acceptance. R06 retains
qualified Pages evidence and inconclusive late Continuous/Automatic deliveries;
no pending PASS from an already-completed task. If R01/R02 exposes one of these
risks, retain the observation and escalate under existing tracking without widening
the campaign. No new issue is necessary merely to record this preparation.

Independent technical review on 7 November verified the source/fixture identities,
cell and process counts, retained failures and descriptive-only pilot. Its two
findings are resolved: explicit R01 dispatch flags and the original R02 first
WPF-read boundary are now pinned in both plan and manifest. Final verdict:
**preparation is technically reviewable; no outstanding actionable findings**.
This review does not certify an unimplemented launcher, sampling implementation,
physical configuration, product acceptance or risk closure.

Preparation is complete when the source/fixture/cell inventory and local links are
checked and independently reviewed. **Execution is not yet qualified:** narrow
launchers/observers, exact runtime/image/geometry and hardware/corpus nominations
are explicit next implementation inputs. This is a finite work list, not authority
for unbounded diagnostics. Goal #77 stays open; PRs #76/#78/#80 stay Draft;
**#76 integration remains NO-GO**. No gate, required check, historical failure,
4.5:1 criterion, production code, signing or distribution policy changes.
