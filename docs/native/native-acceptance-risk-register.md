# Native acceptance risk register

7 November 2026 · Goal #77 · **Design-level criteria approved; no failure closed**

Decision authority is [ADR 007 D10/D13](../adr/007-native-document-rendering-performance.md#decision-matrix--7-november-continuation).
Contract A and Contract B are in the [decision package](native-acceptance-decision-package.md).
The [owner decision](https://github.com/DouglasNLima/local-docs-studio/issues/77#issuecomment-6714211040)
approves the criteria without waiving or closing any risk. The
[qualification plan](native-acceptance-qualification-plan.md) prepares only R01/R02
and calibration; R03–R06 retain the controls and escalation boundaries below.
Unless noted otherwise, facts below come from the [exact 7b1334b0 terminal receipt](https://github.com/DouglasNLima/local-docs-studio/pull/76#issuecomment-6713058791)
and [targeted investigation](https://github.com/DouglasNLima/local-docs-studio/blob/7b1334b09a02ad2fbfd4a0234798e6c387d3e634/docs/native/targeted-regression-closure.md).
All owner assignments below are roles to nominate, not accepted assignments or waivers.

| ID | Classification / present decision | Existing controls | Smallest next evidence / explicit closure decision |
|---|---|---|---|
| R01 Visible completion | **Observed visual FAIL; original attribution INCONCLUSIVE.** Semantic Ready and current text do not clear stale required labels or earlier stale source pixels. Blocks current A and B. | Owner/version/generation, source mapping, geometry/UIA, fixed full frames/crops, passive WPF batch traces; no forced layout or capture-until-pass. | Qualification owner + technical reviewer validate the observer against the design-approved boundary below, check negative controls and inspect one bounded affected journey. No historical FAIL conversion. |
| R02 High Contrast | **Historical 2.542596:1 FAIL retained against 4.5:1; overall INCONCLUSIVE.** Final candidate passes do not explain earlier candidate failures. Blocks current A and B. | Actual OS/WPF HC checks, initial Win32/WPF/pressed-colour logs, both product orders, unchanged ratio assertion. | Accessibility/runtime owner executes the fixed discriminating protocol below; identify application vs propagation scope without excusing a failed supported control. |
| R03 TreeView shutdown | **Intermittent original functional failure; cause INCONCLUSIVE.** Original upgrade saved-close missed 20-second predicate; reinstall unaccepted in that lifecycle. No demonstrated reflow causality. | Payload/source/state hashes; ordinary installed authoring; eight later diagnostic lifecycles / 24 saved closures pass, immediate UIA during exit unavailable. | Review original input/owner/save evidence; keep open with explicit owner disposition. Escalate on a single recurrence in a normal affected qualification or report, with a new discriminating hypothesis, not repeated closure sweeps. |
| R04 Startup | **Incomplete original startup; cause INCONCLUSIVE.** Five early records, no owner/binding/render/resize before 240-second termination; not demonstrated resize regression. | 32 later fresh-process controls pass; passive startup/dispatcher/process markers, trace-off controls and unchanged supervision. | Retain missing chronology and explicit owner disposition. One recurrence before binding triggers targeted process/dispatcher evidence; do not rerun a resize matrix to attribute pre-binding failure. |
| R05 Selection | **Intermittent functional/automation failures with inconclusive attribution.** Original HC preparatory-clear and Pages wheel geometry failures; latest baseline-only UIA GetBoundingRectangles exception is a different observation. | Existing selection suites, baseline/candidate and trace-off/on controls, actual input and expected selected text/visible geometry. Final selection suites/candidate hardening orders pass independently. | Selection/automation owner reviews each original boundary separately. One fixed original HC Pages journey on exact products with one normal control; capture provider exception, input, selection and viewport ownership. A recurrence/wrong selection blocks A; no pass-count closure. |
| R06 Pending interruption | **Incomplete coverage**, not an observed candidate cancellation defect. Twelve Pages inputs qualify actual pending interruption; 24 Continuous/Automatic deliveries arrived 33.7637–54.7149 ms after completion. Blocks a claim that all pending modes are qualified. | Controlled owner/cancellation checks, Pages delivery chronology, outgoing source preservation and rejection of stale Ready. | Review whether each supported mode permits the pending state; predeclare a bounded witness without sleeps or a production delay. If delivery is late again, record INCONCLUSIVE and stop. Owner must decide narrower supported claim or further discriminating evidence; no NOT APPLICABLE merely because the race is short. |

R03/R04 could eventually receive an explicit monitored-risk disposition for
ordinary integration if no required criterion remains failed and impact is bounded;
that would be an owner decision under D13, **not** a claim that the incidents are
fixed. Current #76 remains NO-GO and these incidents are not closed here. A source
loss, stale-owner publication, inaccessible required control or incorrect selection
is never eligible for acceptance merely as unexplained variance.

## R01 — what completion means

| Layer | What evidence can establish | What it cannot establish |
|---|---|---|
| Ready model/UIA state | Application considers the exact owner/version/generation ready; callbacks and labels have changed in memory. | Layout validity, channel submission or visible pixels. |
| WPF layout and source geometry | Current reader has mapped text in its viewport, valid Measure/Arrange and usable navigation/selection geometry. | That updated status/page labels have been submitted or displayed. |
| WPF media channel | A traced batch boundary can order WPF visual work relative to capture. `CommittingBatch` occurs before commit. | A per-window presented-frame fence, physical scan-out or universal cause of earlier uninstrumented failures. |
| Software desktop frame | Exact captured pixels at recorded QPC interval, checked against current source/labels and before/after semantics. | Atomic capture of all future WPF updates or a physical monitor guarantee. |
| Physical user experience | Operator/physical-display evidence for legibility, timing, focus and assistive use on the named device. | Universal behaviour on untested hardware, DPI or runtimes. |

The final targeted review retained 336 frames/960 crops, 24 real label differences,
18 required-label differences in 11 frames (six uninstrumented). The separate
completion campaign retained 414 frames/660 crops: two required-label differences
in one traced HC frame plus three Busy-reader differences. Correct `ReadableEnd`
coexisted with stale `Preview layout changed` and `Page 2 of 259` while semantic
state reported Ready/page 259. OCR errors and clipped crops were reviewed against
full frames, not counted as application failures. Busy secondary readers at a
primary-only boundary remain distinct; their all-readers boundary still must pass.

Four traced reproductions begin the relevant new batch after BitBlt starts; two
others begin during DwmFlush and provide weaker channel attribution. These support
a frame-boundary explanation **for those reproductions only**. The earlier normal
frame with `ReadableStart / Page 1 of 234` versus semantic source 4459/page 257 is
uninstrumented and remains unexplained. Later correct frames erase none of them.
[DwmFlush](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush)
concerns queued work; it cannot make an unsubmitted WPF update appear. The
[versioned WPF media source](https://github.com/dotnet/wpf/blob/v10.0.14/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Media/MediaContext.cs)
and [threading model](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model)
support distinguishing UI work and presentation, not inventing a compositor promise.

**Design-approved boundary; observer qualification and numeric budgets pending:** keep model
Ready as an application state and measure visible completion separately from the
offered action. A qualifying software frame must show the expected current source,
selection and required labels with consistent before/after ownership and layout;
all required readers must qualify within the approved finite completion budget.
The observer must establish the relevant visual update ordering, or mark it
INCONCLUSIVE; Render-priority callback, CommittingBatch or DwmFlush alone is
insufficient. Use predeclared capture times/counts, retain every frame, and classify
any earlier mismatch as a transitional observation, observer ambiguity or defect
only with evidence. Do not search screenshots until one passes. This proposed
boundary is not yet implemented/qualified and does not alter existing pixel assertions.

A genuine defect includes blank/stale source or selection after an accepted visible
completion, wrong-owner content at any success publication, misleading status that
persists beyond the approved visible-completion budget, or inaccessible controls.
False application Ready while required work/ownership is invalid fails immediately,
independently of pixels or latency. A same-instant mismatch alone does not prove a
permanent compositor defect; it also does not satisfy today's failed assertion.
No numeric grace period is authorised. Physical presentation belongs to Contract B.

Minimal next step: review the existing paired traces and full frames; predeclare
one Pages primary/secondary Find journey in normal/actual HC with trace-off/on
controls and negative cases for stale owner/text/labels. Stop if ordering still
cannot be established; request a specific observer design decision, not another
matrix. Keep the existing deadlines and all original failed frames.

## R02 — targeted High Contrast qualification

The mixed pair `#FF0078D4 / #FF263B50` produces 2.542596:1 while Win32 highlight
reports `#FF8EE3F0`. The pre-assertion fails **before application capture launch**;
it proves failure of that colour requirement in the validation process, not a
captured application control defect. The same ratio occurs on baseline and
candidate in separate orders/checkpoints. Runtime/system-colour propagation is a
plausible hypothesis, not established cause. Final candidate 4.528515–7.893273:1
does not clear the historical candidate FAIL. A demonstrated application defect
would require the application to select/render an incorrect pair or fail a supported
theme transition; observe the actual control rather than infer it from this preflight.

Recommended fixed protocol, **not executed here**: one approved disposable Windows
image, exact develop `99349d82` and candidate `7b1334b0`, packaged .NET 10.0.13 and
recorded hosted .NET 10.0.14 as separate controls, both product orders, one fresh
pair per cell (eight product launches total), plus one minimal stock-WPF process
per runtime. Preserve the original pre-assertion process separately: source inspection
shows that it enables HC inside the validation process before creating theme resources,
and may fail before launching the application. HC-already-active application launch
is a distinct diagnostic control, not the same startup order. The qualification plan
specifies both process identities and does not broaden the four-cell product matrix.
Record initial OS/WPF state and all colour pairs before first app interaction.
Observe real theme notifications and one HC→normal→HC transition,
actual pressed control colours/pixels, keyboard
focus and selection. Correlate the preflight PID with the product PID; do not treat
their colour caches as one process. Preserve every cold-start result even if later
transition colours pass. Named runtimes are diagnostic reproductions, not newly
approved release versions.

No prewarming, synthetic notification, colour substitution, deadline increase or
4.5:1 reduction. If only stock WPF/preflight reproduces, record that narrowed scope
and qualify the actual app separately; do not infer safety. If the app reproduces,
fix the demonstrated application path in separately authorised work. If neither
reproduces, stop with INCONCLUSIVE and request explicit residual-risk disposition;
no repetition campaign or closure by later passes. Physical HC/screen-reader checks
remain additional B requirements. [Microsoft guidance](https://learn.microsoft.com/en-us/windows/compatibility/high-contrast-mode)
supports matched system-colour pairs and reacting to theme changes, but does not
establish the cause of this incident.

## Evidence taxonomy and escalation

Proven functional acceptance is bounded to the [review index](pr76-review-index.md)
and exact receipt. Known functional failures (including historical fixed checkpoints)
retain their failed evidence. The shared old blank-preview defect is historically
reproduced and corrected within the integrated scope; it is neither proof of
candidate-specific regression nor general candidate safety. R01/R02 are current
correctness/accessibility blockers, R03–R05 unresolved intermittent observations,
R06 incomplete coverage. Continuous 250 ms is a **performance policy FAIL**.
Physical DPI, screen reader, clean machines and signing are **production certification
gaps**, not evidence that the five completed features failed.

Escalation is bounded: one recurrence with new discriminating evidence or one
demonstrated candidate-specific defect justifies a focused issue/fix under existing
#75/#77/#79 tracking; a new issue is needed only if that tracking cannot contain it.
Maintain exact source, fixture, runtime/OS and observer identities, the unchanged
deadline, input/ownership chronology and original output. Stop on inconclusive
results. Independent technical review and human owner disposition are required;
passing repeats alone never close a failure.
