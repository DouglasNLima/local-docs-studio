# Native editor expansion: Gantt, Mind map and Pie

Implementation branch starts at verified `develop` `1f0c6a4`. The three source
adapters use Mermaider **0.15.1**, the ordinary native SVG conversion, shared
five-editor resources and the existing document/version Apply session. PR #38
remains paused and is not a dependency. Acceptance and packaging are in progress;
the family commits alone do not certify delivery.

| Family | First authoring slice | Preserved/protected source |
| --- | --- | --- |
| Gantt | Title, section names, task labels, explicit `yyyy-MM-dd` starts and positive whole-day `Nd` durations; add/remove/move tasks within a section and complete section groups | Existing IDs are retained; new independent tasks need no ID. Dependencies, implicit starts, statuses, milestones, calendars, incompatible date formats, duplicate IDs and unknown directives protect temporal/structural operations. Labels remain locally editable. `YYYY-MM-DD` and equivalent `yyyy-MM-dd` dateFormat directives are compatible. Empty sections remain source-only until populated. |
| Mind map | One root, text, child/sibling insertion, subtree removal, sibling movement, indent/outdent and reparenting | Occurrence identities handle duplicate labels. Recognised shape delimiters and ID prefixes remain unchanged. Cycles, root deletion/reparenting, ambiguous indentation and unproved icon/class/style attachment are protected locally. The pinned renderer treats unsupported metadata as literal nodes; the editor does not invent icon support. |
| Pie | Title, category labels, invariant decimal values, add/remove/move and actual canonical slice geometry | Untouched numeric lexemes retain precision. Duplicate labels remain separate occurrences. Zero rows stay in source and have no rendered slice. Empty/all-zero/non-finite-total drafts cannot Apply. Label line breaks are safely encoded as `\n`, preserving unchanged escape/`<br/>` spelling. The pinned parser cannot encode embedded double quotes; rejected pending input remains available for correction. showData, comments and unknown directives remain unchanged. |

All source lines retain original terminators and unrelated bytes. Token updates
replace owned spans. Structural operations carry complete source groups: contiguous
immediately preceding same-indent `%%` comments attach to the following row; blank
lines end attachment. Ambiguous trailing comments/directives protect movement.
Unterminated source groups cannot move if that would change their separators.

Canonical correspondence uses actual SVG occurrence order and checked metadata,
not label lookup. Mind map uses the pinned renderer's root-first, branch-post-order
traversal and depth metadata. Pie selection uses real wedge paths/circles. The
semantic navigation list remains a complete accessible authoring route.

Private source/selection snapshots are bounded by the existing 100-step/eight-million
character convention. Every accepted draft reparses and renders before commitment;
native conversion is validated before Apply. Owner Apply keeps existing fence and
version checks, one document Undo step, Cancel and conflict behaviour. No session
draft is written to the owner before Apply.

Focused Core coverage currently contains 19 Pie, 16 Mind map and 23 Gantt cases.
The independent native UIA acceptance route is `--source-editor-ui` with family
`Gantt`, `Mindmap` or `Pie`; actual display/High Contrast mutation runs only on
authorised disposable GitHub-hosted Windows runners. Final-source counts, captures,
installed acceptance, provenance and preview links belong in the delivery receipt.
