# Document statistics validation and decision receipt

**Isolated source/review PASS; Draft preparation only.**
Base `2dbc6d4e61db431001b42bb93b02d8e503eb8096`.
Product/control commit `5b336179b5cbbb97d03880c0cf9674a2decaa000`, complete tree
`67b10c1ce78b4843b35c852bc4b8dabe4cb87817`; native subtree
`5aeb8c1f09f16c2b790de1542e45dcd3b0e41649`; statistics control subtree
`1c751d38a4239cff4fa917cb61fc5fc86bc80e6b`.

| Focused check | Result |
| --- | --- |
| Core statistics, Review, loading state and Flowchart compatibility | 67 passed, zero failed/skipped |
| Shown WPF statistics, Light / Dark | 48 / 48 checks pass: no/empty/large source, rapid edits, queued cancellation, version-only stale rejection, Undo/Redo, open/reload/switch/close, Review keyboard activation, UIA labels, source/BOM bytes and min-width export/Cancel controls |
| Current Find / selection regressions | 48 checks / 16 cases pass |
| Continuous mapped reflow / Pages Settings transition | PASS / 31 checks pass |
| Shown preview loading lifecycle | 106 checks pass, including existing dense ER and closure assertions |
| Offline PR #82 presentation | 11 controls pass |
| Static web integrity | PASS; no web source changed |
| Actual Windows High Contrast | NOT RUN / UNAVAILABLE: desktop helper exposed no Settings window after explicit launch/recovery. No OS appearance changed |
| Hosted qualification / installation / screen reader | NOT RUN; outside this preparation authorisation |

[Exact results, original observations and evidence hashes](evidence/document-statistics/results.json)
retain all intermediate failures. The first Core expectation, premature Review
observer, timing assumption, stock-colour equality, intermediate non-focusable
style and unsupported export-test extension failures are retained. A single-line
large-document control was aborted without a completed result; its cause remains
INCONCLUSIVE. Passing bounded-line controls do not relabel it. The dispatcher
control proves progress while queued behind the bounded worker; source inspection
establishes that snapshot materialisation/parse/count execute inside Task.Run.
It is not a performance benchmark or a single-line editor fix.

The final 67 Core and both WPF statistics runs used the committed product source.
Existing regression runs used identical canonical native product bytes before
the commit; their assembly revision string still names the starting base.
No changed threshold, historical cohort campaign or performance-policy transition
was used. The full browser suite was not repeated because browser assets and
workflows are unchanged; static checks ran.

Independent read-only review passed the exact product/control commit with no
blocking finding. It excludes independent physical GUI/HC execution. The final
provenance review and preparation HEAD/tree are recorded in the Draft PR receipt.
Ordinary strict governance intentionally still rejects the unapproved successor
native source. The inactive proposal identifies every protected pair and added
file; it activates no substitutions and grants no licence scope. Foundation
inventory and actual governance-control outcomes accompany that proposal.

The next owner decision is bounded provenance activation for the exact proposal;
hosted qualification and merge each require separate authorisation. All 30
mandatory develop contexts remain required. No PR #81/#82/#83 approval is reopened.
Continuous 250 ms remains FAIL and unchanged; no performance exception transfers.
ADR 007 remains inactive. No signing, tagging, release, deployment or distribution.
Public release and Native Stable remain NO-GO.

## Historical consolidation (independent outcome)

PR #80 is [administratively closed as SUPERSEDED BY PR #83](https://github.com/DouglasNLima/local-docs-studio/pull/80#issuecomment-6768280191),
not merged. Its receipt maps source-position/media/repetition/ownership/
cancellation/source/selection/caret/Find/zoom/Continuous/Pages/WPF/UIA/HC
requirements to accepted merge `2dbc6d4e…`. The branch and original commits,
NO-GO, performance FAIL, High Contrast failures and uncertain attribution remain
preserved. [Goal #79 stays open](https://github.com/DouglasNLima/local-docs-studio/issues/79#issuecomment-6768287121)
because its broader unresolved acceptance item has no inherited waiver.
PR #76/Goal #75, PR #78 and Goal #77 remain unchanged.
