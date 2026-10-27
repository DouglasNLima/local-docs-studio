# Complex ER rendering and preview loading — Goal #52

## Source and scope

Base commit `bebd1635a66e3be9cc76c7c38e27a26837fcdeb5`, tree `36f09af29c6d64d57125511b62310e77cdb38c4d`, was fetched and verified from remote `develop` before implementation. This is the integration of PR #51. Its [native post-merge run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40569935564) and [web run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40569935551) reached success. Work is isolated on `fix/native-complex-mermaid-rendering`. Issue #52 includes both rendering and loading acceptance; paused PRs #38/#50 are untouched. The Draft PR records final commit/tree and terminal hosted outcomes without embedding a self-referential source hash here.

The required SPC 39/115/284 snapshot was not available in the supplied attachments. Two smaller ER sources in the local SPC project were not equivalent to that snapshot and were not copied. All committed fixtures and evidence use deterministic synthetic names. This is **synthetic-equivalent acceptance**, not certification of the missing customer source.

## Measured cause and correction

Mermaider 0.15.1's ER adapter requests 160 random crossing-search restarts. Each restart repeats greedy insertion refinement, rescoring many possible node positions and copying layer arrays. The baseline detailed fixture was stopped by an external watchdog after **73.58 s wall / 71.70 s CPU**, still inside layout, despite its nominal three-second deadline. The pinned ER adapter did not forward the cancellation token into Sugiyama. A 12-second CPU trace places almost all active renderer samples in `CrossingMinimizer.InsertionRefinement` / `Buffer.Memmove`; percentages in the retained top-functions report also include idle threads and must not be interpreted as process CPU percentages.

`BoundedErLayout` adapts the pinned ER adapter and still calls the same **Sugiyama 0.15.1** library, parser, models and SVG generator. It obtains exact entity sizes from the pinned provider without relationships. Small diagrams (at most 12 entities and 24 relationships) retain the 160-restart policy and upstream geometry, with an explicit canvas correction for previously clipped unlabelled self-loops. Larger diagrams use one deterministic eight-sweep search, retaining greedy refinement, port-aware routing, self-loop geometry, cardinalities, labels and all attributes. Model-order tie breaking for virtual nodes reduced the detailed case's allocation churn from about **1.62 GB to 1.25 GB**. Four/32 sweep and real-node tie-breaking experiments did not justify further policy changes. Geometry of larger graphs can differ from the baseline's expensive search; repeated final renders must produce identical canonical SVG.

Cancellation now reaches the supported Sugiyama checkpoints, and its existing 20,000 expanded-node limit is enforced for ER too. Missing entities or relationship occurrences fail the render. Parsing, resource limits, unsupported syntax, deadlines, layout, SVG and native conversion have separate diagnostic categories. User cancellation propagates separately; no cancelled or partial output enters caches. The cache retains successful results and deterministic syntax/unsupported failures only. An unchanged-source explicit retry therefore recomputes transient failures without an automatic retry loop.

The MIT attribution for adapted code is retained in source and included in native package notices. No dependency version, browser engine, JavaScript runtime or second renderer was added.

## Deadline and resource policy

The algorithmic correction was first measured under the original three-second limit. Three fresh process trials rendered the detailed fixture in 1.27–1.70 seconds before the final virtual-node option; another final-option cold trial took 2.35 seconds. However, shown WPF acceptance still measured **5.19 seconds** before that option and **4.06 seconds** after it. Those original deadline failures are preserved, not replaced by passing reruns.

The resulting production policy is **six seconds for ER; three seconds for all other families**. This is a cooperative rendering deadline, not a guaranteed hard process pre-emption: the pinned library checks cancellation between supported layout phases/layers. It replaces the baseline's effectively non-cancellable ER search, while input (100,000 characters), semantic elements (500), SVG and image budgets remain bounded. Expensive work runs on workers. SharpVectors itself remains synchronous within its STA worker; cancellation is checked before/after conversion and rejects obsolete results. The preview gate prevents concurrent obsolete document builds from accumulating.

Allocation churn remains substantial for dense graphs. It is not retained memory: the complete six-fixture native render/conversion/export/inspection process peaked at **422 MB working set**; fresh renderer-only runs were about 67–75 MB. Those are observed values on an i7-1360P Windows 11 host with .NET 10.0.12, not maximum-memory guarantees for arbitrary inputs. Increasing the deadline permits up to twice the previous nominal ER work budget; cancellation, expanded-node limits and unchanged output limits mitigate that cost. Further upstream refinement improvements remain desirable.

The independent **Continuous 250 ms** limitation and its failing acceptance gate are unchanged. No threshold or sample is suppressed.

A deliberately contended shown run, alongside the full browser suite, also reached the six-second deadline. A concurrent Core run likewise failed one dense case (2,250 passed); the sequential run passed all 2,251 tests in 20 seconds. Its original phases are retained; the allowance was not raised again. The pinned engine's natural-back-edge option reduced allocation churn to about 304 MB but produced **206 relationship/entity-body intersections** in the detailed fixture versus **zero** with the retained routing. It was rejected. Reversing source order left allocation cost unchanged and was also rejected. The primary regressions now explicitly reject routes through unrelated entity bodies. Dense graphs can still exceed the bounded allowance on slower or heavily contended hosts; explicit retry and accurate deadline feedback remain necessary.

## Timing and fidelity evidence

[Native measurements](evidence/complex-er-preview/complex-er.json) and a [final sequential verification](evidence/complex-er-preview/final-sequential-complex-er.json) include CPU, allocation, working set, hashes and cache results. These first/fresh-service timings are from one process; only the first row's first render is process-cold.

| Fixture | Entities / relationships / attributes | First / fresh-service render | Baseline observation |
|---|---|---|---|
| Complete, relationships | 39 / 115 / 0 | 1,672 / 969 ms | Same dense network as the failing primary case |
| Complete, detailed | 39 / 115 / 284 | 1,048 / 971 ms | Watchdog stopped unfinished layout at 73.58 s |
| Operations | 22 / 68 / 195 | 76 / 75 ms | 2,892 / 2,671 ms; about 4.09 GB allocation churn |
| Monitoring | 13 / 46 / 138 | 19 / 10 ms | 1,683 / 712 ms; about 1.01 GB allocation churn |
| Workflow Assignment | 11 / 21 / 82 | 9 / 38 ms | 413 / 28 ms, including process startup/JIT |
| Workflow Definitions | 12 / 20 / 97 | 16 / 13 ms | 24 / 34 ms |

Cache hits use the exact source/theme key before repeated source scans and allocate no measured bytes in the standalone probe. Semantic tests compare every relationship/cardinality/label and attribute, including parallel endpoints, self-links, blank/long labels, identifying/non-identifying connections, reversed declaration order and all four layout directions. Inspection retains distinct occurrence identities. Native verification compares full-canvas SVG and PNG, frozen drawings and canonical inspection pixels. The generator and count manifest are committed; generated ZIPs and proprietary sources are not.

Large diagrams exceeded the existing default 2× PNG dimensions in the first complete corpus run (four failures across detailed/operations Light/Dark). PNG save/copy and document export now select the largest scale in 2×, 1×, ½× … 1/64× which fits the existing 16,000-side / 64-million-pixel guard. The complete canvas is preserved; SVG remains the lossless choice for large diagrams. Explicit-scale rasterisation retains its strict rejection behaviour. Original failure evidence is retained.

## Loading lifecycle

Three final-policy fresh processes rendered the primary detailed case in **2,099 / 2,349 / 1,674 ms**; uncached warm renders took **948 / 1,049 / 875 ms**. CPU time was 2,297 / 2,500 / 1,813 ms cold, with about 1.25 GB allocation churn. The JSON trials retain per-phase timings, cache hits and memory. Shown WPF trials measured 4,014 ms and 3,944 ms layout and completed within the documented ER allowance.

The shown harness also sampled 400 ms with animation enabled and disabled while the preview gate was held. Input-priority dispatcher gaps averaged 25.3 / 27.1 ms (maximum 37.5 / 105.9 ms). Whole-process CPU was 484 / 391 ms and allocations 9.7 / 9.9 MB, including WPF/UIA and warm-up; these short samples are observational, not an isolated per-frame allocation benchmark or a hardware-independent gate. Production code has no frame callback, polling render loop or per-frame document work. The existing separate performance gate remains authoritative.

`PreviewLoadingState` owns generation, document, revision, appearance, layout and terminal outcome. Its 200 ms presentation delay integrates with the existing 300 ms debounce; the visibility timer never schedules rendering. Fast work stays hidden. Repeated edits transfer uninterrupted busy feedback. The spinner is a native WPF progress bar template with an animated render transform, using existing theme resources. Windows client-area animation preferences stop its animation while retaining a static ring/status. It is not focusable or hit-testable.

Incoming documents immediately mask the outgoing content and its interactions. Same-document updates keep the accepted preview visible with a smaller top-right “Updating preview…” indicator. Pipeline stages cover parsing, diagrams, media, construction, attachment and terminal outcomes. Completion waits for the first dispatcher layout opportunity, not the entire background paginator. Old generations cannot hide a new spinner or install stale content/errors. Editor-only mode, document closure and application closure cancel or clear loading. Existing scroll/selection/zoom restoration remains the owner of viewport state.

UIA identifiers are `PreviewLoadingIndicator` and `PreviewLoadingStatus`; the preview exposes Busy/Ready. One polite busy announcement and one completion/error announcement avoid narration of internal stages. An explicit Retry preview toolbar action uses the latest source/theme and the existing scheduler cancellation rules. Individual diagram failures remain beside their source actions and preserve unrelated Markdown.

## Runtime and regression evidence

The shown validation harness constructs the real MainWindow with isolated state and real resources, uses controlled asynchronous pipeline gates for reproducible ordinary-Markdown loading, then renders dense Mermaid without a timing stub. It verifies visible UIA bounds/busy state and captures actual WPF visuals, not a mock-up. Light/Dark × Continuous/Pages, ordinary local images/tables/long text, initial/refresh, rapid edits and three-tab switching, incoming-document masking, selection/zoom/focus preservation, cancellation, error recovery, Editor-only mode, closure and reduced motion are covered. See [lifecycle results](evidence/complex-er-preview/preview-loading.json).

| State | Shown WPF evidence |
|---|---|
| Light initial load | [Initial](evidence/complex-er-preview/Light-Continuous-initial.png) |
| Dark refresh with accepted content | [Refresh](evidence/complex-er-preview/Dark-Continuous-refresh.png) |
| Actual dense Mermaid work | [Loading](evidence/complex-er-preview/complex-loading.png), [completed](evidence/complex-er-preview/complex-completed.png) |
| Individual diagram failure with Markdown retained | [Error](evidence/complex-er-preview/Dark-Continuous-error.png) |
| Pages mode | [Initial](evidence/complex-er-preview/Light-Pages-initial.png), [completed](evidence/complex-er-preview/Light-Pages-completed.png) |

The ordinary app was also built/launched locally with isolated state: its accessibility tree reported the four-diagram document complete in 7,252 ms and ordinary Markdown in 178 ms. The computer-use desktop capture returned a white client area for both the candidate **and an independently built unmodified base** after completion. This capture path does not establish visual acceptance; retained shown-WPF/UIA evidence and hosted screen captures are identified separately. Local OS High Contrast was not toggled: the existing helper requires a disposable GitHub-hosted desktop. Hosted actual OS High Contrast and package/lifecycle outcomes are recorded on the Draft PR. Physical screen-reader use and real multi-monitor/DPI transitions remain uncertified.

Local Core: **2,251 passed, none skipped**. The full native corpus passed 202 cases; static integrity and all 270 Chromium/Edge browser tests passed. All 12 native export-browser checks passed; final hosted outcomes are recorded on the Draft PR and in the validation tracking document as they complete. Initial build/test/three-second/corpus failure logs remain under ignored `artifacts/native/goal52/`; compact shareable baseline and final evidence is committed here. The source review and final recommendation must distinguish these observations from any pending gate.

## First hosted acceptance and compatibility repair

The [first hosted native run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40582261606) at `f2e9e4fbb4f98d3bdd39f08d2d2d9d48e6cea5a3` passed the complex ER job, shown Light/Dark loading and actual Windows High Contrast. [High Contrast results](evidence/complex-er-preview/hosted-high-contrast.json), [initial](evidence/complex-er-preview/High-Contrast-initial.png), [refresh](evidence/complex-er-preview/High-Contrast-refresh.png) and [Pages](evidence/complex-er-preview/High-Contrast-Pages-completed.png) captures are retained with that source identity.

The first controlled Continuous comparison failed before measuring its baseline: the shared compiled Program.Main referenced the newly added PNG export API, which the pinned older product cannot supply. This was an introduced harness compatibility failure, not the inherited 250 ms result. The original stderr is retained. Corpus execution now has a separate non-inlined JIT boundary, and the existing one-argument SVG rendering API remains available alongside the cancellation overload. The identical comparison protocol, fixtures, thresholds and older product remain unchanged. A local mixed-content run of the current harness against the unmodified PR #51 product verifies that compatibility; final hosted comparison results are recorded on the Draft PR.
The next desktop run reached the 100-entity/150-relationship ER editor but its normal-appearance performance check encountered UIA `0x80040201` while enumerating the whole window after selection; the High Contrast counterpart passed. The [original observation failure](evidence/complex-er-preview/original-hosted-er-observation-failure.json) is retained. The check now scopes observation to the canvas and uses the existing bounded transient-observation retry. Exact attribute/relationship counts and all input actions remain required; observations are counted in the receipt. The existing optional 100-entity resource outcome also recognises the new precise deadline wording. No performance threshold or required 10/30-entity case is relaxed.

## Reproduction

Generate fixtures with `node scripts/native/Generate-ComplexErFixtures.mjs`. Build the Release solution and run the Core tests normally. Run the complete prescribed native corpus and `npm test`; the new fixtures are automatically part of the corpus.

Use `dotnet run --project src/native/LensDocsStudio.Native.Validation -c Release --no-build -- --complex-er <fresh-output> .` for phase measurements, counts, full exports and inspection correspondence. Use `--preview-loading-ui <fresh-output> .` for shown isolated WPF/UIA lifecycle acceptance (no injected OS input), or `--preview-loading` for compiled lifecycle checks. The `--high-contrast` variant retains the disposable hosted-desktop guard. The native workflow runs all three along with the existing independent regression and package jobs.
