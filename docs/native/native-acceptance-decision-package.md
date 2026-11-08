# Native Acceptance Decision Package

7 November 2026 · Goal #77 / Draft PR #78 · **Design approved; operational transition pending; overall NO-GO**

B+C in ADR 007 is **approved at design level**: independently measure interaction, first usable
viewport and complete readiness, with finite budgets by workload and qualified
hardware. The [owner decision](https://github.com/DouglasNLima/local-docs-studio/issues/77#issuecomment-6714211040)
names reviewed commit `bc092512c30e152cd1e4384c1eab4f8ba30e059f` and approves D01–D13
designs/methods/plans, including contracts A/B. Numeric limits/margins, certified
support configurations, gate activation/retirement, required-check changes, risk
waivers, integration and release remain unapproved. See the
[bounded qualification preparation](native-acceptance-qualification-plan.md) for next work.
The [ADR decision matrix D01–D13](../adr/007-native-document-rendering-performance.md#decision-matrix--7-november-continuation)
is the single architectural decision register; this package supplies its qualification contracts.

**PR #76 integration verdict: NO-GO.** Its five workstreams and integrated reflow
correction are feature-complete in their documented supported scope. Bounded
functional PASS is preserved. Visible completion and accessibility remain unresolved,
intermittent failures retain their original status, performance non-regression is
unqualified, and the unchanged 250 ms campaign gate fails. No production code,
renderer, threshold, historical evidence, required check or protection changes are
part of this continuation. PRs #76, #78 and #80 remain Draft; Goal #77 remains open.

## Source lock and evidence authority

Live GitHub metadata and fetched Git objects were compared before editing. These
are source commits, not synthetic PR merge checkouts. Reverify them at any later decision.

| Source | Exact commit | Tree |
|---|---|---|
| develop | `99349d82eb06960eb1f9785cf384db2373aed4fa` | `e4d137a12cbdd827583fcb841612890dd88736ec` |
| PR #76, current qualification subject | `7b1334b09a02ad2fbfd4a0234798e6c387d3e634` | `887856d293114442e97e0d06bef2928b340b639a` |
| PR #78, original package preparation start | `f839182ceb16c20c4a8d58ddd39513aa81c267b5` | `62c359f62ff9ca3ebd66fe1ac2246277ecf0653d` |
| PR #78, owner-reviewed design / qualification preparation start | `bc092512c30e152cd1e4384c1eab4f8ba30e059f` | `90f2dc1480a5eaed7b6628d4a41d6cce9e2265aa` |
| PR #80, separate correction | `22c223c6f56fa41fda2a36102e1eed0af44974ec` | `89e012f468801ac7f445113b811ca2fbe041c8d9` |

The delivery comment on PR #78 binds the resulting package commit/tree and checks;
the starting #78 SHA above is not a claim to contain this package. Supporting
[source/check snapshot](evidence/acceptance-decision/source-identities.json),
[PR #76 review index](pr76-review-index.md) and
[risk register](native-acceptance-risk-register.md) keep the detail inspectable.

Primary current evidence is the [7b1334b0 terminal receipt](https://github.com/DouglasNLima/local-docs-studio/pull/76#issuecomment-6713058791):
15 terminal workflows, nine success/six failure; 36 successful Native jobs/two
declared skips; 2,382 Core tests and 157 tests in each browser pass. It records
128 candidate foreground journeys and 388 controlled checks, every original failure,
full log identities and 51 archive hashes. This package audits that evidence; it
does not rerun or relabel it. The [earlier #78 receipt](https://github.com/DouglasNLima/local-docs-studio/pull/78#issuecomment-6698243971)
tested older products; its blank-preview observations are historical evidence,
not a measurement of current #76. #80's correction is already integrated into #76.

[Independent technical review](evidence/acceptance-decision/independent-review.md)
finds the recommendation decision-ready, with both minor clarifications resolved.
This is agent review evidence, not human approval or product acceptance. The delivery
receipt records documentation/static/provenance checks; full product CI is NOT RUN
for this documentation continuation and must not be inherited from the older #78 head.

## Policy precedence: integration protection is not the whole acceptance decision

The existing [governance policy](../open-source/governance.md#controls-and-limitations)
and [activated settings explanation](../open-source/governance-settings.md#exact-status-checks)
deliberately exclude Continuous 250 ms from ordinary integration protection, while
retaining its FAIL as a native production/release blocker and requiring disclosure.
The read-only [ruleset snapshot](evidence/acceptance-decision/ruleset-26127883.json)
confirms active develop ruleset 26127883: 30 contexts, strict checks, no bypass,
PR/review-thread resolution, zero required approving reviews. A legacy protection
endpoint 404 is not absence of protection.

However, the later #75–#77 acceptance instructions and current receipts expressly
keep the historical gate and unresolved observations blocking **this campaign**.
Neither an older general policy nor this proposal overrides that explicit boundary.
D13's design approval explicitly preserves those campaign restrictions. A later
owner decision must authorise any change to that scope before reassessing #76;
D12's migration-design approval does not authorise activation. No implicit exception exists.
Full production certification is not an existing prerequisite for every feature
integration, but existing installed-preview and accessibility checks remain required.

## Contract A — integration into develop (design approved)

Scope: a reviewed feature on an exact source/tree and stated supported workflows.
Every row is independently necessary. No demonstrated candidate-specific correctness
regression is allowed; lack of attribution is not proof of safety. A known baseline
defect needs explicit impact assessment, containment and owner risk disposition;
it cannot clear a mandatory failed criterion or an unresolved current #76 blocker.

| Requirement | Acceptance evidence | Current #76 position |
|---|---|---|
| Source/data preservation | Exact buffers/files/assets before/after; Save/Save As/conflict, Undo/Redo, independent comparison histories, recovery and outgoing unsaved documents preserved. | Bounded PASS in functional/installed receipts; no universal data-safety claim. |
| Stable editor, selection, navigation | Actual input, caret/selection direction and owner, Find/Outline targets, pane switching/closure and supported mode/zoom changes; retain original failures. | Functional PASS; intermittent selection and startup/shutdown risk remains open (R03–R05). |
| Preview ownership and completion | Current document/version/generation; source-mapped usable viewport; reliable cancellation/supersession; truthful Ready/Busy/failure; full required-reader checks. | R01 visible FAIL / attribution INCONCLUSIVE; R06 pending coverage incomplete. |
| Relevant automated regressions | Exact-source Web Chromium/Edge, Core, compiled native controls and foreground journeys. Preserve all 30 required contexts and already-required installed preview, recovery, export and 24-family checks. Record failed/skipped/neutral jobs separately. | Passing final functional suites coexist with six failed workflows. Green aggregate alone is insufficient. |
| Accessibility in supported scope | Keyboard focus/order/operation, labels/UIA patterns and selection, real OS High Contrast; unchanged 4.5:1 assertion. A changed UIA/keyboard/colour path needs targeted real assistive-use evidence if automation cannot assess it. | R02 retained 2.542596:1 FAIL; R05 selection uncertainty. Physical release certification cannot substitute for these checks. |
| Performance and bounded failure | Preserve current applicable checks. Under a future approved B+C policy: paired non-regression plus approved integration action/viewport/completion checks; no blank/stale preview, lost content, inaccessible control or false completion allowed. | Current campaign 250 ms FAIL; new numeric SLO/non-regression not qualified. No replacement authority yet. |
| Review and risk disposition | Separate production/validation/evidence review, exact dependency and provenance assessment, unresolved-risk list and independent technical review; explicit human owner decision on exact SHA/tree. | Index/package ready for review; technical agent review is evidence, not owner approval. |

A feature that touches installation, updater, recovery, accessibility or runtime
support must run the relevant affected safety checks **at integration**. The split
does not defer a feature's own correctness until release. Complete trust/signing,
every physical configuration and clean-machine production certification belong to
Contract B unless a specific change depends on them; record that dependency rather
than imposing all of B on ordinary feature work.

## Contract B — native distribution/release (additional, independent qualification)

Passing A never implies B. Use the exact distributable hashes/source/runtime, not
only a branch build. Existing [release gates](../open-source/release-gates.md) and
[preview readiness limits](windows-preview-readiness.md) remain authoritative.

| Additional obligation | Required release evidence / decision |
|---|---|
| Supported OS/hardware | Owner-approved exact Windows editions/builds, x64 CPU/RAM/GPU/storage floor and runtime. Preserve pending Windows 10/11 obligations unless explicitly changed; no certification from hosted CPU labels. |
| Installation/upgrade/uninstallation | SDK-free clean-machine first launch, restart, historical-preview migration, supported upgrade/reinstall/uninstall; workspace, drafts/assets/state and association choices preserved. Existing hosted checks are supporting evidence. |
| Physical display/DPI | Real 100/125/150/200% displays, mixed-DPI monitor changes, window sizes and application zoom; clipping, focus, selection, geometry and visible completion. No substitute based on RenderTargetBitmap or hosted 96-DPI screenshots. |
| Assistive technology | Named screen reader/version on approved OS, keyboard-only editing/Find/Outline/comparison and preview navigation, announcements and focus continuity; High Contrast transitions and controls. UIA pass alone is not screen-reader acceptance. |
| Signing/trust | Separately approved signer and production CI/trust boundaries, final signature/chain/timestamp/revocation verification, immutable hashes and manifests. No signing is authorised by this package. |
| Installer/updater safety | Exact signed update path, manifest integrity, rollback/repair/failed install and interruption, downgrade controls and no user-data rollback; security review and clean-machine evidence. Mock signing tests do not certify trust. |
| Performance envelope | Approved finite complexity/resource limits, separate interaction/viewport/completion SLOs, sustained load, cancellation and oversize/recovery behaviour, statistical/non-regression acceptance on the physical floor. Existing 250 ms release authority remains until D12 activation. |
| Release authorisation | A separate human decision naming version/channel, source/tree, package hashes, supported envelope, limitations and release/trust evidence. Integration approval is insufficient. |

Current B: **NO-GO / incomplete certification**. This is separate from #76's
integration NO-GO. The five feature workstreams do not need to be reopened merely
because these production obligations remain outstanding.

## Statistical and non-regression proposal

Approve the method before choosing limits or collecting acceptance data. Freeze
action, mode, complexity, appearance, hardware, runtime, cold/warm and load strata;
use both product orders and identical harness/fixture bytes. Process-cold is not
OS/disk-cold. Keep failures, timeouts and missing cells; an invalid collection
cannot pass. A baseline failure is never a candidate pass or justification for a
larger budget. First trials, repeat ordinals and exclusion rules are predeclared.

For A, use the affected correctness journeys and a bounded matched comparison
designed for its approved precision; do not label a smoke test P99 certification.
Choose independent runner/session blocks where shared-host correlation matters;
process launches alone do not make shared machine conditions independent. Report
all sample sizes, process/block counts, medians, maxima, confidence and missingness.
No non-regression PASS until the approved margins and precision are satisfied.

For B (and any replacement gate claiming tails), recommend **one-sided family-wise
95% confidence**, with a predeclared family of K claims and Bonferroni allocation
`alpha_i = 0.05 / K`. K includes the SLO and absolute/relative regression claims
across the relevant actions and strata; it cannot count only favourable cells or
omit the two regression comparisons. For independent trials with zero threshold exceedances,
the exact upper bound is `1 - alpha_i^(1/n)`; to bound exceedance by q require
`n >= ceil(log(alpha_i) / log(1-q))`. At K=1 this needs 59 trials for q=5%,
299 for q=1%; 300 is not sufficient for an unspecified multi-cell family.
Non-zero exceedances require exact binomial bounds. Correlated trials require a
reviewed cluster/block method, not reuse of this independent-trial formula.
Warm observations need process/block-aware intervals; the existing 200/2,000
descriptive-quantile floors do not create independent evidence or certify tails.

For non-regression, require upper confidence bounds on both the paired absolute
degradation and relative degradation to fit predeclared action/class margins;
also meet activated absolute SLOs. Select margins from human task sensitivity and
baseline precision, not the candidate's observed slowdown. If baseline latency is
near zero, predeclare a meaningful absolute-only rule instead of an unstable ratio.
“No statistically significant difference” is not equivalence. The pilot reporter
does not implement these acceptance calculations: review its implementation and
negative controls before activation. Hardware/corpus selection, margin calibration,
power/precision and resource-cost approval remain additional evidence work, not a
request for another unrestricted campaign.

## Historical 250 ms transition — plan only, not activated

| Step | Concrete deliverable and owner decision |
|---|---|
| T0 Preserve current authority | Keep the original workflow, six fixtures, construction/input/cancellation/layout assertions and every historical FAIL unchanged. Record ordinary-integration exclusion, current campaign hold and release blocker separately. D13 design approval preserves that scope; altering it requires a separate explicit decision. |
| T1 Freeze replacement specification | Owner approves exact ADR D01–D11, hardware/OS/display/workload manifest, finite budgets, truthful completion/failure semantics, statistical claim family and non-regression margins. User tolerability constrains limits; baseline calibration tests feasibility; candidate fit cannot set the limits. |
| T2 Validate the replacement | Independent review of input/ownership/visible-content/selection/HC observers, software-frame boundaries and negative controls rejecting blank, stale, wrong-owner, lost-source and false-Ready cases. Qualify collection and confidence aggregation separately. Resolve R01/R02 and mandatory risk disposition; finite timeout/memory limits must not be mistaken for SLOs. |
| T3 Shadow qualification | On approved exact hardware/products, report the unchanged historical result beside proposed interaction, viewport, complete-readiness and non-regression results. Retain both. No retrospective conversion of old FAILs. Qualification is fresh and bounded by the frozen plan. |
| T4 Review authority and checks | Separate owner approval names exact policy version, scope, effective date, old/new check contexts, valid PASS/FAIL/INCONCLUSIVE semantics and rollback. Current 30 contexts all remain. Neither Continuous nor proposed UX collection is currently a named required context; a successful collection workflow must never masquerade as replacement acceptance. Any proposed addition/change needs a separately reviewed ruleset patch and owner activation. This task prepares no settings mutation. |
| T5 Requalify PR #76 | Reverify current develop and #76 SHA/tree, integrated #80 origins, supported modes/secondary panes, required functional/accessibility jobs and R01–R06 dispositions. Apply the approved policy to fresh exact-source evidence; do not borrow old green runs for a changed source. Record separate functionality/correctness/regression/performance/HC/release verdicts and human integration decision. Keep Draft until authorised otherwise. |
| T6 Retain/rollback | Keep the historical benchmark reproducible and labelled with its actual FAIL even after approved retirement of blocking authority. If observer identity/coverage breaks, invalid content passes, false Ready appears, semantics change or qualification is invalid, suspend new PASS decisions, restore the prior policy/check authority through the owner, retain all evidence, and requalify affected PRs. Do not erase the migration or failure record. |

No full suite is required merely to review this documentation update. Required
checks remain required for a future integration decision; intentionally not
running them now is **NOT RUN**, never satisfaction or removal of those checks.

## Recorded owner decision and minimum next work

The owner approved D01–D05 designs/definitions/classification; D06–D08 hardware,
Windows/runtime and display qualification plans; D09 statistical methodology
subject to independent implementation/sampling validation; D10 non-negotiable
correctness/accessibility criteria; D11 comparison methodology; D12 migration
design only; and D13 separate contracts preserving mandatory/campaign checks.
**Still pending and not activated:** exact limits and margins,
supported maximum complexity/resources, physical support claims, validated
confidence implementation, closure of mandatory correctness/accessibility risks,
and final replacement-gate/#76 qualification.

Future operational decision record (unfilled; not an approval):

> Reviewed implementation/qualification commit/tree: __. Design amendments, if any: __.
> Exact hardware models/OS/runtime/display/corpus and operator: __.
> Numeric budgets/margins and statistical plan: pending or exact approved version __.
> Risk R01–R06 owners and closure evidence: __. Contract A campaign reconciliation: __.
> Gate activation: NOT AUTHORISED unless separately identified __.
> PR #76 integration: NO-GO unless separately requalified and approved __.

Authorised next preparation is deliberately narrow: R01 visible-completion and R02
HC protocols plus hardware/corpus calibration inputs, as specified in the
[qualification plan](native-acceptance-qualification-plan.md). R03–R06 remain under
their existing controls; this preparation does not add their diagnostic matrices.
Physical machines and a small real corpus need exact nomination before collection.
No renderer rewrite, new feature, threshold activation or broad campaign is authorised.
