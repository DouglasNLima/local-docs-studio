# Independent review: Windows Job Object supervisor closure

**Disposition: prepared for a separate owner decision; native execution remains BLOCKED / NOT_AUTHORISED.** No remaining material defect was found in the reviewed preparation package after the corrections below. This is an independent technical agent review, not owner approval, real-kernel qualification, acceptance of PR #76 or authorisation to execute the proposed control.

Review date: 8 November 2026. Starting delivery was `a38131cced93f5c29c1a7aafdb5316b97e8c5903`. Reviewed source **S** is `331dc5ab220e5ca7dd145ff21a650d3d2578111f`, tree `776ef245307e9e969e19352597662de46bd35907`. The final documentation delivery **E** does not yet exist at review authoring; its exact commit/tree must be bound separately before any new owner decision. This report does not manufacture that identity.

The review covered the [supervisor](../../../scripts/native/qualification_hosted/live_process.py), [pure evidence verifier](../../../scripts/native/qualification_hosted/job_evidence.py), [local-control driver](../../../scripts/native/qualification_hosted/local_job_control.py), affected collector audit, fake-kernel controls, [protocol](job-supervisor-closure.md), [unapproved plan](job-supervisor-control.json) and retained original failure. Work consisted of source inspection, Git/file/archive hashing and separate-process Python tests. No native fixture, collector, real Win32 getter, Job API, hosted workflow, UI, input, display or appearance operation was executed by this reviewer. Archive members were read as bytes, not extracted or launched.

## Exact binding checks

The source commit/tree were independently resolved from Git. All **16 canonical source hashes**, **16 local raw-file hashes** and **16 canonical snapshots** inside the offline archive match the plan and source receipt. All 16 members of the separate raw-source ZIP match both the local files and `executionFiles`. The sole raw/canonical difference is `.github/workflows/native-ux-performance.yml`: its local bytes are exactly the canonical LF bytes converted to CRLF. The two hashes are deliberately distinct. There is no unexplained source difference.

| Bound object | Independently checked SHA-256 |
|---|---|
| [Plan](job-supervisor-control.json) | `415e0ecb2e10328e3a6850da926ab1c9ef237072352a7a903fb1f46424081be3` |
| [Source receipt](../evidence/job-supervisor-closure/source-receipt.json) | `ce52f5bb9d472f7d42e2e9f0305d2d916317f2ab14a0badd880e8fd3f5f1ec6f` |
| [Offline evidence ZIP](../evidence/job-supervisor-closure/offline-evidence.zip), 93,358 bytes / 72 unique members | `36355fb0469dc7b0a4cd5ced4c051a3c48233730952775faaf1cfd06e7898c92` |
| [Raw-source ZIP](../evidence/job-supervisor-closure/raw-source.zip), 64,338 bytes / 16 unique members | `6da92b5c88c1a3d511fd9b7aab742e29fc3c59d1dfc97137723099f58887602e` |
| [Original fixture archive](../evidence/buffer-safety-recovery/offline-original-bytes.zip), 1,229,220 bytes | `d28cc4f433b02f93bdfac1604b270123a16419da0229387104ccc990f5a29c27` |
| `attempt-6/NativeBufferSelfTest.exe` inside that archive, 20,480 bytes | `311655cd165d4a82631efce4b827decea6a5199d2e723da67175b94a4539e549` |
| `attempt-6/imports-NativeBufferSelfTest.exe.stdout.txt` inside that archive | `007a224faeeb1504527dbdf47767e77091b74129896cd0fb400d2249e1ed2bff` |
| [Original local failure archive](../evidence/highcontrast-live-readiness/preparation-evidence.zip) | `09e7c8918b1e48fc8cd24f43b700e57dfcff423f31ce4f2cdc5cf562e65fb9aa` |
| [Unchanged collector configuration](highcontrast-live/config.json) | `c2cf91cbd5678752d028d7f64a6d87a90e183fb1f73c12de35b9cc967ef13d7a` |

Both new ZIPs pass CRC/duplicate-member checks. The offline ZIP contains byte-identical copies of the plan and receipt, the 16 Git-source snapshots, retained unsuccessful preparation records and the author's final isolated test receipts. These checks establish artefact consistency; they do not turn test logs into native observations.

The observed local Python files also match the plan: `python.exe` is `42ac541168e97dedb9aabd8be335539fc41c682e414b9e8d137b164fb68683b0`; `python312.dll` is `e7890e38256f04ee0b55ac5276bbf3ac61392c3a3ce150bb5497b709803e17ce`. Their absolute paths and exact CPython 3.12.0 version string are recorded in the plan. These are file identities, not certification of every loaded Windows/CRT module.

The focused implementation and test files below are byte-identical between the reviewed working copy and S. The full 16-file closure remains in the plan rather than being duplicated here.

| File | Raw and canonical SHA-256 |
|---|---|
| `scripts/native/qualification_hosted/live_process.py` | `227495954a6822d9ea672fa1c00a410da9d3582ff74fd67022c8e31fbb79e3d4` |
| `scripts/native/qualification_hosted/job_evidence.py` | `62e413974daef35e4e14ba9f68a0c9cd29eeaf3f79542dbfef4333d36e8b3f1b` |
| `scripts/native/qualification_hosted/local_job_control.py` | `627a4ddd0a6dc9d9b2275ef67bf240426fed249bed1595360b2f21558913e5ac` |
| `scripts/native/qualification_hosted/live_verification.py` | `62c358eb7cbccf0f8ee4db23735638fcbb12fcb88d13041e977b1ba9a6a893dc` |
| `tests/unit/test_native_job_supervisor.py` | `221442b1793560428e7d9077b2505449da83935d16b5758797e7c940e061f91c` |
| `tests/unit/job_supervisor_support.py` | `4632f8019ec7946e59f443fcb76038223d683c2b5b31e6aa2ee67bcda93c4080` |
| `tests/unit/test_native_live_verification.py` | `7dca05ba0ad57928ea0c427b9a266c7f0cec0392756da84b23361483a45798c4` |

## Historical failure and API interpretation

The original failure archive and collector configuration are byte-identical to their Git objects at the starting delivery. The retained PID 37804 record still reports exit 0, total processes 2, active processes 1 and `cleanupVerified=false`. Its supervisor snapshot remains `787b01ba1a3abdd87b78feb5bd4b5615630e5949798ef11b4be2f2957fb1c78a`. The original **FAIL** and **INCONCLUSIVE cause** are preserved.

No accounting structure-size, offset or information-class defect was demonstrated. Microsoft documents a newly created Job as initially empty, and cumulative accounting can include processes whose attempted association failed because of limits. That does not identify the extra count in this record, establish an extra running child, or justify subtracting it. Initial private-Job state, raw PID-list evidence and post-handle-release observations were missing from the original control. [Job creation](https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-createjobobjectw), [accounting](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_basic_accounting_information), [assignment](https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-assignprocesstojobobject).

One source defect is confirmed: an exception during termination in the old combined cleanup block could skip its later wait, exit and Job observations. The original PID 37804 record does not report that exception, so the defect does not explain its 2/1 result. The review's earlier `WAIT_FAILED` classification hypothesis was withdrawn after checking the exact CPython 3.12.0 implementation: its wrapper already raises `OSError` for that native result. Structured retention improves evidence without claiming an old wrapper defect. [Exact CPython wrapper](https://github.com/python/cpython/blob/v3.12.0/Modules/_winapi.c#L1739).

Process exit, release of owned handles, Job membership and closing a kill-on-close Job remain separate observations. Termination is asynchronous; successful closure does not itself prove that an empty membership observation occurred. The PID-list written prefix must be checked against its header and returned length rather than assumed equal to the allocated buffer. [Termination contract](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-terminateprocess), [PID-list contract](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_basic_process_id_list), [query contract](https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-queryinformationjobobject).

## Findings resolved during review

| Finding | Final disposition |
|---|---|
| Cleanup initially depended on acceptance of the child image/PID. A failed identity check after creation could leave an unassigned suspended child without a termination attempt. | Ownership now follows successful CreateProcess independently of experimental identity. Cleanup uses the owned handle, with no PID reopening. Negative controls verify termination/wait after rejected identity. Invalid or aliased handles cannot produce a passing result. |
| Journal write/flush exceptions could prevent cleanup calls. | Logging failure is sticky and blocks further creation/assignment/resume. Cleanup still attempts termination, wait and each close independently. Close attempts are registered before invocation and are not retried. |
| A passing summary could coexist with missing or contradictory operation records. | The pure verifier requires all 25 successful normal operation pairs, exact ordering and linkage of arguments, handles, results, times and raw snapshots. The three reproduced false-PASS variants now fail. |
| Membership disappearance was tracked jointly, allowing one stream to reappear. | Active-accounting and PID-list disappearance are tracked separately. Only monotonic transitions involving the known child are eligible; total 2, quota terminations or foreign PIDs remain sticky failures. |
| Cleanup observations could be bunched late while claiming four targets. | Four fixed samples remain mandatory, with start windows `[0,0.1)`, `[0.1,1)`, `[1,4)` and `[4,5)` seconds. Missing, bunched or late evidence fails; there is no early stop on an empty result or retry until PASS. |
| The proposed local entry point lacked enforced interpreter isolation and immediate source revalidation. | `-I -S` is required before plan/Git/network/native access. The exact plan, clean HEAD, local source and Python files are checked before launch and again after collection. Canonical and raw source hashes remain distinct. |
| The journal-failure test fixture mishandled keyword-only `Path.open(mode='rb')`, producing an unclosed-stream warning. | The fixture now handles positional and keyword modes. The old transcript remains retained; the final independent test runs have no such warning. This was a test-fixture issue, not evidence of a native handle leak. |

The protocol retains one suspended child, one assignment attempt, active-process limit 1 and ten fixed Job boundaries. Each boundary retains the three native query results and raw buffers. The eight PID slots are diagnostic capacity, not permission for eight processes. The original 30-second observation bound and separate five-second cleanup bound are unchanged. Synchronous APIs and thread scheduling do not provide a hard real-time guarantee; late or unverified outcomes fail rather than extending the bounds.

## Independent validation

After sealing S, the reviewer ran these modules in separate isolated Python processes:

| Command | Independent result |
|---|---|
| `python -I -S -m unittest discover -s tests/unit -p test_native_job_supervisor.py -v` | **29 PASS**, 0.842 seconds |
| `python -I -S -m unittest discover -s tests/unit -p test_native_live_verification.py -v` | **17 PASS**, 2.514 seconds |

Supervisor tests supply a fake kernel and clock and reject accidental real Kernel construction; the ABI control checks ctypes layout without loading kernel32. The local-entry refusal control verifies no authority request, Git command or native construction. The existing live tests include a local CLI refusal before native access. These are offline controls, not observations of Windows Job behaviour. No broad combined suite, browser campaign, native process or hosted run was used.

The sealed author's receipts independently retain the same 29 and 17 passing module counts. Earlier evidence is not overwritten: the 19/20 float-equality failure, the test-stream warning and the whitespace check's exit 2 remain in the offline archive. The float assertion correction did not widen observation windows or change native acceptance. Later passing results apply to their later source, not retroactively to the unsuccessful invocation.

## Owner decision and remaining limitations

The plan is `NOT_AUTHORISED`, with `approved=false`, `campaignExecutionApproved=false` and `supervisorQualification=UNPROVEN_PENDING_SEPARATE_LOCAL_CONTROL`. Its exact bounds are one fixture process, one assignment attempt, ten Job observations, eight PID slots, 30 seconds plus five seconds cleanup, and zero retries, collectors, hosted runs, qualification or calibration processes. The expected fixture report preserves all 21 original native checks and zero actual HIGHCONTRAST calls.

The archived import report contains Kernel32 and CRT/C++ dependencies, with no User32, SPI, process-creation, ShellExecute, LoadLibrary or GetProcAddress import. This supports the previously reviewed simulation scope; it is not a complete loaded-module certification. No rebuilt or substituted binary is proposed. The original local control's inherited environment was not retained, so equivalence to that environment is not claimed.

The owner can approve or reject **one separately executed local fixture control** after the final E/tree and this plan hash are presented. Execution requires a new unedited Goal #77 JSON comment from the exact owner identity, binding those identities and bounds, with its body hash checked before launch and after collection. The proposal with `approved=false` cannot satisfy that check. The fixed exclusive output directory records the spent attempt; deleting or renaming it to retry is outside the proposal. This review grants no such authority.

Any failed identity, cumulative count other than one after assignment, foreign PID, quota termination, missing or contradictory evidence, missed bound or unverified cleanup retains BLOCKED. A later valid single-fixture observation would still require independent evidence review and would not explain the original 2/1 failure or certify the HIGHCONTRAST collector. The unchanged older collector packet remains blocked and its source closure no longer matches the revised diagnostics; a future packet requires a separate reseal and decision.

PR #76 remains **NO-GO**. R01/R02, hosted discovery, appearance/display experiments, calibration, numeric-policy changes, gate migration, signing, release and ADR operational activation are not authorised by this package. Historical geometry, restoration, contrast and heap-corruption failures remain open under their existing classifications.
