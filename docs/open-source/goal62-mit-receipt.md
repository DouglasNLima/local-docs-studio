# Goal #62 — Stage A: scoped MIT adoption

Status: **IMPLEMENTED in the feature branch; NOT MERGED into develop**.
The exact final PR SHA/tree and terminal validation are supplied by the
[consolidated receipt](goal62-receipt.md) and PR body.

The root LICENSE contains unmodified MIT terms and the approved
`Copyright (c) 2026 DouglasNLima`. Sole ownership of original personal-project
material and the R1/R2 grant with R3–R7 exclusions were explicitly approved in
Goal #62. No reconfirmation or broader copyright assertion is requested.

## Rights validation and exact scope

The [explicit grant manifest](configuration/mit-scope.json) is derived from the
approved Goal #60 R1/R2 ledger at baseline
`0890ec9b2ade8af24b9b3fca6cbe98cd7fd329ba`, plus enumerated original Goal #62 work.
The [current ledger](evidence/goal62/rights-ledger.csv) hashes canonical Git bytes
and records every path, including excluded material. New paths do not receive
MIT automatically. The ledger's own bytes are bound by the final Git tree.

The original project declaration, existing path audit, attribution scan and
preserved upstream hashes support review; none proves legal title independently.
The scan found the already documented BoundedErLayout Mermaider adaptation; its
R6 status and Nullean notice remain intact. CommandIcons.xaml remains R4.
Additional conservative exclusions are dedicated DocsSiteTemplates resources,
the complex-ER fixture generator, native built-in-catalogue.json and the issue
submission template (R3), plus native-853-original-evidence.json (R5).
No excluded content was edited or individually relicensed.
The positive grant contains 1,076 paths for original portions, with three
complete-file SPDX additions. The baseline preservation check covers 2,423 files.

Mixed files grant original portions only. Examples in
`Sync-ContentCatalogue.mjs`, `MermaidDiagramCatalogue.cs`, test harnesses and
documentation remain excluded; control logic is distinct from copied sample
payloads and artwork. No full-file SPDX was added to these mixed files. New
SPDX headers are restricted to the positively identified original tooling files
listed explicitly in the manifest. R7 is a usage boundary; private content was
not inspected.

## Distribution and metadata

README, contribution/governance guidance and package metadata now state the
restricted operative grant. npm uses `SEE LICENSE IN LICENSING.md`, not a
blanket MIT package assertion. Native first-party copyright metadata uses the
approved notice with an adjacent scope comment; no whole-binary licence is set.
Historical Goal #56/#58/#60 receipts and all upstream evidence remain unchanged.

Independent review identified an existing native builder that would copy the
new LICENSE alone. The fix requires LICENSE plus FIRST-PARTY-SCOPE.json and a
native LICENSING.md disclosure linked to the exact source SHA. Missing scope
fails before any notice copy. Pages, offline cache and legacy static-asset staging
also retain the licence and scope. No installer identity, signing policy,
publication trigger or production trust changes.

The [preserved-byte inventory](evidence/goal62/preserved-files.json) protects
upstream/vendor notices, historical evidence, update/signing and Continuous
controls. Metadata/notice delivery is the only packaging delta. Per-file binary
rights remain NOASSERTION where unresolved; a source grant does not license all
compiled resources, fonts, apphost/runtime or Inno code.

## Validation and remaining release gaps

Positive checks require exact MIT bytes/notice, explicit manifest, canonical
ledger hashes, correct metadata and original upstream notices. Negative controls
reject changed notice/terms, absent licence, blanket MIT, removed exclusions,
vendor/fixture/artwork/evidence grants, unknown new paths, missing attribution
and missing native scope. A temporary native payload test checks actual copied
bytes and exact source reference, without installation or signing.

Fresh Highlight.js reconstruction and upstream archive validation must match the
existing historical bytes. Hosted final-source checks and actual preview-payload
inspection are recorded in the final receipt; the existing .957 SBOM is historical,
never presented as a Goal #62 binary. Known Mammoth/PDF.js notice closure, excluded
artwork/examples, fonts/provider interpretation and legacy notice gaps remain
BLOCKED for affected redistribution/release. Continuous 250 ms FAIL and clean
Windows/signing limitations are unchanged.
