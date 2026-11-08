# Authorised local Job fixture: result

**FAIL / BLOCKED. The single authorisation is spent; no retry is authorised.**
One reviewed driver invocation created one fixture and attempted one assignment.
The Job nevertheless reported an additional, unidentified PID and cumulative count
2. The fixture's zero exit and 21 checks do not qualify the supervisor.

The [owner record](https://github.com/DouglasNLima/local-docs-studio/issues/77#issuecomment-6726673440)
is a new, unedited JSON comment with
`decision=AUTHORISE_ONE_LOCAL_JOB_FIXTURE_CONTROL` and `approved=true`.
Every other field matches the reviewed proposal. Its UTF-8 body SHA-256 is
`5550d95aafd502981f91a439447e689e61827bf31350341d2285c5b3a13e7e59`.
See the [raw evidence and receipts](../evidence/job-supervisor-authorised-control/README.md)
and [independent evidence review](job-supervisor-control-review.md).

## Exact execution

| Binding | Observed identity |
|---|---|
| Execution E / tree | `d4261e58b55d6755feddf179ea9e922a687e3e9f` / `2768237f85171258cded141101a1213d3c9bd1c6` |
| Reviewed source S / tree | `331dc5ab220e5ca7dd145ff21a650d3d2578111f` / `776ef245307e9e969e19352597662de46bd35907` |
| Approved plan | [Unchanged sealed plan](job-supervisor-control.json), SHA-256 `415e0ecb2e10328e3a6850da926ab1c9ef237072352a7a903fb1f46424081be3` |
| Fixture | Original 20,480-byte `NativeBufferSelfTest.exe`; SHA-256 `311655cd165d4a82631efce4b827decea6a5199d2e723da67175b94a4539e549` |
| Python executable | CPython 3.12.0 x64; SHA-256 `42ac541168e97dedb9aabd8be335539fc41c682e414b9e8d137b164fb68683b0` |
| Python DLL | `python312.dll`; SHA-256 `e7890e38256f04ee0b55ac5276bbf3ac61392c3a3ce150bb5497b709803e17ce` |
| Time | 2026-11-08 20:26:27.216817–20:26:32.853337 UTC, outer driver capture |
| Identities | Capture parent 14352; isolated supervisor 28708; fixture 39616; fixture thread 12880 |
| Owned handles | Job 700; process 824; thread 812 |
| Driver / fixture exit | 1 / 0; these are separate results |

The clean worktree, E/S trees, all 16 canonical and raw source hashes, exact
interpreter executable/DLL, fixture archive/member and plan were verified before
execution. The approved authority was read back and verified before launch. The
reviewed driver checked it again immediately before creating the fixture. Post-run
byte auditing confirmed the same identities and clean checkout before result
documentation was added. Canonical and raw workflow line endings remain separately
bound; no source, plan, fixture or interpreter substitution occurred.

The capture wrapper invoked the existing driver once with `python -I -S`, the
comment ID and exact body hash. It saved raw driver stdout/stderr and exit status;
it contains no retry or native observation code. The exclusive spent-attempt
directory remains intact. Git and Python orchestration are outside the experimental
Job. The one-fixture process ceiling still failed because of the observed extra
Job member; one direct creation call is not proof of one total member.

## Ten fixed observations

All 30 native Job queries returned successfully with complete allocated raw bytes,
explicit capacity and returned length. Accounting was 48 bytes; limits were 144;
the PID buffer was 72 bytes with capacity eight. The diagnostic capacity did not
increase the permitted active limit. Configured flags were `0x2008`, active limit 1,
at every subsequent readback.

| Boundary | Total | Active | PID list | Observation |
|---|---:|---:|---|---|
| CREATED_EMPTY | 0 | 0 | empty | Unconfigured empty Job |
| CONFIGURED_EMPTY | 0 | 0 | empty | Limits applied and read back |
| BEFORE_ASSIGNMENT | 0 | 0 | empty | Suspended fixture identity accepted |
| AFTER_ASSIGNMENT | 1 | 1 | 39616 | One successful assignment; association readback true |
| AFTER_RESUME | 1 | 1 | 39616 | Resume returned previous suspend count 1 |
| PROCESS_EXIT | 2 | 1 | **57332** | **Cumulative discrepancy and foreign membership** |
| CLEANUP_0 | 2 | 0 | empty | Empty after a successful termination request; discrepancy persists |
| CLEANUP_1 | 2 | 0 | empty | **Recorded at 0.0939999999 s, before the fixed 0.1 s lower bound** |
| CLEANUP_2 | 2 | 0 | empty | Recorded at 1 s from cleanup start |
| CLEANUP_3 | 2 | 0 | empty | Recorded at 4 s; final count remains 2 |

The direct child wait signalled at 0.063 s and its recorded exit was 0. The
subsequent `QueryFullProcessImageNameW` returned 0 / WinError 31. The at-exit
identity is consequently null, not verified. Attempting to validate that null
result produced a secondary `TypeError`; both errors remain in the evidence.
The Job's exit-boundary observation was still collected.

The supervisor requested `TerminateJobObject` once for `FAILED_OBSERVATION`,
recording success. It then recorded successful thread and process handle closes,
all four cleanup observations and successful Job-handle closure. Cleanup finished
at the recorded 4 s within its separate five-second bound; the 30-second supervision
deadline was not reached. Empty final membership and handle closure are confirmed
observations, while **`cleanupVerified=false` remains unchanged**. They cannot erase
the foreign member, total 2, missing identity or invalid fixed observation time.

## Evidence and attribution boundaries

| Question | Disposition |
|---|---|
| Did the intended harmless fixture run? | Yes: exact pre-resume image/hash and PID, matching original 21-check JSON, empty fixture stderr and direct exit 0. Its report records zero actual HIGHCONTRAST calls. |
| Was a second direct creation or assignment attempted? | No: the complete journal records one of each, one Job creation and zero retries. |
| Was there an extra Job member? | Yes: raw PID-list bytes at PROCESS_EXIT identify 57332, different from the assigned 39616. |
| What was PID 57332? | **INCONCLUSIVE**: no image, parent, creation time or cause was captured. Do not label it a helper, leaked child or particular Windows component. |
| Why did the at-exit identity query fail? | **INCONCLUSIVE**: WinError 31 is retained. The failed query does not demonstrate that the fixture image changed. |
| What is known about the supervisor's Job context? | Its parent-identity record says `inAnyJob=true` and `inPrivateJob=false`. The ancestor Job identity and any relationship to PID 57332 were not established. |
| Why was CLEANUP_1 early in the recorded clock? | **INCONCLUSIVE**: the recorded interval violates the unchanged window; no rounding allowance or timing waiver is applied. |
| Does this explain the historical PID 37804 total-2/active-1 result? | No. The old observation remains FAIL with inconclusive cause. This run adds evidence of an unidentified member in a different execution. |
| Is this a product correctness or performance result? | No. It is failed infrastructure qualification, with no candidate correctness PASS or demonstrated product regression. |

The driver stopped at the unchanged verifier's first rejection, `Supervisor has
missing or failed operations`, and emitted `failure.json`. It did not emit the
success-only `owner-after.json` or `terminal.json`. Their absence is explicit; no
replacement records were inserted. A separately labelled, read-only API fetch
after failure confirmed the authority remained unedited. It does not substitute
for the missing driver success boundary.

The 52 journal entries, matching `process.json` operation records, all raw Job
bytes, source/runtime bindings, stdout/stderr, termination and close results are
retained byte for byte. The pure verifier still rejects these bytes. Validation
of the archive and rejection behaviour is not a PASS for the native control.

## Independent disposition and remaining decisions

The completed independent review confirms **FAIL / BLOCKED**, valid source/file
bindings and usable raw diagnostic records. It also identifies two source-level
diagnostic gaps: validation dereferences a missing exit identity, and the collection
loop does not enforce the lower scheduling boundary after its sleep. The unchanged
pure verifier rejects failed evidence; no implementation or acceptance change was
made in this execution continuation.

The recommendation is to stop native execution. If the owner chooses to continue,
the minimum next scope is bounded offline design and review of exit-identity
handling, lower-bound timing enforcement and ownership-safe evidence for an
unexpected Job member. Its identity must not be guessed. Any later live proposal
would require newly reviewed source/configuration bindings and separate explicit
owner authority. **No new execution is ready or authorised by this result.**

R01/R02, HIGHCONTRAST verification, appearance/display experiments, calibration and
ADR operational activation remain blocked. The historical 250 ms and 4.5:1
requirements, all original failures and all product code remain unchanged.
PRs #76/#78/#80 remain Draft; PR #76 remains **NO-GO**.
