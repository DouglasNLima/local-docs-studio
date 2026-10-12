# Native validation report

Status: internal preview; production acceptance is incomplete. This record separates the latest completed CI revision from later local work. Feature-specific scope is in the [parity matrix](functional-parity-matrix.md); package paths/hashes are in the [engineering handover](engineering-handover.md).

## Latest completed CI: 6e572f6

Both native runs (PR 38312511577, push 38312508340) and both web runs (PR 38312511565, push 38312508397) pass on attempt 1. The web logs each record 262 passing cases. Independently downloaded native reports verify 850 core tests, 40 diagram/export cases, all five PDF list groups and all three Word export groups. Four 62-check desktop contexts pass, including three installed contexts loading both runtime modules from their installation directory. Five wizard outcomes, four files-in-use assertions, 20 preserved state files, restored sessions and disposable-certificate cleanup also pass.

Preview.236 matches exact clean PR merge `4958d3d06405faa33cd214dd04a995842475c969`, captured from GitHub for branch head `6e572f6af2a6af45d3b14841b4d15d21551d9416`; preview.235 matches that clean branch head. All eight package hashes, sizes, runtime ZIP entries and unsigned metadata pass. The latest installer SHA-256 is `65905429168E261D496F299D3A58137646B8D3228A555600ED2729B7CA09F6AE`; its ZIP is `EF10724152870A44853D9B5C32EFED0E710051C4BD630DD1BA5046C2772712F7`. Evidence is under `artifacts/native/ci-6e572f6-pr` and `ci-6e572f6-push`, including `package-verification.json` and `runtime-evidence-review.json`. No downloaded installer was executed locally. These builds include the Word export correction; the later reimport correction below is locally validated and awaits its own CI.

## Previous completed CI: b3a4d59

Native PR 38310145934, native push 38310140757 and web PR/push 38310145715/38310140748 all pass on their first attempts. Both web logs record 262 passing tests. Downloaded PR preview.234 identifies clean merge `cbaf55eea7c5473f76b07df07b5487d8c2493c6a`, captured from GitHub for head `b3a4d59cc6b32dbe201b3109f54994fa49e0cdea`. Push preview.233 independently identifies that exact clean branch revision. All eight installer/ZIP hashes, unsigned metadata, sizes and bundled runtime entries are verified; no installer was executed locally.

Both downloaded reports pass 839 core tests, the 40-case diagram/export corpus, four 62-check desktop contexts, installed runtime origins, five wizard outcomes, four files-in-use assertions, 20 preserved lifecycle state files, session restoration and disposable-certificate cleanup. The five PDF list outcome groups pass in both runs, including exact selectable word order across 90 continuation paragraphs on five pages. All seven hosted push PDF pages were rasterised and inspected, with page two also checked at higher resolution. Reports are under `artifacts/native/ci-b3a4d59-pr` and `ci-b3a4d59-push`; the latter includes `pdf-visual-review.json`.

The later Word list correction is separately validated below and is not included in preview.234. Earlier transient failures remain retained; this passing batch does not establish their root causes or production certification.

## Previous verified native PR: 56502e3

Native PR 38307579534, native push 38307575579 attempt 2 and web PR/push 38307579564/38307575323 pass. Both web logs record 262 passing cases on their first attempts. Downloaded preview.232 evidence matches exact clean PR merge `6fe047ecd37c80870badf08ea55f0fd2ef372196`, captured from GitHub for head `56502e3c3734a541236d370fb439f97581246310`. All four installer/ZIP hashes, unsigned metadata and runtime files pass independent read-only verification. No downloaded installer was executed locally.

All 839 core tests, the 40-case diagram/export corpus, four 62-check desktop contexts, five wizard outcomes, four files-in-use assertions, 20 preserved lifecycle state files, restored sessions and test-certificate cleanup pass. The new runtime records show matching application executable paths and exactly one loaded `coreclr.dll` and `wpfgfx_cor3.dll` in every context. Installed/upgraded/reinstalled applications load both modules from their installation directory. The built-app context correctly records its framework runtime. `artifacts/native/ci-56502e3-pr/runtime-evidence-review.json` and `package-verification.json` retain the verification. This does not certify SDK-free Windows.

Native push 38307575579 attempt 1 passed 11 exported-site tests but failed one while taking `native-site-light.png`: Chromium closed after 49 successful assertions, before the remaining assertions ran. The original log, trace and `failure-review.json` remain under `artifacts/native/ci-56502e3-push-failure`. Attempt 2 passes with source and assertions unchanged. No underlying browser-crash cause is established. The subsequent PDF correction below awaits its own CI.

## Previous completed CI: cfe11e5

All four runs pass: native PR 38304292939, native push 38304289265 attempt 2 and web PR/push 38304296486/38304289262. The initial push UIA acquisition failure is retained below. Downloaded PR evidence is under artifacts/native/ci-cfe11e5-pr. The clean package revision f1590a455cdb2e3ca0b31622ff5dd12c184fc334 matches the GitHub PR merge revision for head cfe11e5; evidence-review.json, source-verification.json and package-verification.json retain the checks below.

| Evidence | Verified result |
|---|---|
| Core | 839 tests pass in the downloaded TRX |
| Native corpus | 40 diagram/export cases, zero failures |
| Desktop | 61 checks in each of built/installed/upgraded/reinstalled applications |
| Diagram copying | Eight keyboard copies retain exact 471 × 149 pixels at 175%/35% zoom, survive closing the drawing window and preserve source; the reinstalled PNG was inspected |
| Installer wizard | Five outcomes, including all four running-application preservation assertions |
| Files in use | Cancel retains the same live application, exact unsaved Unicode draft/baseline, source, installed files/directories, shortcuts and registration; installation snapshots are identical |
| Lifecycle | 20 state files and workspace source preserved; sessions reopen after same-source preview upgrade/reinstall |
| Trust fixture | Both test certificate stores cleaned, no private-key export, source build and production trust unchanged |
| Packages | Four preview.230 installer/ZIP hashes match exact clean PR merge metadata; bundled runtime confirmed, installers NotSigned, manifests without signing identity/signature, no local execution |
| Details transitions | All 18 pass, maximum input 66.335 ms with complete text, formatting and source |

Initial nested-details input/maximum gap is 54.676 ms, initial call 54.404 ms and total construction 117.8524 ms. These samples do not explain the historical 583 ms details or 360.6 ms table pauses. The 250 ms input bound is unchanged. The user has no configured production certificate/service/publisher; unsigned update execution remains blocked.

The cfe11e5 push's first attempt fails in UIA ElementFromHandle/GetMainWindow with TimeoutException wrapping COM 0x80131505, before any workflow assertion. Its captured window shows workspace opening; the saved session retains the exact fixture. dotnet-dump 10.0.750501 reads the retained stacks: application thread 0x2448 is in WPF Dispatcher.GetMessage/ITfMessagePump.GetMessageW at capture. That snapshot does not establish the timeout's cause. The original log, inspected screenshot, dump, dump-analysis.log and failure-review.json remain under artifacts/native/ci-cfe11e5-push. One unchanged-source retry passes the complete workflow; no assertion or timeout was relaxed and no root-cause fix is claimed.

The push retry's artefact 11772379399, created at 20:52:55 UTC, is retained under artifacts/native/ci-cfe11e5-push-attempt2. Its four preview.229 package hashes, clean cfe11e5 source, bundled runtime and unsigned metadata verify independently. Downloaded reports confirm 839 core tests, 40 corpus cases, four 61-check desktop/clipboard contexts, 20 retained state files, session restoration and certificate cleanup. The run retains two artefacts named native-validation; a name-only download selected the first attempt, so the successful evidence was fetched by its explicit artefact ID. evidence-review.json records this distinction.

## Word allocation correction: 0c20f64

The Word exporter now preflights an aggregate limit of 65,536 code DOM elements before template/package allocation. This addresses a Low finding in the immutable cf436b0 review. Tabs, breaks, text spans and paragraph/run formatting share the budget across nested blocks; normal Unicode, syntax colours and CRLF remain intact.

| Local evidence | Result |
|---|---|
| Release solution | Zero warnings/errors |
| All core tests | 839 pass; artifacts/native/tests/native-word-code-budget-all.trx |
| Seven new allocation regressions | Large tab/text/break inputs rejected with less than 4 MiB cumulative rejection allocation; nested aggregation, exact actual XML boundaries, cancellation and Mermaid/formula exclusion pass |
| Complete native harness | 40 diagram/export cases, zero failures; artifacts/native/word-budget-validation |
| Word fidelity | Actual native preparation retains editable Unicode, colours, tabs, line breaks and all 800 fallback rows; schema checks pass |
| Operational test guards | Five desktop/Office commands and Test-NativeInstaller.ps1 refuse before arguments/paths; artifacts/native/desktop-test-guards.json |
| Static/PowerShell checks | Pass |

The first focused run failed because its fixture counted a MathBlock as the sole FencedCodeBlock. native-word-code-budget-focused.trx retains the failure; selecting the Mermaid fence fixes the fixture without weakening the expected output. The full suite then passes.

Both native CI runs pass the correction and all desktop/installer stages; preview.227 packaging is independently verified. These results certify the recorded internal-preview fixtures, not production signing or SDK-free Windows.

The web push passes 262 cases. Web PR attempt 1 records 261 passed tests and a Chromium process exit 3221225477 during browserContext.close for the preview context-menu test. All 17 assertions in that test's retained trace succeed; the errors belong to Close context, its fixture and After Hooks. The original log, trace and trace-analysis.json are under artifacts/native/web-ci-0c20f64-pr-failure. The web source and tests are unchanged since the prior green cf436b0 range. The failed-job rerun (attempt 2, same run ID) passes all 262 cases; the underlying browser crash cause is not established and assertions are unchanged. The complete rerun log is artifacts/native/web-ci-0c20f64-pr-retry.log.

## Diagram clipboard regression

Diagram image copying has stable automation identifiers. The cfe11e5 native PR passes keyboard activation at 175% and 35% zoom, every BGRA pixel/dimension against a separately prepared matching-palette reference, actual window closure, clipboard persistence and retained edited/saved source in all four contexts. All eight copies are 471 × 149 with pixel SHA-256 96AFA26F014EAF71EEA9C38029210B917D8519AEFE16F209B8BA59C07F2FCA29. Reports and PNGs are in the downloaded native-ui-diagram-clipboard.json/native-diagram-clipboard.png files; clipboard-review.json consolidates them. The reinstalled PNG was inspected. No local clipboard execution occurred. External Office/Azure DevOps paste destinations remain separate.

## Subsequent PDF list correction

The actual pre-change PDF repeated `7.` for three paragraphs in one item and flattened nested bullets to the body margin. The corrected exporter uses one pending marker per item, measured hanging indentation and available-width propagation for images and tables. `NativePdfListChecks` joins the complete harness and is also available through `--pdf-lists <output> <repository>`.

Five focused groups inspect actual selectable text, marker counts, glyph positions, bold/Unicode/link retention, zero/nine-digit starts, quote/heading/code first blocks, table-first/nested-only items, image bounds and exact word order across 90 continuation paragraphs on five pages. All seven fixture pages were rasterised and inspected. Evidence is under `artifacts/native/pdf-lists-regression`, with the final exact-word check under `artifacts/native/pdf-lists-exact`. The first tightened count mistakenly included words from the final prose paragraphs; scoping that count to item 41 corrects the test, while the independent whole-document word comparison remains exact. No production assertion was weakened.

Release has zero warnings/errors, all 839 core tests pass (`artifacts/native/pdf-list-core/pdf-list-core.trx`), the complete native harness has 40 diagram cases and zero failures (`artifacts/native/pdf-list-full-validation`), and static checks pass. No local desktop interaction occurred. Hosted validation of the PDF change now passes in both b3a4d59 native runs as recorded above. Visual list hierarchy does not establish tagged PDF accessibility.

## Subsequent Word list correction

Seven original `DocxListTests` fail before the correction: continuation paragraphs advance numbering, headings/code lose the first marker, table/nested-only items lose their parent marker and empty items disappear. The retained failure record is `artifacts/native/word-lists-before/word-lists-before.trx`. Eleven final regressions pass after adding independent image/cell geometry, explicit template margins, narrow template tables and list-code allocation-boundary checks. Continuations use `w:numId=0`, the [Open XML override for inherited numbering](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.numberingid?view=openxml-3.0.1), rather than advancing the list. Styles, original table-width settings and image part bytes remain intact.

Release builds with zero warnings/errors, all 850 core tests pass in `artifacts/native/word-lists-after/word-list-widths.trx`, and the complete native harness passes 40 diagram/export cases with zero failures in `artifacts/native/word-list-width-validation`; static checks pass. The new `--word-lists` check is also part of that harness. Its two actual DOCX files preserve 12 marked fixture items, unnumbered continuations and first-block native Mermaid/formula images, with valid package schemas and unchanged source. The initial custom-margin fixture used the wrong Open XML property types; the final successful build uses the actual numeric margin and enum types.

These are structural/semantic checks. Both 6e572f6 native runs now validate this export correction as recorded above. No local Office application was launched. Actual Word pagination, wide-marker positioning, template style-derived margins and screen-reader behaviour remain unverified. The previous b3a4d59 CI covers the PDF correction only.

## Word list and heading reimport correction

The exporter correction exposed a separate importer defect: any numbering properties, including the explicit `numId=0` override, became a new list item. Starts and levels were discarded. Seven initial round-trip regressions fail in `artifacts/native/word-import-before/word-import-before.trx`. An additional exact heading-level regression fails in `heading-before.trx`: Heading 1 through Heading 5 were demoted one level. The earlier substring assertions did not detect that error.

The importer now reconstructs direct list levels, start overrides, restart/never-restart counters, matching continuation indents, first-block headings/tables/nested lists and empty items. Separate numbering instances remain separate Markdown lists. Headings retain levels 1–6. Unsupported formats/composite markers and lists flattened inside pipe-table cells produce warnings; invalid levels and numbers outside Markdown's nine-digit range are rejected without changing input. Numbering lookup caches and bounded level traversal avoid repeatedly scanning preceding lists; cell conversion retains its own output buffer instead of copying accumulated body text.

All 868 core tests pass, including 18 new import cases, in `artifacts/native/word-import-after/word-import-heading-final.trx`; Release builds with zero warnings/errors. The full native harness passes 40 diagram/export cases with zero failures in `artifacts/native/word-import-heading-validation`; static checks pass. Its new probe reimports the two actual prepared DOCX files, checking six lists, 12 items, six continuation paragraphs, exact ordered starts, heading levels and both original native diagram/formula image payloads. The earlier native probe used `ContainerInline.ToString()` as text and failed its continuation assertion; the check now uses the existing `MarkdownService.InlineText` utility. Two expected Markdown restart fixtures needed blank lines before nested ordered lists starting above one. These fixture corrections did not relax item or counter assertions.

The counter rules follow the documented [start override](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.startoverridenumberingvalue?view=openxml-3.0.1) and [level restart](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.levelrestart?view=openxml-3.0.1) semantics. Scope is direct numbering within Word's nine levels, not a complete Word round trip. Style-inherited numbering, linked styles and broader template fidelity remain unverified. The fixture still loses hyperlink destinations and code/quote block styling during import; readable text and this batch's list boundaries are retained. These local changes are later than the 6e572f6 CI build and require their own hosted run.

## Word hyperlink import correction

Eight initial cases fail before this change in `artifacts/native/word-links-before/word-links-before.trx`: DOCX hyperlink destinations disappear, internal bookmarks do not resolve, labels can become unintended Markdown links, and missing/unsupported destinations have no warning. The importer now traverses inline content without discarding hyperlink boundaries. HTTP/HTTPS/mailto references retain formatted labels and escaped destinations; paired, unambiguous referenced bookmarks become safe document anchors in an import-specific namespace. A relationship ID takes precedence over an internal anchor. Missing/unsupported destinations, nested links, unsupported document locations and field results produce explicit warnings; no link is fetched and no Word field is evaluated.

Fifteen core regressions cover links in paragraphs, lists and table cells, formatting/Unicode, Markdown punctuation and entity-like query text, linked image bytes, invalid/duplicate bookmarks, relationship precedence, inert fields, malformed names and bounded inline nesting. Literal text is escaped once, including table pipes; emphasis retains surrounding whitespace. All 883 core tests pass in `artifacts/native/word-links-after/word-links-final.trx`. Release builds with zero warnings/errors, static checks pass, and the full native harness passes 40 diagram/export cases with zero failures in `artifacts/native/word-links-final-validation`. Its reimported list fixture now retains the actual `https://example.org/list` destination.

The mapping follows the documented [Word hyperlink relationship and anchor semantics](https://learn.microsoft.com/en-us/dotnet/api/documentformat.openxml.wordprocessing.hyperlink?view=openxml-3.0.1). This is later than 4cbce1d and awaits its own hosted validation. File/relative relationship targets remain text with warnings; field-based hyperlinks retain their stored results. Code/quote block styling, style-inherited numbering and full Word/template fidelity remain incomplete. No Office application or downloaded installer was run locally.

The native import completion message now includes every distinct conversion note instead of only the first. The existing status control binds both its visible text and tooltip to that message; preview rendering updates the separate PreviewStatus property. This change is source/build-verified. No local foreground import interaction was performed, and the existing 62 desktop checks do not specifically certify this tooltip interaction.

## Runtime and diagnostic work

The loaded-runtime check now passes in the 56502e3 native PR as recorded above. It captures the actual application executable and coreclr.dll/wpfgfx_cor3.dll module paths after editor/diagram readiness. Test-NativeInstaller.ps1 explicitly requires both loaded modules to belong to the installation directory. The normal built-app context records its framework runtime. Each context saves native-ui-runtime.json before any failed assertion. Release has zero warnings/errors, static/script checks pass and the new option still refuses local desktop execution. This does not claim that the hosted runner lacks an SDK.

Three unchanged local processes ran hidden-HWND layout checks for the unresolved latency concern. All twelve table/quote/details/list cases pass exact content/source and the unchanged 250 ms input bound; maximum gap is 89.58 ms. Details component initialisation ranges from 10.77 to 15.67 ms. Samples and analysis.json are in artifacts/native/construction-cfe11e5. These do not reproduce the earlier pauses or establish a cause or fix.

## Security evidence

The sealed b45246e..cf436b0 review is 475201d4-5562-4ffa-bc7a-166818c61152: all 101 changed inventory files and six supplemental source files reviewed with a fresh architecture model. One Low Word DOM allocation finding survives; the ContextColor hypothesis is rejected because the pinned Mermaid renderer removes the supplied paint forms before native conversion. A bounded original-build probe with 65,536 tabs allocated 12,416,296 cumulative managed bytes for a 6,142-byte DOCX under a 32,768-byte output limit. It did not measure peak memory or induce OOM. The subsequent 0c20f64 correction is regression-tested separately and does not alter that sealed result.

The earlier b45246e scoped review reported two Low image-allocation findings, subsequently corrected with part deduplication and repeated-image preflight/output limits. Those exact reports and regression evidence are retained in history. Neither review certifies the entire product, all dependencies or external environments.

## Acceptance limits

Required external evidence remains: production issuer/timestamp/revocation and signed upgrade, SDK-free clean Windows, physical screen-reader/DPI/multi-monitor behaviour, live Azure DevOps attachments, real historical schema migration, physical power-loss/removable-drive failures. The tested upgrade changes preview versions from the same source and is not historical migration certification.

Broader Word/template rendering, PDF complex-script shaping/BiDi/joined or colour emoji, explicit unsupported Mermaid/HTML/LaTeX forms and large-document continuous/page layout remain limited. Prior Office 16.0.20430.20092 Word/Excel destination checks establish only their recorded fixtures. Current windowless glyph/package checks cannot replace external destination validation. Intermittent initial WPF construction delays remain unresolved despite passing subsequent samples.

No downloaded installer, foreground native application, clipboard, Office instance or certificate-store test ran locally in this batch. No merge, release or tag changed. Earlier baseline, failures, corrections, run IDs and artefact paths remain in [history through 0c20f64](history/validation-report-through-0c20f64.md) and [earlier validation history](history/validation-report-through-9fddfd9.md).
