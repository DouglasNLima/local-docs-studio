# Authorised Job fixture control: immutable result evidence

**Native control FAIL; supervisor and collector readiness BLOCKED. The one-run
authority is spent.** One explicit fixture creation and assignment were recorded,
but the Job reported total 2 and unidentified PID 57332. Fixture exit 0 and its
21-check report do not erase that failure, the missing exit identity or the early
cleanup sample. No retry occurred.

- [Result, source identities and ten observations](../../qualification/job-supervisor-control-result.md).
- [Completed independent review](../../qualification/job-supervisor-control-review.md).
- [New unedited owner authority](https://github.com/DouglasNLima/local-docs-studio/issues/77#issuecomment-6726673440).
- [Original bytes](raw-evidence.zip), [every member's SHA-256](member-sha256.json),
  [validation receipt](validation-receipt.json) and [package file hashes](files.json).
- [Unchanged reviewed plan](../../qualification/job-supervisor-control.json) and
  [previous source/offline package](../job-supervisor-closure/README.md).

Execution E is `d4261e58b55d6755feddf179ea9e922a687e3e9f`, tree
`2768237f85171258cded141101a1213d3c9bd1c6`. Source S is
`331dc5ab220e5ca7dd145ff21a650d3d2578111f`, tree
`776ef245307e9e969e19352597662de46bd35907`. The later result-delivery commit/tree
are published in Goal #77 and PR #78 after the commit exists; they are not the
source executed by this control.

| Object | SHA-256 |
|---|---|
| Raw ZIP, 26 members, 53,990 bytes | `c0157c0a66e11baf8b10eb5ecf545456fa4278c30b2f8c5d7bc63d1c08d0f2f9` |
| Authority body, UTF-8 | `5550d95aafd502981f91a439447e689e61827bf31350341d2285c5b3a13e7e59` |
| Approved plan | `415e0ecb2e10328e3a6850da926ab1c9ef237072352a7a903fb1f46424081be3` |
| Fixture | `311655cd165d4a82631efce4b827decea6a5199d2e723da67175b94a4539e549` |
| Process record | `35ab4f7eb63a943ee5f09376d7d9d346a04e4b4ef3cbf53a37df93c21a8c789d` |
| Operation journal | `6dcfda7102792df97cb49b7210a2fbf04d55d926b9b6fdc4dffdb3c779dc02ca` |
| Independent review | `99b16ce5abf16ef11c1898afeb1f1452991773be7ca2025c254f5ab4224c41c6` |
| Independent audit | `0d0fd960f85a99d06a205ee8f589baef2b49b1bc3bc34fb9b25b7336585a64ca` |

## Archive index and limitations

`attempt/` contains the eleven original files in the exclusive spent directory:
the ten files covered by the driver's manifest plus that manifest. This includes
complete raw Job accounting/limits/PID lists, all 52 journal events, termination
and close results, fixture stdout/stderr, failure status, plan, authority and exact
fixture bytes. The fixture is retained as evidence, not permission to execute it.

`capture/` contains the eleven preflight, authority, wrapper, driver stdout/stderr,
launch/exit and read-only audit files. The driver exited 1; its stderr traceback is
retained as raw bytes. The external post-observation API object is supplementary.
It does not reconstruct missing driver `owner-after.json` or `terminal.json` files.

`independent/` contains the separate reviewer's four final files: the audit script,
audit JSON, review and receipt. No native control or Windows diagnostic API was
executed by either post-run auditor. Their checks independently preserve the
failure classification while establishing source/file and raw-query consistency.

ZIP metadata is packaging metadata; member contents preserve the source bytes,
including original CRLF and empty streams. The member manifest records byte length
and SHA-256 for every entry. The published review is an exact byte copy of the
archived review. Source snapshots remain in the previous immutable package; all
16 canonical/local bindings were checked again before and after this execution.

The final four observations show empty membership and the three owned-handle closes
succeeded. Full `cleanupVerified` nevertheless remains false. The at-exit image
query returned WinError 31, followed by a secondary null-validation error. The
0.0939999999-second CLEANUP_1 offset violates the unchanged 0.1-second lower bound.
The identity/origin of PID 57332 and the causes of these observations remain
inconclusive. The original PID 37804 total-2/active-1 failure is unchanged.

## Delivery validation

Static, governance, provenance, archive/member-hash and local documentation checks
are recorded in the validation receipt. An initial governance check ran while the
new independent-review path was allowlisted but its file had not yet been copied;
that draft-stage failure is retained with the later successful check. No acceptance
assertion was bypassed. Native source was unchanged, so no native control, unit
suite, browser suite or hosted workflow was repeated for this evidence delivery.

No product, renderer, workflow, threshold, protection or operational ADR change is
included. R01/R02, HIGHCONTRAST verification and calibration remain blocked;
PRs #76/#78/#80 remain Draft and PR #76 remains **NO-GO**.
