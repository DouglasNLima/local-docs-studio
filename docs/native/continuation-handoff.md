# Native Windows development continuation handoff

Recorded on 13 October 2026. The [engineering delivery report](engineering-completion-report.md) is the current acceptance record, including all 28 original conditions and evidence-backed residual gates. The [previous handoff](history/continuation-handoff-through-7008751.md) retains earlier state without superseding that report.

## Adopt the delivered state

- Worktree: `C:\Users\dougl\.codex\worktrees\native-wpf-rebuild\MarkdownReader`; original `C:\Code\MarkdownReader` remains separate.
- Branch: `feature/native-wpf-rebuild`; draft [PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21), base `develop`.
- Application baseline: `7008751a52cb9a782619e97aac27cd6bcc899bdf`; PR checkout merge: `22ce42192ecfbacbc5116c6a7498c67f608c2283`.
- Four baseline pipelines succeed: native push/PR `38367786368`/`38367792092`, web push/PR `38367786419`/`38367792045`.
- Independently checked packages/evidence: `artifacts/native/ci-7008751-{push,pr}`; previews .285/.286. Read the report for exact hashes/source and download commands.
- Baseline native evidence: 1,035 Core tests, 40 corpus cases, four 66-check desktop contexts, full lifecycle, 20 preserved state files and two restored sessions. Web: 264 cases per run. Local final Core: 1,037, including two additional bounded shared-description regressions.
- The final documentation/regression revision follows the application baseline. Inspect its actual Git status, commit and latest PR checks; never relabel baseline packages as that later revision.
- Existing goal thread: `01a12ae6-11c3-7600-a953-76b371529b11`. Its status is separate from production certification. Preserve the original objective and inspect the actual latest record; do not create a replacement goal.

## Constraints

Keep .NET 10/WPF/native editing and preview, portable Markdown/assets, guarded data/recovery, passive untrusted artefact metadata, British English and restrained Lens appearance. Preserve the buildless web/PWA/Pages product and legacy shell. Do not merge, publish releases, move tags or invent a production identity.

The user confirmed no signing certificate/service/thumbprint/publisher. Keep pluggable support, empty production trust and blocked unsigned update download/execution. Do not repeat a credentials question already answered.

Actual foreground/clipboard/Office/installer/certificate suites belong on genuine disposable hosted Windows. Never fabricate runner markers or bypass guards. Local headless browser and windowless checks remain available. Ask only for a genuinely necessary external resource or decision.

## Remaining acceptance work

The report's residual-gate table is authoritative: production identity/pins and positive signed upgrade; SDK-free Windows; physical accessibility/displays; actual supported Office/Azure DevOps destinations; real historical schemas; physical power-loss/removable-drive scenarios; bounded compatibility and broader performance. A later passing layout sample does not resolve the retained `38344048333` details failure at 587.9933 ms. Continuous list attachment remains synchronous. Do not claim a universal timing or fidelity guarantee.

Ordinary source review and regression validation are authorised independently of Daybreak. Protected scan `36e5ad44-ba7f-4cc1-8f8d-b24b9b629856` was last recorded running in validation for frozen `cf436b0..840cf15`; attack-path and final completion are unrecorded. Do not retarget, reseal, read around access controls or claim that review covers current code. The separate earlier sealed review remains immutable.

Inspect current state before changing anything. Resume only work supported by an actual outstanding acceptance case/resource; retain original failures. When fixing a reproduced defect, make the focused change, run meaningful regressions, commit/push and verify the relevant CI. Do not recreate completed milestones or broaden unsupported formats without a concrete requirement.
