# Scoped MIT decision packet — non-operative

[Goal #60](https://github.com/DouglasNLima/local-docs-studio/issues/60) accepts the
owner's personal-project declaration: no employer/customer code, no applicable
employer/customer contractual restriction, free open-source intent and one human
maintainer. These decisions need no reconfirmation. They do not identify the
exact public copyright holder or clear third-party/visual/example material.

The [current ledger](evidence/goal60/rights-ledger.csv) names every current
tracked/proposed path. R1/R2 rows form the **candidate affected-file list**, subject
to approval of that exact set and its exclusions. No row grants a licence.
The final PR SHA/tree binds the paths to bytes. The
[Goal #58 ledger](evidence/goal58/rights-ledger.csv) remains unchanged historical
evidence, superseded for current classification.

| ID | Proposed treatment | Disposition |
|---|---|---|
| R1 — implementation | Browser, native .NET/WPF, project Windows shell, original tests/build scripts/tooling/configuration, excluding R3–R6 material | OWNER APPROVAL REQUIRED for exact scope and notice |
| R2 — documentation | Original first-party guides/policies, excluding preserved upstream text, quotations and uncleared content | OWNER APPROVAL REQUIRED for exact scope and notice |
| R3 — fixtures/examples | Fixtures, registries, native corpus/samples and example/template/snippet content excluded by default, including embedded examples within R1/R2 files | OWNER APPROVAL REQUIRED for individual originality or appropriate upstream-grant evidence; synthetic names do not prove rights |
| R4 — artwork/branding | Name, logos, graphical identity, screenshots and other uncleared visuals outside default MIT scope | OWNER APPROVAL REQUIRED for separate copyright/branding treatment; trademark reservation alone does not reserve visual copyright |
| R5 — historical evidence | Recorded outputs, captured content, screenshots and upstream notice evidence retain provenance/original rights | NOT APPLICABLE to default first-party grant; any publication/relicensing change needs separate review |
| R6 — dependencies/adaptations | Vendored web code, NuGet, pinned Mermaider, fonts/resources, .NET/WPF runtimes, Inno components and legacy Windows App SDK/WebView2 binaries retain upstream terms | NOT APPLICABLE to first-party MIT; preserve all grants/notices |
| R7 — user content | Imported/created Markdown, templates, assets, workspaces, local state and exports retain their owners' rights | NOT APPLICABLE; app use transfers or relicenses no content |

R7 is a usage boundary, not an assertion that private workspaces were inventoried.
No private user content was inspected.

An attribution scan found
`src/native/LensDocsStudio.Core/Rendering/BoundedErLayout.cs` outside the
PinnedMermaider folder. Its header and that folder's notice establish a Mermaider
adaptation; the new ledger explicitly classifies it **R6**. The old R1
classification was not permission. Preserve Nullean's existing MIT attribution.
Native machine evidence is R5; visible images also remain subject to R4 clearance.

Original test harnesses are R1 candidates, but embedded sample payloads are not
automatically cleared. A text-marker scan is bounded evidence, not proof of
originality. Upstream licence text/quotations within R2 documents retain their
own rights. No path rule overrides these content-level exclusions.

## Exact proposed notice

```text
Copyright (c) 2026 DouglasNLima
```

This repeats the earlier candidate to make the decision concrete.
**DouglasNLima is a GitHub handle, not a verified legal copyright holder.**
Approve only if correct, or replace with the correct individual/entity and any
additional holders. Observed commit years are 2026; approve/correct the actual
creation year/range. See the [complete MIT candidate](licence-proposal.md).

No root LICENSE, package licence metadata or new first-party SPDX identifier is
added. Personal development does not silently authorise adoption.

## Decision and subsequent adoption

Approve/correct the notice and the R1/R2 paths in the frozen ledger **with all
content-level and R3–R7 exclusions**; list any further removals or individually
cleared examples. R3/R4 additions require provenance evidence. Silence is not approval.

After approval, a separate adoption change adds root LICENSE with the unmodified
approved MIT text, LICENSING.md with the cleared scope/exclusions and frozen
path reference, appropriate SPDX identifiers only on verified first-party files,
and accurate README/package metadata. Preserve upstream dual grants, notices and
asset conditions; unresolved file conclusions stay NOASSERTION. Do not label the
whole installer or all included binaries MIT.

MIT permits recipients' commercial reuse/resale. Free official distribution is
project policy, not a restriction on recipients. No endorsement or impersonation
permission is implied. **MIT adoption: OWNER APPROVAL REQUIRED, non-operative.**
[Dependency redistribution gaps](third-party-closeout.md) remain unresolved and
separate from the first-party candidate. Engineering checks do not certify title.
