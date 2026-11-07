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

The first integrated checkpoint, `08be234c753bacdc74e051513fc84e9ee7bc29e2` (tree `c3bd6be4e574f9d0887f85ee238d4df8af30d2bc`), completed all 13 hosted workflows. Its [foreground integration campaign](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42357319688) passed 32/32 candidate original mixed journeys; baseline passed 30/32, with one normal and one actual High Contrast blank transition. Candidate original controls passed 48/56: all eight Pages cases failed. Candidate comparison foreground passed 57/96, with 16 zoom/reset failures and 23 Pages return-to-pane failures; all 80 candidate off-screen comparison controls passed. Baseline comparison foreground passed 64/96, with 32 Pages failures; 36/48 comparison controls passed. This disagreement is why off-screen controls alone cannot qualify the feature.

The retained checkpoint led to further source-position corrections, without changing the original assertions: zoom buttons and reset now capture before changing geometry; Pages resolve the still-visible explicit navigation target through a uniquely cancellable asynchronous paginator request and observe actual visible geometry; opening/closing Find captures before changing reader height. A native request to go to an already-current page is checked immediately instead of waiting for a layout event that it need not raise. Shared/secondary bookmarks use the same mapped source target. Fresh Find and preview keyboard navigation cancel obsolete restoration. The implementation follows the [WPF per-request paginator API](https://learn.microsoft.com/en-us/dotnet/api/system.windows.documents.dynamicdocumentpaginator.getpagenumberasync?view=windowsdesktop-10.0); it adds no forced layout or timed retry.

The comparison resize observer was corrected to use Up/Down for stacked rows and Left/Right for columns, require keyboard focus on the divider, and require actual geometry change. Zoom and reset each require the mapped end text to remain visible before proceeding. These strengthen the observer; the previous failed evidence remains preserved. New wide/compact zoom, independent-selection and Find-bar controls supplement the unchanged original journey. A checkpoint documentation regression added an unintended bundled Help topic; the user guidance now stays within the existing 22 topics, and the original topic-count assertion is unchanged.

The checkpoint retained mandatory failures in the original 250 ms gate, the exact-develop performance comparison, one hardening selection diagnostic and the bundled Help topic test. Selection and actual OS desktop appearance workflows passed at that checkpoint. These are independent verdicts; later source must be revalidated rather than inheriting their result. Full terminal workflow JSON/logs and all 20 SHA-256-verified integration artefacts are retained locally, with representative original failures and an immutable checkpoint receipt committed alongside this report.

The original six failed observations and all 42 files remain byte-preserved at [PR #80's exact original evidence](https://github.com/DouglasNLima/local-docs-studio/tree/22c223c6f56fa41fda2a36102e1eed0af44974ec/docs/native/evidence/blank-preview/original). The [source-labelled PR #80 investigation](https://github.com/DouglasNLima/local-docs-studio/blob/22c223c6f56fa41fda2a36102e1eed0af44974ec/docs/native/native-blank-preview-recovery.md) and [final receipt](https://github.com/DouglasNLima/local-docs-studio/pull/80#issuecomment-6700496712) remain independent evidence. PR #76-specific attribution remains INCONCLUSIVE; successful repeats do not erase intermittent failures.

The original mixed fixture is 4,475 bytes, SHA-256 `6b7cd92a7dcf1dd88ff31e68fb196bde2ab76655f99e9f9fee93d82bf57280a0`. All seven inherited fixtures match their original manifest. Git attributes preserve their bytes on Windows. A preliminary local control used checkout-converted line endings; it is retained separately and excluded from exact-fixture qualification.

## Predeclared validation and limits

The inherited original foreground journey, source/content/visual/UIA assertions, 30-second observation deadline and 240-second child bound are retained. Eight independent normal/actual OS High Contrast, initial-order and tracing cohorts each run four fresh baseline/candidate pairs and three original interaction cycles. Original layout/zoom/selection/supersession controls run separately.

Twelve comparison cohorts cover Continuous/Automatic/Pages, both actual appearances and both initial orders. Each runs fresh processes for closure, ownership (Editor/Editor, Editor/Preview, Preview/Preview, Find, selection and Undo/Redo), pane resizing/zoom, and rapid switching/closure. Each scenario has an uninstrumented and instrumented process on both exact products. Independent off-screen controls exercise closure, switching, resizing and deterministic pending reflow; Continuous additionally covers 50/75/150/200 percent zoom.

The same compiled harness is used against exact pre-integration PR #76 and candidate product assemblies. Protocol records retain commit/tree and binary hashes. The runner checks foreground ownership before real pointer/keyboard input and desktop capture; local off-screen controls cannot count as foreground evidence. Opt-in transition diagnostics contain identities, versions, generations, attachment, template/visual-tree geometry and status, without authored content. The original performance report code is diagnostic infrastructure only and grants no performance-policy acceptance.

Complete affected native and Chromium/Edge suites, original Continuous 250 ms, selection and actual OS High Contrast gates remain mandatory. Every failure is retained; there is no retry-to-PASS rule. Baseline and candidate outcomes, functionality, preview correctness, regression acceptance, performance and High Contrast require separate verdicts. Passing bounded samples establish only the tested behaviour, not the absence of every intermittent failure. Physical screen-reader and physical-machine certification remain separate.

Keep PRs #76, #78 and #80 Draft. No merge, deployment, release, signing, tag, branch-protection or policy action is authorised by this integration.
