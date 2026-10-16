# Native integration readiness

This is the integration review record for `integration/native-wpf-candidate`, targeting `develop`. Validation is in progress; no readiness certification or merge approval is implied. Historical reports retain their original revision and environment labels.

## Source and ancestry

Remote references were fetched on 16 October 2026. Baseline `develop`: `2cc3b5680958c9c553dad23856e6010c158fccd7`. Initial integration merge: `7ab224b7c45722f6a59db20496faa7e72e479c92`. Its first parent is that baseline, its second parent is the cumulative accepted head `599a44eaa2e11ffbdb020b76a3059b4b37743154`, and its tree is identical to the accepted head. No conflict resolutions, replayed commits or changed author attribution were required.

Every row's previous head is its actual merge-base and an ancestor (`git merge-base --is-ancestor` exit 0). Left/right exclusive commit counts are shown relative to the previous row.

| PR | Head | Previous / head exclusive commits |
|---|---|---|
| Baseline | `2cc3b5680958c9c553dad23856e6010c158fccd7` | — |
| #21 | `9c7454eeb892614af93881b762a1b6aecc3259d2` | 0 / 177 |
| #23 | `c566144529c3c0e4393c1604544201b936e4a6a2` | 0 / 7 |
| #24 | `5a373a412e86b93ea8af1b9e5b867250e3d23ee1` | 0 / 38 |
| #25 | `42c3fc614feb6db6f69a242312604e46ff76c238` | 0 / 21 |
| #26 | `eb81939835a1bb2a92cf76570de77d8d69e571ff` | 0 / 20 |
| #27 | `47e78d6bff490a9b0c0b5cb2022dab262fe14e5e` | 0 / 16 |
| #28 | `599a44eaa2e11ffbdb020b76a3059b4b37743154` | 0 / 22 |

PR #22 head `83eeb9e489bd3c9513f9ffb17fe0b833c1a088ff` is not an ancestor. Its `src/native/spikes/ContinuousViewer*`, measurement script and separate workflow are excluded. All original PRs remain Draft, open and unmodified.

## Integration changes

The initial cumulative diff has 679 files, 70,243 insertions and 67 deletions. The application is retained as implemented. Integration changes enable native push checks on the named candidate branch, enable desktop UX PR checks targeting `develop`, and add pinned original-package upgrade tests. Web checks continue on both browsers. Pages deployment remains restricted to the repository default branch outside pull requests.

## Regression evidence

Direct local evidence on the initial integrated tree: Release build passed with zero warnings/errors; 1,229 Core tests passed, zero failed/skipped; static integrity passed (50 modules, 57 shell assets, 150 vendor assets, 56 external-dependency scans). Hosted native rendering, authoring, fidelity, UIA, High Contrast, installer, package rejection and browser results are pending. Historical PASS counts are not current acceptance.

## Historical compatibility and provenance

All six original installer/ZIP pairs were preserved under the ignored `artifacts/integration/historical/` engineering directory. Their SHA-256 values match the retained original sidecars. No installer or application was executed on the user's desktop. Exact source/run/artifact and independently recorded package pins are in [`native-historical-previews.json`](../../tests/fixtures/native-historical-previews.json). These are original branch-source packages, not rebuilds or PR merge packages.

| Original | Original source | Upgrade status |
|---|---|---|
| `.317` | `dd6c4c33f1a6fa86206e76b2153afcc7b151a523` | Pending, highest priority |
| `.393` | `5a373a412e86b93ea8af1b9e5b867250e3d23ee1` | Pending |
| `.433` | `ef392b0df46243805e5596d160d2c077a1c9f8dd` | Pending |
| `.473` | `36b331ac6c2b29ebcf53e1a16735a5696ae8b2ac` | Pending |
| `.497` | `302096110631c6ad5ad12cdc4b44587cc293ccf6` | Pending |
| `.531` | `4940c58a542b635e7e008eeaf31b03ca4ec5878a` | Pending |

Disposable upgrade jobs build each original revision's validation harness, install the pinned original binary and produce synthetic state through its actual UI/services. They then install the integrated package, restore those tabs/text/settings/workspaces, compare retained file hashes and exercise uninstall/reinstall/recovery. Building a historical harness does not rebuild or relabel the historical application package. Full feature and migration matrices, exact results and limitations will follow hosted execution.

## Security, performance and production boundaries

The focused cumulative security review is in progress. .NET 10/WPF, AvalonEdit, Markdig, Mermaider, SharpVectors and FlowDocument remain the native runtime. The browser edition stays buildless and independent. No rejected renderer, embedded browser or required local server is authorised.

The 250 ms Continuous input diagnostic target remains unmet in representative large documents. Intermittent construction delays remain unresolved. Consolidation does not claim either fixed. Actual performance observations are pending.

SDK-free clean Windows certification: **BLOCKED**; hosted SDK-equipped Windows runners do not establish it. Production signing identity: **BLOCKED**. Unsigned in-app update execution remains fail-closed. This work can establish an internal unsigned preview only. Physical DPI/multi-monitor/accessibility and Microsoft Word visual fidelity are not established by schema or hosted UI validation.

## Package and workflow identity

Fresh packages, independent verification receipts, exact SHA-256 values, source revision and workflow URLs are pending. Branch-source packages and PR merge-checkout packages will remain separately identified. A selected package will not be relabelled after documentation-only commits.

## Proposed later merge strategy

Prefer Option A: review this consolidated Draft PR, then authorise a normal merge preserving the original commits after all mandatory checks and the explicit restrictions are accepted. The chain is cumulative with no divergence, making one integrated target less error-prone. The trade-off is the large review diff; thematic original PRs remain available for review and later administrative closure by separate authorisation.

Option B: merge the original stacked PRs in dependency order, retargeting and validating each step. This gives smaller thematic reviews but requires multiple intermediate states, base changes and validations. No strategy has been executed against `develop`, and original PRs must not be closed or retargeted automatically.
