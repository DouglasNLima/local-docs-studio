# Eight-editor engineering receipt

[Draft PR #40](https://github.com/DouglasNLima/local-docs-studio/pull/40) adds
Gantt, Mind map and Pie to the ordinary Flowchart, Sequence, State, ER and Class
editors. It starts from verified `develop` `1f0c6a416f421dcbacb084ffd0931ed22bc56ea8`.
The PR delivery record owns the final clean source SHA, CI results, actual counts,
preview version, installer/portable hashes, provenance and installed lifecycle.
Package `version.json` and compiled informational versions must agree with that
SHA. A source-labelled check below does not certify a later revision by itself.

The implementation keeps .NET 10/WPF, Mermaider 0.15.1, trusted native SVG conversion
and stock Continuous. It changes no renderer/runtime dependency or browser asset.
PR #38 remains open/draft/PAUSED at `31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`.
The original Continuous 250 ms goal and `.749` incident remain incomplete/unresolved;
this work does not repeat their controlled investigation or change acceptance bounds.

| Milestone | Reviewable commits / evidence |
| --- | --- |
| Lossless source occurrences, bounded private history and shared native workspace | `241d5c7`, `3a049c4`; existing owner Apply session and five-editor resources reused |
| Pie first slice | `efb0f7c`; safe line-break encoding/underflow guard `04f06e8`; canonical canvas pointer routing `cef5ff3` |
| Mind map first slice | `2191625`; root/subtree/indentation/shape-ID preservation; protected initialisation metadata `f8e8263` |
| Gantt first slice | `5b09d98`; conservative structural scheduling guards, safe independent suffix date edits and compatible date formats `f8e8263` |
| Combined authoring / packaging infrastructure | `b11f3ae`, `1deb39f`; original five installed suites retained; new three use the established isolated lifecycle |
| Final-source verification and new unsigned preview | Exact results and artefact links in the PR delivery record |

The [supported/protected matrix](gantt-mindmap-pie-editors.md) documents the bounded
syntax contract. Source spans/complete source groups retain comments, Unicode,
IDs, recognised delimiters, untouched decimal lexemes, directives and line endings.
Private drafts never write the owner before Apply. Invalid intermediate fields,
cancelled/stale validation and owner conflicts retain the draft for correction.
Apply uses the existing fence/tab/version checks and one owner Undo operation.

At `f8e8263`, local Core passes **1,713** cases; the ordinary native corpus/export
route passes **134 cases with zero failures**. Focused new source coverage comprises
19 Pie, 16 Mind map, 26 Gantt and four cross-family protection/concurrency cases,
plus the Help topic regression. Native fidelity compares complete ordinary-preview
and editor pixels without tolerance and verifies actual canonical occurrence geometry,
compiled selection resources and owner sessions.

At `cef5ff3`, [six disposable Windows authoring jobs](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39368154592)
pass normal/actual High Contrast. Two independent UIA connections inspect selection;
real fields, pointer selection, structural commands, private Undo/Redo, Apply/Cancel,
owner Undo, Save/restart/reopen, stale-owner rejection, mixed protected source and
off-screen keyboard navigation are exercised at 880×600 and 1280×800.
[Inspect the compact source-labelled results](evidence/expanded-editors/cef5ff3-authoring-summary.json).

Meaningful authored states, captured from that revision:

| Family | Light | Dark | Actual High Contrast |
| --- | --- | --- | --- |
| Gantt | [Capture](evidence/expanded-editors/cef5ff3-gantt-light.png) | [Capture](evidence/expanded-editors/cef5ff3-gantt-dark.png) | [Capture](evidence/expanded-editors/cef5ff3-gantt-high-contrast.png) |
| Mind map | [Capture](evidence/expanded-editors/cef5ff3-mindmap-light.png) | [Capture](evidence/expanded-editors/cef5ff3-mindmap-dark.png) | [Capture](evidence/expanded-editors/cef5ff3-mindmap-high-contrast.png) |
| Pie | [Capture](evidence/expanded-editors/cef5ff3-pie-light.png) | [Capture](evidence/expanded-editors/cef5ff3-pie-dark.png) | [Capture](evidence/expanded-editors/cef5ff3-pie-high-contrast.png) |

Earlier failures remain failures: initial Gantt/Mind map hit controls used an
incompatible Button style; `b11f3ae` corrected this. Screenshot review then exposed
missing selection outlines, fixed in `ff9d500`. The real Pie pointer check failed at
`04f06e8`/`f8e8263` despite successful list authoring and windowless geometry checks;
[retained failure evidence](evidence/expanded-editors/f8e8263-pie-pointer-failure.json)
led to canonical canvas hit routing in `cef5ff3`. Help's exact topic-count checks
were updated from 15 to 16 after adding the new guide topic; assertions were retained.

The final preview uses the ordinary self-contained Windows x64 installer/portable
path. Its two preview versions form a **source-identical synthetic upgrade**;
historical upgrade jobs remain branch-conditioned skips. Independent download
inspection executes no installer on the ordinary desktop. Package and actual OS
appearance operations run only on authorised disposable hosted Windows. Preview
`.812` and earlier artefacts are preserved. No merge, release, tag movement,
production signing, trusted updater publication or ordinary desktop installation
is part of this delivery. SDK-free clean Windows, higher-DPI and physical screen
reader certification are not claimed.

The inherited installer wizard/lifecycle originally enabled optional Open with
registrations on disposable runners. That exceeded this goal's association boundary;
the candidate run was stopped. Final acceptance explicitly excludes that task and
asserts no native ProgID/OpenWith entries, while retaining the existing optional-task
checks on other branches. The ordinary desktop was never modified.
