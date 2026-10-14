# Windows preview readiness

Recorded 14 October 2026. This is the authoritative report for the dedicated `release/native-preview-readiness` branch, based on **`9c7454eeb892614af93881b762a1b6aecc3259d2`**. Draft PR 21 remains the reconstruction review; Draft PR 22 remains an isolated **NO-GO** experiment. The production application remains C#/.NET 10/WPF with FlowDocument preview, no embedded browser/JavaScript/local server, and separate browser/PWA/legacy Windows products.

**Acceptance in progress: no new preview is recommended until its exact packages and mandatory hosted checks are verified.** This report will record the source, version, Actions identities and independently downloaded package hashes after validation; no historical package is relabelled.

## Adopted evidence and corrections

PR 21 head `9c7454e` passes native push/PR [38444910458](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38444910458) / [38444916000](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38444916000) and web push/PR [38444910182](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38444910182) / [38444915963](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38444915963). The [engineering report](engineering-completion-report.md), [performance report](performance-and-responsiveness.md), [handoff](continuation-handoff.md), [packaging](update-and-packaging.md), [parity matrix](functional-parity-matrix.md) and [experience improvements](native-experience-improvements.md) retain prior scope and failures. PR 22's feasibility report/ADR were reviewed in its isolated branch; no code or dependencies were copied.

Review found no dedicated first-run onboarding window: existing menus, toolbar and offline guide provided the entry points. Empty drafts now offer optional native workspace/document/help actions and concise guidance; typing hides the strip. Explicit Continuous now displays non-blocking advice using the existing Automatic-mode characteristics (262,144 source characters or 1,000 mapped elements). It never overrides Continuous or changes source/selection. No performance fix is claimed.

Packaging now requires a clean committed checkout, records branch versus PR merge-checkout provenance, excludes development symbols and bundles the [preview guide](windows-preview-guide.md) and [release notes](windows-preview-release-notes.md). Independent read-only verification and negative controls check hashes, metadata, runtime and excluded payloads. CI triggers include the requested branch and PR base; all previous gates and timing thresholds remain intact.

## Environment and outstanding gates

The current desktop is Windows 11 Home build 26300 with .NET SDK 10.0.401 and development tools. One read-only infrastructure check found no accessible disposable Windows VM tooling/image; running virtualisation services do not establish one. The remote Linux host is unsuitable for WPF certification. **Clean-machine certification: BLOCKED.** No newly built native app/installer was installed or launched on this ordinary desktop; new GUI, installer, clipboard and signing-fixture execution belongs to disposable hosted Windows.

Unsigned installer/update trust remains unchanged: no production certificate, signing service, verified publisher, approved thumbprint or pinned manifest key. In-app unsigned download/execution is blocked. Positive production-signed upgrade is BLOCKED. No public release, tag movement, merge, telemetry, certificate-store or desktop association change is part of this goal.

Continuous's unchanged **250 ms** objective remains **FAIL** in recorded large/complex cases; the **587.9933 ms** details construction delay remains unresolved. Automatic still chooses Pages for large sources. Prior Word open/edit/save/reopen has its original scope; live Excel paste remains BLOCKED by first-run global settings and Azure DevOps by the unavailable authorised disposable work item. Physical accessibility/displays, hardware interruption, broader compatibility/fidelity and PdfPig internal allocations remain limited.

Security review will record current dependency audits and applicable regressions. The aggregate-PDF correction remains present: bounded page/aggregate characters and UTF-8 output, pre-append rejection and cancellation. Previous sealed/protected scan scopes are immutable and are not represented as current-source certification.

## Pending acceptance record

| Gate | Current status |
| --- | --- |
| Local locked restore / Release build / baseline Core | PASS: 1,057 tests; zero failed/skipped; build zero warnings/errors |
| Local static integrity | PASS |
| Final hosted native/rendering/workflow/UIA/lifecycle gates | NOT RUN on committed readiness source yet |
| Independent installer/ZIP verification | NOT RUN on committed readiness source yet |
| Production Unicode/malformed-input probes | NOT RUN: isolated hosted processes required |
| Final hosted browser regression | NOT RUN on committed readiness source yet |
| Clean Windows and production signing | BLOCKED |

Final classification remains pending actual hosted evidence. BLOCKED and NOT RUN are never reported as PASS.
