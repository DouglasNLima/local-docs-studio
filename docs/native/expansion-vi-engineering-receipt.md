# Expansion VI engineering receipt

Implementation is in progress. The retained native authoring count is **twenty** until Packet, TreeView, Treemap and Venn independently pass bounded authoring and final-source acceptance. Preview compatibility is not editor or installation acceptance.

## Accepted starting source

Fetched `origin/develop` independently on 23 October 2026. Current HEAD is `7b21fa62f58b10e79c74302b4b9e887be3aceadc`, exact tree `604fb3f6abfb6fae200166e53148eb354679cea7`. No later develop changes were present. The new managed worktree starts at that clean commit; the original checkout and unrelated worktrees remain untouched.

Independently read both workflow records and their complete job lists:

- [Native develop push 39991049058](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39991049058): SUCCESS, attempt 1, exact SHA above; 21 successful jobs, two explicitly skipped jobs (original .853 replay and historical upgrade).
- [Web develop push 39991048987](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39991048987): SUCCESS, attempt 1, same exact SHA; Chromium, Edge and aggregate success, Pages deployment skipped.

Raw responses are preserved under `artifacts/native/expansion-vi/precondition/` in this worktree. These are the accepted integration gates; the earlier .853 failure does not reopen that precondition. PRs #44/#45 are not modified. PR #38 remains open/draft/paused at `31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`; no experimental renderer is resumed. Original .853 coloured PDF, earlier Journey and intermediate ER readiness root causes remain INCONCLUSIVE. Prior .852/.853/.854/.858 evidence retains its original source and location.

## Pinned compatibility before controls

Inspected the existing upstream checkout at `C:/Users/dougl/.codex/worktrees/native-mermaid-expansion-iv/MarkdownReader/artifacts/pinned-mermaider`; actual HEAD is `71b64845570d633a94d29e85579c0a02786fece8`. Mermaider 0.15.1's NuGet repository metadata identifies the same source. Inspected all four parsers, public models, renderers, detector/preprocessor, pipeline resource checks and parser/renderer tests. None exposes a public parser/layout interception for these four families. Production uses the existing public render pipeline; no private engine API, fork, replacement layout, extra icon pack or runtime dependency is introduced.

Compatibility harness milestone `ad3f1ce5945633c4f318c2dfbed04d000e20425c` precedes every new editor control. Its ordinary native run is recorded in `artifacts/native/expansion-vi/ad3f1ce-baseline/expansion-vi-compatibility.json`. Status is **BASELINE_GAPS**, with six reproduced leading-inert-trivia dispatch failures. Do not relabel this run PASS.

All five representative fixtures (including both TreeView representations), each Light/Dark, passed ordinary document preview, trusted SVG conversion/PNG, HTML and DOCX exact embedded pixels, DOCX validation, parsed PDF image presence, actual Docs Site ZIP pixels, standalone export preparation and exact Markdown/standalone source bundle export/import. Entire canonical preview comparisons use zero pixel tolerance. Generated images were visually inspected: actual Packet row split and bit numbers; TreeView default folder/file glyphs, connectors, highlight and hidden icon; Treemap squarified positive tiles and derived totals; Venn fixed circles and rendered union label. These are offscreen native compatibility images, not hosted appearance or installed-authoring certification.

Additional baseline observations prove empty/simple aliases, later Packet title and accepted-only cursor movement, repeated segments, skipped overflow, bit 4095, TreeView first-line mode and four-space tabs, mixed branch glyphs, quoted directory labels, annotation removal and hidden icons, Treemap one-character tabs/spaces and zero filtering, fallback/duplicate labels, Venn duplicate-ID ambiguity, unresolved/unlabelled unions and size-independent radius 130. Explicit TreeView icons, including registered and unknown aliases, generate SVG images and are rejected by the unchanged sanitisation policy. They remain protected; default and hidden icons are the independently proved native choices.

## Bounded adapter and resource work

The leading-trivia adapter uses the existing public preprocessing preparation, removes inert leading trivia only from renderer input and retains `DiagramResult.Source` exactly. Configuration still goes through the pinned public preprocessor.

Upstream Packet, TreeView and Treemap omit element checks. Application preflight enforces the existing 500-element ceiling, including Packet split segments. An iterative hierarchy preflight enforces the existing 64-level nesting bound before Treemap's recursive `ComputedValue`, rejects non-finite decimal/aggregate values and non-finite native squarification scale. Venn already counts parsed sets/unions upstream; its silently infinite size metadata now fails closed before native parsing. The existing 100,000-character input, 1,500 editor lines, three-second pipeline deadline, private-history and PNG dimension/pixel budgets remain unchanged. No sanitisation or policy is relaxed.

Candidate focused resource tests: **19 PASS**, zero failures/skips; raw TRX and log under `artifacts/native/expansion-vi/tests/` and `resource-candidate.log`. This result is a candidate check, not final-source acceptance.

## Implementation milestones and combined acceptance work

| Milestone | Source | Status and boundary |
|---|---|---|
| Ordinary compatibility harness before controls | `ad3f1ce5945633c4f318c2dfbed04d000e20425c` | BASELINE_GAPS retained; six leading-trivia failures |
| Leading-trivia adapter and resource guards | `a6d00ef40ef368bd636118d48e5cb77443bafeff` | Local ordinary compatibility/2,028 Core PASS; hosted compatibility run 40011612138 SUCCESS, attempt 1 |
| Packet | `5c2c1030cdc365c0607e4f0dcfb13b31af3ff652` | Exact token/range authoring, accepted-only cursor and downstream guards; 21 focused candidate tests PASS |
| TreeView | `bbaf93f082650b2889e65e4dd27b61ea26bd90d9` | Exact fields and complete simple indentation subtrees; box structure/custom artwork protected; 15 focused candidate tests PASS |
| Treemap | `ba52075d400d7fe56684548f358aafe0df31574f` | Quoted source hierarchy/decimals, derived totals/zero keyboard access; 17 focused candidate tests PASS |
| Venn and pending-source safeguards | `39d6ab2` | Unique IDs/references, explicit absent-label rename semantics, stored sizes/fixed native geometry; 17 Venn and five cross-family pending-source candidate tests PASS |

At clean committed implementation milestone `39d6ab2`, all **2,103 Core tests PASS** (zero failures/skips), all four ordinary compatibility paths PASS, and the existing full native editor fidelity/owner-session harness PASS in Light/Dark for Packet, indentation TreeView, box TreeView, Treemap and Venn. Complete actual WPF editor/preview canvases use identical source/theme/scale/options and zero pixel tolerance, without masks. Raw source-labelled outputs are `artifacts/native/expansion-vi/39d6ab2/`, `39d6ab2-*-fidelity.log`, `39d6ab2-compatibility.log` and `tests/four-slice-core.trx`. Later combined-acceptance code changes require their own clean-source rerun; this is not final-source certification.

An early TreeView candidate test found missing mixed-syntax branch-glyph protection. Its original failed result (14 PASS, one FAIL) remains in `treeview-candidate.log` and `tests/treeview-candidate.trx`; the corrected independent run is `treeview-branch-protection-candidate.log`/TRX (15 PASS). Do not erase or relabel the first failure.

Combined work extends the existing 30 hosted appearances to 38, retains the 25-second single-resize owner readiness gate and existing deadlines, and adds four families to the ordinary installed harness for 72 lifecycle phases. The original eighteen package-negative controls remain; four independently targeted controls cover Packet's native type, TreeView's Core node type, Treemap's actual embedded catalogue resource and Venn's pinned dependency manifest. Each mutates/rechecks its own target with recomputed ZIP checksum, without executing package code. Their acceptance remains pending until run against final packages. The verifier also inspects all four embedded catalogue sources against exact fixtures and requires Mermaider/Sugiyama 0.15.1 in the actual compiled dependency manifest.

## Outstanding work

Draft PR [#46](https://github.com/DouglasNLima/local-docs-studio/pull/46) targets develop. Post-adapter clean source `a6d00ef40ef368bd636118d48e5cb77443bafeff` passes all **2,028 Core tests** and ordinary four-family compatibility locally. Raw receipt: `artifacts/native/expansion-vi/a6d00ef-compatibility/expansion-vi-compatibility.json`. Independent hosted compatibility: [run 40011612138](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40011612138), pending when recorded.

Packet candidate slice passes **21 focused tests** and the existing complete native editor fidelity/owner-session harness in Light/Dark, including zero-tolerance complete canvas pixels, multirow segment grouping, LF/CRLF duplicate-fence ownership, one-step Undo/Redo and stale-owner rejection. Raw candidate receipts/logs are under `artifacts/native/expansion-vi/packet-candidate*`; these precede the slice commit and remain candidate evidence. Range edits/removals/moves reject changed neighbour cursor interpretations rather than silently rewriting `+count`. Actual canonical segments are combined into one occurrence geometry; ambiguous overlapping ranges remain keyboard/source accessible. Native document/context, standalone and Insert routes are wired; hosted UIA/install acceptance remains pending.

The initial Draft-PR event queued the controlled Continuous workflow before this new branch was added to the established expansion exclusion. Run `40011645398` was explicitly cancelled to preserve #38's paused scope; its original run metadata is saved in `precondition/early-performance-workflow.json`. The branch exclusion changes only the controlled comparison; ordinary visible Continuous correctness/interaction jobs remain enabled on subsequent events. This cancellation is not a pass or a benchmark result.

Four slices and committed-source compatibility/fidelity are implemented. Final combined-source acceptance, 38 hosted appearances, 72 installed lifecycle phases, 22 package-negative controls, fresh unsigned version-pair packages, independent downloaded-package inspection, actual Windows captures and the completed Draft-PR handoff remain outstanding. No installer has been executed on the ordinary desktop. Internal acceptance retains `internal-no-associations` and its seven-site contract. No merge, release, signing, updater, tags, Pages deployment or optional association change is authorised.
