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

