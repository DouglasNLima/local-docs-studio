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
