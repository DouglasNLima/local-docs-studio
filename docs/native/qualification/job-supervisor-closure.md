# Windows Job Object supervisor closure

**Current verdict: BLOCKED / NOT_AUTHORISED for native execution.** The revised
diagnostics and offline controls are prepared for independent review. One local
fixture control is proposed, not executed. Neither supervisor qualification nor a
new HIGHCONTRAST observation has been established.

Starting commit: `a38131cced93f5c29c1a7aafdb5316b97e8c5903`; tree:
`fa5ceba3952aa1a18fbe8401735a208ba390e276`. PR #76 remains
`7b1334b09a02ad2fbfd4a0234798e6c387d3e634`, PR #80 remains
`22c223c6f56fa41fda2a36102e1eed0af44974ec`, develop remains
`99349d82eb06960eb1f9785cf384db2373aed4fa`. All three PRs stay Draft;
PR #76 stays **NO-GO**. No product/renderer, threshold, protection or ADR activation.

## Causal assessment

The [original sealed evidence](../evidence/highcontrast-live-readiness/README.md)
is unchanged. Its preparation ZIP SHA-256 is
`09e7c8918b1e48fc8cd24f43b700e57dfcff423f31ce4f2cdc5cf562e65fb9aa`.
The retained supervisor snapshot SHA-256 is
`787b01ba1a3abdd87b78feb5bd4b5615630e5949798ef11b4be2f2957fb1c78a`.
PID 37804 exited 0 and its 21 harmless fixture checks passed, but the original
Job observation was **total 2 / active 1**, with `cleanupVerified=false`.
That remains **FAIL**, with causal attribution **INCONCLUSIVE**.

| Finding | Evidence and disposition |
|---|---|
| Original accounting ABI/query class | No demonstrated defect. x64 Basic/extended/accounting sizes are 64/144/48 bytes; accounting DWORD offsets are 32/36/40/44, consistent with the SDK. |
| Cumulative count 2 | Does not identify a helper, running extra process, repeated successful assignment or leak. Microsoft includes some failed associations in cumulative accounting. Do not subtract or normalise it. |
| Initial Job state | Not captured by the original control. A new unnamed Job is documented to start empty with zero accounting. The revised protocol verifies this before creating a child. |
| Original cleanup exception path | **Confirmed diagnostic defect:** a failed termination inside one try block skipped subsequent wait, exit-code and Job observations. Cleanup stages now run independently, retaining the original error. The PID 37804 record reports no such exception, so this does not explain its 2/1 result. |
| Original wait wrapper | An initial review hypothesis was withdrawn: CPython 3.12.0 already converts native `WAIT_FAILED` into `OSError`. Structured error retention is added; no wait-classification defect is claimed. |
| Child exit versus Job cleanup | Direct child exit, cumulative associations, current membership and handle closure are different facts. The original observation preceded child-handle release and did not prove empty membership afterwards. This is missing evidence, not an established timing explanation. |

Primary contracts: [new Job state](https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-createjobobjectw),
[accounting semantics](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_basic_accounting_information),
[assignment and limit failures](https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-assignprocesstojobobject),
[PID list, including nested descendants](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_basic_process_id_list),
[query return lengths](https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-queryinformationjobobject),
[asynchronous termination](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-terminateprocess),
and [the exact CPython 3.12.0 wrapper](https://github.com/python/cpython/blob/v3.12.0/Modules/_winapi.c#L1739).

## Fixed observation protocol

The [supervisor](../../../scripts/native/qualification_hosted/live_process.py) keeps
one suspended child, one assignment attempt, active-process limit 1, kill-on-close,
no breakaway and no retry. The [pure verifier](../../../scripts/native/qualification_hosted/job_evidence.py)
is shared with the collector observer. It does not import or call Windows APIs.

| Boundary | Required observation |
|---|---|
| CREATED_EMPTY | New unnamed, non-inherited Job; zero accounting and no PID before limits or child creation. |
| CONFIGURED_EMPTY | Read back flags `0x2008` and active limit 1; membership/accounting remain empty. |
| BEFORE_ASSIGNMENT | Parent is not in this private Job; actual child handle/PID, thread owner, image path/hash and creation time recorded. Child is suspended and not already associated. Job remains empty. |
| AFTER_ASSIGNMENT | Capture the native BOOL/error and raw queries even after a failed attempt. A successful path requires total/active 1 and exactly the returned PID; association is read back before resume. |
| AFTER_RESUME | Retain ResumeThread result and raw Job observation. The fixture may already have exited; only the known PID can disappear. |
| PROCESS_EXIT | Preserve native wait result/error and exit code, handle-backed identity and Job snapshot before closing child/thread handles. |
| CLEANUP_0–3 | Four observations with targets 0, 0.1, 1 and 4 seconds from cleanup start, all retained even after an empty observation. |

Each Job observation contains three ordered native queries: accounting (48 bytes),
extended limits (144 bytes), and a PID-list buffer with eight slots (72 bytes).
Eight slots are diagnostic capacity, **not permission for eight processes**.
Return values, failure error codes, capacities, returned lengths, complete allocated
raw bytes and call start/end times are retained. The PID header and every returned
pointer must fit the actual written prefix and allocated capacity. A failed or
truncated query is invalid; the buffer is never enlarged or retried.

The queries are not atomic. Accounting and PID-list disagreement can be retained
as a transition involving only the known child. Disappearance is tracked separately
for the active-count stream and PID-list stream: neither may reappear later. Total
2, an extra/foreign PID, quota termination, malformed evidence, failed assignment
or failed termination is a sticky failure even if a later sample is empty.

The original 30-second supervision deadline and separate five-second cleanup bound
remain unchanged. Cleanup sample start windows are `[0,0.1)`, `[0.1,1)`, `[1,4)` and
`[4,5)` seconds; every query must finish within five seconds. These are predeclared
diagnostic windows, not product performance SLOs. Missed/bunched/late observations
are invalid; safety cleanup still proceeds. There is no polling until PASS.

Normal acceptance requires the final fixed observation to have total 1, active 0,
no PIDs or quota terminations, followed by successful Job-handle closure. Job closure
is recorded separately and cannot substitute for an observed empty Job. Synchronous
kernel calls and thread scheduling are not hard real-time guarantees; a stalled API
or unverified cleanup remains blocked, never accepted by increasing the deadline.

## Ownership, failure and evidence controls

Ownership of the process/thread handles returned by successful CreateProcess is
separate from acceptance of the image/PID/hash. If experimental identity fails,
cleanup still attempts termination and an exit wait on the owned handle; it never
reopens or terminates an inferred PID. Aliased/invalid returned handles are rejected
before assignment/resume and cannot be certified as safe cleanup.

Termination, wait, exit-code retrieval, identity observation, Job queries and each
handle close are independent cleanup stages. A failed stage does not suppress the
later stages, and later success cannot erase the error. Close attempts are recorded
before invocation and never retried. Journal write/flush failure becomes sticky,
prevents further creation/assignment/resume, and cannot prevent native cleanup.

The complete normal operation order is checked. Every native result, argument,
handle, identity, timestamp and raw snapshot must agree with the final summary and
the retained journal. A zero fixture exit or a fabricated `cleanupVerified=true`
cannot qualify inconsistent records. Raw stdout, stderr, exit status, operation
journal and all fixed observations survive unsuccessful collection where storage
remains available; unavailable evidence is a failure, not a replacement PASS.

## One proposed local control

The [local driver](../../../scripts/native/qualification_hosted/local_job_control.py)
has no default authority and is not connected to a workflow. It requires a **new,
unedited owner comment in Goal #77**, matching an exact approved JSON record/body
hash, E/tree, source S/tree and plan hash. The final delivery prepares that record
with `approved=false`. The configuration and CLI do not infer approval from this
document, previous collector authority or an earlier local fixture run.

The proposal uses the **same 20,480-byte harmless NativeBufferSelfTest.exe**:
SHA-256 `311655cd165d4a82631efce4b827decea6a5199d2e723da67175b94a4539e549`,
member `attempt-6/NativeBufferSelfTest.exe` of the unchanged original offline archive
SHA-256 `d28cc4f433b02f93bdfac1604b270123a16419da0229387104ccc990f5a29c27`.
Its reviewed imports exclude User32/SPI/GUI and dynamic loader calls. No rebuild,
managed control or HIGHCONTRAST collector is included. The native fixture retains
all 21 original checks and its exact expected report; stderr must be empty.

The parent is the observed local CPython 3.12.0 x64 executable/DLL, with file hashes
in the plan. The driver requires `python -I -S`, disabling Python environment/user
path influence and site initialisation before loading the reviewed modules. It
launches a child with only SystemRoot, WINDIR, TEMP, TMP, USERPROFILE
and a fixed System32 PATH. Git metadata checks and the Python supervisor are
orchestration; the experimental native-process ceiling is **one fixture**. The old
control's inherited environment was not retained, so equivalence to that old
environment is not claimed. Available Python file hashes are not certification of
all loaded Windows/CRT modules.

The exact local raw source hashes are recorded separately from canonical Git hashes,
including line endings. Checkout conversion or another source/runtime change
invalidates the plan. A fixed exclusive output directory per E records the spent
attempt; it must not be deleted/renamed to retry. Any failure requires a new owner
decision. API/authority failures stop before launch or invalidate collected evidence.

## Decisions and stop criteria

1. Review the sealed source, offline receipts and independent disposition. A proposed
   local control remains **NOT_AUTHORISED** until the owner explicitly approves its
   exact record. No native control was executed in this continuation.
2. If authorised later, collect only that one fixture process. Any foreign PID,
   cumulative discrepancy, unexpected limit, deadline failure, incomplete record or
   unverified cleanup preserves BLOCKED; there is no automatic second attempt.
3. Independently review the full resulting evidence. Even a valid one-process result
   establishes only this supervisor/fixture observation, not the original 2/1 cause,
   native HIGHCONTRAST behaviour, R01/R02 readiness or release acceptance.

The older collector configuration is preserved unchanged and blocked. Its source
closure no longer matches the revised diagnostics; a future collector packet would
need separate resealing/review and owner authority after supervisor disposition.
R01/R02, appearance/display experiments, calibration, signing/distribution and ADR
operational activation remain unapproved. The original geometry, empty-scheme
restoration, High Contrast contrast-ratio and heap-corruption failures are retained.
