# Independent tooling review — Goal #77

8 November 2026 · Technical subagent review, not human owner approval.

Scope: R01/R02 validation tooling beginning at
`45eccf7b3bd4c266b3a3223cbfe007248f03f39d`, the generated exact-source harnesses,
offline negative controls and [execution inventory](../../qualification/goal77-execution.json).
The delivery receipt binds the resulting commit/tree and check identities.

The reviewer inspected the original #76 sources and implementation independently,
ran the 40-test offline suite, reran the affected full-envelope negative control,
and independently passed the two harmless console-child Windows job controls.
It did not run an application window, change HC, dispatch a campaign or certify
physical behaviour. The implementation agent also compiled both pinned products
and all three harnesses and performed pure metadata/contrast checks.

| Finding | Resolution verified |
|---|---|
| Job assignment could fail before the child joined the job | Direct-child termination fallback, finally-close and negative control. |
| Mutable worktree inputs could be compiled under a pinned source label | Isolated Git snapshots; checks before/after build; separate product/tooling revisions and compiled metadata/hash validation. |
| Hidden secondary reader could pass before comparison closure | Only `closed-comparison` permits that exception; explicit negative control. |
| Trace-off captures lacked independent clock validation | Finite, non-negative monotonic timestamps and the original 30-second bound apply independently of tracing. |
| Stock/preflight notification coverage was absent | Passive Win32-only native observer, no WPF prewarming or synthetic theme messages; original WPF first-read sequence stays verbatim. |
| Missing result was manufactured into an assertion failure | Require a result or retained original failure; absence alone is INVALID/INCONCLUSIVE. |
| Unknown job cleanup could escape the success predicate | `JOB_TREE_UNVERIFIED` stops collection and prevents success; actual job-tree checks and mocked-close-false control. |

The original R01 journey/capture classes remain byte-identical. The generated
validation-only Program overlay, hooks, direct-entry guards and failure-frame scope
are independently inspectable. R02 observes the actual template Surface background
and Button/CommandIcon foreground while pressed, with distinct process identities.

Final reviewer verdict: **no outstanding findings from the focused review; ready
for preparation-only delivery**. Exact execution configuration and separate owner
authorisation remain absent. Live foreground/HC behaviour, appearance restoration,
visible pixels, supported runtime/hardware certification and sampling remain
unqualified. No historical failure or risk is closed. **PR #76 remains NO-GO.**
