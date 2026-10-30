# Goal #56: Save/UIA causal investigation

Established on 30 October 2026, before new hosted execution. Diagnostic-only;
PR #69 and existing acceptance sources remain unchanged. No integration or release authority.

## Identities and historical evidence

Fresh fetch confirms develop `86d60322f6f1f1dec06974bf5f9e1738a3bb80c7`,
tree `552158f43ebf2501c684d210dd9ebae80a3e6f8f`, and candidate
`c85a51118ac0f175cb21c79c3f7503ee1bc8ecd0`, tree
`9f578b8bc6ceb99dd58e5ab195f0170e8c3232db`.
Prior protocol `5b1ca14b8e4af318980c19af3ec545e2ca2b30bc`, tree
`a0a528499c61fb9c4a89fcadd448d1af712bafc3`, is preserved separately.
This branch starts from develop; its diagnostic additions do not contain PR #69.
The workflow records its own immutable commit/tree before testing.

Original [run 41060148158](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41060148158)
failed in [job 124077546833](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41060148158/job/124077546833)
at `MermaidCreationUiChecks.cs:132`, following Architecture/default Apply, Undo,
Redo and ordinary Save, when replacing source with a duplicate via ValuePattern.
Its exception is `System.InvalidOperationException: Operation is not valid due
to the current state of the object.` Original archive SHA-256:
`b4f84c6c64189667569c4191b7e91a92c50023145c86670944ba910be7c3bb3f`.
The archive/log lack boundary state and an explicitly captured HRESULT.
Previous eight successful attempts establish non-reproduction only.

## Questions and observations

Test persistence/read-only overlap, binding notification order, dispatcher
activity, provider identity/lifecycle, focus/foreground ownership, initialisation
and source-specific changes. No hypothesis is selected in advance.

Patch only disposable source copies using the same checked script for both SHAs.
Record Save entry/exit, IsSaving setter callback and INotifyPropertyChanged,
actual read-only dependency-property changes, document flush/save completion,
session/preferences scopes, shared persistence gate acquisition, dispatcher
posted/started/completed/aborted boundaries, provider focus and Value calls.
Provider wrapping forwards to the original AvalonEdit IValueProvider exactly once.
The diagnostic ItemStatus exposes an actual read-only native snapshot; it does
not replace any functional assertion. No source content is logged by Save/UIA
probes. Rendering probes record only these controlled test fixtures.

Each process records UTC, monotonic ticks/frequency, sequence, PID, thread,
synthetic session and nested operation/Save IDs. A named shared-memory marker
links client Save/SetValue requests to native provider entry without a handshake,
sleep, refocus or reacquisition. Sequence-lock inconsistencies are retained.
Use same-process sequence and nested scopes plus the shared marker to establish
ordering; unrelated UTC clocks alone cannot establish causation. Post-Save
continuations retain their AsyncLocal Save identity across dispatcher boundaries.

External bounded before/after snapshots retain HWND/PID, RuntimeId, AutomationId,
control type, focus/foreground, enabled/offscreen, Value/Text support, read-only,
and diagnostic native state/pending operations. Failure snapshots additionally
reacquire a separate editor and retain exact exception/HRESULT/stack. They never
replace the original element before its operation. The original exception is
rethrown and the acceptance test fails. Unsupported reads remain explicit errors.

Limitations: observations perturb timing; UIA reads can themselves dispatch work.
Snapshot fields are sequential observations, not an atomic cross-process state.
Provider entry is the native call boundary, if reached; absence of entry does not
identify which upstream component rejected the call. Dispatcher hooks are not a
complete queue inventory or proof of idleness. The bounded background JSONL writer
has no dispatcher disk writes; abrupt process termination can lose its final
queued events. Missing tails, sequence gaps, writer errors, inconsistent markers
or the 30,000-event cap invalidate claims requiring those observations. No telemetry
is added to release builds. Flush waits occur only after failure/test completion.

## Fixed budget and order

Exactly one new hosted workflow run, attempt 1; two equivalent fresh Windows
Server runners, one baseline and one candidate. Source-job start order is not
used as evidence. Within each job: unchanged locked restore/build; unchanged
historical rendering selection once; unchanged High Contrast Creation suite
once; patch and build a separate diagnostic checkout; the two named rendering
tests once with phase instrumentation; diagnostic High Contrast Creation suite
once; then a separate synthetic Save/persistence barrier experiment once.
Maximum: four ordinary High Contrast suites and two synthetic scenarios.
No reruns or extra repetitions after non-reproduction or infrastructure failure.
One failed step must not erase later independent evidence; final job status fails
if any required step fails. Build failure prevents dependent tests and is recorded.

Locally, run the unchanged historical selection once per exact source, sequentially,
and the diagnostic two-test selection once per exact source, sequentially. Local
GUI/High Contrast is NOT TESTED to protect the owner's interactive environment.
Compilation repairs before the first hosted execution are allowed; test failures
do not authorise extra executions. No arbitrary wait, retry or changed deadline is
introduced into the original acceptance path. Three-second/250 ms limits stay fixed.

The historical selection is captured in `historical-tests.txt` from the preserved
92-case TRX. Baseline lacks the new installer-choice cases; report actual counts
and compare common cases explicitly. Diagnostic rendering activity records actual
parse/layout/SVG phases and deadline exception stacks without changing assertions.
Inspect source at the render boundary and the original failure's preservation
behaviour; never infer invalid output merely from a deadline. Local original phase
and actual output cannot be reconstructed where the old trace did not capture them.

## Deterministic synthetic experiment

In its own fixture/process only, hold the next preferences write reached *inside*
an actual Save after document persistence, using an explicit test-only asynchronous
barrier. Its ten-second safety bound and polling belong only to this separate
experiment. Observe disk bytes, IsSaving, real read-only and pending operations;
attempt the same retained editor's real UIA SetValue once; retain its exception;
release persistence; observe Save finish and try one writable control operation.
Do not inject an exception. Preserve the normal save sequence and original provider.
This can establish `RACE_DETERMINISTICALLY_DEMONSTRATED` for the overlap, and whether
it is sufficient to reject SetValue in this environment. It cannot establish
`ORIGINAL_FAILURE_CAUSALLY_ATTRIBUTED` without matching original evidence.

## Environments, retention and stop conditions

Hosted SDK pinned to 10.0.403; record actual Core/WPF runtime, Windows build,
runner image, CPU, source commit/tree, all patch/untracked-file hashes and outcomes.
Local SDK 10.0.401, Core/WPF 10.0.12, Windows build 26300; do not equate environments.
Retain raw JSONL, TRX, screenshots, original errors, source patches, tool versions,
per-file hashes and hosted ZIP digests for 30 days, with local verified copies.
Do not relabel modified binaries as exact unchanged source acceptance.

Stop at the budget, or preserve a reproduced original exception for independent
review before considering any production correction. A separate reviewer challenges
causality, instrumentation and each rendering result. Success without attribution
remains `ROOT_CAUSE_INCONCLUSIVE`, `PR69_INTEGRATION_NO_GO`, `NATIVE_STABLE_NO_GO`.
Preserve `NATIVE_CONTINUOUS_250MS_FAIL` and `SIGNPATH_ELIGIBILITY_UNCONFIRMED`.
No merge, ruleset change, threshold change, certificate change, signing, tag,
release, public preview, SignPath contact or production fix without demonstrated cause.
