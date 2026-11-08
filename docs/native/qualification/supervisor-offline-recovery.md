# Supervisor failure recovery: offline decisions

**Offline corrections only; live readiness remains BLOCKED / NOT_AUTHORISED.**
No fixture, collector, hosted workflow or Windows diagnostic API was executed in
this continuation. No new executable control plan or authority record is prepared.
The original authorised control remains **FAIL/BLOCKED** and its authority is spent.

Starting result commit: `b5ca97716e3b1596a0e6684786624c27937403b0`, tree
`5095477179948c1f625741b76dce2220250d38c1`. Evidence authority is
[Goal #77 comment 6726819657](https://github.com/DouglasNLima/local-docs-studio/issues/77#issuecomment-6726819657):
execution E `d4261e58b55d6755feddf179ea9e922a687e3e9f`, reviewed source S
`331dc5ab220e5ca7dd145ff21a650d3d2578111f`. The original
[26-member archive](../evidence/job-supervisor-authorised-control/README.md), SHA-256
`c0157c0a66e11baf8b10eb5ecf545456fa4278c30b2f8c5d7bc63d1c08d0f2f9`, is unchanged.
See [new offline receipts](../evidence/supervisor-offline-recovery/README.md) and
[independent review](supervisor-offline-review.md) for the delivered source hashes.

## Confirmed findings and limits

| Finding | Disposition |
|---|---|
| At-exit image query returned 0 / WinError 31 | Confirmed native failure. Why that call failed remains **INCONCLUSIVE**. |
| Validation dereferenced the absent result | Confirmed secondary diagnostic defect; corrected without replacing the native error or the null identity. |
| CLEANUP_1 recorded at 0.09399999992456287 s | Confirmed violation of the unchanged 0.1 s lower bound. Historical FAIL remains unchanged. |
| Single sleep followed only by upper-window checks | Confirmed scheduling defect; early wakes were not gated. Logging delay and first raw-query timing also need the same boundary checks. |
| Extra PID 57332 and cumulative total 2 | Confirmed raw Job observations; image, creation time, parentage and cause remain **INCONCLUSIVE**. |
| Final empty membership and successful closes | Confirmed narrower observations; they do not change `cleanupVerified=false` or qualify the failed run. |

## Exit identity: correction and proposed contract

The retained owned process handle signalled and yielded exit code 0 before the
image query failed. In the reviewed implementation, `Kernel.image` queried the
image path first; its failure prevented the subsequent creation-time and PID reads.
Those missing at-exit values must not be inferred from the before-exit record.

The correction skips derived identity validation after a failed native query,
preserving the exact native error in the operation journal and leaving
`childIdentityAtExit=null`. A backend returning null without an error is explicitly
rejected. The pure verifier also rejects a non-dictionary identity. Exit/Job
observation and independent cleanup stages continue. The complete before/after
identity requirement is unchanged; missing identity still fails qualification.

Microsoft documents that owned process/thread handles remain valid until closed
after termination. That is a handle-lifetime guarantee, not a guarantee that every
information query succeeds. The image-query contract specifies success/error
handling but gives no unconditional post-termination success guarantee.
[Handle lifetime](https://learn.microsoft.com/en-us/windows/win32/procthread/process-handles-and-identifiers),
[image-query contract](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-queryfullprocessimagenamew).

| Property | Before termination | After signalled exit, while retaining the same owned handle |
|---|---|---|
| Handle provenance | CreateProcess returns the owned process/thread handles; verify their relationship before resume. | Retain those handles; never reopen the child by PID. Closing them ends this binding. |
| PID / thread identity | Query and compare to the creation results. | Handle-backed queries are candidates for separate observation; retain each native failure. They were not completed in the historical at-exit image-first path. |
| Creation / exit FILETIME | Creation time identifies the handle-backed instance; exit time is undefined while running. | GetProcessTimes explicitly exposes the exit time; a proposed split would require creation-time agreement and valid exit chronology. No API success is assumed. |
| Signalled state / exit code | Running status is not completion. | Already observed through the owned handle; it is insufficient as complete identity. |
| Image path and file hash | Preserve the exact path and on-disk file hash before resume. | Continue the current required observation. A failure is not an image change or a successful identity check. An on-disk hash is not a hash of mapped process memory. |

[GetProcessId](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocessid),
[GetProcessTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocesstimes),
and [GetExitCodeProcess](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getexitcodeprocess)
define the relevant query rights and error handling. The report does not certify
their availability on this terminated fixture; no replacement native read occurred.

**Proposed alternative, not activated:** split future identity evidence into
independent handle-backed queries, so a path failure does not erase other diagnostic
results. Keep the before-resume full identity, same owned handles, matching PID and
creation time, thread ownership, signalled exit and exit code mandatory. Record the
post-exit image query independently as success or unavailable with its native error.
Any proposal to make that unavailable image result non-blocking is a distinct
acceptance-contract decision requiring explicit owner approval, implementation and
review. It must not reclassify the old run. The current code implements no fallback
or relaxed contract.

## Fixed cleanup scheduling

The targets remain **0, 0.1, 1 and 4 s** from the original cleanup start. Their
start windows remain **[0,0.1), [0.1,1), [1,4), [4,5)**; every query must finish
within five seconds. The 30-second child supervision deadline is unchanged.

The revised wait reads only the existing monotonic clock and sleeps for the
remaining time to the same target. At most **eight** such waits are permitted per
fixed phase. This is a finite control-flow bound against a stalled clock or endless
tiny advances, not an acceptance tolerance, time extension, process retry or extra
Job observation. Requested sleep is additionally capped by the original remaining
five-second interval and by five seconds cumulatively across all four phases; it
is never reset per phase. Non-finite, regressing or non-progressing clocks and
exhausted wait bounds abort further timed collection while handle closure continues.
Missed windows fail without replacement.
No target or deadline is reset; Job state and acceptance results never control the
wait. The clock source is unchanged. Independent review caught an iteration-only
draft that could request repeated full delays from a stalled clock; that draft and
its test receipts are retained, and the final controls cover this failure explicitly.

The window is checked at entry, again after journal I/O immediately before the
backend query, and against the first raw accounting-query timestamp. Query finish
is checked against the existing five-second bound. The pure verifier applies the
same raw-start requirement: an early entry timestamp cannot conceal a late query.
These checks enforce the declared recorded boundaries; they are not a hard
real-time guarantee against OS pre-emption or a stalled native API.

An accepted record still requires exactly the original **ten** ordered Job
observations. Once a missed window is detected at the pre-backend gate, no query is
started for that phase and no replacement phase is added. A call can still acquire
a late raw timestamp after pre-emption beyond that gate; its original raw evidence
is retained and rejected. A failed collection can therefore contain fewer than ten
complete observations, or ten observations with invalid timing; it can never pass. Owned-handle
cleanup continues. A test that previously expected a late CLEANUP_0 after its entire
window was consumed now requires its absence, the remaining fixed phases and FAIL.
No successful-path observation or required cleanup evidence was removed.

CPython 3.12.0 selects GetTickCount64 for Windows `time.monotonic`; Microsoft describes
that timer's usual coarse resolution. This is a source-backed mechanism consistent
with quantised recorded times, not proof of the exact wake/timestamp cause in this
run. No resolution measurement was retained there, and no timing tolerance is
introduced. [CPython 3.12.0 source](https://github.com/python/cpython/blob/v3.12.0/Python/pytime.c),
[timer contract](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-gettickcount64).

## Unexpected member: bounded read-only design

The retained sequence is 1/1/PID 39616 after assignment and resume, then
2/1/PID 57332 at PROCESS_EXIT, then 2/0/empty at all four cleanup observations.
All query returns and raw bytes agree with that interpretation. Limits read back as
`0x2008`, active limit 1. There was one explicit assignment and no recorded quota
termination. The assigned child process/thread handles were not closed until after
the exit-boundary query. Cumulative accounting, concurrent active limit, PID-list
membership and handle lifetime describe different facts; none permits subtracting
the extra association or treating it as the same child.

The accounting/limits/PID queries are sequential, not an atomic snapshot. Microsoft
includes terminated processes and some failed associations in cumulative accounting;
active accounting also depends on reference release. The extra PID-list entry is
separate evidence, beyond a cumulative counter alone. An active limit of one is not
a lifetime total-one guarantee. The stricter qualification invariant remains total
one, and its failure is not normalised.
[Accounting contract](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_basic_accounting_information).

The supervisor reported membership in another Job but not in its private Job.
The ancestor Job's identity and hierarchy were not recorded. Windows Job PID lists
include child jobs and nested accounting aggregates descendants; these documented
possibilities do not identify the origin of PID 57332 or prove a helper process.
[PID-list contract](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_basic_process_id_list),
[nested-Job semantics](https://learn.microsoft.com/en-us/windows/win32/procthread/nested-jobs).

The proposed future diagnostic has a strict capability boundary:

1. Consume one already retained, complete PID-list observation and its timestamps.
   Never enlarge the list, repeat the Job observation, infer a PID from a cumulative
   counter, or inspect a historical PID on today's desktop.
2. If separately authorised, attempt one non-inheritable OpenProcess with
   `PROCESS_QUERY_LIMITED_INFORMATION` for the one unexpected PID. No terminate,
   suspend, write, duplicate-handle, assignment, all-access or debug-privilege rights;
   no escalation, retry, enumeration or replacement process.
3. With a successfully opened **query handle**, independently record GetProcessId,
   creation FILETIME, image-query result and current IsProcessInJob against an
   explicitly supplied query-capable Job handle. Retain native return/error and
   start/end time for every call, including partial/unavailable information.
   A query handle owns only a reference to close, not the process or its lifecycle.
4. Close that reference exactly once in a finally path. Never pass it into child
   ownership, termination, suspension, assignment or cleanup code. Do not reopen
   the PID after failure or closure. Failure to close remains a diagnostic failure.
5. Label access denial, exit-before-open, PID mismatch, missing creation time,
   changing/inaccessible image, unavailable membership or elapsed deadline explicitly.
   Even matching current PID/membership with a newly obtained creation time does
   **not** prove identity at the earlier PID-only snapshot. PID reuse remains an
   attribution limit; no historical creation-time binding exists to compare.

[OpenProcess and handle closure](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocess),
[current Job-membership query](https://learn.microsoft.com/en-us/windows/win32/api/jobapi/nf-jobapi-isprocessinjob),
and the handle/PID lifetime contract above support this design. It introduces no
OpenProcess path or foreign-process mutation in the delivered source.

**Integration incompatibility remains BLOCKED:** the existing supervisor can call
TerminateJobObject and has kill-on-close enabled. Those operations act on Job
members, including an unexpected member; a query-only helper cannot make that whole
experiment read-only. They must not be silently bypassed or changed, nor may a
diagnostic delay required owned-child cleanup to keep a foreign process available.
Synchronous query calls also lack a guaranteed cancellable duration.

The recommended future design boundary is a separately reviewed read-only observer
with an explicitly supplied **borrowed** Job handle and no process/Job lifecycle
authority. Query rights alone are insufficient: closing even a query-capable last
handle to a kill-on-close Job can terminate its members, while retaining an extra
Job reference can postpone that last-close action. A future observer must neither
own/duplicate/close the Job handle nor race its owner's closure/reuse. A guaranteed
borrowed-handle lifetime and deadline-compatible owner shutdown must be designed
and reviewed before any wiring; no such guarantee is established by this packet.
[Job lifecycle](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects),
[CloseHandle](https://learn.microsoft.com/en-us/windows/win32/api/handleapi/nf-handleapi-closehandle).

The proposed process-query handle has its own acquisition and exactly-once closure
record, separate from owned-child handles and any historical numeric-handle close
set. It must not be wired into the current terminating supervisor as though the
contracts were compatible. The provision/lifetime of the borrowed Job capability
and any separate observation scope require explicit design decisions and review.
No extra process, new runner, executable package or live control is prepared here.
Unknown/expired identity remains a valid diagnostic outcome, never PASS.

## Decision register and stop point

| Decision | Recommendation and trade-off | Current disposition |
|---|---|---|
| Accept null-validation and scheduling corrections | Accept the offline evidence and review. They enforce the existing contract; a missed window remains incomplete and failed. | Implemented and tested offline only. |
| Independent exit-property collection | Approve/reject the proposed design split. It could preserve useful handle-backed facts when image lookup fails, but does not establish them retrospectively. | Proposed; no alternative implementation or acceptance activated. |
| Make post-exit image lookup optional for qualification | Keep the current strict requirement pending a separate explicit contract decision and sufficient evidence for the alternative. | Not approved; absent identity remains FAIL. |
| Foreign-member diagnostic | Review the minimal-rights capability design separately; reject wiring it into the terminating/kill-on-close path under a read-only claim. PID-reuse and synchronous-call limits remain explicit. | Design-only, not executable; integration BLOCKED. |
| Any further native experiment | Do not authorise or prepare one from this packet. A future proposal must resolve the above conflicts and obtain its own reviewed source/configuration and explicit authority. | NOT_AUTHORISED. |

The frozen local and collector configurations are unchanged and intentionally do
not match the corrected source hashes. The spent local authority cannot authorise
these bytes. The new source receipt is an **offline review receipt**, not a launch
manifest. R01/R02, collector verification, display/appearance experiments,
calibration, release/signing and ADR operational activation remain blocked.
PRs #76/#78/#80 remain Draft; **PR #76 remains NO-GO**. The historical 250 ms and
4.5:1 requirements, original source/evidence archives and failed verdicts remain.
