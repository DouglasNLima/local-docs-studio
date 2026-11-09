# PR #81: nine-file Flowchart provenance decision

**PROPOSED — NOT APPROVED — NOT ACTIVE.** This packet requests one bounded owner decision. The validated implementation, protected hashes and effective governance policy remain unchanged.

Approve or reject proposal **pr81-flowchart-provenance-nine-v1**, the [candidate JSON](candidate-refresh.json) with canonical Git SHA-256 **97582b1d4e1c7bf1d2f8762bf812a50c2b8aae976be1491daacb9e75bb8abb4c**, and the future governance file scope below. The owner decision must reference this exact proposal and the final preparation commit/tree in the delivery receipt. Any change to its nine pairs, source identities, exclusions or authority requires fresh review and a new decision. The current request authorises preparation and push, not activation.

## Exact source identities

| Role | Git commit | Git tree |
| --- | --- | --- |
| Verified develop / previous effective bytes | `99349d82eb06960eb1f9785cf384db2373aed4fa` | `e4d137a12cbdd827583fcb841612890dd88736ec` |
| Exact starting head / candidate bytes | `b29dbb862bd6fe2eebc26deba25bab072857048f` | `53aa0f0c18455ca61cb2bc7b4abf111124de50dc` |
| Original validated hotfix / unsigned .1055 source | `3a8f10d737791a1182d092676ecb17a47e2fde30` | `1f13a1afa64fd0b4419303bcaf0136f1ffcecfe7` |
| Goal #62 original evidence source baseline | `0890ec9b2ade8af24b9b3fca6cbe98cd7fd329ba` | `fd0e968cd8fb3ece8ebb73d78a859705e1605fe3` |
| Last change to frozen Goal #62 preservation record | `45216360bfbecaafcdc19c1dd1edce128dbc7ea7` | `fa73f9484b90cd7246269f66ed47cbabbeaf52ba` |
| Inspected extraction source, Draft PR #76 | `7b1334b09a02ad2fbfd4a0234798e6c387d3e634` | `887856d293114442e97e0d06bef2928b340b639a` |

The Goal #62 baseline is the identity recorded *inside* the later evidence; it does not claim the preservation JSON existed at `0890ec9`. The immutable `docs/open-source/evidence/goal62/preserved-files.json` remains blob `65906b90de2f8659c736fca59882f7ba94f1863f`, canonical SHA-256 `d56bdbcc1f16c1a5ce26a5936778baaf3f45a5b4c94bc661d4d61d8fbb845822`.

This preparation is a descendant of exact `b29dbb8`, changing only new evidence and the generated current inventory. Its future commit/tree cannot be embedded into itself; the final delivery receipt supplies both. No package is reattributed to it. The native subtree stays `8c90d7e3491615d4f156b3e33b3abb7b13cbc59f`.

## Exact nine-path decision

All digests are **SHA-256 of canonical Git blob bytes**, not Windows CRLF working-copy hashes. Direct byte comparisons confirm each candidate differs from verified develop and equals its original validated `3a8f10d` blob. [Validation results](validation-results.json) also record blob IDs, byte lengths and the older Goal #62 hashes.

| Exact protected path | Previous effective approved SHA-256 | New candidate SHA-256 |
| --- | --- | --- |
| `src/native/LensDocsStudio.Core.Tests/LensDocsStudio.Core.Tests.csproj` | `5744f4a8b7e8784519a74b362d3a7f7f5a1d657b2c295b6c3d4d2b66ff585316` | `bcc5936898b464e3da8116937349c04a411755db2d78e4376d3d763017c275b4` |
| `src/native/LensDocsStudio.Core/Arrangement/ArrangementEngine.cs` | `8e12c1260ad97b08b509b1210dafe0124c2b117701f5d2a160b3a59f356af859` | `ae71b419863ebf55de83e48984106023c583cf76520edd77d12d93d966120d77` |
| `src/native/LensDocsStudio.Core/Mermaid/MermaidOperations.cs` | `bc11d28656c15935077a20ff0d981fd4aaafaaae099b805fa87f03bfe6c31245` | `fde06abc85a0f3a68e33064d55c6df3f4fd9355eca2ef9f02ee5b8f27f894f70` |
| `src/native/LensDocsStudio.Core/Mermaid/MermaidSemanticValidation.cs` | `d2860c996f00e0a6a145ed1e006069b1118aab033c75d249ab13fc384e49a6a5` | `311c66b3e3136767838aab0eeadc40bedd8ba35d4a368b01444da2ddc00cdeca` |
| `src/native/LensDocsStudio.Core/Mermaid/MermaidSourceParser.cs` | `ca956434be85b52c1ec5c66d3bcded2b4145fc4517de72f0c77fac18464dce8c` | `7e8304ebb33911ceb58ec63401d3f17cc67b11ef26d912abca8b6e07a20af5d3` |
| `src/native/LensDocsStudio.Core/Rendering/DiagramService.cs` | `c8a3b7b97bf0fc91e3aa7adfbce99646709380083fd53015eea9742192df4d0d` | `158554c6f6d661e9563c8515f91a6985234da59aec123eeaf06246ae60961275` |
| `src/native/LensDocsStudio.Core/SourceAuthoring/SourceText.cs` | `33615f2d44fc1faecbbdf5f72e506769073bc9cbbd95a437f7c291a8b50fd738` | `7c99911849b28f54be0b0615b4c91374a0b791d86e897a40fa177e6d0cad888c` |
| `src/native/LensDocsStudio.Native.Validation/Program.cs` | `3f7abecc36518604a7e644cc122d98fac8ef772d9e4e4ecbb574fc56977c1219` | `e2e1977ff193a343de5dfe4f3ed1c8fc8f44621b9345c1957fa6726b39d2f2ca` |
| `src/native/LensDocsStudio.Native/Rendering/NativeMarkdownRenderer.cs` | `60ab0564756fc1a336656d437eea5ae16705db2620c3913dc2c797afe7c9c649` | `a71d05d445699f1ed8adf8fa385c886b55613a74bd822af86ea98fbd20f3083c` |

These are the **effective** previous pins after the existing policy, find-caret and selection-sync refreshes. `Program.cs` chains from selection-sync's `3f7abecc...`, not its frozen Goal #62 `00288b43cf8bddcd82f53c4befbb2428f8bb32b3d05eff65dbfec8b6fc295ddf`. That older hash stays immutable.

Read-only enumeration checks all **2,421** effective protected paths: develop has zero mismatches; the candidate has exactly these nine. All **2,526** pre-existing tracked files other than the regenerable current ledger remain unchanged from `b29dbb8`. The protected user guide remains identical to verified develop.

## Necessity, tests and source attribution

All changed hunks were inspected. The paths below are relative to `src/native/`.

| File | Necessary role | Existing support |
| --- | --- | --- |
| `LensDocsStudio.Core.Tests/LensDocsStudio.Core.Tests.csproj` | Copies the exact SPC fixture to test output; no dependency change. | Upstream reproduction and exact SPC rendering tests load this copied fixture. |
| `LensDocsStudio.Core/Arrangement/ArrangementEngine.cs` | Applies the same bounded SVG repair after arrangement rendering, retaining subgraph styling. | SVG colour/dash assertions, existing `ArrangementTests.Move_has_final_routes_preserves_source_and_keeps_canonical_export` and independent call-path review. There is no dedicated new arranged-SPC styling test; no direct coverage of that case is claimed. |
| `LensDocsStudio.Core/Mermaid/MermaidOperations.cs` | Replaces only the selected node's explicit class assignments, retaining other targets/comments; fixes an independently reproduced editing regression. | `Selecting_a_class_replaces_only_the_selected_nodes_assignments` (three cases), Undo/Redo and independent before/after probes. |
| `LensDocsStudio.Core/Mermaid/MermaidSemanticValidation.cs` | Uses compatibility grammar for full render correspondence and authored declarations for edit validation; avoids synthetic effective-class collisions. | Supported edits, deleting a multiply-styled node, adding a colliding authored class, independent reproductions. Expected-text and protected-source checks remain. |
| `LensDocsStudio.Core/Mermaid/MermaidSourceParser.cs` | Aligns parse/render semantics, reports unsupported directives, retains readable protected labels without enabling destructive typography edits. | Exact SPC graph/labels remain Protected; malformed/compound styling and supported-edit tests. |
| `LensDocsStudio.Core/Rendering/DiagramService.cs` | Shares SVG repair and fidelity warning with canonical native rendering/export; retains original source. | Exact nodes/edges/classes/colours/dashes; Light/Dark preview, inspection and SVG/PNG/document-export parity. |
| `LensDocsStudio.Core/SourceAuthoring/SourceText.cs` | Routes flowcharts through renderer-private preparation. | Semicolon, multiline, comment, repeated/unknown class and source-preservation tests; other families remain unadapted. |
| `LensDocsStudio.Native.Validation/Program.cs` | Adds focused command routing and invokes the check in the existing native corpus without workflow changes. | Original 19-check Windows completion; current-head native #1056 passed. Failures still propagate. |
| `LensDocsStudio.Native/Rendering/NativeMarkdownRenderer.cs` | Displays the unsupported-typography warning in WPF preview and its automation name. | Native checks require the warning and unchanged preview/export pixels; original foreground captures. |

Functional deltas entered through `d09ea9b847a9479ee601d78e03b636ad1dc4776f` and `3a8f10d737791a1182d092676ecb17a47e2fde30`. Compatibility extraction is attributed to PR #76 commits `86caa102d5d2a81f5ad8226b5ecc98df545e9dde` (tree `b7454b42fff1b45c335b6555b7a81bf176b89308`) and `3c1c100b7313b49f2bd742eb8f634fc1e5d28531` (tree `0382f5fbb8c117bbb6135fc4fd921c5ca51f41db`), followed by the independently justified editing remedies. No PR #76 merge/cherry-pick or reading-experience, comparison, High Contrast or performance implementation was imported.

The retained technical review is in the [original evaluation evidence ZIP](https://github.com/user-attachments/files/34579969/LensDocsStudio.Native-0.1.1-preview.1055-flowchart-evidence.zip), `review/independent-review-3a8f10d.md`; see the [original final receipt](https://github.com/DouglasNLima/local-docs-studio/pull/81#issuecomment-6740761520). A separate agent reviews this packet in the [independent provenance review](independent-review.md). AI technical review is neither owner consent nor independent human/legal verification.

## Rights and attribution constraints

The nine paths remain **R1 / MIT_ORIGINAL_PORTIONS_ONLY**, already enumerated in unchanged `mit-scope.json`. The [Goal #62 owner declaration](https://github.com/DouglasNLima/local-docs-studio/issues/62) and notice `Copyright (c) 2026 DouglasNLima` retain their existing scope. Those established decisions need no reconfirmation. Path membership and Git authorship do not prove title to every added fragment; this proposal authorises protected-byte substitutions only.

R3 examples/embedded samples, R4 artwork/branding, R5 evidence, R6 upstream/adapted content and R7 user content remain excluded. Test assertions/control logic are distinct from SPC/sample payloads. The SPC fixture remains R3 / OWNER APPROVAL REQUIRED. Mermaider stays pinned to **0.15.1** with its own grants/notices. `PinnedMermaider/**`, `BoundedErLayout.cs`, vendor bytes, upstream notices, locks and package metadata remain unchanged. No complete-file MIT SPDX or whole-binary MIT claim is added.

These new hotfix paths remain **BLOCKED** for the first-party grant, regardless of any approval of the nine substitutions:

- `src/native/LensDocsStudio.Core.Tests/FlowchartCompatibilityTests.cs`
- `src/native/LensDocsStudio.Core/Rendering/FlowchartCompatibility.cs`
- `src/native/LensDocsStudio.Core/Rendering/FlowchartSvgCompatibility.cs`
- `src/native/LensDocsStudio.Native.Validation/NativeFlowchartCompatibilityChecks.cs`
- `docs/native/flowchart-styling-hotfix.md`

They supply functional context, not additional amendment paths or rights clearance. The preparation packet is R5 evidence, excluded from the default MIT grant; its checker adds no SPDX declaration. Unresolved rights/distribution obligations remain BLOCKED. This amendment does not authorise distribution.

## Proposed mechanism — only after exact owner approval

`licence_scope.py` currently copies the frozen Goal #62 map, applies five exact policy pairs, then the find-caret and selection-sync overlays. The latter two fix their path scope/previous pins but accept any syntactically valid new digest. The proposed design retains that overlay model and binds **both sides** of all nine pairs to the exact reviewed values.

A future, independently reviewed implementation would:

1. Keep all frozen evidence and previous refreshes unchanged. Add one overlay after selection-sync and before the existing `validate_preserved` call.
2. Require the exact repository, proposal ID/digest, develop and candidate commit/tree identities, and an authentic explicit owner decision tied to them. A local approved flag, agent review or this preparation request is insufficient.
3. Require exactly nine case-sensitive paths and exact previous/current pairs; reject duplicate JSON keys, missing/extra paths/fields, malformed or valid-looking but different hashes. Require previous values to match the effective map and candidate blobs to match the approved digests. No globs, fallback hashes, bypass switches or regenerated approvals.
4. Copy the map, replace only nine values, and run the unchanged full protected-byte predicate. Any other difference fails. Preserve licence/scope/exclusion, SPDX, metadata and required-check validation.
5. Reject stale source identities or approvals before applying anything. A changed base or source needs a fresh proposal, not inferred approval or a silent rebase.

Hardcode the approved table (or its exact canonical digest) and approval-receipt digest in independently reviewed code. Regex validation of mutable current hashes is insufficient. Authenticate the owner decision before introducing the pinned receipt; an offline file cannot authenticate a future GitHub approval.

### Exact future governance files

Only these files would need modification/addition after approval:

| Exact path | Permitted future change |
| --- | --- |
| `scripts/compliance/licence_scope.py` | Add/call the nine-pair overlay with approval/identity checks; retain existing validations. |
| `scripts/compliance/test_governance.py` | Add positive/rejection controls without weakening existing tests. |
| `docs/open-source/evidence/flowchart-provenance-81/approved-refresh.json` | New exact approved pairs/identities, separate from this proposed record. |
| `docs/open-source/evidence/flowchart-provenance-81/owner-approval.json` | New actual owner receipt, exact decision source, proposal digest and scope. |
| `docs/open-source/evidence/flowchart-provenance-81/activation-validation.json` | New implementation validation/failure-control evidence; retain old results. |
| `docs/open-source/evidence/flowchart-provenance-81/activation-independent-review.md` | New review tied to the concrete activation source and receipt. |
| `docs/open-source/evidence/goal62/rights-ledger.csv` | Regenerate only the current inventory, preserving classifications/grants. |

**No activation change is made here.** `check_governance.py` already calls the licence validator and needs no change. LICENSE, LICENSING.md, mit-scope.json, original approvals, frozen Goal #62 pins, previous refresh JSON, rulesets, workflows, required checks, signing/update trust, performance controls and ADR 007 remain outside this change scope.

This preparation adds only the six files in this directory and regenerates the current ledger to list them as R5. Every pre-existing ledger row, hash and classification must remain identical to `b29dbb8`. The current inventory is not the immutable preservation record.

## Offline validation and independent requirements

~~~powershell
python -B docs/open-source/evidence/flowchart-provenance-81/validate-proposal.py
# After staging the evidence and regenerating the current ledger:
python -B docs/open-source/evidence/flowchart-provenance-81/validate-proposal.py --check-ledger
# Optional on the original evaluation workstation, where retained artefacts exist:
python -B docs/open-source/evidence/flowchart-provenance-81/validate-proposal.py --check-local-evidence
git diff --check
~~~

The checker reads Git objects and uses `git hash-object` **without -w**. It reconstructs effective pins from unchanged policy data/historical functions, then calls the existing pure `validate_preserved` predicate. It neither runs/replaces `licence_scope.main` nor writes Git objects, accesses the network, executes native code or activates policy. Diagnostic PASS means the proposal is consistent; the production protected-byte result remains **FAIL**.

The **35** in-memory negative controls reject missing/extra paths, wrong previous/current hashes, wrong commit/tree identities, changed Goal #62 identity, lost exclusions, extra override fields, missing grant membership for each of nine paths, actual byte mismatch, a protected mismatch outside the nine, R3–R7/trust/performance substitutions, duplicate JSON keys and asserted approval/activation. No activation option exists. The 2,421-path simulation succeeds only in a disposable map without mutating its input.

The checker also runs **20 existing read-only governance tests**, all passing with no skips. Four existing tests that write temporary package payloads are deliberately omitted; their names and reason are recorded in the results. The optional local-evidence check verified **15 retained execution evidence files**, including the original evidence ZIP, historical failure logs, raw TRX and prior independent review. A fresh checkout without those local artefacts can reproduce the main read-only checks without this optional flag.

Before future activation is accepted, an independent reviewer must recompute all nine pairs, replay the historical chain, inspect every hunk and source attribution, retain mixed-content/owner-decision boundaries, authenticate the exact owner receipt and review the concrete overlay and rejection tests. Run ordinary offline governance/foundation checks on that implementation, verify all unrelated pins remain unchanged, and record exact implementation commit/tree and outcomes. This packet is not future activation validation.

No native/build/browser or Continuous execution is repeated for evidence-only preparation. Retained raw TRX shows **2,320 executed/passed Core tests, no skips**, including **22 Flowchart cases**; the original Windows receipt has **19 checks**. These are earlier execution results, not new runs or new-source certification.

## Preserved failures and delivery

[Conflict comment 6741989699](https://github.com/DouglasNLima/local-docs-studio/pull/81#issuecomment-6741989699) is retained in [source observations](source-observations.json). At `b29dbb8`, [OSS #102](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42624169917) failed on the nine files and [Continuous #233](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42624170280) failed with **28/31** candidate samples above the unchanged gate. Earlier [OSS failure](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42616164670) and [Continuous 28/27 failure](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42616165018) remain historical failures. The guide mismatch was corrected in `b29dbb8`, not erased from earlier logs.

Original .1055 portable ZIP SHA-256 stays `8f3b0d167205d0cecd89c6bd77eeaebe7cb2e3dacd59ad1703cabc4d0100cc32`; installer `191272a893f147d3ab8c56fb73036c9fe42aad7fa1a2eab7e1e58bc57a042689`; evaluation evidence ZIP `2c1cf3bf11e2226a464a31fcf545c46efdf00f16c467d0f8b09c04230c09fcb7`. Attribution stays with `3a8f10d`.

Continuous triggers on every develop-targeted PR update. This preparation commit uses **[skip ci]** to honour the no-rerun instruction. [GitHub documents](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/skip-workflow-runs) that push/pull_request execution is skipped and required checks remain pending, blocking merge. No check is waived, marked successful, removed or retried. The delivery receipt must verify no new Continuous run started.

PR #81 remains Draft and integration BLOCKED. PRs #76/#78/#80 stay unchanged and Draft. The 250 ms gate, 4.5:1 requirement, proposed ADR 007, Goal #77 and Native Stable remain unchanged. No merge, release, signing, deployment or distribution.

## Remaining owner decision

Approve or reject **only this exact nine-pair amendment and the future governance file scope**. Suggested explicit wording:

> I approve proposal pr81-flowchart-provenance-nine-v1, candidate-refresh.json canonical SHA-256 97582b1d4e1c7bf1d2f8762bf812a50c2b8aae976be1491daacb9e75bb8abb4c, as delivered at the recorded preparation commit/tree, for the nine exact previous/current substitutions only. I authorise the bounded future governance implementation listed here, subject to independent implementation review and validation. Existing licence scope, approvals, R3–R7 exclusions, historical evidence, trust/performance controls and required checks remain unchanged. This is not approval to merge, release, sign, deploy, distribute, rerun Continuous or activate ADR 007.

This is unsubmitted proposed wording, not owner approval. New-path grants and unresolved third-party clearances are outside the decision and need a separate scoped proposal if pursued later.
