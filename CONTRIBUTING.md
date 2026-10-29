# Contributing to Lens Docs Studio

Local Markdown, Mermaid, and documentation studio.

The project direction is free software maintained with community contributions:
no subscriptions, paid licences or features restricted by a plan. The original
first-party portions enumerated in [LICENSING.md](LICENSING.md) use [MIT](LICENSE).
Dependencies, mixed content and the R3–R7 exclusions retain their own treatment.

Start a focused issue for a substantial change, then open a pull request against
`develop`. Keep one purpose per change and explain the user outcome, validation
and remaining limitations. Read `AGENTS.md` and, for native work,
`src/native/AGENTS.md`. Use British English. Preserve local documents, recovery,
workspace assets, settings and portable formats.

The web edition remains buildless and compatible with GitHub Pages. Use native
ESM and relative assets; keep the service-worker cache consistent. Run `npm ci`
and `npm test` (Chromium and Edge on Windows). The separate native edition uses
.NET 10/WPF; restore with `--locked-mode`, build Release and run
`dotnet test src/native/LensDocsStudio.Core.Tests -c Release -p:RestoreLockedMode=true`.
See [native validation](docs/native/validation-report.md) for the corpus, UIA and
disposable hosted installer checks. Never run installer/certificate-store tests
against a personal installation or weaken their host guards.

Contribute only material you have permission to submit. Identify copied/adapted
code, its exact upstream revision, copyright, licence, notices and modifications.
Keep upstream notices intact; do not add MIT headers to vendor files. Explain
asset/font/sample provenance and exclude customer documents, private templates,
credentials and personal screenshots. A commit author field is not proof of
ownership. Submit original contributions intended for the covered scope under
MIT and identify any exceptions explicitly. The maintainer must review rights
and update the explicit scope manifest for new paths; unreviewed files stay
BLOCKED. No CLA, copyright assignment or clearance of excluded material is implied.

The sole human maintainer, **@DouglasNLima**, reviews external contributions,
including source, lockfiles, dependency rights and CI changes, before integration.
For owner-authored changes, use a focused branch/PR, recorded source SHA/tree,
change/risk assessment, relevant tests and hosted CI, then a recorded owner
self-review decision before a merge commit. Refresh that decision after source
changes. No second human or approving GitHub review is required for the owner's
own PR; an AI review is technical evidence, not independent human approval.

Signing and public release each require separate explicit owner decisions;
a passing PR does not authorise either. Review the [governance policy](docs/open-source/governance.md),
[Code signing policy](docs/open-source/code-signing-policy.md),
[Security policy](SECURITY.md) and [Code of conduct](CODE_OF_CONDUCT.md).

Security-sensitive reports must use the route described in `SECURITY.md`.
Ordinary support belongs in [GitHub issues](https://github.com/DouglasNLima/local-docs-studio/issues).
