# Corrected HIGHCONTRAST live verification readiness

**Current verdict: BLOCKED / NOT_AUTHORISED.** The corrected collector's borrowed
ownership remains independently reviewed. A single-process verification protocol,
strict observer and owner-record format are prepared, but a new **local supervisor
control failed**. That infrastructure prerequisite must be resolved before this
configuration can become executable. No hosted run or actual HIGHCONTRAST call was
made in this continuation.

Starting source is `4e2e37f2aa285cf318edd50d9cf2d0233b7eb4e6`, tree
`e86f3367e3ac0dc8d95de8d2ef026dcbb36ba8c5`. PR #76 remains
`7b1334b09a02ad2fbfd4a0234798e6c387d3e634`, PR #80 remains
`22c223c6f56fa41fda2a36102e1eed0af44974ec`, develop remains
`99349d82eb06960eb1f9785cf384db2373aed4fa`. All three PRs remain Draft;
PR #76 remains **NO-GO**. No product/renderer, performance threshold, 4.5:1
requirement, historical evidence, branch protection or ADR operational policy changes.

## Prepared protocol

| Boundary | Exact proposed contract |
|---|---|
| Process ceiling | One measured collector process, cumulative Job count 1; no second runtime probe, stock control, build process or diagnostic helper in the experiment. GitHub runner/checkout/upload and the Python supervisor are orchestration, not extra collector observations. |
| Collector | Sealed `DesktopProbe.exe` apphost with its original system-DPI manifest, DLL, PDB, deps and runtimeconfig. Both fixed HIGHCONTRAST GETs remain; the borrowed reader is unchanged and no borrowed-pointer release exists. |
| Native identities | Verify the actual apphost, assembly, CoreLib, x64/runtime/source and loaded hostfxr/hostpolicy/coreclr plus other loaded runtime-native files **inside the same PID before GETs**, then again after GETs. Report file hashes, not relocated executable-memory hashes. |
| Runtime | Core-only 10.0.14, no roll-forward; all 193 original archive files SHA-256 checked against the earlier environment lock. No WindowsDesktop runtime or WPF prewarming is needed or added. |
| Host | Disposable `windows-2025`; observed image `win25-vs2026` / `20261103.298.1`, Windows Server Datacenter 26100 / UBR 33674. Exact full Windows/image record and observed workspace path must match. A moving image invalidates the configuration. |
| Launch | Create one suspended console process without a console window; inherit only stdin/stdout/stderr handles; assign an unnamed non-inheritable Job with active-process limit 1 and kill-on-close before resume. Verify its PID list and limits while suspended. |
| Supervision | Original 30-second deadline including setup; watchdog **thread**, no second process. Separate maximum 5-second cleanup accounting; record creation/assignment/resume/exit/cleanup chronology and exact native exit code. No retry. |
| Allowed observation | Existing Win32 getters, exact raw flags and NULL/EMPTY/NAMED scheme copy, geometry, theme and colour readback. No display/appearance change, foreground input, window creation, WPF/Forms load or resource prewarming. |
| Acceptance | One matching PID, two complete fixed GET/copy sequences, 31 ordered journal records including BEFORE_GETS/AFTER_GETS identities, strict complete stdout, exit 0, exact file closures and observed empty Job with cumulative count 1. Any contradiction/crash/missing record is invalid. |

The supervisor uses [suspended/no-window creation flags](https://learn.microsoft.com/en-us/windows/win32/procthread/process-creation-flags)
and [Job limits](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_basic_limit_information).
Its process error mode temporarily disables critical-error UI and Windows Error
Reporting in the child, with parent readback/restoration; it does not suppress exit
faults or enable alignment-fault correction. See [SetErrorMode](https://learn.microsoft.com/en-us/windows/win32/api/errhandlingapi/nf-errhandlingapi-seterrormode).
Complete raw stdout/stderr and native exit codes remain mandatory. No crash-dump
collection or debug heap is implicitly authorised by this proposal.

Windows API calls and thread scheduling are not hard real-time guarantees. A stuck
CreateProcess may not return a handle to terminate; later kernel calls may also be
non-preemptible. The original deadline is retained and late/unverified cleanup is
invalid, never accepted by increasing a timeout. The outer job timeout only bounds
runner disposal and cannot make incomplete child evidence valid.

## Local control failure and remaining prerequisite

Exactly one harmless local `NativeBufferSelfTest.exe` control was executed to check
the supervisor's real `_winapi`/Job/handle-list path. This was the already reviewed
fixture with no User32/SPI/GUI imports, SHA-256
`311655cd165d4a82631efce4b827decea6a5199d2e723da67175b94a4539e549`.
It is **not the collector or a hosted verification**. Its 21 native fixture checks
passed; PID **37804** exited 0 after approximately 0.125 seconds. However, Job
accounting returned **TotalProcesses=2 / ActiveProcesses=1**. The original result is
**FAIL**, with `cleanupVerified=false`; Job close was requested with kill-on-close,
but a final empty Job was not independently observed. No subsequent native control
was run to replace or close that observation.

Independent inspection found no demonstrated ABI/query error: Windows SDK
10.0.22621.0 agrees with Basic size 64, extended limits size 144 and accounting size
48, including DWORD offsets 32/36/40/44. Failed associations can affect Job counters;
that is a possible explanation, **not established attribution**. No extra process,
console host or successful child execution is inferred from TotalProcesses alone.

The next narrowly required evidence is raw accounting bytes/returned length,
applied-limit readback and exact Job PID lists while suspended and during cleanup,
plus verified termination/empty membership. That instrumentation is now proposed
and covered with mocks, but **not live validated**. Do not subtract a presumed helper,
normalise 2 to 1, relax the ceiling or call closing the Job proof of empty membership.
The configuration sets `supervisorQualification=BLOCKED_LOCAL_JOB_ACCOUNTING_UNRESOLVED`;
the coordinator rejects this before authority/network/native/process access.

## Source, binary and authority binding

The sealing recipe separates collector/tooling source commit **S** from the final
execution/package commit **E**. It compiles canonical Git blobs from clean S locally
with observed SDK 10.0.401, embeds S in assembly metadata, hashes the complete five-file
binary closure and records compiler/SDK files and build output. It never executes the
collector. E adds the immutable configuration/receipts. The exact owner record is
prepared in the final Goal #77 delivery comment **after E exists**, avoiding a
self-referential commit hash. It binds E/tree, S/tree, configuration SHA-256,
collector/archive identities, runtime archive, image and all process/deadline bounds.

The GitHub checkout step pins `core.autocrlf=false` through process-scoped Git
configuration, so checked-out coordinator bytes match canonical Git hashes.
`.gitattributes` is unchanged; canonical/source bytes and prior raw local bytes are
not represented as the same hash. Python runs in isolated mode and the child receives
an explicit environment allowlist with no token, profiler, startup hook, additional
dependency/shared-store or runtime-selection override.

The prepared record has `approved=false` and `decision=PROPOSED_NOT_AUTHORISED`.
Even after the supervisor prerequisite is resolved, a **new, unedited owner comment**
must explicitly authorise the revised exact packet. The coordinator checks owner
login/numeric ID, issue/comment URL, exact body hash/timestamps and all payload fields,
then rechecks before launch and after collection. A draft record or previous R01
authority is not approval. Changing the current blocked configuration requires fresh
hashes, review and a new E; approving this blocked packet cannot bypass its gate.

The separate opt-in job uses existing registered workflow ID **409767103** and disables
every other job in that dispatch. It requires attempt 1, the owner branch/actor and
matching workflow/source SHA. Repository-wide concurrency serialises this job; history
checks before/after collection require exactly the current dispatch for E, including
failed/cancelled attempts. No retry or automatic expansion is implemented. Run-history
reads are snapshots, not atomic GitHub consumption tokens: a later dispatch can
invalidate evidence, and queued jobs must reject reuse. An API failure stops execution.

## Evidence disposition and decisions

Native observation validity, native verification, original product correctness,
presentation attribution and independent-review status remain separate. A mechanical
PASS would mean only two valid fixed reads; original correctness remains NOT_RUN,
presentation order INCONCLUSIVE, infrastructure BLOCKED and independent review PENDING.
Final review must verify the GitHub run/artifact origin and terminal job status,
owner record, complete file manifest, binary/runtime closures, native records and
original failures with the offline `audit_package` entry point. No final live evidence
exists in this preparation and no final live review is claimed.

Owner decisions still required:

1. Decide how to obtain the narrowly missing supervisor evidence. The local 2/1
   accounting failure must receive independent disposition before a hosted collector run.
2. After that prerequisite passes, review the revised exact source/binary/configuration
   and authorise **one** read-only collector process explicitly, or reject it. An image
   mismatch requires a new environment decision; it never selects a substitute image.
3. Review the resulting native evidence independently before any further action.

R01/R02, 1280 × 800/100% geometry recovery, appearance round trips, calibration and
ADR operational activation stay blocked. The empty-scheme → `High Contrast Black`
restoration failure and run 42500925642 heap-corruption evidence remain unchanged.
