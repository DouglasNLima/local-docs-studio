# Independent implementation review: approved Flowchart amendment

Review date: 9 November 2026. Reviewer: separate Codex agent, independent of the governance implementation author. The reviewer inspected the owner decision, implementation, historical sources and tests; reproduced one preparation defect and verified its correction; and authored only this review. This is technical review, not a new owner decision, legal opinion or release approval.

**Reviewed-code verdict: PASS.** No remaining actionable implementation finding was identified in the exact files below. The nine-pair overlay follows the approved scope and retains the full protected-byte validation. Final ledger/foundation validation, the complete 39-test result after bookkeeping, the final implementation commit/tree and its depth-1 checkout result must be recorded separately before delivery. This review does not predeclare those later outcomes.

## Exact authority and reviewed bytes

The reviewer independently used Codex `read_thread` to read the direct owner approval, then compared all 1,650 characters of `owner-approval.json`'s `approvalText` to that source: exact equality.

- Chat: `01a1c0a7-6da0-78e2-a093-03479bd7d30c`.
- Turn: `01a1c0c9-468d-7881-91e5-b88ca212a4b2`.
- User message: `01a1c0c9-4763-7ee1-a947-686395a9d833`.
- Approval received: `2026-11-09T14:49:38.000Z`.
- Approved proposal: `pr81-flowchart-provenance-nine-v1`.
- Approved preparation: commit `5c3210d7acd39593f6ae6495a32c74564b50fa98`, tree `0599acd4077d75b71fb065a9564d574fb3d18950`.
- Approved candidate manifest canonical SHA-256: `97582b1d4e1c7bf1d2f8762bf812a50c2b8aae976be1491daacb9e75bb8abb4c`.

This is the owner's direct conversation decision, not a fabricated GitHub review or an electronic signature. The pinned receipt makes the already authenticated decision immutable for this implementation; possession of an arbitrary local approval flag would not authenticate a new decision.

| Reviewed path | Canonical Git SHA-256 |
| --- | --- |
| `scripts/compliance/licence_scope.py` | `6303051730352d38fddabc51fb32e0655d08f867306645e8bb8c7e20166bd7ed` |
| `scripts/compliance/test_governance.py` | `4517eba3d3cc05249f69029e93e88126b48ed634d89ff8690b4d8836a367e375` |
| `docs/open-source/evidence/flowchart-provenance-81/approved-refresh.json` | `140568d16736ac59a0bb35b0d8199a3becda851e65588dfd5be97c8851c74a47` |
| `docs/open-source/evidence/flowchart-provenance-81/owner-approval.json` | `7232a84cb6f90256a818b0e027c3a22f95acc69e3c1a21faa5f7edcffc6aaec5` |

The reviewer checked these digests against Git clean-filter object identities without writing repository objects. The final delivery must bind the committed implementation to these reviewed bytes. The permitted seven-file scope remains the two scripts, approved refresh, owner receipt, activation validation, this review and the regenerable current rights ledger. The original proposal, candidate manifest and preparation review remain unchanged historical records.

## Scope, historical chain and ownership

All nine approved previous/current pairs match the unchanged candidate manifest, the previously reviewed protected-file differences and their actual Git blobs. The historical identities remain exact:

| Role | Commit | Tree |
| --- | --- | --- |
| Verified develop | `99349d82eb06960eb1f9785cf384db2373aed4fa` | `e4d137a12cbdd827583fcb841612890dd88736ec` |
| Protected candidate source | `b29dbb862bd6fe2eebc26deba25bab072857048f` | `53aa0f0c18455ca61cb2bc7b4abf111124de50dc` |
| Original validated hotfix | `3a8f10d737791a1182d092676ecb17a47e2fde30` | `1f13a1afa64fd0b4419303bcaf0136f1ffcecfe7` |
| Source baseline recorded by Goal #62 | `0890ec9b2ade8af24b9b3fca6cbe98cd7fd329ba` | `fd0e968cd8fb3ece8ebb73d78a859705e1605fe3` |

The reviewer independently replayed the historical map and compared actual committed blobs. All **2,421** protected entries match the develop baseline. The candidate has exactly nine mismatches before the approved overlay and zero afterwards. The overlay returns a copy, changes exactly nine existing values and leaves its input untouched. `Program.cs` chains from the effective selection-sync digest, retaining its older Goal #62 digest in the frozen record.

AST comparisons independently confirm the original validators, both earlier refresh functions and all original **24** test methods/classes are unchanged. The inserted overlay runs after selection-sync and before the unchanged full `validate_preserved` predicate. Existing licence terms, explicit grant validation, metadata disclosures and SPDX checks remain in the call path.

Each amended path remains R1 / MIT_ORIGINAL_PORTIONS_ONLY. New helper/test/document paths remain BLOCKED for the first-party grant, and the SPC fixture remains excluded R3. Evidence, artwork, upstream/adapted material and user content retain R3–R7 exclusions. The approved source trees do not grant rights to their contents. No change to existing owner approvals, dependency versions/notices, trust, accessibility, performance or required-check policy is introduced.

The original frozen Goal #62 JSON, previous refresh files, grant manifest, candidate manifest, earlier proposal/review/results and all functional native files remain unchanged. Original `.1055` package attribution remains with `3a8f10d`; this governance implementation does not create a new functional execution result.

## Failure controls and shallow-checkout design

The approved refresh must equal a complete hardcoded schema, including repository, proposal, approval/manifest digests, preparation identity, historical commit/tree identities, source subtree identities, exclusions and all nine exact pairs. Duplicate JSON keys are rejected. Missing, extra, substituted, unapproved, malformed or well-formed-but-different values cannot replace those constants. Each previous hash must equal the effective map, each current hash must equal actual canonical bytes, and each path must retain its existing R1 original-portions grant.

The current source check requires these exact HEAD subtree identities:

- `src/native`: `8c90d7e3491615d4f156b3e33b3abb7b13cbc59f`.
- `tests/fixtures/native-flowchart`: `4b0a5ae1e829c87e70d0796e5bbaed9a1e6d4b35`.

It also compares the complete current path set and canonical working-file objects within those subtrees, including the new unprotected helpers. Missing, additional or changed source files fail. There is no absent-history bypass, fallback digest or network fetch.

Those HEAD objects exist in a depth-1 checkout. Historical full-root commit/tree identities are pinned approval metadata whose actual resolution and blob comparisons are independently reproduced with full history and retained in activation evidence. Runtime does not falsely claim to resolve unavailable historical objects. This separation preserves exact source/content identity while leaving the existing workflow unchanged. An actual local depth-1 checkout validation of the final commit remains required before push.

## Finding reproduced and corrected

The initial implementation hashed the receipt and candidate after unconditional CRLF-to-LF replacement. That could accept bytes whose actual canonical Git digest differed on an `autocrlf=false` or `-text` path. The reviewer reproduced this without editing source: the normalising function accepted a CRLF receipt despite Git confirming canonical SHA-256 `d477a733408aa3ee33d86a3d7a73a2c910d6642ae55b1dd34caf8edf5f03949b`, different from approved `7232a84c...`.

The author corrected the production entry point to obtain both evidence digests through the existing Git clean-filter implementation and call `validate_flowchart_evidence_digests` before applying the overlay. This preserves legitimate Windows CRLF checkouts while rejecting different canonical bytes. The original duplicate-key and exact-receipt checks remain.

The reviewer inspected the fix and independently ran its regression control using a temporary Git repository: `autocrlf=false` plus `*.json -text` rejects the changed CRLF evidence; `*.json text eol=lf` accepts the same checkout bytes because their actual canonical content equals the approved LF bytes. The finding is **resolved**, and its earlier negative observation is retained here rather than reclassified as a pass.

## Independent verification performed

The reviewer independently ran all **15 FlowchartProvenanceTests**, including the real Git-filter regression: all passed. The tests include mutation subcases for every missing/unapproved path, exact previous/current values, source identities, approval/manifest integrity, excluded/trust/performance substitutions, duplicate fields, helper source changes and the preserved full-map failure outside the nine. No native binaries, browser workflow, installer or Continuous acceptance was executed by the reviewer.

In a separate read-only probe using actual committed blobs rather than the test fixture's synthetic actual map, the reviewer:

1. Reproduced the historical full-map failure at `src/native/LensDocsStudio.Core.Tests/LensDocsStudio.Core.Tests.csproj`.
2. Applied the approved overlay in memory and validated all 2,421 entries successfully, with exactly nine replacements and unchanged input.
3. Replaced each actual candidate digest in turn with an incorrect, well-formed digest; all nine attempts failed.
4. Confirmed the frozen Goal #62 record, existing refreshes and licence-scope bytes remain unchanged.

The reviewer inspected the author's `historical-and-current-proof.json`; its identities, pairs, counts and retained original failure agree with these independent observations. The previously reviewed preparation record retains the original 15 execution-evidence digests. Final activation evidence must verify their continued equality and record the complete applicable governance/foundation results.

## Remaining delivery checks and limits

At this review point the final regenerated ledger, complete post-bookkeeping validation, implementation commit/tree and local depth-1 checkout were not yet complete. The primary agent must verify the exact seven-file delta, preserve every prior classification/grant and historical record, run full applicable governance/foundation checks and all **39** tests, and demonstrate the final committed source works without historical objects. A final independent committed-delta check should bind those outcomes to the delivered source.

Historical OSS provenance and Continuous failures remain failures, including 28/27 and 28/31 candidate samples. A current approved governance pass does not relabel those outcomes or satisfy pending hosted checks. PR #81 must remain Draft; PRs #76/#78/#80 remain unchanged and Draft. No merge, release, signing, deployment, binary distribution, Continuous rerun, threshold change, CI waiver or ADR 007 activation is authorised by this review.
