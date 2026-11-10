# Independent review of the two PR #83 corrections

**REVIEW PASS — OWNER PROVENANCE DECISION REQUIRED**

One separate read-only reviewer found both previously blocking defects
corrected. No new blocking finding was identified within this bounded review.
The reviewer did not implement these changes, edit files, rerun product suites,
initiate hosted CI or activate provenance.

Reviewed preparation: `d16c43d3308d7824603862f73e85987f08ce8328`, complete tree
`cbce6c76829b9398e0e2aa21b0a22413449e91e9`.
Reviewed/tested product: `932335027314fe710671d54266367a008fca5547`, complete tree
`c58468fc41222f56eb38f277950486686e820553`, native subtree
`4deeb6f0b54fd2e982525968500fbbe94030563d`, focused control subtree
`a04548454b40bb63f954d240e02cea5ac0b0ec7d`.
Preparation contains identical product/control bytes. Subsequent review/evidence
preparation must preserve these identities and the manifest below; the final
preparation HEAD/tree must be supplied in the owner decision.

## Product correction

`RestorePreviewLayoutAsync` checks effective Scroll mode and current
source/document ownership before creating a cancellation source, loading ticket
or asynchronous restoration. It clears incompatible or obsolete retained anchors
and returns. Changing the hidden reader to Pages therefore cannot invoke the
Scroll-only helper and classify valid Pages content as failed. Source, document,
caret, selection, focus, zoom and legitimate loading state are preserved.

The correction is confined to the already-added `MainWindow.PreviewReflow.cs`.
The other four reviewed product components, normal cancellation/ownership paths,
helper, renderer, exports and launch infrastructure are unchanged. No unrelated
PR #76 functionality was introduced.

`PagesTransitionControl` uses the production Settings Apply callback in a shown
WPF window. It meaningfully covers direct Preview and Split returns, Preview →
Split, return to Continuous, source/version ownership, absence of inappropriate
loading/failure publication, UIA-visible text, editor/reader state and rapid
document/layout replacement.

Both original-source FAIL records were inspected: the initial control detects
the retained incompatible anchor; the settled record demonstrates `Failed` /
`Preview unavailable` while Pages text is visible. Both are preserved.
Corrected-source records pass 31 normal checks and 31 with actual Windows/WPF
High Contrast flags. All eleven original focused controls pass against the exact
corrected product identity.

## Canonical provenance

Independent binary Git-blob reading gives canonical manifest SHA-256
`340745a23758a94cb50b9aadeb761b817697777481a39762dbe0465ef64a8681`
for `preview-reflow-integration-three-v2`. Status remains
`PROPOSED_NOT_APPROVED`, with `ownerApproval: null`.

The verifier separates committed identity from checkout integrity: it requires
the pinned committed regular blob, matching index identity, working bytes
differing only through line endings, and the same path-specific Git-clean
object. It never parses/re-serialises JSON to manufacture identity.

All eight canonical-control implementations/results were inspected: actual
LF/CRLF materialisation, semantic/spacing changes, missing/substituted working
content, staged substitution with restored working bytes, changed committed
content and missing committed content. The full-repository Windows clone results
also pass in both LF and CRLF checkouts with identical canonical identity despite
different working-file digests.

Independent verification established:

- Exactly the specified three protected paths change within the 2,421-path
  inventory relative to accepted develop; all previous/current hashes match.
- All nine added source/control/fixture digests match committed bytes.
- All eight pinned historical approval/scope records match accepted develop.
- Native, control, fixture and historical-evidence trees match the proposal.
- Original v1 proposal files and local evidence remain unchanged.
- Active compliance scripts, licence configuration and workflows are unchanged.
- All 22 retained correction-evidence digests match committed bytes; both recorded
  product/control binary digests match the available built assemblies.

The reviewer independently ran the read-only proposal verifier: PASS, with all
nine manifest-scope mutations rejected. This is not active OSS-provenance approval.

## Evidence and decision boundary

Retained evidence records Core 2,320 passes, Find routing 48 checks, selection
navigation 16 cases and offline loading 11 controls. The single new-source shown
loading execution records 106 passes, explicitly including dense ER and closure.
This does not establish the cause of the original baseline/candidate failures
after 104 checks at the unchanged six-second deadline. Those FAIL outcomes and
unreached closure assertion remain intact. Fresh mandatory hosted qualification
is still required; this review and the local PASS waive none of it.

Owned-window WPF/UIA evidence supports the scoped functional/accessibility
conclusions, not manual physical-foreground, display or screen-reader
certification. The desktop helper identity problem remains separate.

The previously noted possibilities of indefinite waiting for geometry and
unexpected exceptions lacking a dedicated observer were neither changed nor
newly demonstrated. They are not additional established blockers for this scope.

The smallest next owner decision is bounded activation of proposal v2, explicitly
identifying its canonical digest, product commit/tree and final preparation
HEAD/tree. Limit activation to its seven listed paths and preserve the three
substitutions, nine added-file identities, source/history pins and prior approvals.
After separately authorised activation, use one normal hosted qualification with
all 30 mandatory develop contexts. Keep PR #83 Draft; do not merge automatically.
The historical 250 ms FAIL supplies no inherited exception. No release, signing,
deployment, distribution, policy migration or Native Stable certification is
approved by this review.
