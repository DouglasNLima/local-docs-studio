# Reading preview reflow integration

Goal #75 / Draft PR #76 remains **NO-GO** until every mandatory acceptance gate passes. This receipt describes the integrated source and predeclared validation; the PR conversation and Goal carry the exact terminal hosted results. It does not activate proposed ADR 007.

## Source and independent review

The starting PR #76 source is `75de42474514af0e7a6e521235a6517f4acad14b` (tree `6cb88161e24573649321bcb1d9f3418ba128194b`). PR #80's final reference is `22c223c6f56fa41fda2a36102e1eed0af44974ec` (tree `89e012f468801ac7f445113b811ca2fbe041c8d9`).

All three product commits were cherry-picked, in order, with their origins retained:

| PR #80 source | PR #76 integration | Review |
| --- | --- | --- |
| `3f58591743abf81a5c96df5a4183c5b0b46fb995` | `13be8af7c97b93bfac8ed2c5e4e9bcdde3b6538e` | Capture the visible source before changing layout, restore through natural WPF layout, guard document/version/generation and cancellation. |
| `4f133d1b110dd6b600d3e1ae06fe725a598feb4a` | `1332221463b5e457e67835995ba35f5719213808` | Convert character geometry at the current zoom into native scroll units. |
| `a798eaf051baa14470a14b1af1825abe2284a9ec` | `570a6546a359e95843a2adccb57dc4e90ffd1d1f` | Re-selecting the same layout must preserve its pending presentation. |

No textual conflicts occurred. The semantic review retained PR #76's comparison-mode normalisation, active slot ownership, editor/source bindings, selection restoration and synchronisation guards. It found two additional integration paths:

1. Closing Preview/Preview widens the shared reader without changing its layout name. A controlled run with the exact original mixed fixture reproduced a blank viewport: the same current document remained attached, Measure/Arrange were valid, status remained Ready, and the mapped end text moved below the viewport. Capture before detachment and restore after closure now cover this geometry change.
2. Activating the secondary pane replaced the shared reader without first saving the secondary document's reading position. A controlled switch returned that document to the top despite prior valid mapped end text. Both readers now save version-owned source positions; secondary reconstruction restores a mapped source anchor rather than trusting an old pixel offset. Selection restoration is restricted to its owning source version. Secondary completion follows restoration and rejects obsolete generations.

These are focused comparison changes in `MainWindow.Comparison.cs` and `MainWindow.ComparisonPreview.cs`. The source-position mechanism remains in `PreviewViewportAnchor`; the three imported commits retain their synchronisation and cancellation changes. No authored source buffer, persistence, Undo/Redo, export or recovery format is changed. No observation deadline, performance threshold, High Contrast requirement, retry or forced layout loop is added.

A retained 150 percent switch/closure failure also showed the same reader receiving the queued WPF Unloaded/Loaded pair during reparenting. Its valid restoration was cancelled on Unloaded before the new geometry arrived. Comparison closure now waits for the normal Loaded dispatcher boundary before attaching restoration observers; status remains Busy and the original ownership/cancellation guard stays active. This is a lifecycle boundary, not a timed delay or forced layout. Three independent local repeats and the pending-closure control subsequently passed; the failed observation remains evidence.

## Preserved evidence

The three new failed controls are preserved under [the integration evidence manifest](evidence/preview-reflow-integration/sha256.json).

The original six failed observations and all 42 files remain byte-preserved at [PR #80's exact original evidence](https://github.com/DouglasNLima/local-docs-studio/tree/22c223c6f56fa41fda2a36102e1eed0af44974ec/docs/native/evidence/blank-preview/original). The [source-labelled PR #80 investigation](https://github.com/DouglasNLima/local-docs-studio/blob/22c223c6f56fa41fda2a36102e1eed0af44974ec/docs/native/native-blank-preview-recovery.md) and [final receipt](https://github.com/DouglasNLima/local-docs-studio/pull/80#issuecomment-6700496712) remain independent evidence. PR #76-specific attribution remains INCONCLUSIVE; successful repeats do not erase intermittent failures.

The original mixed fixture is 4,475 bytes, SHA-256 `6b7cd92a7dcf1dd88ff31e68fb196bde2ab76655f99e9f9fee93d82bf57280a0`. All seven inherited fixtures match their original manifest. Git attributes preserve their bytes on Windows. A preliminary local control used checkout-converted line endings; it is retained separately and excluded from exact-fixture qualification.

## Predeclared validation and limits

The inherited original foreground journey, source/content/visual/UIA assertions, 30-second observation deadline and 240-second child bound are retained. Eight independent normal/actual OS High Contrast, initial-order and tracing cohorts each run four fresh baseline/candidate pairs and three original interaction cycles. Original layout/zoom/selection/supersession controls run separately.

Twelve comparison cohorts cover Continuous/Automatic/Pages, both actual appearances and both initial orders. Each runs fresh processes for closure, ownership (Editor/Editor, Editor/Preview, Preview/Preview, Find, selection and Undo/Redo), pane resizing/zoom, and rapid switching/closure. Each scenario has an uninstrumented and instrumented process on both exact products. Independent off-screen controls exercise closure, switching, resizing and deterministic pending reflow; Continuous additionally covers 50/75/150/200 percent zoom.

The same compiled harness is used against exact pre-integration PR #76 and candidate product assemblies. Protocol records retain commit/tree and binary hashes. The runner checks foreground ownership before real pointer/keyboard input and desktop capture; local off-screen controls cannot count as foreground evidence. Opt-in transition diagnostics contain identities, versions, generations, attachment, template/visual-tree geometry and status, without authored content. The original performance report code is diagnostic infrastructure only and grants no performance-policy acceptance.

Complete affected native and Chromium/Edge suites, original Continuous 250 ms, selection and actual OS High Contrast gates remain mandatory. Every failure is retained; there is no retry-to-PASS rule. Baseline and candidate outcomes, functionality, preview correctness, regression acceptance, performance and High Contrast require separate verdicts. Passing bounded samples establish only the tested behaviour, not the absence of every intermittent failure. Physical screen-reader and physical-machine certification remain separate.

Keep PRs #76, #78 and #80 Draft. No merge, deployment, release, signing, tag, branch-protection or policy action is authorised by this integration.
