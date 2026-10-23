# Packet, TreeView, Treemap and Venn native authoring

Expansion VI implements four bounded slices alongside the previous twenty native editor families. Final-source hosted acceptance and package results belong to the [engineering receipt](expansion-vi-engineering-receipt.md); implementation and offscreen compatibility alone do not certify installed authoring. The renderer remains Mermaider **0.15.1**, upstream `71b64845570d633a94d29e85579c0a02786fece8`, on official .NET 10/WPF. This does not imply comprehensive Mermaid grammar support, newer Mermaid compatibility or an unpinned browser rendering path.

Open **Visual Edit** from a document preview, open a standalone `.mmd`/`.mermaid` document, or use **Insert → Packet / TreeView / Treemap / Venn visually…**. The catalogue contains source templates and snippets for all four. Actual canonical SVG supplies native shapes; no replacement grids, tree layouts, tile packers or circle-overlap solvers are used.

## Shared ownership and exact source

The shared shell retains an adjustable source pane, persistent independently scrolling inspector, complete keyboard occurrence list, Locate source, original-source inspection, bounded private Undo/Redo and owner Apply/Cancel. Untouched Unicode, LF/CRLF, comments, quoting, whitespace, numeric spelling, ignored lines and neighbouring Markdown fences stay exact. Field edits patch token spans. Structural operations move complete proved source/comment groups; ambiguous blank/comment attachment protects an operation. Identities travel with source groups and private snapshots. Manual source validation retains unchanged prefix/suffix occurrences and assigns new identities to changed/ambiguous rows without matching repeated labels.

Invalid pending properties or newly malformed/unproved source remain visible with an actionable diagnostic, preserve the last valid private draft and disable Apply. Existing protected source remains inspectable without being discarded or silently repaired. Apply reparses and natively renders the exact private draft before the existing owner/source/version guard commits one owner Undo action. Cancel, stale-owner rejection, Save/restart/reopen and standalone/Insert routes keep their existing ownership contract.

## Packet

Headers are `packet` and `packet-beta`. The last effective header/body `title` is edited; earlier titles remain exact. Fields retain their original absolute inclusive range (`0-15`), single bit (`16`) or relative count (`+8`). Labels are literal quoted text; embedded double quotes, markup and control characters have no proved new encoding. Existing skipped/malformed fields stay source-only.

The inspector shows source order and effective inclusive start/end. The native renderer splits fields across 32-bit rows. All actual segments of one unambiguous field form one occurrence target. Overlapping/duplicate ranges have keyboard/source access, with no guessed pointer identity. Gaps and overlaps are reported; fields are never packed or renumbered.

Only an **accepted** field advances the parser cursor. A range/count edit, removal or move reparses every retained field and rejects any change to another field's accepted/skipped status or effective range. Complete downstream cursor-impact authoring is therefore source-only in this slice; the editor never silently turns `+count` into absolute syntax. New integer tokens must be complete and bounded to bits **0..4095 inclusive**, with positive relative counts, no overflow and no inverted ranges. New fields are entered explicitly; source spelling and existing numeric lexemes remain unchanged when untouched.

## TreeView

Headers are `treeView` and `treeView-beta`. The **first nonblank body line** chooses indentation or box-drawing mode. Later glyphs cannot change that decision. In indentation mode each TAB expands to four literal spaces before parent membership is computed. Box mode uses the first branch character's column after tab expansion. Modes, glyphs and original whitespace are preserved.

The bounded field slice covers literal and proved quoted labels, the directory trailing slash, optional `## description`, `:::class` and default/hidden icon states. Annotation edits are reparsed to prove the intended label, slash, description, class and icon values. Ambiguous removal-order substrings, malformed quotes/prefixes and mixed syntax stay source-only. Only the native `highlight` class promises its existing visual treatment; other parser-compatible class names retain source metadata.

Default file/folder glyphs are native vectors. `icon()` and `icon(none)` hide the icon, distinct from an omitted/default icon. Explicit icon aliases produce SVG image elements, even for registered or unknown aliases, and remain subject to the existing rejection policy. No remote artwork or extra icon pack is offered. Actual glyph/highlight geometry supplies pointer targets; hidden icons and indistinguishable labels retain full keyboard access.

Complete simple indentation subtrees support add/remove/sibling order, parent moves and safe indent/deindent with complete membership proof. Tab re-encoding, uncertain trivia attachment and unsupported structures remain protected. **Box-drawing structural moves are source-only** until independent prefix-glyph re-encoding proof exists; basic proved field edits remain available without changing prefixes.

## Treemap

The header is `treemap-beta`. Editable grammar is a quoted section (`"Section"`) or a quoted leaf and non-negative decimal (`"Leaf": 030.50`). Raw trimmed-label fallback forms remain source-only. **Each space and TAB counts as one indentation character**, independently of TreeView's rules.

The inspector separates an explicit leaf token from read-only `ComputedValue`. Parent totals remain derived; the editor never writes them into source. Label/value edits retain untouched decimal spelling and exact source occurrences, including duplicates. New decimal tokens allow at most 15 significant digits and 15 decimal places, without signs, exponents, incomplete forms, overflow or non-finite values. Existing lexemes remain exact through unrelated field edits.

Simple complete quoted subtrees support add/remove/order and section membership moves after all retained parent relationships are proved. Mixed/tab indentation and ambiguous comments/blank lines protect structural re-encoding. Explicit-value leaves cannot be chosen as new derived parents. Positive values retain the native squarified geometry. Zero-value roots/leaves and tiles omitted for lack of space have **no invented tile** and remain in the keyboard list. Duplicate labels never grant guessed rectangle identity. No freeform area/position editing is offered.

## Venn

The header is `venn-beta`. Safe unique set IDs, separately optional display labels, existing decimal size tokens and simple union references are editable. Safe IDs use ASCII letters/digits, underscore and hyphen, beginning with a letter or underscore. Proved existing quotes around safe IDs/references remain exact. Arbitrary quoted comma-containing IDs, escapes, missing/duplicate IDs, repeated membership and unresolved references remain protected.

A unique rename patches every proved union reference atomically, retaining quoting and spacing. An absent display label defaults to the ID. Choose **Make set label explicit** to preserve the old display text before renaming, or explicitly choose **rename default display with ID**. No rename silently changes that effective meaning. Connected set removal rejects remaining unions unless the explicit complete atomic-removal command is chosen. Simple sets/unions can be added, removed and reordered; no implicit sets are created. Absent union label/size annotations can be added through exact source; existing proved tokens are editable in the inspector.

**Sizes are stored source metadata.** The pinned renderer uses radius 130 and arrangement based on set count, ignoring set/union sizes for geometry. Size edits preserve exact tokens but do not resize circles or compute proportional overlaps. Unions chiefly emit labels, with no separately bounded intersection region. Every union stays in the keyboard list, including absent/unrendered labels; no union hit region is invented. Pointer selection uses actual set circles and rejects overlapping targets in favour of explicit keyboard selection.

## Resource and acceptance boundaries

The existing limits remain: 100,000 renderer source characters, 1,500 editor lines, 500 elements, a three-second render deadline, existing bounded private history, upstream 64-level hierarchy recursion and PNG 16,000-pixel/64-million-pixel budgets. Narrow preflight closes upstream missing element checks, counts Packet split segments and bounds Treemap depth/finite aggregation before recursive calculations. Unsafe source remains available with a diagnostic. SVG sanitisation, remote-resource blocking, package checks, CSP, stock Continuous and installer defaults are unchanged.

Hosted appearance acceptance retains all 30 previous runs and adds eight new normal/actual Windows High Contrast runs at 880×600 and 1280×800. Installed acceptance requires 24 families × install/upgrade/reinstall = 72 independently recorded phases on disposable hosted Windows, with optional associations excluded. A source-identical version-pair upgrade is **SYNTHETIC**, not historical migration certification. Higher-DPI interactive, physical screen-reader and SDK-free clean-Windows certification remain unavailable unless separately demonstrated. Actual hosted screenshots and exact source labels are retained with the receipt; offscreen compatibility images do not replace them.
