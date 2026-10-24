# Expansion VI engineering receipt

## Current status - 24 October 2026

The authoritative [Current Windows readiness](current-windows-readiness.md) records
the accepted 24-family develop baseline, internal-preview restrictions and
[Goal VII Draft PR #47](https://github.com/DouglasNLima/local-docs-studio/pull/47).
PR #46 is merged and its post-merge native/web runs succeeded at
`33a751c60886a364102c45cb43e36af1e7f13517`. Higher-DPI, physical-reader, clean-Windows,
historical-upgrade and production-trust certifications remain separately blocked.

## Historical source-labelled records

The original chapters below retain their dates, revisions, package hashes, counts
and then-pending decisions. Their present-tense milestone wording is historical;
use the canonical summary and exact-source PR delivery receipt for current status.


Four bounded native authoring slices are implemented. The accepted count remains **twenty** until Packet, TreeView, Treemap and Venn independently pass final-source acceptance. Preview compatibility is not editor or installation acceptance. This committed receipt preserves the implementation chronology; the source-labelled delivery receipt and final verdict are attached to Draft PR #46 after its exact clean source completes the gates.

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

### Clean combined candidate and catalogue validation correction

Clean candidate `1cb4d343968da576a3ac785709e3521e92f9723d`, tree `fc0ccd28f1cf2029c4189d98c9a0d588abc3af6c`, passed all **2,114 Core tests**, local four-family compatibility/fidelity, hosted compatibility run `40019864210`, hosted Chromium/Edge/aggregate run `40019869109`, stock Continuous normal/actual High Contrast run `40019869287` (controlled measurement skipped), original desktop UX run `40019869324` and consolidation run `40020149499`. Local unsigned preflight package `.10046` and all **22** independent negative controls passed without executing an installer. These results retain this candidate's source; they do not certify a subsequent commit.

Full native run `40019869560`, attempt 1, retained **176 corpus/export cases with two failures**: its separate native catalogue/help UI harnesses still asserted the previous 15 Mermaid snippets and 20 guide topics. Packaging and installed phases were skipped. The correction asserts exact new inventories of 19 Mermaid snippets and 21 guide topics, checks every new snippet in its actual category, supplies each new snippet's actual parser header/context and searches/renders the new help topic with explicit parser/geometry caveats. No assertion, deadline or fixture is relaxed. Raw logs, job records and the complete failed native artefact are retained under `artifacts/native/expansion-vi/1cb4d34-native-failure*`.

The first uncommitted correction run retained 176 corpus/export cases with one help failure: the new semantic assertion exposed that the guide described relative counts without spelling the literal `+count` token. The guide now includes it. Its original log/artefacts remain under `catalogue-correction-full-validation*`; a fresh complete run uses a separate output. The final clean-source acceptance and downloaded package receipts belong to the delivery record, never to these earlier candidate results.

The same `1cb4d34` appearance run `40019864182` retained a Gantt normal failure after successful Apply/Undo/Redo/Save. On the second owner launch, the old three-family harness acquired Transform immediately after `GetMainWindow`; that provider call timed out. The original raw job log and full artefact are preserved under `1cb4d34-gantt-normal-failure*`. This is the observed failing call, not a proved root cause or an explanation of historical .853/Journey/ER failures. Gantt/Mindmap/Pie's owner launches now use the already accepted `NativeExpansionLaunch.Prepare` gate: the same 25-second total budget, actual PID/HWND/source/readiness proof, one Resize only and full failure observations. No product code, timeout, assertion or retry policy is changed. The original failed workflow is not rerun; the new source receives independent gates.

The fresh uncommitted `catalogue-correction-2-full-validation` run passes all **176 corpus/export cases with zero failures**, including 114 native catalogue diagram appearances and the searchable rendered help topic. Its raw output stays candidate-labelled. The subsequent committed delivery source must pass all required gates independently.

### First combined candidate and bounded corrections

Candidate `8e9c4ab6cb5736d479fcd779e87ad085702dfa1c`, tree `66bfe3c9aacede7b9e1a759efbc01c7ccc984717`, produced **36 PASS / two FAIL** in [38-appearance run 40015836510](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40015836510), attempt 1. All 30 originals passed. Packet, Treemap and Venn each passed normal and actual Windows High Contrast. TreeView passed pointer, fields, history, small windows, owner Apply/Undo/Redo, Save/restart, Cancel/conflict, standalone/Insert and navigation, then both jobs failed the final protected-source diagnostic assertion. The actual rejected operation preserved the exact box tree; its message said structural re-encoding was unproved but omitted the asserted word “protected”. The normal screenshot and raw log prove that bounded failure. The message is now explicit, with the original assertion and deadline retained. Raw evidence: `8e9c4ab-treeview-{normal,high-contrast}-failure/`, `8e9c4ab-treeview-normal.raw.log` and `8e9c4ab-appearance-jobs.json` under the task evidence root. No failed job was rerun.

The same candidate's Core run retained **2,107 PASS / one FAIL**, because adding the independent guide topic raised its exact inventory from 20 to 21. [Native run 40015846505](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40015846505) stopped at that check; its skipped package/lifecycle jobs are not acceptance. The exact inventory assertion is updated to 21 and a new semantic guide test checks all four caveats. The earlier catalogue candidate similarly retained a 23 PASS / one FAIL inventory mismatch (22 versus 26 Mermaid templates); its corrected inventory and new native catalogue tests passed before `8e9c4ab` was committed. Raw logs/TRX retain both failures.

Four further configuration tests initially failed (five pending-source tests passed) when the generic Flowchart arrow heuristic mistook YAML `---` for an incomplete arrow. The four families now join the existing family-specific exclusion from that unrelated heuristic; their own parser/protection/resource checks remain. Original `configuration-candidate` logs/TRX are preserved. The corrected configuration/TreeView check passed 24 tests; the ordinary compatibility harness now also checks every Light/Dark fixture with and without front matter/init directives and exact export/source round trips. Its development receipt reports PASS and stays labelled an uncommitted candidate, not final-source acceptance. A further TreeView test proves the parser expands TABs throughout label/description text while untouched raw token bytes remain exact.

The hosted harness also now explicitly clicks both actual multirow Packet segments, rejects an actual overlapping Venn pointer, inspects hidden TreeView/zero Treemap occurrences, and writes exact source/image SHA-256 metadata and `.mmd` companions for new captures. Sparse Packet bit 4095 remains native-renderable but its 128 native rows exceed the unchanged default PNG dimension budget; the compatibility test requires a clear fail-closed raster rejection with preserved source.

Local correction-candidate checks passed **2,114 Core tests**, configured ordinary compatibility including the sparse Packet raster rejection, and static/service-worker integrity. The raw `correction-candidate-*`, `configuration-arrow-guard-candidate*`, `treeview-effective-tab-candidate*` and `raster-bound-correction-candidate*` receipts retain their uncommitted-candidate scope. The complete local browser run at committed `8e9c4ab` passed **270/270** (135 Chromium, 135 Edge) in 19.5 minutes; `8e9c4ab-browser.log` retains the original output. These do not replace the later clean-source full gate.

Candidate screenshots in the grammar guide are explicitly labelled `8e9c4ab` and retain the TreeView failure scope. Their source/image hashes are in `evidence/expansion-vi/8e9c4ab-captures.json`; they do not substitute for final-source raw captures.

The original `.858` pairs at `C:/Code/lens-native-journey-evidence/da37b790bb7eae3295c6f0e560e8c090eda9f433/package-inspection/packages/` were read-only rehashed: all four installer/portable hashes still match their prior SHA256SUMS. Original `.852`/`.853` archives, `.854` repair evidence and later historical receipts remain at `C:/Code/lens-native-853-evidence/` and `C:/Code/lens-native-journey-evidence/`. The accepted develop run `39991049058` was downloaded separately to `artifacts/native/expansion-vi/preserved-develop-39991049058/`; its actual package versions are `.859`, source `7b21fa6`, and are never relabelled `.858`. The task's initial download log retains its earlier assumed filename; actual metadata and preservation paths are authoritative. This fresh comparison baseline does not mutate any earlier download.

Draft PR [#46](https://github.com/DouglasNLima/local-docs-studio/pull/46) targets develop. Post-adapter clean source `a6d00ef40ef368bd636118d48e5cb77443bafeff` passes all **2,028 Core tests** and ordinary four-family compatibility locally. Raw receipt: `artifacts/native/expansion-vi/a6d00ef-compatibility/expansion-vi-compatibility.json`. Independent hosted compatibility: [run 40011612138](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40011612138), pending when recorded.

Packet candidate slice passes **21 focused tests** and the existing complete native editor fidelity/owner-session harness in Light/Dark, including zero-tolerance complete canvas pixels, multirow segment grouping, LF/CRLF duplicate-fence ownership, one-step Undo/Redo and stale-owner rejection. Raw candidate receipts/logs are under `artifacts/native/expansion-vi/packet-candidate*`; these precede the slice commit and remain candidate evidence. Range edits/removals/moves reject changed neighbour cursor interpretations rather than silently rewriting `+count`. Actual canonical segments are combined into one occurrence geometry; ambiguous overlapping ranges remain keyboard/source accessible. Native document/context, standalone and Insert routes are wired; hosted UIA/install acceptance remains pending.

The initial Draft-PR event queued the controlled Continuous workflow before this new branch was added to the established expansion exclusion. Run `40011645398` was explicitly cancelled to preserve #38's paused scope; its original run metadata is saved in `precondition/early-performance-workflow.json`. The branch exclusion changes only the controlled comparison; ordinary visible Continuous correctness/interaction jobs remain enabled on subsequent events. This cancellation is not a pass or a benchmark result.

Four slices and committed-source compatibility/fidelity are implemented. Final combined-source acceptance, 38 hosted appearances, 72 installed lifecycle phases, 22 package-negative controls, fresh unsigned version-pair packages, independent downloaded-package inspection, actual Windows captures and the completed Draft-PR handoff remain outstanding. No installer has been executed on the ordinary desktop. Internal acceptance retains `internal-no-associations` and its seven-site contract. No merge, release, signing, updater, tags, Pages deployment or optional association change is authorised.
