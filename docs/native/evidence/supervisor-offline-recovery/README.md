# Offline supervisor recovery: evidence and receipts

**62 mocked controls PASS, independently repeated. Native qualification remains
BLOCKED / NOT_AUTHORISED; the authorised fixture result remains FAIL.** No native
fixture, collector, hosted workflow, desktop experiment or new live control was
prepared or executed by this continuation.

- [Findings, corrections and owner decisions](../../qualification/supervisor-offline-recovery.md).
- [Independent technical review](../../qualification/supervisor-offline-review.md).
- [Original failed control, unchanged](../job-supervisor-authorised-control/README.md).
- [Raw offline evidence](offline-evidence.zip) and [every member's hash](member-sha256.json).
- [Source/Python/protected-file bindings](source-receipt.json),
  [delivery validation receipt](validation-receipt.json) and [package hashes](files.json).

The starting result is `b5ca97716e3b1596a0e6684786624c27937403b0`, tree
`5095477179948c1f625741b76dce2220250d38c1`. Original execution E remains
`d4261e58b55d6755feddf179ea9e922a687e3e9f` and reviewed source S remains
`331dc5ab220e5ca7dd145ff21a650d3d2578111f`. The new delivery commit/tree is published
in Goal #77 and PR #78 after creation; no invented self-referential commit identity
is embedded here. This source receipt is **not an execution configuration**.

| Object | SHA-256 |
|---|---|
| Offline ZIP: 67 members, 211,546 bytes | `cf48d4a01d9da163073b26271de37521af9f3668e184b16f1a1e8fcd0c6a8a7d` |
| Source receipt | `0439aa325fa7f82a55ef25e4d01222ce4ebe2ab858ec379226ed8e2634c15864` |
| Independent review | `6037fc75d236b450191381358285c93cb8b052eb3964c8d344ebb45cc66def5c` |
| Independent receipt | `25a03f7db49623ee017c64facfac14a5d43916339972a5f85c5edcbe566a2d3a` |
| Corrected supervisor | `17ad90b592c5e56454892e3402f6a4d5cc207000ce79f3186a7849adc3e06054` |
| Corrected pure verifier | `54390f3e21ff2fdd5c49927075a78b77118844d9ec098cc885d174e0195ef89e` |
| Original failed-control ZIP, unchanged | `c0157c0a66e11baf8b10eb5ecf545456fa4278c30b2f8c5d7bc63d1c08d0f2f9` |

## Scope and retained iterations

The supervisor preserves native WinError 31 and absent exit identity without a
secondary null dereference. Its cleanup wait enforces the same fixed windows before
entry, after journal I/O and against raw query timing. Invalid/stalled clocks stop
timed collection; all requested sleeps share the original five-second bound, with
a finite wake cap. Owned-handle closure remains independent. No native Kernel method
or API declaration changed; no foreign-process opener was added.

Archive paths below are original bytes, not rewritten logs. Early failures and the
independent finding remain inspectable alongside the corrected result.

| Archive path | Result and meaning |
|---|---|
| `preflight.json`, `evidence-authority.json` | Exact starting E/S/result trees, authority source comment, original 26-member ZIP and Job sequence verified without extracting or executing the fixture. |
| `baseline-regressions/` | 12 new controls against the unchanged implementation: 7 failures, 2 errors, 3 passes. This includes the expected absent new wait constant and rejection of the historical failed record. Raw transcripts and exact test/source bytes are retained. |
| `corrected-1/` | 58 controls; 2 failures retained. One fixture expected a complete journal despite injected NaN timestamps; the other expected a late CLEANUP_0 after its window was consumed. Both were corrected to assert explicit incomplete/failure evidence and continuing handle cleanup. |
| `corrected-2/` | 59 PASS at that draft. Independent review subsequently demonstrated that an iteration-only wait cap could request eight four-second sleeps from a frozen clock; this draft is not the accepted correction. |
| `independent/initial-wait-finding.json` | Pure-function reproduction: 32 seconds of requested sleep, with no real sleep or native call. Closed by non-progress rejection, a shared requested-sleep budget and stopping later timed phases on clock failure. |
| `corrected-3/` | 62 PASS after the wait-budget correction. The cross-phase budget test was then strengthened to demonstrate successful earlier targets followed by rejection at target 4, before cumulative requested sleep could exceed five seconds. |
| `corrected-4/` | Final root run: **62 PASS**, 3.831 s unittest time, from 16 recovery + 29 existing supervisor/driver + 17 existing live-evidence controls. Exact source hashes accompany stdout/stderr. |
| `independent/before-wait-correction/` | Independent final-byte runs: **16 + 29 + 17 PASS**, 0.410 / 0.811 / 2.592 s. The provisional directory name is misleading; its before/after source hashes and 16-case count prove it contains the corrected run. It is preserved without renaming. |
| `independent/review.md`, `independent/receipt.json` | Completed review, exact source/report hashes, unchanged native Kernel AST, protected archives/plans and no remaining material offline implementation finding. |
| `final-source/raw/`, `final-source/canonical/` | Six exact executed/supporting source files in raw and canonical Git form, with explicit differences where line endings differ. |
| `stale-plan-check.json` | The frozen local plan correctly rejects the changed source. The plan and spent authority were not updated to permit execution. |
| `static-check.json` | Static asset/service-worker/dependency checks PASS. No browser or hosted suite was launched. |

The isolated root driver rejects native library loading during its mocked test run.
The fake fixture file contains non-executable text. The new controls cover error
31/null handling, missing identity, early wake, no progress/tiny progress, requested
sleep budget, non-finite/regressing clocks, oversleep, journal/backend delay, invalid
raw timestamps, supplementary foreign identity claims and the unchanged archived
failure. Existing controls retain limits, PID/raw-buffer rejection, assignment,
deadline, termination, handle and authority checks. Tests do not certify Windows
behaviour or explain PID 57332.

ZIP timestamps are fixed packaging metadata; member contents preserve original byte
streams and empty outputs. The published review is an exact copy of its archived
version. All member lengths/hashes and final source bindings were verified.

## Remaining boundary

Post-exit image lookup is still mandatory; missing identity remains FAIL. An
alternative split of independent handle-backed properties is proposed and reviewed
only as a design. Foreign-member identification remains design-only: minimal process
query rights cannot resolve historical PID reuse, and borrowed Job-handle lifetime
must be compatible with the existing terminating/kill-on-close cleanup contract.
That integration remains BLOCKED; no process-control capability is granted.

The source, test, documentation, provenance and governance changes are validation
only. No product, renderer, workflow, historical archive, required check, performance
threshold, contrast requirement or ADR operational state changed. PRs #76/#78/#80
remain Draft; **PR #76 remains NO-GO**. All live work requires a separate decision.
