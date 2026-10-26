# Goal VIII-B — local acceptance investigation

The original Goal VIII failures remain FAIL. PR #48 remains open and draft.
Accepted develop is `9b1e73bbf564c53b48149e8c0dcd6fd7acbe308f`, tree
`1fb777317ad6e5c8531eebcb565fefae4227e354`. The original candidate is
`11957e294a6f87b9729913b174b919ff23d176ed`, tree
`ced9a65224b5ae00be2100112aaf27093a745511` (portable .880).

## Before editing

Re-fetched both refs; independently rechecked eight candidate and two develop
workflow runs, all attempt 1 and successful, with their existing intentional skips.
Read the original report, delivery receipt, c01 Find failure and before/after UIA
captures, s08 pointer differential and s14/s15 failures. Original hashes and UTC
file times are retained in the ignored source-labelled evidence root
`artifacts/native/g8b/11957e294a6f87b9729913b174b919ff23d176ed/f01/original-evidence-hashes.json`.
The original c01 observations at 21:03:13–21:03:34 UTC on 24 October show
FindText focused, Find in editor, PID 7532, HWND 2101514, DPI 144, and no successful
exact selection assertion. They do not contain an in-process editor selection.

| Hypothesis | Existing observation | Falsifying experiment |
| --- | --- | --- |
| Enter never reaches FindText | Query/focus captured; native key delivery unobserved | Process-local key event observation around one Enter |
| A command or control handles Enter before FindKeyDown | Handler is attached to bubbling KeyDown; interception unobserved | Observe routed event handled state and native selection, then compare one existing Next click |
| Native selection exists but UIA omits it | Editor.Select exists in source; no native state receipt | Compare native start/length/text with UIA range at the same action boundary |
| Query, source, Unicode range or context differs | UI shows Café and editor context; query bytes not retained in old capture | Record exact query, source SHA, UTF-16 occurrence offsets and focus |
| UIA stale/provider timeout | s14 records a TextPattern timeout | Record provider errors and fresh same-HWND observations within original 20 seconds |
| Another cause | None demonstrated | Preserve unexpected observations without assigning an unsupported cause |

The new diagnostic modes use separate fresh guarded sessions and one action each.
They cannot replace full shell acceptance. Source, package, runtime and preservation
guards, 25-second owner readiness and 20-second observation waits are unchanged.
No product change has been justified yet. The incomplete-arrow fixture has a
DiagramService pre-parser guard; its actual result must be tested, separately from
Mermaider parser behaviour, before a new GUI expectation is accepted.

HOST PORTABLE, INTERNAL BETA and PUBLIC PRODUCTION remain BLOCKED. Installer,
physical screen reader, alternate scale and clean-machine certification remain
NOT RUN. PR #38 and its incomplete 250 ms goal remain paused.

## New observations, 25 October 2026

The clean diagnostic client at `9b10c48f3399a8c70054215f72c2dd12f6c89039`, tree
`a7d49461844617004ec930f2a219e3c95fc522c5`, used the original independently verified
.880 portable bytes. Each sandbox has its own Plan, Prepare, runtime, UI and
Complete records. No old session was relaunched or overwritten.

| Requirement | Before | New evidence | Outcome |
| --- | --- | --- | --- |
| FindText Enter selects exact Café | c01 and s08/s14/s15 FAIL | b01: PID 35872, HWND 26810466, DPI 144; one Enter; UIA empty range at offset 0 before/after and after closing Find; unchanged editor/disk source | FAIL, cause UNPROVED |
| Existing Next button differential | Earlier s08 pointer selected Café | b02: PID 79144, HWND 9639990; foreground refused before any input; normal owned close, no action replay | NOT RUN / BLOCKED |
| Original truncated label expectation | c01 invalid-source expectation FAIL | Exact original source renders geometry and one native image; no renderer error | Original FAIL preserved; fixture assumption disproved |
| Deterministic rejected source | No accepted local invalid case | Exact `flowchart TD\n  A -->\n`: ordinary DiagramService error, zero geometry, native `DiagramError0` with actionable message; positive companion renders normally | Focused framework-dependent test PASS; portable GUI NOT RUN |

b01 exact UTF-8 file SHA-256 is
`990c0ec966d38842f5ab666c67c3d7a9d6c40f1a216d8cf450bc8701454fdb42`.
The three Café occurrences start at UTF-16 offsets 2, 384 and 544. Query/value,
focused controls, foreground/focus/caret HWND, UIA selection errors/ranges, UTC
timestamps and loaded Core/WPF 10.0.13 module hashes are retained. UIA exposes no
selection even after the editor regains focus, which weakens a focus-only provider
explanation but does not prove that FindKeyDown was called or native selection absent.
Neither a screenshot nor a caret/status label is used as selection proof.

The pinned Mermaider DLL SHA-256 is
`9982611afdd5f0250ca19ec1474d966ff4f015f97a9bdbc5b532edc2c65c54bb`, identical to
the portable .880 DLL. Direct Mermaider accepts both the original truncated label
and the incomplete arrow. The latter is rejected by the existing **DiagramService
pre-parser arrow guard**, not an invented parser rule. Its exact fixture SHA-256 is
`d0c9cd07c331e3386c26620cd524a8d2a436b43a1037812c6558f53c5d9bdb3d`.
NativeMarkdownRenderer produces the message “An arrow is missing its destination.
Complete the connection and try again.” and no diagram image. Original trusted
fixtures are unchanged. Only newly prepared host `invalid.mmd` uses the proved source.
The new shell check additionally requires exact error text, no geometry, editable
unchanged source and successful valid-diagram reopening; it has not passed locally.

The focused native-control test uses framework-dependent .NET 10.0.12 and is **not
portable GUI acceptance**. `renderer-01` was a preliminary dirty-worktree build;
`renderer-clean-9b10c48` is the subsequent distinct clean committed client result.
These do not change either original failed classification.

## Evidence and next experiment

All paths below are relative to this existing PR #48 worktree:

- `artifacts/native/g8b/11957e294a6f87b9729913b174b919ff23d176ed/f01/`: old hashes,
  host preflight, new logs and focused fixture receipts.
- `artifacts/native/development-host/11957e294a6f87b9729913b174b919ff23d176ed/b01/ui/`:
  `diagnostic-before.json`, `before-action.json`, `after-action.json`,
  `after-close-find.json`, `runtime.json`, failed `completion.json`.
- The adjacent `b02/ui/completion.json` preserves the foreground refusal and
  `normal-close-request.json` identifies the exact normally closed process.
- Both sandbox `complete.json` records independently pass observed host preservation.
- The old `c01` and baseline `s08`, `s14`, `s15` roots remain unchanged.

Further GUI testing stopped at the foreground guard. The next experiment requires
an available unlocked desktop and fresh sandboxes. Use `FindEnterObserved` and
`FindNextObserved` separately, then `FindTypedObserved` if needed. The diagnostic
startup observer exists only in the validation assembly, explicitly enabled in the
one child process's environment. It checks executable/state/harness identity,
requires the prepared synthetic source, writes at most 200 observations to a new
file, and observes WPF routed key events (including handled events) and actual
AvalonEdit selection start/length/text. It neither consumes events nor changes
focus/selection. No machine-wide hook or shipping telemetry is added. This observer
is compiled but has not yet been executed in the portable process.

Compare native state with the independent UIA ranges before/after one action. Only
that evidence can justify a product fix or an observation-path correction. No
product fix is currently justified. Full original shell completion, preview Find,
ordinary editor Enter while Find is visible, and portable invalid/valid reopening
remain unproved. A green new hosted suite cannot override these local outcomes.

The previous 27 entry cases, separate export API, hosted 72 phases and normal/actual
High Contrast suites retain their original source attribution. GUI export dialogues,
installer, physical reader, alternate scale, full visual review and inspector splitter
persistence remain NOT RUN. Final new-head CI results belong to the post-commit
receipt; no old workflow is rerun or relabelled. Beta requires an owner decision
after the mandatory local gates pass; production retains all separate release gates.
