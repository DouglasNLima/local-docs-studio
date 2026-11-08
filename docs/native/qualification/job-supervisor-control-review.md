# Independent review: authorised local Job fixture result

**Final disposition: FAIL / BLOCKED. The one-run authority is spent; no retry or subsequent native execution is authorised.** The evidence is sufficiently intact to establish specific infrastructure failures. It does not qualify the supervisor or identify the unexpected Job member. No product correctness or performance result is inferred.

This is an independent technical agent review of the single execution under [owner comment 6726673440](https://github.com/DouglasNLima/local-docs-studio/issues/77#issuecomment-6726673440), not owner approval of another action. Review date: 8 November 2026. The reviewer used read-only file/Git/archive inspection, independent binary decoding and the unchanged pure verifier. No native fixture, collector, Job/Win32 diagnostic API, hosted workflow, UI, input, appearance or display operation was executed during this review. Newly derived files were written only under `artifacts/goal77-job-authorised-independent`.

The [result report](job-supervisor-control-result.md) and [raw evidence index](../evidence/job-supervisor-authorised-control/README.md) describe the retained execution. The independent script and machine-readable audit accompany those raw records in the evidence package; they do not replace or alter them.

## Source, authority and evidence integrity

Execution commit **E** is `d4261e58b55d6755feddf179ea9e922a687e3e9f`, tree `2768237f85171258cded141101a1213d3c9bd1c6`. Reviewed source **S** is `331dc5ab220e5ca7dd145ff21a650d3d2578111f`, tree `776ef245307e9e969e19352597662de46bd35907`. All 16 canonical source hashes at E match S, and all 16 raw local hashes match the sealed plan. The Python executable/DLL, original fixture archive/member, extracted fixture and copied plan also match their reviewed identities. Documentation-only result work began after the initial post-run clean-tree observation; it did not change these implementation bindings.

The three retained pre-launch authority representations match the exact approved payload, owner identity, unedited creation/update timestamp, E/tree, S/tree, plan hash and bounds. Their body SHA-256 is `5550d95aafd502981f91a439447e689e61827bf31350341d2285c5b3a13e7e59`. The independently inspected supplementary API record collected after failure has the same owner/body identity. It remains a supplementary record, not the absent driver `owner-after.json` boundary.

The original run manifest covers its exact ten non-manifest files; all hashes match. The 52 JSONL events match the operation array in `process.json`, with 26 correctly paired, ordered operations. Independent `struct` decoding of all **30 raw Job queries** matches the recorded decoded values. Native query returns, class identifiers, allocated lengths, written prefixes, PID headers and timestamps are internally consistent. Accounting and limits returned 48 and 144 bytes; PID-list writes returned 8 bytes when empty and 16 bytes for one PID, within the unchanged 72-byte allocation.

| Retained object | SHA-256 |
|---|---|
| Approved plan | `415e0ecb2e10328e3a6850da926ab1c9ef237072352a7a903fb1f46424081be3` |
| Fixture executable, 20,480 bytes | `311655cd165d4a82631efce4b827decea6a5199d2e723da67175b94a4539e549` |
| `process.json` | `35ab4f7eb63a943ee5f09376d7d9d346a04e4b4ef3cbf53a37df93c21a8c789d` |
| `supervisor.jsonl` | `6dcfda7102792df97cb49b7210a2fbf04d55d926b9b6fdc4dffdb3c779dc02ca` |
| Fixture `stdout.json` | `233a2a41997041f7c13cc37ae4fe92f597073a784be5771a3269c2d931aa366e` |
| Outer `driver-exit.json` | `ebabda3279e52d21aedc0c5c877955499bbd666cc496cd042c88a4548da110e3` |
| Independent `audit.py` | `ffee174646c43f4eeb90e88289822143300c361c7f9cbe088f3fc2056ba5928a` |
| Independent `audit.json` | `0d0fd960f85a99d06a205ee8f589baef2b49b1bc3bc34fb9b25b7336585a64ca` |

The independent audit includes hashes for every run file and retained outer-capture file. Its integrity checks passed while its explicit overall result is `FAIL_BLOCKED_AUTHORITY_SPENT`. Reapplying the unchanged pure `verify_supervision` function rejects the original record with `Supervisor has missing or failed operations`. Passing provenance checks are not a native-control PASS.

## Confirmed observations

The outer capture records one invocation of the reviewed isolated `-I -S` driver, PID **28708**, with driver exit **1**. Its journal records one private Job creation, one explicit fixture creation and one explicit assignment. The suspended fixture identity is PID **39616**, thread **12880**, with the expected executable path/hash and creation time. Owned handles are Job **700**, process **824**, thread **812**. The supervisor was in another Job but was not in this private Job at the recorded pre-launch check; no identity or causal claim about that other Job is made.

| Fixed boundary | Cumulative total | Active | Recorded PID list |
|---|---:|---:|---|
| CREATED_EMPTY | 0 | 0 | empty |
| CONFIGURED_EMPTY | 0 | 0 | empty |
| BEFORE_ASSIGNMENT | 0 | 0 | empty |
| AFTER_ASSIGNMENT | 1 | 1 | 39616 |
| AFTER_RESUME | 1 | 1 | 39616 |
| PROCESS_EXIT | **2** | **1** | **57332** |
| CLEANUP_0 | 2 | 0 | empty |
| CLEANUP_1 | 2 | 0 | empty |
| CLEANUP_2 | 2 | 0 | empty |
| CLEANUP_3 | 2 | 0 | empty |

Configured limit flags remain `0x2008` and active limit 1 throughout. Quota-termination accounting is zero in every snapshot. That counter does not substitute for the separately recorded explicit termination request.

The direct fixture wait signalled and exit code **0** was read through the owned process handle at recorded time **0.06300000008195639 s**. Fixture stdout exactly matches all **21** expected checks, reports zero actual HIGHCONTRAST calls, and fixture stderr is empty. Those scoped fixture observations pass. They do not establish the required total-one Job invariant: a different PID, **57332**, is present in the raw exit-boundary PID list.

The subsequent exit-identity operation records `QueryFullProcessImageNameW` returning **0 / WinError 31**. No complete `childIdentityAtExit` was produced. Validation then dereferenced that null result, producing the retained secondary `TypeError`. Cleanup and the exit Job observation continued despite those diagnostic errors.

The supervisor made one `TerminateJobObject` request for `FAILED_OBSERVATION` and recorded success. Thread and process handle closes succeeded, then all four cleanup snapshots reported active 0 and an empty PID list, and the Job handle close succeeded. These are confirmed cleanup observations. The entire cleanup interval is recorded as **4 seconds**, within its five-second upper bound; direct-child observation was within 30 seconds. Nevertheless, `cleanupVerified=false` remains correct for the complete contract because the cumulative discrepancy and other failures persist.

## Qualification failures and attribution limits

1. **The required total-one Job invariant failed.** The Job was initially empty, then contained the expected suspended fixture, and subsequently reported total 2 with member PID 57332. One explicit supervisor creation/assignment is confirmed; it does not prove that only one process became associated. No image path, parentage, creation time or causative component for PID 57332 was captured. Calling it a helper, leak, console process or particular Windows component would exceed the evidence.
2. **Complete exit identity failed.** Native error 31 is confirmed; its exact cause is inconclusive. The evidence does not show that the fixture image changed or that the process handle was replaced. The secondary null dereference is a confirmed diagnostic-handling defect, not another independent native failure. It did not suppress the later cleanup observations.
3. **The fixed cleanup schedule failed.** Cleanup started at `0.06300000008195639`; CLEANUP_1 started at `0.15700000000651926`. The observed offset is **0.09399999992456287 s**, below the unchanged 0.1-second lower bound. The other three sample starts match their windows. No rounding allowance or retrospective timing waiver is justified. The exact reason for the early recorded interval is inconclusive. Source inspection also confirms that the collection loop checked the upper scheduling boundary after its single sleep but did not reject an early wake/read before observation; the pure verifier has the lower-bound check. This gap does not turn the existing failed result into a pass.

The original PID **37804** failure remains byte-identical in its archive: total 2, active 1, `cleanupVerified=false`, causal attribution inconclusive. This execution provides an identified numeric extra member in a different run; it does not identify a common cause or retrospectively explain the original result.

All ten observations and their raw queries were collected, so the record contains useful valid diagnostic evidence. Full protocol qualification still fails. Empty final membership and successful owned-handle closes establish narrower facts; they cannot erase the additional member, failed exit identity or early fixed sample.

## Missing evidence and next-decision boundary

The driver rejected the record before its success path and did not produce `owner-after.json` or `terminal.json`. Their absence is preserved. The separately captured post-observation authority object confirms the same unedited payload in that retained object; it does not recreate the missing driver boundary. Missing exit identity and the unidentified member's origin also remain missing.

The draft result report was reviewed against the raw bytes and accurately preserves these distinctions. References to one creation or assignment describe the reviewed supervisor's explicit calls, not every possible process-creation mechanism in the system.

After completing this review, the recommended disposition is to **retain BLOCKED and stop native execution**. Minimum further technical work, if the owner wishes to continue, is a bounded **offline design/review** addressing exit-identity handling, lower-bound timing enforcement and an ownership-safe way to obtain sufficient identity evidence for an unexpected Job member. That design must preserve the existing failures, limits and no-retry rule; it must not assume the member's identity. No new execution is recommended as ready now. Any future changed-source experiment needs its own reviewed scope, exact bindings and explicit owner decision.

This completed local authorisation does not authorise HIGHCONTRAST collector verification, hosted discovery, R01/R02, appearance/display changes, calibration, signing, release or policy activation. PR #76 remains **NO-GO**; the historical 250 ms and 4.5:1 requirements and all previous failures remain unchanged.
