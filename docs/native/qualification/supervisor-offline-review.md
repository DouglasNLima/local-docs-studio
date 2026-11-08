# Independent review: offline supervisor corrections

**Disposition: the bounded offline corrections are technically reviewable, with no remaining material implementation finding in this scope. Native execution remains BLOCKED / NOT_AUTHORISED.** This is an independent technical agent review, not owner approval, real-kernel qualification, a new executable plan or acceptance of PR #76.

The review starts from result commit `b5ca97716e3b1596a0e6684786624c27937403b0`, tree `5095477179948c1f625741b76dce2220250d38c1`. It covers the uncommitted implementation bytes identified below, the [offline decision report](supervisor-offline-recovery.md), mocked regression controls and preserved failed evidence. The [evidence index](../evidence/supervisor-offline-recovery/README.md) retains the source snapshots, raw test results, initial finding and this review. Final delivery identity must be bound separately after the commit exists.

Only source/Git/archive/file inspection, official API-documentation research and isolated Python tests with fake kernels/clocks were used. No native fixture, collector, hosted workflow, actual Job/Win32 diagnostic API, display, appearance, input or GUI operation was executed. No production source was changed by the reviewer. New reviewer files were written only under `artifacts/goal77-supervisor-offline-recovery/independent`.

## Reviewed source and tests

The following raw SHA-256 values were recorded before and after the independent test run and remained equal. Later edits were documentation only.

| File | Reviewed SHA-256 |
|---|---|
| `scripts/native/qualification_hosted/live_process.py` | `17ad90b592c5e56454892e3402f6a4d5cc207000ce79f3186a7849adc3e06054` |
| `scripts/native/qualification_hosted/job_evidence.py` | `54390f3e21ff2fdd5c49927075a78b77118844d9ec098cc885d174e0195ef89e` |
| `tests/unit/test_native_supervisor_recovery.py` | `9191fc12b17bcb15e36cedd78f6a27f22266d8fb5d427241987bda5d04004da2` |
| `tests/unit/test_native_job_supervisor.py` | `87a1cd22115db66064383cbbe2fca0ef9d8cc0145cde18b8c416a975a5fc3eb9` |
| `tests/unit/test_native_live_verification.py` | `7dca05ba0ad57928ea0c427b9a266c7f0cec0392756da84b23361483a45798c4` |

Independent commands were run in separate processes:

| Command | Result |
|---|---|
| `python -I -S -m unittest discover -s tests/unit -p test_native_supervisor_recovery.py -v` | **16 PASS**, 0.410 seconds |
| `python -I -S -m unittest discover -s tests/unit -p test_native_job_supervisor.py -v` | **29 PASS**, 0.811 seconds |
| `python -I -S -m unittest discover -s tests/unit -p test_native_live_verification.py -v` | **17 PASS**, 2.592 seconds |

The retained reviewer log directory is named `before-wait-correction`, but that provisional name was chosen before noticing that the correction had landed. Its before/after hashes, 16-test recovery count and raw logs identify the **corrected** source above. It is not evidence of running the earlier 13-test draft. The directory and receipts are preserved without renaming or rewriting that history.

These **62 passing tests** exercise fake clocks/kernels and rejection paths. The supervisor tests reject accidental real Kernel construction; recovery controls also block native loading/process execution on their modelled paths. The existing CLI negative control runs only Python and rejects before native access. No native control, broad combined suite or hosted run was repeated.

## Exit-identity correction

The native failure and the derived validation failure are now separated. If the at-exit identity query raises, its original native result/error remains in the journal, `childIdentityAtExit` stays null and the invalid null dereference is skipped. PROCESS_EXIT observation and independent cleanup continue. A backend returning null without a recorded failure gets an explicit missing-identity error. The pure verifier rejects non-dictionary identities.

Tests preserve the simulated `QueryFullProcessImageNameW` return 0 / WinError 31, without the secondary `TypeError`, and still reject qualification. They also reject null, incomplete and malformed exit identities. The strict complete before/after identity requirement is unchanged: there is no fallback, PID reopening, substitution of pre-exit values or acceptance waiver.

Microsoft's handle-lifetime guarantee does not promise that every information query succeeds after termination. The image-query API specifies error handling, not unconditional post-exit availability. This correction does not explain historical WinError 31 or certify a replacement query. [Handle lifetime](https://learn.microsoft.com/en-us/windows/win32/procthread/process-handles-and-identifiers), [image-query contract](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-queryfullprocessimagenamew).

## Scheduling correction and resolved review finding

The original targets **0, 0.1, 1 and 4 seconds**, start windows **[0,0.1), [0.1,1), [1,4), [4,5)**, five-second cleanup limit, 30-second child limit and ten accepted Job phases remain unchanged. Waiting reads only the existing clock and sleeps towards the same target. It never samples Job state to decide whether to wait again and never creates a replacement observation.

Independent review found that the iteration-only draft could request eight four-second sleeps from a frozen clock: **32 seconds of requested waiting** before rejection. This was a pure model with a list-append replacement for sleep, not a measured native or wall-clock run. The initial finding is retained in `initial-wait-finding.json`.

**That finding is resolved.** The final function shares `lastStamp` and cumulative `requestedSeconds` across all cleanup phases, rejects non-progress/regression/non-finite clock values, caps requested waiting against both the remaining original interval and five seconds cumulatively, and retains a finite eight-wake cap. `CleanupClockFailure` stops further timed collection while owned-handle closure still proceeds. New controls show one four-second request before a frozen-clock rejection, rejection of a second excessive request with tiny positive progress, and a shared-target example stopping at 4.5 seconds of requested waiting rather than extending the budget.

The window is checked at observation entry, again after journal I/O before the backend call, and against the first raw accounting-query timestamp. Query finish is checked against the original cleanup bound. The pure verifier requires the same raw-start window. Tests cover an early clock wake, stalled/tiny/regressing/non-finite clocks, a missed upper boundary, journal delay and backend delay. An earlier entry timestamp cannot conceal a late raw query.

A phase whose window is already missed at the gate is not sampled or replaced. Pre-emption after that gate can still produce a late raw timestamp; the original observation is retained and rejected. Incomplete or mistimed collections cannot satisfy the unchanged ten-phase acceptance requirement. The adjusted existing failure-path test now rejects a late CLEANUP_0 instead of requiring that out-of-window sample; it still requires continued cleanup, the remaining eligible phases and FAIL.

These are recorded-boundary checks, not a hard real-time guarantee against scheduling delay or a stalled synchronous API. No timing tolerance was added. The unchanged [CPython 3.12.0 clock implementation](https://github.com/python/cpython/blob/v3.12.0/Python/pytime.c#L976) uses GetTickCount64 on Windows. That source fact does not establish the precise cause of the historical 0.094-second recorded interval; its attribution remains inconclusive.

## Foreign-PID diagnostic remains design-only and blocked

No OpenProcess path was added. AST comparison confirms that the complete native `Kernel` class is unchanged from the starting commit, including termination and kill-on-close behaviour. The proposed minimal-rights process-query handle would own only its acquired reference, with its own exactly-once closure record. It must never become owned-child provenance or be passed to termination, suspension, assignment or cleanup authority. Access denial, exit-before-open, partial information, PID mismatch, close failure and missed deadlines remain explicit unsuccessful or inconclusive outcomes. A later handle cannot retroactively identify PID 57332 from a PID-only historical snapshot. [OpenProcess](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocess), [PID lifetime](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocessid).

**The integration incompatibility is real and remains unresolved.** The existing supervisor can terminate every private-Job member and closes a kill-on-close Job. A query-only helper cannot make that whole experiment non-mutating. Query rights on a Job handle are also insufficient: an extra reference can postpone last-handle cleanup, and closing the last reference can trigger termination. The final report now explicitly requires a borrowed-Job lifetime contract, with no observer ownership/duplication/closure of that Job handle and no race with its owner's closure or handle reuse. Such a contract and deadline-compatible owner shutdown are not established here. Current cleanup flags and calls must not be silently changed. [Job lifecycle](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects), [Job-wide termination](https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-terminatejobobject), [CloseHandle](https://learn.microsoft.com/en-us/windows/win32/api/handleapi/nf-handleapi-closehandle).

The report appropriately separates independent future exit-property collection from any proposal to make post-exit image lookup optional. Neither alternative is implemented or accepted. Foreign-member attribution remains design-only, with no new observer executable, process, runner, capability grant or live packet.

## Preservation and final boundary

The reviewer compared these exact files with their Git objects at the starting commit; all remain byte-identical:

| Preserved object | SHA-256 |
|---|---|
| Spent local plan | `415e0ecb2e10328e3a6850da926ab1c9ef237072352a7a903fb1f46424081be3` |
| Blocked collector configuration | `c2cf91cbd5678752d028d7f64a6d87a90e183fb1f73c12de35b9cc967ef13d7a` |
| Authorised failed-control raw archive | `c0157c0a66e11baf8b10eb5ecf545456fa4278c30b2f8c5d7bc63d1c08d0f2f9` |
| Original PID 37804 failure archive | `09e7c8918b1e48fc8cd24f43b700e57dfcff423f31ce4f2cdc5cf562e65fb9aa` |
| Original buffer-control archive | `d28cc4f433b02f93bdfac1604b270123a16419da0229387104ccc990f5a29c27` |

The regression control independently reads the unchanged authorised failure, confirms PID 57332 and cumulative total 2, and requires rejection. No extra-member claim, supplementary identity field, final empty snapshot or passing offline test changes that verdict. Historical WinError 31, PID 57332 attribution and the original PID 37804 cause remain inconclusive where previously classified.

The offline corrections are ready for owner/reviewer assessment as enforcement of the existing contract. They provide no native qualification evidence. Both old executable plans are intentionally unchanged and stale against the corrected source; the spent authority cannot authorise these bytes. No new live authorisation packet exists. Resolving a future borrowed-capability design or changing the mandatory exit-image criterion requires a separate explicit decision and review, followed by any separately authorised execution scope.

R01/R02, collector verification, hosted discovery, appearance/display experiments, calibration, signing, release and ADR operational activation remain blocked. PR #76 remains **NO-GO**. The historical 250 ms and 4.5:1 requirements and all failed evidence remain intact.
