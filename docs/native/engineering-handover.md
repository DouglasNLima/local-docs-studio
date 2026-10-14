# Native Windows engineering handover

For the active desktop UX goal, use [desktop-ux-modernisation.md](desktop-ux-modernisation.md) and [draft PR 24](https://github.com/DouglasNLima/local-docs-studio/pull/24). The existing reconstruction and preview-readiness PRs remain separate and unchanged.

Read the [engineering delivery report](engineering-completion-report.md) for the current architecture, features, Windows improvements, parity/compatibility, installer/updater, exact tests, performance, limitations, packages, branch/PR, Actions and external prerequisites. Its table accounts for all 28 original acceptance conditions and distinguishes internal preview from production certification.

Application baseline `7008751a52cb9a782619e97aac27cd6bcc899bdf` has four successful pipelines and independently checked previews .285/.286. Branch `feature/native-wpf-rebuild` and [draft PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21) against `develop` contain the delivered work; the report/regression revision follows that baseline and must keep its mandatory checks green. No merge, public release or tag change is authorised.

The confirmed signing identity is empty. Keep signing pluggable and unsigned update download/execution blocked. Clean-machine, production-signed upgrade, physical accessibility/displays and other listed destination/historical-data/hardware scenarios remain unverified. Ordinary defensive validation does not complete the frozen protected security scan.

Use the [continuation handoff](continuation-handoff.md) for future acceptance work. The [historical handover](history/engineering-handover-through-7008751.md), [validation history](validation-report.md) and ignored local artefacts retain previous results/failures. Later successful samples do not prove elimination of the unresolved intermittent construction pause.
