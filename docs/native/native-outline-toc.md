# Advanced Outline and Copy Table of Contents

This isolated native feature starts from accepted develop
`50bc2354e49662c9831338310d23c91cfe7a7b8a`, complete tree
`7685cd6939275d3bef442b3ff1df579616a348aa`. Historical Draft PR #76 at
`7b1334b09a02ad2fbfd4a0234798e6c387d3e634` is a reference, not an integration source.

## Using the Outline

The Outline is a native TreeView. Each H1–H6 heading has its actual parent and
children, a level label and a parser-owned source position. Arrow keys traverse
visible headings; Left collapses a section or moves to its parent; Right expands
or enters a section. Home/End select the first/last visible heading. Enter goes
to the selected heading's source while leaving focus in the Outline. Selection
uses the existing source navigation and current-preview mapping services.

Expand all and Collapse all remain available. The current source heading is
highlighted. Filtering reveals matches together with their ancestor path; clearing
the filter restores saved expansion. Expansion changes made while filtering are
temporary. Each open document has independent session-only collapsed state.
Text anchors retain that state when source moves or duplicate parser identifiers
are renumbered. A removed, renamed or level-changed heading discards its obsolete
state; reopening a closed document starts expanded.

No-document, empty, no-results, updating and failed-update messages are distinct.
Stale headings are disabled immediately. A native TreeView provides SelectionItem
and ExpandCollapse automation semantics; heading names include their H1–H6 level.
The toolbar actions are keyboard focusable and use existing command icons and
dynamic theme resources. The supported main-window minimum remains 860 pixels.

## Copy TOC

Use **Copy Table of Contents** in the Outline toolbar. The native dialog previews
the exact Markdown that Copy will place on the clipboard. Defaults are H1–H6,
bullets, normalised hierarchy and no wrapper. Options include a heading range,
bullets, numbered lists, plain text and a collapsible `<details>` wrapper.
Invalid ranges clear the preview and disable Copy. Empty documents have a useful
message. Escape/Close dismisses the modal and preserves caller focus.

Normalisation indents according to the selected headings' parent relationships,
closing gaps from skipped levels. Without normalisation, indentation reflects
the original H1–H6 levels. Numbered lists use `1.` markers with three-space
nesting; bullet/plain styles use two-space nesting. Plain text contains labels
without links. Non-plain labels escape Markdown punctuation; parentheses use
entities to avoid the supported maths delimiter. Fragments use URL encoding of
the exact parser-generated anchor. No independent slugger is used.

## Ownership and limits

The action captures an immutable snapshot of the current unsaved buffer. Cached
headings are used only for the matching document, source version and parser mode.
Otherwise parsing runs off the dispatcher. A shared worker gate prevents repeated
close/reopen operations from overlapping parser calls. Requests are cancellable;
Markdig parsing itself is synchronous, with cancellation checked before and after.
Options generation and result publication also check cancellation and ownership.
Changed/replaced documents invalidate the dialog; Copy rechecks ownership before
writing. No linked assets, saved source or export pipeline is read by this action.

The existing 4 MiB source-character contract applies. Generated output is bounded
to four times that character limit, accommodating escaped labels and encoded
fragments. Oversized output has a clear error and requires a smaller level range.
Copy does not edit, save or dirty the buffer, change its selection, or rerender
the preview. Clipboard failure remains recoverable through an explicit message.

The [validation receipt](native-outline-toc-validation.md) distinguishes local
results from unavailable High Contrast and unauthorised hosted qualification.
The [provenance proposal](../open-source/evidence/outline-toc/proposal.md) is
inactive. All prior approvals, exclusions, historical failures and release holds
remain in force. Slash commands and document comparison are outside this slice.
