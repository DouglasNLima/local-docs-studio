# Kanban, Requirement and GitGraph native editors

Expansion IV adds three bounded source-preserving editors to the fourteen existing
families. See the [seventeen-family matrix](diagram-compatibility.md) and
[engineering receipt](expansion-iv-engineering-receipt.md) for source-labelled
validation and certification status. The generic documentation studio and browser
product retain their existing architecture.

All three use the shared source/inspector window, stable line occurrence keys,
bounded private snapshots, actual native SVG geometry and guarded owner Apply
sessions. Nothing regenerates complete diagrams from a reduced model. Comments,
Unicode, whitespace, line endings, configuration and untouched tokens remain
portable source. Same-indent immediately preceding comments attach to an occurrence;
ambiguous trailing/intervening comments protect structural operations. The complete
keyboard occurrence list includes source without a visible mark.

| Family | Editable slice | Protected or outside this slice |
|---|---|---|
| Kanban | Effective body title; ordered columns/cards; explicit IDs/titles; coupled bare/bracket forms; assigned/ticket/priority; destination card movement; complete column groups | Mixed first-column indentation, ambiguous comment attachment, partial metadata, unproved escaping and implicit card deletion. Deeper indentation is flat cards, never subtasks. |
| Requirement | Body title, TB/BT/LR/RL; six kinds; display ID/text/risk/verification; element type/docRef; seven relation types; unique symbolic rename; simple block ordering | Duplicate/hidden names, unclosed/nested-looking bodies, unresolved endpoints, parallel/self/cyclic topology and unknown reference-bearing source. Safe independent fields remain editable where native correspondence holds. |
| GitGraph | Ordered command list; anonymous/explicit-ID commits; tags; NORMAL/HIGHLIGHT/REVERSE; append branch, checkout/switch and merges; independent final-run commit moves/removal | Ignored orientation/order, repeated branches, missing/empty heads, unknown commands, cherry-pick ancestry, branch rename, implicit main rename and arbitrary history rewriting. |

## Pinned grammar and precedence

Mermaider remains **0.15.1**, package source
[`71b64845570d633a94d29e85579c0a02786fece8`](https://github.com/nullean/mermaider/tree/71b64845570d633a94d29e85579c0a02786fece8).
The official .NET 10/WPF runtime and ordinary renderer are unchanged. No private
engine API, browser fallback or alternate layout is used.

Kanban counts each tab/space as one indentation character. Its first content row
sets the permanent column threshold; later rows at or below it are columns and
deeper rows are cards. Global `title` lines are recognised at any indentation.
`id[Title]` distinguishes ID/title; `[Title]` and bare text couple both. Unmatched
ID/bracket forms fall back to bare text. Known metadata keys are case-insensitive,
with the last recognised key effective. Unknown keys, repetitions, quotes, order
and spacing remain exact. Clearing writes an empty final value rather than revealing
an earlier key. Only Very high, High, Medium, Low and Very low have native priority
presentation. New delimiter-bearing labels/metadata are rejected when escaping is
unproved. A column with cards needs explicit complete removal or prior card moves.

Requirement relationships resolve **symbolic names**, never the `id` display
property. Forward and reverse arrow spellings retain their endpoint semantics.
Requirements are painted before elements; the first box per name wins. Unknown
endpoints can disappear from SVG. Repeated case-insensitive properties are last-wins;
blanking an effective field writes `""` and preserves older occurrences. Risk accepts
low/medium/high; verification accepts analysis/demonstration/inspection/test. Existing
unsupported values remain exact unless explicitly changed. General escaped strings
and multiline property authoring are unproved. Native route endpoints are checked
against actual SVG boxes before topology selection; no new graph layout is computed.

GitGraph is a stateful diagram stream. Branch creation changes current branch;
checkout changes context. Anonymous numbering counts commits, merges and cherry-pick
commands, while anonymous merges have no ID label. Native parent paths and HEAD
must agree with proved branch/head references. Adding a commit can alter later
generated labels; source identities travel independently and no IDs are minted in
untouched commands. The renderer ignores parsed TB/BT orientation and branch order.
Unknown checkout can use lane zero; missing merge heads can omit a parent. Repeated
branches act like checkout, and cherry-pick does not prove ancestry. Those forms
remain visible in the list/source and protect stateful edits. New branches use a
conservative simple name grammar; new merges need two distinct non-empty heads.
Double quotes/line breaks in new IDs/tags have no supported escape.

## Ownership, validation and limits

Invalid pending fields remain visible with an explanation and disable Apply while
the last valid private source survives. Apply reparses/renders the exact current
source and uses the established owner/source/version guards. One Apply is one owner
Undo step. Cancel, private Undo/Redo, owner Undo/Redo, Save and reopen remain separate
operations. The existing 100,000-character, 1,500-line, 500-element, three-second
render and bounded-history budgets remain in place.

The compatibility gate is `--workflow-compatibility`; exact canonical drawing,
geometry, ordinary context actions and owner-session checks are
`--workflow-editor-fidelity`. The independent UIA harness adds normal and actual
Windows High Contrast runs for each new family without removing the eighteen
existing shared-source runs or original five-editor UX coverage. Installer and OS
appearance mutation execute only on disposable hosted Windows runners. Higher DPI,
physical screen reader and SDK-free clean Windows certification remain unavailable.

Actual screenshots and final package/run links are added to the receipt only after
their exact committed-source runs complete. Older captures remain historical.


## Inspected Windows captures at cc7c267

These are actual independent UIA captures from source
`cc7c26793249838e4c8d824bccc1661f508d03fe`,
[24/24 hosted appearance runs](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39446459035).
They are labelled intermediate source; later grammar guards do not change presentation.
All six new-family runs include pointer/keyboard selection, small/normal windows,
private and owner histories, conflicts, Apply/Cancel and Save/restart/reopen. Final
source repeats these gates and supplies fresh captures in the PR delivery record.

| Family | Light | Dark | Actual Windows High Contrast, 880x600 |
|---|---|---|---|
| Kanban | [Capture](images/expansion-iv/cc7c267/kanban-light.png) | [Capture](images/expansion-iv/cc7c267/kanban-dark.png) | [Capture](images/expansion-iv/cc7c267/kanban-high-contrast-880x600.png) |
| Requirement | [Capture](images/expansion-iv/cc7c267/requirement-light.png) | [Capture](images/expansion-iv/cc7c267/requirement-dark.png) | [Capture](images/expansion-iv/cc7c267/requirement-high-contrast-880x600.png) |
| GitGraph | [Capture](images/expansion-iv/cc7c267/gitgraph-light.png) | [Capture](images/expansion-iv/cc7c267/gitgraph-dark.png) | [Capture](images/expansion-iv/cc7c267/gitgraph-high-contrast-880x600.png) |

Raw source-labelled UIA summaries are retained in
`evidence/expansion-iv/cc7c267/`. UIA does not certify a physical screen reader.
