# Goal #56 — Save/UI Automation readiness correction

Fixed before hosted execution, 31 October 2026. Refs #56 and Draft PR #69.
No merge or release authority. The original incident remains
`ROOT_CAUSE_INCONCLUSIVE`; the demonstrated synthetic overlap does not establish
the state of the original failed SetValue call.

## Immutable sources and bounded protocol

- Baseline: `86d60322f6f1f1dec06974bf5f9e1738a3bb80c7`, tree
  `552158f43ebf2501c684d210dd9ebae80a3e6f8f` (freshly fetched develop).
- Candidate: `c85a51118ac0f175cb21c79c3f7503ee1bc8ecd0`, tree
  `9f578b8bc6ceb99dd58e5ab195f0170e8c3232db` (unchanged PR #69).
- Historical diagnostic: `0f164161e9a328b1a862838000825be3c8c5b331`, tree
  `1b7dadc9d3c88fe8d4e534878ebf3f94420defaa`; local inventory correction
  `20ed907e22323d5a9ba13d2deedbd28c35f614f6`, tree
  `0787a8921209e0ef7dc4aa5d9c63370453c831b8`.
- Separately verified inventory/control helper source:
  `b7b4774a05428e279f1c5b96c957fa4b5cd020ee`; its workflow runs only synthetic
  inventory files. Historical run `41091786808` remains FAILURE.

Initial comparison: one workflow, two fresh windows-latest jobs, no blind
reruns. Pin SDK 10.0.403 and capture actual image, OS, CPU, SDK/runtime, source
commit/tree, harness commit/tree, patch and helper hashes. Initially copy the same
two validation files into clean copies of both sources; the corrective amendment
below adds a third file. No product change in ordinary
Creation runs. Run sessions and actual High Contrast once per source; candidate
also runs Light and Dark once. Build separate diagnostic copies for released and
never-completing controls, one of each per source, with actual High Contrast.
Run normal protected-PR CI separately and retain all failures.

## Readiness contract

The old wait established disk text only. Real Save awaits preferences and session
persistence after writing that text; IsSaving keeps the actual editor read-only.
This sequence exists on both sources. All three Creation Save paths now retain
the original window/editor/selected tab identities before Save and wait for:

1. Exact expected UTF-8 fixture bytes on disk and exact editor source.
2. Original PID, HWND and UIA RuntimeIds; editor ancestry under the same window;
   original selected tab and its full source path via supported UIA HelpText.
3. Enabled editor, available ValuePattern and actual IsReadOnly false.
4. SaveDocument enabled again and exact successful Saved status. The generated
   async SaveCommand disables the button until its complete task has returned;
   status alone precedes final persistence and is insufficient.

These fixtures must differ from disk before Save, witnessed at capture. This
prevents stale status/bytes satisfying readiness before Save starts. The helper
deliberately rejects idempotent reuse without a separate generation observation.
The original 20-second bounded Wait and shared clock remain; provider calls are
capped by the remaining budget. Observations repeat, mutations do not. Missing
or unsupported required state fails safely. No focus/activation, new sleep,
editor replacement or application API is introduced by readiness. Every original
SetValue site still forwards exactly once and is counted. Per-Save receipts retain
first observed matching disk state, final postconditions, deadline and failures.
Sequential UIA observations are not an atomic snapshot or a global-idleness proof.

## Corrective verification amendment — fixed before its hosted execution

Initial harness `a504eebe3dd6144bbc69929fc12a5c1d2afe62d2`, tree
`0a9592ac51d8b9ad1c70db0e3f21cf2c1f76ba85`, ran once in
[41179427126](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41179427126).
It remains FAILURE. Baseline passed all eight stages; candidate passed ordinary
High Contrast/Light/Dark and the released control, but its never-completing control
failed with ArgumentException from UIA TransactionTimeout at 19,995.0805 ms.
The client attempted to set a provider timeout below the supported range near
the unchanged deadline. This is an evidenced harness defect requiring correction,
not a product fault or a reason to rerun unchanged source.

The corrected helper uses whole-millisecond provider limits of at least 50 ms.
If less than 50 ms remains it makes no provider request, records an incomplete
observation and returns false to the existing shared Wait. That Wait expires at
its original 20 seconds. No UIA exception is caught or relabelled. Six pure boundary
checks cover negative/zero, 49.999, 50, 50.999 and 3001 ms; the existing 1,481
session checks remain, giving 1,487 total. The session test file is the third
equivalent validation overlay; product source remains unchanged.

Exactly one new corrected comparison is justified by this concrete code defect:
the same two sources and stage counts, one attempt each, no repetition matrix or
blind rerun. Retain both original archives, failed negative receipt and successful
stage evidence. New successes cannot relabel the first workflow. Normal final-head
PR CI runs separately after the correction.

Required OSS validation also exposed omitted source bookkeeping. Regenerate only
the rights ledger (new paths remain BLOCKED) and add exact historical/current
digest pairs for the two authorised, independently reviewed validation files in
the existing integrity validator. Keep frozen historical evidence, the 1,081-path
grant allowlist and every other notice/trust/performance invariant unchanged.
Retain the initial failed OSS workflow and report this maintenance explicitly.

## Deterministic controls

Use only disposable diagnostic copies, reusing the retained real post-disk
preferences persistence barrier and native state probe. The barrier's diagnostic
safety bound is 30 seconds so it remains held beyond the unchanged 20-second
acceptance deadline. It never enters normal/production builds.

- Held: exact bytes, native IsSaving true, actual UIA read-only true, readiness
  false, zero follow-up SetValue calls. No invalid mutation is attempted.
- Released: release explicitly; the same readiness helper succeeds, native
  IsSaving/read-only clear, remaining session persistence retains source/workspace,
  then exactly one UIA SetValue succeeds on the same native editor/document.
  Verify both client and native provider counts and unchanged saved bytes.
- Never: retain the barrier through readiness timeout. Preserve the failed
  readiness receipt and screenshot, native IsSaving/read-only, and zero follow-up
  calls. The control passes only by proving the underlying readiness validation
  FAILED. Release during cleanup and kill only the disposable application.

No functional Creation assertions, families, variants or rejection fixtures are
removed. Ordinary, reset and standalone Save paths share the contract. Other
visual-authoring Save paths were inspected: those that close/relaunch before
later SetValue do not reuse a still-saving editor and are outside this correction.

## Attribution and release limits

Preserve `SAVE_READONLY_OVERLAP_CONFIRMED`, `SYNTHETIC_UIA_REJECTION_REPRODUCED`
and `ORIGINAL_FAILURE_CAUSALLY_ATTRIBUTED_NOT_ESTABLISHED`. New passes can verify
the harness correction, not retrospectively identify the original exception.
PR #69 privacy acceptance remains historical evidence tied to its unchanged HEAD;
this work cannot certify the entire product. A later harness integration requires
PR #69 to follow the normal protected update and exact-source revalidation flow.

Keep `NATIVE_CONTINUOUS_250MS_FAIL`, historical arrangement-responsiveness failure,
`SIGNPATH_ELIGIBILITY_UNCONFIRMED`, unresolved third-party distribution obligations
and `NATIVE_STABLE_NO_GO`. No performance fixture, 250 ms threshold, rendering
deadline, product trust, signing selector, privacy implementation, ruleset, release
workflow, public release or tag changes. Integration recommendation is separate
from owner approval of a final exact source and check results.
