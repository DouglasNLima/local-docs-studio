# Native validation report

Status: implementation in progress; not certified for internal preview or production.

## Baseline (11 October 2026)

- Revision: 2cc3b5680958c9c553dad23856e6010c158fccd7 (origin/develop).
- npm ci: passed, zero reported vulnerabilities.
- npm test: passed; 254 Chromium/Edge tests in 9.3 minutes. Local static server exercised throughout.
- Static checks: 49 modules, 56 shell assets, 150 vendor assets and 55 external-dependency scans passed.
- dotnet build src/windows/LensDocsStudio.Windows.sln -c Release: passed, 0 errors, 6 pre-existing PRI qualifier warnings.
- Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -KeepSmokeRoot: passed.
- Evidence: artifacts/native/baseline/web-tests.log, legacy-build.log, legacy-smoke.log.
- Legacy fixture root: %TEMP%/lens-docs-studio-native-smoke-6551c34c70ad4597a8030c76c69e0f82.

## Acceptance items

Native Release build: passed with 0 warnings and 0 errors. Core tests: 24 passed (TRX in artifacts/native/tests/native-core.trx).

An actual WPF window was launched against a copied mixed.md fixture using isolated state in artifacts/native/gui/state. Computer Use observed native editor/preview, rendered diagram labels and shapes, outline and automation names. Ctrl+F selected the editor context; search selected Checklist; Escape restored editor focus. Save/reopen, other focus scenarios and further workflows remain pending.

The initial Mermaid corpus exercised 24 light/dark cases. 22 passed; the two reported failures were the deliberately incomplete autocomplete.mmd fixture (source: flo), now classified as expected-invalid alongside broken-mermaid.md. An expanded corpus is being run. PNG output was visually inspected for a simple flowchart; broader visual fidelity is still open.

Cold mixed-document preview observed in the UI: 4092 ms on this host. The isolated corpus's first native render was 717 ms; subsequent simple render 35 ms. These are observations, not performance certification.

Native UI automation, broad interactive GUI, export fidelity, performance, installer, update trust, clean-machine and CI certification remain pending. No unexecuted check is reported as passed.
