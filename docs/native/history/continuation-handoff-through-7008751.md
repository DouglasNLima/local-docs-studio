# Native Windows development continuation handoff

Recorded on 13 October 2026. This handoff supersedes older pending statements in the engineering records. The development baseline is `23bcb834ec1c445fd1b0603fa19c3a69e0ddd67e`; a later documentation-only commit may contain this handoff. Re-read Git status and current CI before making changes.

## Resume the existing work

Continue the existing Lens Docs Studio native Windows rebuild, without recreating the project or repeating completed implementation. Deliver the next evidence-backed acceptance milestone through implementation, focused regressions, commits, push and CI verification. Preserve the original objective; production acceptance and the overall reconstruction remain incomplete.

- Working checkout: `C:\Users\dougl\.codex\worktrees\native-wpf-rebuild\MarkdownReader`.
- Original checkout: `C:\Code\MarkdownReader`; do not switch to it or overwrite it inadvertently.
- Branch: `feature/native-wpf-rebuild`.
- Draft [PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21), open against `develop`.
- The working tree was clean before this handoff was added; the development baseline was already pushed.
- Original specification: [reconstruction brief](reconstruction-brief.txt), especially the final 28 acceptance conditions.
- Read root `AGENTS.md` and `src/native/AGENTS.md`, then the [parity matrix](functional-parity-matrix.md), [validation report](validation-report.md) and [architecture decisions](architecture-decisions.md).

The persisted goal belongs to thread `01a12ae6-11c3-7600-a953-76b371529b11`. Its latest tool record says `blocked`, although the user explicitly requested resumption and useful implementation subsequently progressed. Retain its objective; do not create a replacement goal or claim completion from that status. The user reports a previous Daybreak/model compatibility interruption. Ordinary defensive code review and validation are authorised using available tools; unavailable protected checks must remain unclaimed.

## Constraints that remain in force

- Native edition: C#, .NET 10, WPF/XAML and native controls. No embedded browser, JavaScript runtime, Node dependency or local web preview in the native product.
- Preserve the buildless browser/PWA/GitHub Pages product and the separate legacy WinUI/WebView2 shell.
- Portable UTF-8 Markdown and images; preserve unsaved drafts, baselines, recovery, workspace context and export settings. Reject workspace escapes, reparse points and active content.
- Optional artefact metadata remains untrusted, passive import context. Never infer evidence levels or promote candidate findings to confirmed findings.
- British English for first-party source, UI and documentation; neutral surfaces with restrained `#FF883E` accents. Generic tagline: Local Markdown, Mermaid, and documentation studio.
- No merge, release publication, tag movement or invented production identity.
- Signing configuration is confirmed empty: no Authenticode certificate, signing service, thumbprint or verified publisher. Keep signing pluggable. Internal packages are unsigned; automatic execution of unsigned updates remains blocked by the embedded trust policy.
- Do not bypass hosted-desktop/Office/installer/certificate guards or fabricate runner environment markers. Run actual desktop and trust suites on disposable GitHub-hosted Windows. Local windowless/hidden-control checks are available. The previous continuation recorded an isolated local WPF launch; it is not an interactive-suite or clean-machine certification.
- Ask the user only for genuine external blockers. Do not repeatedly ask for the already-confirmed signing configuration.

## Latest implementation milestone

The following commits are already implemented, committed and pushed. Do not redo them:

| Commit | Result |
|---|---|
| `840cf15` | Validation-only preview-stall watcher. Separate owned-process helper, minimal thread/stack captures after 300 ms, unchanged 250 ms acceptance gate, unrelated-target refusal and primary-failure preservation. |
| `c226533` | Bounds generated DOCX import text before buffer growth, including intermediate links, escaped runs, code fences and streamed table content. Sixteen regressions cover limits and unchanged input. |
| `5976b24` | Preserves supported direct Word strikethrough/underline, including combinations, links and tables. Double strike becomes single strike with a note; explicit off remains plain text. Nine regressions and actual WPF decoration checks. |
| `23bcb83` | Recognises already-supported UI Automation timeout HRESULTs wrapped in `TimeoutException`. Nine deterministic checks retain observation deadlines, propagate unrelated errors and preserve persistent failures. Input actions and installer assertions are not replayed or waived. |

The reproduced DOCX defect was shared hyperlink expansion: 4,200 references to one 8,192-character target generated 34,524,000 Markdown characters from a 6,167-byte package. The earlier description-expansion candidate was a hypothesis; do not substitute it for this executed reproduction. Current `WordImportLimits.MaximumTextCharacters` defaults to 33,554,432 UTF-16 characters. `WordMarkdownBuilder` checks before appending and chunks escaping. The original input and previous document remain intact on rejection, and a subsequent normal import succeeds in actual hosted UI checks.

Relevant implementation and regressions:

- `src/native/LensDocsStudio.Core/Imports/{DocumentImportService,WordMarkdownBuilder,WordHyperlinkReader,WordListMarkdownWriter}.cs`.
- `src/native/LensDocsStudio.Core.Tests/{WordImportTextLimitTests,WordDecorationImportTests}.cs`.
- `src/native/LensDocsStudio.Native.Validation/{NativeWordImportFixtureChecks,NativeWordImportPreviewChecks,NativeUiWordImportChecks,NativeObservationWaitChecks,NativeUiChecks}.cs`.

Earlier completed workflows include the source-preserving table grid, literal badge/shortcut insertion, workspace roots retained per tab, native preview/rendering, safe file operations, import/export, libraries, installer and consent-based updater. See the parity matrix rather than treating this list as whole-product certification.

## Verified CI and packages

All four development-baseline runs completed successfully. Their status was checked again when preparing this handoff:

| Pipeline | Run |
|---|---|
| Native PR | [38355992281](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38355992281) |
| Native push | [38355987905](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38355987905) |
| Web PR | [38355992282](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38355992282) |
| Web push | [38355987897](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38355987897) |

Persisted evidence records 1,011 core tests, none skipped; 40 native corpus cases with zero failures; nine timeout regressions; 262 browser cases per web run; four 66-check desktop contexts per native run; complete installer lifecycle; 20 preserved state files; restored sessions; eight verified installer/ZIP files. Mandatory workflows were not disabled or relaxed. Downloaded TRX, corpus and lifecycle counts were read again for this handoff.

Evidence is local and ignored by Git:

- `artifacts/native/continuation-23bcb83-result.json` and `ci-23bcb83-results.json`.
- `artifacts/native/ci-23bcb83-{pr,push}/`, including package verification, runtime reports, TRX, UI and lifecycle evidence.
- `artifacts/native/docx-text-growth-reproduced/before.trx`, `docx-text-core/core.trx`, `docx-text-validation/`.
- `artifacts/native/word-decorations-final-core/core.trx`, `word-decorations-final-preview/`.
- `artifacts/native/uia-observation-before.log`, `uia-observation-after/native-observation-wait.json`, `uia-observation-full/`.
- `artifacts/native/local-docx-ready-7c2b6b60d1d7409d8e5f20d5ce555096/launch.json`.

Ignored evidence may be absent in a different checkout or computer; download the matching `native-validation` artefact instead of inventing a local result. Successful CI alone is distinct from downloaded package inspection.

Latest PR-merge installer:

- `artifacts/native/ci-23bcb83-pr/packages/0.1.1-preview.280/LensDocsStudio.Native-0.1.1-preview.280-win-x64-Setup.exe`.
- Clean package revision: `216ded588f166607c79447c8f6544c371fd38745`.
- 58,881,866 bytes; SHA-256 `97D3B2B09B3DE8DB75A129B7EFEEF5D16E3CA4A8FA5BFD6A48379FA2765D1D35`.

Latest branch installer:

- `artifacts/native/ci-23bcb83-push/packages/0.1.1-preview.279/LensDocsStudio.Native-0.1.1-preview.279-win-x64-Setup.exe`.
- Clean package revision: `23bcb834ec1c445fd1b0603fa19c3a69e0ddd67e`.
- 58,880,896 bytes; SHA-256 `DEE93F10C632755F6A197E64C441741AF2EECFE216B7C0B3E704DC2150D15A6E`.

Both installer hashes were recalculated for this handoff. Both remain `NotSigned` internal previews. A later documentation-only commit does not change these package revisions or establish new package certification.

## Retained failures and review state

- Native `5976b24` push run `38354661411` failed installer discovery after earlier checks passed; lifecycle was skipped. Original evidence is under `artifacts/native/ci-5976b24-push-failure`. The later `23bcb83` runs validate the narrow wrapper correction. Do not rewrite the failed run as successful.
- The intermittent nested-details construction pause remains unresolved. Earlier `77be570` PR run `38344048333` recorded 587.9933 ms input delay. Later passing samples and empty stall-capture lists do not prove the cause eliminated; the 250 ms gate is unchanged.
- The sealed `b45246e..cf436b0` security scan remains immutable. Its Low Word-code allocation issue was fixed separately; later changes are outside that sealed review.
- A second scan, `36e5ad44-ba7f-4cc1-8f8d-b24b9b629856`, remains **running in validation** for frozen `cf436b0736132c6b9abf583ae2555cf4892c0237..840cf150f4afb2dee49e22e4b8d0560dbc070e1b`. It is not a completed review of the current branch.
- That scan has one canonical candidate, `candidate-7f466295e015abb0`, for possible shared drawing-description amplification. Discovery accounted for 62 source inventory entries and 19 supplementary changed files. Candidate runtime validation, final architecture reconciliation and finalisation were not recorded as completed. Preserve its identity and source range; do not recreate or silently retarget it.
- Its independent architecture artefact is `artifacts/01_context/independent-architecture.json` in the managed scan directory, SHA-256 `2eb4df72921b78e5248accf0273038ac1e95da4c70e48c97b85b90d608134ea9`. The managed scan directory is `C:\Users\dougl\.codex\state\plugins\codex-security\scans\MarkdownReader\840cf150f4afb2dee49e22e4b8d0560dbc070e1b_20261013T045954Z_ah7qo77e`.
- Use the plugin's permitted read/write tools for managed review state. If protected access is unavailable, preserve the open scan and report the exact limitation; do not read around access controls or claim a sealed result. Ordinary authorised source review, regression tests and implementation can continue independently.

## Next unfinished work

1. Inspect the current branch, tree, recent commits, goal and CI. Adopt this baseline and retain all original failed evidence. Check whether documentation-only CI from this handoff is pending.
2. Reconcile the remaining acceptance conditions against the original brief and current code. Update stale top-of-document pending statements using verified `23bcb83` evidence; retain historical sections with their original meaning.
3. Continue a justified implementation slice. The intermittent preview-construction pause and broader continuous/page-layout performance remain engineering work. The owned-process diagnostics are available if the existing gate fails again; do not relax it or rerun unchanged failures until green.
4. Supported Word import still excludes inherited run-decoration formatting and additional underline styles. Broader Word template/destination fidelity, PDF complex shaping/BiDi/joined emoji/tagging, and explicitly unsupported Mermaid/HTML/LaTeX forms remain documented limits. Select the next concrete contract from the brief and parity matrix rather than inventing a new requirement.
5. For meaningful changes, reproduce the defect with bounded fixtures, implement in the owning service, run focused tests and the required broader checks, commit, push, and inspect actual CI/package results. Record skipped or blocked checks accurately. Update the draft PR description to the final implementation and evidence; its old text still mentions preview.269.

External production acceptance remains unavailable: an authorised production signing identity and positive signed upgrade; SDK-free clean Windows installation; physical screen-reader/DPI/multi-monitor evidence; actual supported Office/Azure DevOps destinations; genuine historical user-schema fixtures; and physical power-loss/removable-drive scenarios. These are not satisfied by mock trust, package metadata, hosted aggregate green CI or synthetic same-source upgrades. Do not claim production readiness or overall goal completion.

## Working commands

Use PowerShell 7 at `C:\Program Files\PowerShell\7\pwsh.exe`, `login: false`, the working checkout above and `$ErrorActionPreference = 'Stop'`. Do not supply `sandbox_permissions` in this environment.

```powershell
git status --short
git log -6 --oneline
dotnet build src/native/LensDocsStudio.Native.slnx -c Release
dotnet test src/native/LensDocsStudio.Core.Tests -c Release --no-build
dotnet run --project src/native/LensDocsStudio.Native.Validation -c Release --no-build -- artifacts/native/<new-evidence-folder> .
npm run test:static
```

Actual desktop checks belong to the existing hosted workflow. No `npm run test:unit` script exists. Do not run browser suites or passed broad checks again without a relevant new change, failure or unresolved question.

GitHub writes use the already-authorised `DouglasNLima` account. Obtain its credential with `gh auth token --hostname github.com --user DouglasNLima` into a temporary process environment variable without printing it, disable interactive Git credential prompts, perform the branch push/PR update, then clear that variable. Do not change the global active account or persist credentials. For multiline PR text, use a body file. Preserve the draft PR and `develop` base.

## Prompt for the next chat

> Continue the existing Lens Docs Studio native Windows rebuild from `docs/native/continuation-handoff.md` in `C:\Users\dougl\.codex\worktrees\native-wpf-rebuild\MarkdownReader`. Inspect the latest persisted state and adopt the existing work. Keep the WPF, portability, data-preservation, signing and unsigned-update controls; preserve the web product and legacy shell. Proceed autonomously through the next unfinished acceptance milestone, including implementation, regressions, commits, push and CI verification. Do not recreate completed work, bypass unavailable checks, merge, publish releases or claim production completion. Ask for intervention only for a genuine external blocker.
