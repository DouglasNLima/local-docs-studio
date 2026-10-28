# Contributing to Lens Docs Studio

Local Markdown, Mermaid, and documentation studio.

The project direction is free software maintained with community contributions:
no subscriptions, paid licences or features restricted by a plan. Adoption of an
open-source licence is pending the [licensing review](docs/open-source/licence-audit.md).
This document does not grant a licence or imply that MIT has already been adopted.

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
ownership. While licence approval is pending, new contributions require an
explicit rights review; no automatic inbound licence or CLA is implied.

Maintainers review source, lockfiles, dependencies and CI changes. Signing and
publication require separate human approval; a passing PR does not authorise
either. Review the [governance proposal](docs/open-source/governance.md),
[Code signing policy](docs/open-source/code-signing-policy.md),
[Security policy](SECURITY.md) and [Code of conduct](CODE_OF_CONDUCT.md).

Security-sensitive reports must use the route described in `SECURITY.md`.
Ordinary support belongs in [GitHub issues](https://github.com/DouglasNLima/local-docs-studio/issues).
