# Native validation report

Status: internal preview; production acceptance is incomplete. This record separates the latest completed CI revision from later local work. Feature-specific scope is in the [parity matrix](functional-parity-matrix.md); package paths/hashes are in the [engineering handover](engineering-handover.md).

## Latest completed CI: 0c20f64

All four runs pass: native PR/push 38300926460/38300922818 and web PR attempt 2/push 38300926434/38300922779. Downloaded push evidence is under artifacts/native/ci-0c20f64-push; its evidence-review.json and package-verification.json record the checks below. The initial web PR teardown crash and successful rerun are detailed separately below.

| Evidence | Verified result |
|---|---|
| Core | 839 tests pass in the downloaded TRX |
| Native corpus | 40 diagram/export cases, zero failures |
| Desktop | 60 checks in each of built/installed/upgraded/reinstalled applications |
| Installer wizard | Five outcomes, including all four running-application preservation assertions |
| Files in use | Cancel retains the same live application, exact unsaved Unicode draft/baseline, source, installed files/directories, shortcuts and registration; installation snapshots are identical and the retained editor screenshot was inspected |
| Lifecycle | 20 state files and workspace source preserved; sessions reopen after same-source preview upgrade/reinstall |
| Trust fixture | Both test certificate stores cleaned, no private-key export, source build and production trust unchanged |
| Packages | Four preview.227 installer/ZIP hashes match exact clean revision metadata; bundled runtime confirmed, installers NotSigned, manifests without signing identity/signature, no local execution |
| Details transitions | All 18 pass, maximum input 74.9417 ms with complete text, formatting and source |

Initial nested-details input/maximum gap is 65.6778 ms, initial call 65.4164 ms and total construction 139.5582 ms. These samples do not explain the historical 583 ms details or 360.6 ms table pauses. The 250 ms input bound is unchanged. The user has no configured production certificate/service/publisher; unsigned update execution remains blocked.

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

## Next hosted regression

Diagram image copying now has stable automation identifiers. The pending desktop check uses keyboard activation at 175% and 35% zoom, compares every BGRA pixel/dimension with a separately prepared matching-palette reference, waits for actual window closure, checks clipboard persistence and retains edited/saved source. It saves native-ui-diagram-clipboard.json and native-diagram-clipboard.png in all four contexts. Release and static checks pass; actual clipboard execution has not occurred locally. External Office/Azure DevOps paste destinations remain separate.

## Subsequent runtime and diagnostic work

A loaded-runtime check is compiled but has not run in hosted CI. It captures the actual application executable and coreclr.dll/wpfgfx_cor3.dll module paths after editor/diagram readiness. Test-NativeInstaller.ps1 explicitly requires both loaded modules to belong to the installation directory. The normal built-app context records its framework runtime. Each context saves native-ui-runtime.json before any failed assertion. Release has zero warnings/errors, static/script checks pass and the new option still refuses local desktop execution. This does not claim that the hosted runner lacks an SDK.

Three unchanged local processes ran hidden-HWND layout checks for the unresolved latency concern. All twelve table/quote/details/list cases pass exact content/source and the unchanged 250 ms input bound; maximum gap is 89.58 ms. Details component initialisation ranges from 10.77 to 15.67 ms. Samples and analysis.json are in artifacts/native/construction-cfe11e5. These do not reproduce the earlier pauses or establish a cause or fix.

## Security evidence

The sealed b45246e..cf436b0 review is 475201d4-5562-4ffa-bc7a-166818c61152: all 101 changed inventory files and six supplemental source files reviewed with a fresh architecture model. One Low Word DOM allocation finding survives; the ContextColor hypothesis is rejected because the pinned Mermaid renderer removes the supplied paint forms before native conversion. A bounded original-build probe with 65,536 tabs allocated 12,416,296 cumulative managed bytes for a 6,142-byte DOCX under a 32,768-byte output limit. It did not measure peak memory or induce OOM. The subsequent 0c20f64 correction is regression-tested separately and does not alter that sealed result.

The earlier b45246e scoped review reported two Low image-allocation findings, subsequently corrected with part deduplication and repeated-image preflight/output limits. Those exact reports and regression evidence are retained in history. Neither review certifies the entire product, all dependencies or external environments.

## Acceptance limits

Required external evidence remains: production issuer/timestamp/revocation and signed upgrade, SDK-free clean Windows, physical screen-reader/DPI/multi-monitor behaviour, live Azure DevOps attachments, real historical schema migration, physical power-loss/removable-drive failures. The tested upgrade changes preview versions from the same source and is not historical migration certification.

Broader Word/template rendering, PDF complex-script shaping/BiDi/joined or colour emoji, explicit unsupported Mermaid/HTML/LaTeX forms and large-document continuous/page layout remain limited. Prior Office 16.0.20430.20092 Word/Excel destination checks establish only their recorded fixtures. Current windowless glyph/package checks cannot replace external destination validation. Intermittent initial WPF construction delays remain unresolved despite passing subsequent samples.

No downloaded installer, foreground native application, clipboard, Office instance or certificate-store test ran locally in this batch. No merge, release or tag changed. Earlier baseline, failures, corrections, run IDs and artefact paths remain in [history through 0c20f64](history/validation-report-through-0c20f64.md) and [earlier validation history](history/validation-report-through-9fddfd9.md).
