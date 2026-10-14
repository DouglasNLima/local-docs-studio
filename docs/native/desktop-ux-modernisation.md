# Native desktop UX modernisation

Work starts at verified revision `c566144529c3c0e4393c1604544201b936e4a6a2`, on `feature/native-desktop-ux-modernisation` in [draft PR 24](https://github.com/DouglasNLima/local-docs-studio/pull/24). The goal remains active: expanded desktop certification, final packages and the completion audit are outstanding. This report supersedes intermediate notes without relabelling failures as passes.

## Implemented experience

Wrapping tabs, a verbose workspace path, cramped outline and hidden formatting commands were the original problems. Shared WPF resources now provide neutral surfaces, high-contrast text, restrained `#FF883E` indicators, consistent spacing, focus/disabled states and original vector command icons. Workspace, outline and search selections use neutral rows with compact indicators. Grouped toolbars reserve native overflow space; menus, keyboard commands, accessible names and tooltips remain available.

The original documentation-page artwork has an SVG source and transparent ICO frames at 16, 24, 32, 48, 64, 128 and 256 pixels. It is assigned to the EXE, main/dialogue windows, About and installer; shortcuts inherit the EXE icon. Package verification compares actual PE images with all authored frame hashes and checks dimensions/transparency. The built EXE passes; final installer inspection awaits packaging.

| Area | Delivered behaviour |
|---|---|
| Panels | Independent toggles, bounded widths, hidden splitters and private persistence |
| Workspace | Concise name/path tooltip, filename/path filter, retained nodes/expansion, expand/collapse/refresh, icons and edit indicators |
| Context actions | Captured clicked-item paths, open/new tab, Explorer, path/Markdown copy, reviewed rename/move and confirmed recycling |
| Tabs | One scrolling row, truncated titles/path tooltips, dirty indicators, close/middle-click, keyboard switching, context actions and persisted pins/order/active tab |
| Outline | Heading indentation, tooltips, filter, current section, source navigation and optional keyboard group expansion |
| Editing | Existing source commands exposed through compact formatting toolbar, six headings, lists/tasks/quotes/code/Mermaid/tables/images/links and grouped Undo/Redo |
| Productivity | Keyboard-searchable Quick Open/Command Palette, fuzzy filename/path matching, shared actions and disabled states |
| Viewing | Direct reading/zoom/search/copy, quick theme switch, restored Full Screen bounds, temporary Focus Mode and contextual busy cancellation |
| Diagrams | Space+drag/middle pan, mouse-centred Ctrl+wheel zoom, bounded offsets, Fit/Actual Size/Reset and keyboard navigation |

Pins require unpinning before normal close; bulk close skips pins and stops after a cancelled dirty prompt. Closing tabs never deletes files. Folder deletion reviews a bounded complete inventory, retains saved documentation and unsaved text in snapshots, stages/rechecks contents and uses the Windows Recycle Bin. Cancellation/failure restores the folder. Root mutation, workspace escapes, reparse traversal and private-state deletion are rejected.

## Shortcuts and focus

| Shortcut | Action |
|---|---|
| Ctrl+P / Ctrl+Shift+P | Quick Open / Command Palette |
| Ctrl+Alt+B / Ctrl+Alt+O | Workspace / outline toggle |
| Ctrl+Tab / Ctrl+Shift+Tab | Next / previous tab |
| F11 / Ctrl+Alt+Z | Full Screen / Focus Mode |
| Escape | Dismiss Find, menus/dialogues, then Full Screen/Focus Mode in context order |
| Diagram Space+drag / middle drag | Pan |
| Diagram Ctrl+wheel / F / Home | Pointer zoom / Fit / Reset |

Established shortcuts remain, including Ctrl+B, Ctrl+I, Ctrl+S and Ctrl+Shift+K. Toolbar formatting retains editor selection; searchable windows restore originating focus. New pane focus cancels pending synchronisation.

## Synchronisation and state

Follow caret, scroll and selection are independent persisted opt-in choices. They operate in Split with both panes visible and retain the existing explicit Automatic/Pages/Continuous choice.

Events coalesce over 60 ms. Document identity/source versions reject stale renders; edits, tab/layout/mode/preferences, pointer input and pane focus cancel pending navigation. Programmatic feedback is suppressed. Selection and scroll reuse the immutable preview without reparsing/rebuilding it.

Parser-bounded UTF-16 literal spans identify repeated occurrences, Unicode, emphasis, code and link labels. Endpoints cross bounded empty formatting gaps only. Reverse selections retain syntax between literal endpoints. Delimiters alone, generated markers, entities/transformed text, images and drawings use a declared semantic block highlight/selection. The preview adorner cannot change export formatting.

Scroll aligns visible editor source positions with native text/block anchors, including the displayed page. It uses logical regions rather than overall percentages and keeps caret/focus unchanged. Unmappable positions use the nearest defensible block.

Layout changes reuse a completed, unchanged preview when no render is pending. Hiding/reopening it retains its selection and avoids reparsing. Source edits and explicit rendered-copy preparation keep the existing invalidation/current-source path; stale or pending results are rebuilt before reuse.

Private `Desktop` settings store visibility, widths and independent sync choices with safe defaults. Existing outline/theme/layout/preview contracts remain. Focus Mode does not overwrite permanent visibility. Private sessions retain pins/order/active tab, caret/scroll and logical preview position; AvalonEdit version movement updates that position after edits. Preference changes merge with current state.

Source bytes, LF/CRLF, Undo/Redo, images, relative links, drafts, snapshots and guarded saves retain existing contracts. UI state stays outside workspaces. The implementation remains .NET 10/WPF/AvalonEdit/Markdig/FlowDocument, without new dependencies, browsers or PR 22's renderer. The separate web edition remains buildless and compatible with GitHub Pages.

## Evidence and outstanding certification

- Baseline: 1,057 Core tests; exact base native [38530939885](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38530939885) passed.
- Expanded local Core: 1,086 passed, none skipped. Release builds have zero warnings/errors. Windowless mapping, workspace/session and overflow checks pass.
- Actual hosted controls and bidirectional repeated-word/Unicode selection in both reading modes passed at `5f15889` in [38560750881](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38560750881). Full native later failed an outdated help expectation, corrected to the actual 13-topic guide.
- Broader `d716e3a` selection checks exposed focus sequencing after successful heading/formatted/soft-break checks. Pane cancellation and confirmed UIA focus correct that failure. Expanded bidirectional cases now pass both modes at `d430d35` in [38569082328](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38569082328).
- That run also passes pins, close-to-right, active survival, dirty-prompt cancellation, actual complete-folder recycling, saved/unsaved snapshots and unrelated source preservation. Earlier folder input landed within a parent's expanded child bounds; the corrected test clicks the header and verifies the confirmation target.
- Hosted baseline/current Light/Dark screenshots cover 860×540, 1280×800, 1440×900 and 1920×1080. Review corrected overflow clipping and broad blue selections. Actual temporary Windows high-contrast capture passes at `d430d35`; review found capture/layout timing and active-tab visibility after resizing, now corrected with stronger assertions. Final visual evidence remains pending. Restored disposable settings do not establish physical DPI/accessibility certification.
- Web [38560750925](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38560750925) and [38560744378](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38560744378) passed. Final native/web runs must pass on the delivery revision.

Pointer-driven independent panel widths and bidirectional/rapid semantic scroll pass both modes at `12e541a` in [38570653085](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38570653085), with unchanged caret/focus and no preview rebuild. That run subsequently fails a caret fixture whose UIA selection did not move AvalonEdit's actual caret; native Find plus a caret key now establishes its exact source line. Windowless hard-break and cross-paragraph literal endpoints also pass in both themes.

Final caret/tab cancellation, expanded hidden-pane/edited-source selection, pointer-anchor zoom, full corpus/export/workflow regression, final visual inspection and installer lifecycle remain under certification. The `d430d35` scroll test failed an unsupported UIA `GetVisibleRanges` request. It now inspects actual heading-range screen geometry instead; [AvalonEdit's provider](https://raw.githubusercontent.com/icsharpcode/AvalonEdit/master/ICSharpCode.AvalonEdit/Editing/TextRangeProvider.cs) supports this observation. Primary/cleanup/diagnostic failures are preserved separately; assertions, timeouts and fixtures are not weakened to obtain passes.

At `8c903a2`, [38572216722](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38572216722) additionally verifies hard-break/cross-paragraph selection in both directions and modes, exact opt-out occurrence retention, hidden-preview preservation, tab cancellation and selection after a valid Unicode edit. It then fails a one-Undo fixture because keyboard automation emitted separate UTF-16 input events. That fixture now uses the existing atomic text-paste command while retaining exact source and one-Undo assertions. A native outline test adds group/filter retention, keyboard expansion, exact source-line navigation and current-caret highlighting without preview reconstruction.

The outline test at `de14715` exposes a real focus defect: collapsing a group replaces its focused item container, so the subsequent Right-arrow command misses the outline. The correction retains outline focus and defers focus to its current replacement row, guarded by document/selection identity and current focus. It does not force preview layout.

At `6640012`, the complete outline test passes before the edited-source selection test exposes a restoration race: focused-pane input could arrive while viewport restoration was suppressing feedback and be discarded. Focused selections/caret changes now supersede that restoration; programmatic changes in the other pane remain excluded by focus. Cancellation invalidates pending navigation in both viewers. A hidden native 4.14 MB editor fixture verifies explicit cancellation leaves viewport, selection and source intact, alongside existing stale/superseded navigation checks.

The atomic paste case also retains its selected boundaries while changing their contents, so no later selection event is required to fire. Installing a new immutable preview now reconciles the focused source's existing selection/caret before restoring an older viewport. High-contrast pressed commands use the system highlight-text colour; normal Light/Dark pressed text/icon contrast passes windowless checks, and the hosted high-contrast capture verifies the actual system pair.

The full native run at `7e4675e` passes the complete expanded selection check, including the atomic Unicode edit/Undo, exact disabled occurrence, hidden preview and tab cancellation. Its follow-caret check then exposes a focus-entry gap after Find: current caret position was correct but no new position event was required. Editor focus now reconciles the current selection/caret. The high-contrast harness's new resource check also required WPF Application initialisation for pack URIs; that validation setup is corrected.

Minimum-width visual review adds temporary proportional panel fitting with a usable central document area. Preferred widths remain untouched and return when the window expands. Combined maximum preferences, hidden panels and wide restoration pass native geometry checks. The overflow indicator now uses larger original circle geometry, with actual drawing/bounds checks in an invisible native test host. Final hosted screenshots and workflow validation of these refinements remain pending.

The focused hosted UX suite passes at `c63ac63` in [38581680252](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38581680252). Downloaded JSON verifies complete outline, panel/tab/folder actions, all selection semantics, independent follow-caret, both scroll directions/rapid latest-region coalescing, tab cancellation and unchanged source. Actual diagram pointer coordinates remain stable (0.33404 → 0.33398), with pan/Fit/Reset and original clipboard pixels retained. Light/Dark and actual high-contrast screenshot matrices pass; inspected minimum-size and high-contrast captures show reclaimed document space, complete active-tab close controls and readable command/text colours. Physical DPI/accessibility certification remains unclaimed.

The complete native suite at that revision passes the new checks and long editor navigation, then its older preview-focus click hits the SVG export button at the pane's centre. The retained screenshot shows the resulting Save As dialogue. That test now targets actual selectable text before Ctrl+F; its context, source and keyboard assertions remain. Full regression and package/lifecycle results are still pending.

At `bf4c8d0`, the focused suite passes again and the complete suite progresses through preview search/diagram/file workflows. An older deletion check assumes exactly two snapshots across the whole shared test state; the new folder checks correctly add separate records. The deletion assertion now requires exactly two records for its own source path and exact saved/unsaved text, and the browser selects that document's unsaved record explicitly. No recovery assertion was removed.

Continuous retains its warning and unchanged 250 ms input budget. Downloaded `8c903a2` evidence verifies 1,086 Core/40 rendering cases and the persisted export workflow, but reports first-layout input of 811 ms for the list and 374 ms for the mixed fixture: both remain FAIL against 250 ms. Compatibility is not performance acceptance; final large/mixed-document and intermittent construction observations must retain this distinction.

Final installer/ZIP source, version, SHA-256 and independent lifecycle evidence are pending. Delivery remains an unsigned internal preview; production update trust, signing credentials, public releases, tags and merges are outside scope. Recommendation: **draft, certification in progress**.
