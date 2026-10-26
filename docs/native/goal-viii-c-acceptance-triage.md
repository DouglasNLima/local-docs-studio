# Goal VIII-C — required CI and development-host acceptance triage

PR #48 remains OPEN / DRAFT. No merge or release is authorised. PR #38 remains
paused at `31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`; controlled 250 ms acceptance
remains INCOMPLETE. This change adds evidence, not a product contrast or Find fix.

## Fresh verification and preserved evidence

On 25 October 2026, fetched develop and the existing feature branch and read both
PRs through GitHub. Accepted develop remains
`9b1e73bbf564c53b48149e8c0dcd6fd7acbe308f`, tree
`1fb777317ad6e5c8531eebcb565fefae4227e354`.
Original feature head is `75ab77ed4db31aee1ad0f19974ed3385ea9cc588`.
Its actual Git tree is `28d529b1a2747e5c46216fdc1b62b3d2a3d5a95c`;
the different tree supplied in the task is a transcription error.
Earlier candidate `11957e294a6f87b9729913b174b919ff23d176ed` and its
`ced9a65224b5ae00be2100112aaf27093a745511` tree retain their original attribution.

All eight original feature workflows completed at attempt 1:

| Event / workflow | Run | Conclusion |
| --- | --- | --- |
| push / native | 40145976494 | SUCCESS |
| pull_request / native | 40145980344 | SUCCESS |
| push / web | 40145976488 | SUCCESS |
| pull_request / web | 40145980362 | SUCCESS |
| push / expanded editors | 40145976544 | SUCCESS |
| pull_request / workspace | 40145980363 | SUCCESS |
| pull_request / stock Continuous | 40145980430 | SUCCESS; controlled comparison SKIPPED / INCOMPLETE |
| pull_request / desktop UX | 40145980388 | FAILURE; baseline job 121027546878 |

No original run or attempt was rerun. The existing final-head receipt and the
earlier candidate's selected evidence were read and independently hashed.
99 evidence files are bound by the new ignored `original-evidence-hashes.json`.
Old failed records remain untouched. New investigation evidence is under
`artifacts/native/g8c/75ab77ed4db31aee1ad0f19974ed3385ea9cc588/a01/`.

## What actually failed in the baseline job

The job name does **not** identify the failing application. The original log shows
the historical `47e78d6bff490a9b0c0b5cb2022dab262fe14e5e` normal captures and
Mermaid baseline step succeeded. The current normal shell also succeeded.
The later **current-shell actual High Contrast** invocation failed at
`NativeUxScreenshots.cs:27`, before its capture loop or child application launch.
The assertion evaluated the **current validation process's** ThemeService resources,
not brushes observed inside the old baseline executable.

The immutable historical source is unchanged. The assertion, ThemeService,
NativeTestHighContrast and workflow are identical between the failed source and
the previous successful PR #47 head `84665322addd9d45faecde7d1347233a22d0d38d`.
Both job logs report runner 2.338.0, Windows Server 2025 10.0.26100,
image `windows-2025-vs2026 / 20261021.278.1`, SDK 10.0.402 and runtime 10.0.13.
This does not prove equal transient OS/WPF state, a runner cause or a flake.

| Archived evidence | Failed run | Earlier successful run |
| --- | --- | --- |
| Run / job | 40145980388 / 121027546878 | 40103324125 / 120899947362 |
| Attempt | 1 | 1 |
| Artifact ID | 12580873244 | 12563499263 |
| Artifact name | native-ux-visual-evidence | native-ux-visual-evidence |
| Expiry UTC | 2026-11-08T00:31:06Z | 2026-11-07T14:42:34Z |
| ZIP members | 46 | 90 |
| Actual HC evidence | failure.json, empty Observations; no captures | four captures and screenshots.json |

Published and independently downloaded ZIP SHA-256 values match:

- Failed: `5d10ec9bd658777cd69f8d42dd90f8c388e5d8ac76e24906d29da55fb467739e`.
- Earlier success: `5f79630b4f7a5bd3e374ddd546f3d7fd242623ea3b4f5a41ad0ea0a3f91c1828`.

Each ZIP member also has an independent content hash in `archive/member-hashes.json`.
The old logs do not record the failed RGB values, equality subcondition or ratio.
Historical causation remains UNPROVED.

## Bounded new experiment

The validation harness now writes `high-contrast-pre-assertion.json` after the
existing ThemeService.Apply and resource reads, immediately before the unchanged
equality / 4.5 contrast assertion. It records effective and bound requested colours,
binding status, WPF colours, read-only Win32 GetSysColor values, OS/WPF HC flags,
ratios, loaded theme resources, runtime/image/run identity and independent hashes
of the harness, WPF, current theme assembly and target executable/assembly.

Diagnostics do not prime the WPF cache before Apply, pump the dispatcher, reapply
themes, change settings, alter selection or replace an assertion. The hosted-only
HC guard and restoration remain intact. Geometry, screenshot/pixel checks,
deadlines, fixtures and intentional skips are unchanged.
Current screenshot revision labels now use the actual checked-out head; GitHub's
merge SHA remains separately recorded. The historical comparison SHA is unchanged.

A new clean commit must execute all existing workflows with separate run IDs.
A successful experiment would not retroactively explain or pass the failed run.
If contrast is reproduced, use its actual values to distinguish OS state, cached
values, bindings and rendering before proposing any correction. Do not run actual
HC changes on the personal development computer.

## Local C4 and real-console acceptance

Original local Core remains **FAIL: 2,116/2,117**, zero skips. Raw TRX and stdout
identify `C4SourceAuthoringTests.Explicit_label_clear_takes_precedence_over_alias_fallback_when_later_slots_are_added(variant: "C4Container")`,
154.4101 ms, failing in `C4VisualDraft.Read` at its Node.Match (line 68), during
construction of the short `Container(original_alias)` fixture.
SourceText.Pattern uses the unchanged 100 ms regex timeout.
The old receipt does not establish contemporaneous machine workload or concurrency.
Hosted same-head native Core passed 2,117/2,117. New focused and full local runs
must be sequential, retain raw TRX/timing/runtime/source and preserve the old FAIL.
Neither a new pass nor hosted success proves historical causation.

The process-local Find observer remains validation-only and read-only, with explicit
child environment, sandbox/source/executable/harness checks and an attach-before-input
requirement. Original c01 and b01 exact Find Enter observations remain FAIL / UNPROVED;
b02 was blocked before input. One Next click cannot replace an Enter result.
A fresh native/UIA differential requires the normal active unlocked console and
unchanged guards. Stop this phase at a foreground refusal; no focus-policy bypass.

The exact replacement invalid source remains `flowchart TD\n  A -->\n`,
SHA-256 `d0c9cd07c331e3386c26620cd524a8d2a436b43a1037812c6558f53c5d9bdb3d`.
Its DiagramService arrow guard is proved in the separate API client; Mermaider itself
accepts the syntax. Portable DiagramError0, editable original bytes, zero images and
valid reopening still require actual GUI evidence. The original truncated-label
fixture remains accepted by the renderer and cannot serve as an invalid fixture.

HOST PORTABLE and INTERNAL BETA remain BLOCKED until the focused requirements and
the entire original shell pass on a newly verified final-source Actions package.
HOST INSTALLER remains NOT RUN on this personal host. PUBLIC PRODUCTION remains
NO-GO pending genuine clean Windows, physical reader, historical migration,
signing/trust and formal release authorisation. Local API-client 10.0.12 results
must remain separate from portable Core/WPF 10.0.13.

Final experiment results belong in a post-commit receipt and PR #48's current
acceptance summary, retaining this source-labelled pre-experiment record. No
passing future outcome is claimed here.

## First clean experiment and observer readiness

Diagnostic commit `694b9fad0d4fd46c3ecf6f3e3680da69f4a46005`, tree
`1b96d802939e66c6522bbcecafbe8d575591ec8a`, passed its local zero-warning/error
build. Sequential local Core experiments passed all three focused variants
(5,638 ms command elapsed), then 2,117/2,117 with zero skips (25,814 ms).
Raw TRX, logs, source/workload inventory and .NET 10.0.12 identity are retained in
`artifacts/native/g8c/694b9fad0d4fd46c3ecf6f3e3680da69f4a46005/a01/`.
The historical Core failure and its unproved cause are unchanged.

Fresh `.880` sandbox `development-host/11957e294a6f87b9729913b174b919ff23d176ed/d01`
independently verified the original Actions package and all 528 payload files.
Its `FindEnterObserved` process, PID 72568 / HWND 52696954, loaded the explicitly
process-local observer at 07:24:16 UTC on 25 October, using actual .NET 10.0.13.
At 07:24:20 the observer stopped because the editor had not yet loaded the exact
synthetic source. The external client then observed exact source but timed out
waiting for `probe-attached` at 07:24:42. **No input was sent**; the result is
BLOCKED at observer readiness, not a product Find failure or a foreground result.
The verified process closed normally through its exact owned HWND at 07:25:59.

The observer's one ApplicationIdle callback could run before asynchronous document
loading. The bounded correction listens for the existing editor TextChanged event
and attaches only when text exactly equals the prepared synthetic source. It does
not load, edit, focus or select anything. Pending handlers detach when the window
closes; the external 20-second attach deadline is unchanged. After attachment,
the original stop-on-source-change rule still applies. A new real-process test
must verify this correction; no Enter/Next result is inferred from attachment.

Two pre-launch invocation errors are also retained: an absolute-path guard rejected
the first relative preflight output path; an early RunUi invocation rejected the
absent preparation marker before Prepare finished. Neither created a UI session
or sent input. The actual d01 run began only after successful preparation, with a
separate log. Completed d01 evidence is not reused.

## Real input differential and mandatory local stop

Clean observer correction `bb3a04ae2b95a3444094bde52abe819b732f83a4`, tree
`dca33822167241ac49fff743229e259d96ec155c`, built with zero warnings/errors and
passed static checks. Shipping application/Core source is unchanged.

| Fresh .880 diagnostic session | Outcome |
| --- | --- |
| d02 / FindEnterObserved | FAIL: one Return delivered; native selection and UIA both empty |
| d03 / FindNextObserved | BLOCKED at foreground guard before any input; stop local GUI phase |

d02 attached before input at 07:33:09 UTC, PID 43036 / HWND 41881968, actual DPI
144. Before the action, owner foreground and FindText focus were verified; native
query was exactly `Café`, with source UTF-16 occurrences at 2, 384 and 544.
At 07:33:26 Return reached window and FindText PreviewKeyDown with Handled=false;
handled-events-too bubbling observations then had Handled=true. Native selection
stayed start 0, length 0, selected text empty, caret 0; no selection-change event
occurred. UIA independently reported empty text/start 0/length 0 after the action
and after Close Find. Source SHA remained
`990c0ec966d38842f5ab666c67c3d7a9d6c40f1a216d8cf450bc8701454fdb42`.

This disproves a UIA-only omission for that action. It proves key delivery, not
execution of FindNext. Interception before its ordinary bubbling handler versus
another FindNext-path cause remains unresolved (A/B); no precise WPF class-handler
cause is claimed. Foreground moved away during the later read-only wait; that is
recorded, and no further keyboard/mouse input was sent. Source remained exact and
the process closed normally.

d03 attached at 07:37:06 UTC, PID 85680 / HWND 30677148. The unchanged 20-second
foreground guard refused at 07:37:27 before Ctrl+F, query or Next input (outcome D).
Its exact owned unmodified process closed normally at 07:39:39. No focus-policy
bypass or second attempt occurred. Both completed sandboxes passed observed host
preservation and all 528 unchanged payload-file checks; d01 is also preserved and
normally closed. Probes ran inside actual portable Core/WPF 10.0.13, separately
from the local framework-dependent .NET 10.0.12 validation client.

No shipping Find fix is justified by a complete differential yet. Typed query,
preview Find, ordinary editor Enter, portable invalid/error/valid reopening and
the entire original shell are NOT RUN after the mandatory stop. HOST PORTABLE and
INTERNAL BETA remain BLOCKED regardless of CI; HOST INSTALLER remains NOT RUN;
PUBLIC PRODUCTION remains NO-GO.

Next operator-assisted action: provide the normal unlocked console, close any Lens
instance normally, prepare a fresh final-source verified sandbox and ensure its
visible window can receive foreground focus before the guarded single-action
test. Do not alter Windows focus policy or retry a completed sandbox. Obtain the
separate Next control and any justified before/after Enter evidence before the
invalid/valid GUI test and once-only complete original shell.

## First new hosted contrast outcome

At `694b9fa`, desktop UX run 40172266497 / baseline job 121105428009, attempt 1,
completed successfully, including the immutable historical comparison and current
actual High Contrast capture. OS and WPF HC=true, bindings active, pressed text
`#FF263B50`, background `#FF8EE3F0`, ratio **7.893273424942036**.
Requested/effective resources, WPF SystemColors and Win32 GetSysColor agree;
the original foreground equality passes. Image remains 20261021.278.1 and runtime
.NET 10.0.13. Screenshot labels correctly identify the checked-out feature head,
with GitHub's merge SHA recorded separately.

Artifact 12590274547 expires 2026-11-08T07:29:48Z. Published and downloaded ZIP
SHA-256 both equal
`da870edfd75d8ce8acf2e6bd7989b3554859d4618919053d167cfd96ddb5f24c`.
Independent member hashes and diagnostic JSON are retained in the `694b9fa/a01`
root. All original thresholds, equality, geometry/pixel rules, deadlines and skips
are unchanged. The original failure's cause remains UNPROVED. Final-source CI and
package evidence require their own post-commit receipt.
