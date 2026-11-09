# Independent review: PR #81 provenance preparation

Review date: 9 November 2026. Reviewer: separate Codex agent, independent of the proposal author and the original functional implementation. The reviewer performed read-only inspections and offline checks, then authored only this review. This is AI technical review, not owner consent, an independent human review or legal verification of copyright title.

**Scoped verdict: PASS for presenting the inert nine-file owner decision.** No remaining actionable preparation defect was found in the reviewed proposal, candidate manifest or checker. The amendment is not approved or active. Integration remains BLOCKED; the original provenance and Continuous acceptance failures are retained.

## Reviewed identities

| Role | Commit | Tree |
| --- | --- | --- |
| Verified develop | `99349d82eb06960eb1f9785cf384db2373aed4fa` | `e4d137a12cbdd827583fcb841612890dd88736ec` |
| Exact starting head / protected candidate bytes | `b29dbb862bd6fe2eebc26deba25bab072857048f` | `53aa0f0c18455ca61cb2bc7b4abf111124de50dc` |
| Original validated functional source | `3a8f10d737791a1182d092676ecb17a47e2fde30` | `1f13a1afa64fd0b4419303bcaf0136f1ffcecfe7` |

The reviewed preparation files have these SHA-256 digests of canonical Git bytes. The reviewer independently checked that LF-normalised bytes give the same Git object ID as the path's clean-filter result, without writing objects.

| File in this directory | Canonical SHA-256 |
| --- | --- |
| `candidate-refresh.json` | `97582b1d4e1c7bf1d2f8762bf812a50c2b8aae976be1491daacb9e75bb8abb4c` |
| `proposal.md` | `bcb5a481c0f249e8b71875c646b094e0c96e0114bf388c0f0868b7d6090dd313` |
| `validate-proposal.py` | `d7b2eb16f0c83b175c756711eaa03d2e86c45a37a6725a3dd298335ae6200968` |
| `source-observations.json` | `988a94bc5a8a91d9e1f30a061c76e4d5a33de0a3210d58ad54b4ac01ec7835d0` |
| `validation-results.json` | `f3ea097dafc217c67c4f8e14f7af8ccee3478d292f7be9ead9bf5c8bba6b7229` |

The manifest's Git blob ID is `791cd61309e9925fa60fef09a9498122875b55c9`. A final preparation commit/tree did not yet exist when this review was written; the delivery receipt must supply and validate those identities. This review does not claim to have inspected that future commit, its regenerated ledger or post-push workflow state.

## Independent scope and byte verification

The reviewer inspected every diff hunk in the nine exact protected paths enumerated in the [proposal](proposal.md) and [candidate manifest](candidate-refresh.json), recomputed all previous/current hashes directly from Git blobs, and independently reconstructed the complete effective protected map from the frozen Goal #62 evidence plus the existing policy, find-caret and selection-sync refreshes.

- All **2,421** effective pins match verified develop; there are zero baseline mismatches.
- At `b29dbb8`, exactly the proposed **nine** paths mismatch; there are no other protected mismatches.
- Every proposed previous/current pair agrees with independently calculated canonical hashes. Every candidate blob is byte-identical to the original validated `3a8f10d` blob and differs from develop.
- `Program.cs` correctly chains from selection-sync's effective `3f7abecc36518604a7e644cc122d98fac8ef772d9e4e4ecbb574fc56977c1219`, rather than replacing its historical Goal #62 `00288b43cf8bddcd82f53c4befbb2428f8bb32b3d05eff65dbfec8b6fc295ddf` record.
- The revised checker verified **2,526** existing tracked files, excluding the regenerable current ledger, unchanged from the starting head. The functional implementation, tests, fixture, workflows and governing modules remain unchanged.

The Windows working-file digests differ from canonical Git hashes because of checkout line endings. The proposal now identifies the canonical digest explicitly and checks its clean-filter interpretation. Approval must bind those canonical bytes and the final preparation commit/tree.

The proposal also correctly distinguishes `0890ec9b2ade8af24b9b3fca6cbe98cd7fd329ba`, the source baseline recorded by Goal #62, from the later preservation record. The frozen JSON last changed at `45216360bfbecaafcdc19c1dd1edce128dbc7ea7`, tree `fa73f9484b90cd7246269f66ed47cbabbeaf52ba`; its unchanged canonical digest is `d56bdbcc1f16c1a5ce26a5936778baaf3f45a5b4c94bc661d4d61d8fbb845822`.

## Necessity, functional evidence and attribution

All nine file changes have a necessary role in the isolated hotfix. The test project makes the exact fixture available; SourceText, DiagramService and the source parser share renderer-private compatibility handling; semantic validation preserves complete render correspondence and authored edit validation; class selection avoids the independently reproduced shared-assignment regression; arrangement preserves supported SVG styling; the native renderer surfaces the fidelity limitation; Program.cs connects the focused check to the existing validation corpus. No unrelated implementation hunk was identified.

The reviewer read the retained raw TRX: **2,320 executed and passed, zero failures or skips**, including **22 passing FlowchartCompatibilityTests cases**. The original native completion receipt identifies `3a8f10d` and records **19 passed checks** covering Light/Dark preview, inspection, SVG reopening, PNG/document-export parity and source preservation. The earlier independent technical review and regression receipts separately document the reproduced shared-class replacement, multiply-styled deletion and generated-class-name collision failures, followed by their successful remedies. Those earlier executions were inspected, not rerun in this provenance review.

Arrangement coverage is accurately bounded: existing arrangement tests and independent call-path review support the reuse of the tested SVG repair, but there is no dedicated new arranged-SPC styling test. The proposal does not overstate that coverage. This review did not recreate GUI captures, execute native binaries, run installers or rerun any performance workflow.

Extraction attribution to PR #76 source `7b1334b09a02ad2fbfd4a0234798e6c387d3e634` and commits `86caa102d5d2a81f5ad8226b5ecc98df545e9dde` / `3c1c100b7313b49f2bd742eb8f634fc1e5d28531` is supported by the repository history and retained functional review; their recorded tree identities were independently checked. Git authorship and same-repository history support traceability but do not prove originality of every fragment. Original evaluation packages and execution evidence remain attributed to `3a8f10d`, not to this preparation.

The reviewer recomputed these raw hashes of earlier local evidence without editing it:

| Earlier evidence | Raw SHA-256 |
| --- | --- |
| `artifacts/flowchart-review/independent-review-3a8f10d.md` | `a4b5d1141e9df096ed195a09e9eb50e8cdc98f39cc6c4e56ff8cb2c10756d02c` |
| `artifacts/flowchart-review/final-3a8f10d-regressions.json` | `07277a020cc995ceb68df1f3cbf858aeb8692b523299ea9559353af8c68ebd5c` |
| `artifacts/flowchart-hotfix/final-3a8f10d/native/completion.json` | `29852274fd3f9441ac94149740d8bd5d4f32b23098c64a3534e74c3887617bed` |

## Ownership and governance boundaries

All nine paths remain **R1 / MIT_ORIGINAL_PORTIONS_ONLY** in the existing explicit grant. No path membership, copyright notice, owner declaration or approval is altered. Embedded sample material, artwork, historical evidence, adapted/upstream content and user content remain excluded under R3–R7. A protected-byte amendment is not a licence expansion or rights clearance.

The new Flowchart helper, test and validation files and hotfix document remain BLOCKED for the first-party grant. The SPC fixture remains R3 / OWNER APPROVAL REQUIRED. Mermaider 0.15.1, upstream notices, adapted ER material and dependency locks remain unchanged. Unresolved distribution rights do not become cleared by this review or by approving the nine substitutions.

The future governance file list is concrete and appropriately bounded: the existing licence validator and governance tests, a separate approved refresh and genuine owner receipt, separate activation validation/review evidence, and the current ledger. It excludes the immutable historical pins, existing refreshes, grant manifest, required checks, workflows and trust/performance controls. The proposal requires a separate independently reviewed implementation after authentic owner approval; no proposed wording is represented as an approval already given.

## Failure controls and read-only execution

The reviewer inspected the checker and independently ran:

~~~text
python -B docs/open-source/evidence/flowchart-provenance-81/validate-proposal.py --check-local-evidence
~~~

Result: **exit 0; preparation consistency PASS; 35 negative controls PASS; 20 existing read-only governance tests PASS; 15 retained execution evidence hashes match; amendmentActive false; ownerApproval null**. The independently generated result exactly equals the saved `validation-results.json`. The unchanged production protected-byte predicate still returns:

~~~text
Protected notice/trust/performance bytes changed: src/native/LensDocsStudio.Core.Tests/LensDocsStudio.Core.Tests.csproj
~~~

The controls reject missing/extra/substituted paths, each missing grant membership, incorrect previous/current or actual hashes, a mismatch outside the nine, altered identities/exclusions, duplicate JSON keys, extra override fields and asserted approval/activation. Exact pairs and identities are hard-bound; a syntactically valid but unreviewed current hash is rejected. R3–R7, trust and performance examples cannot be substituted. The passing overlay is only a disposable map; the original input is checked unchanged. No activation mode exists.

The checker verifies existing production modules before importing their unchanged pure predicates. It checks committed, working-tree and staged changes against the preparation-only scope, including a staged protected change that might be hidden by restored working bytes. Its Git hashing omits `-w`; it does not execute the rights-ledger generator, full governance entry point, native code or network requests. It is proposal evidence, not a replacement required check. Future activation still needs authentic approval, concrete implementation review and its own positive/negative validation.

The reviewer inspected all 20 selected existing tests and their setup: they read existing files and exercise validators in memory. They pass with no skips. Four existing tests that write temporary package payloads and invoke package scripts are explicitly omitted for this read-only preparation; the result identifies their names and reason. The full 24-test suite is not claimed to have run again. The optional evidence check verifies the 15 recorded local artefacts, including original failure logs, terminal workflow snapshots, the original evidence ZIP, earlier independent review, raw TRX and native completion. It does not execute those artefacts or change their historical outcomes.

## Retained failures and delivery limitations

The historical provenance FAIL and Continuous failures remain failures; the proposal explicitly retains the earlier 28/27 and current-head 28/31 failed candidate samples. No threshold, required check, ADR 007 or release condition is weakened. A technical preparation PASS cannot resolve either integration blocker.

The current Continuous workflow has an unfiltered develop-targeted PR trigger. The proposal's planned `[skip ci]` preparation commit avoids starting a new run while leaving required checks unresolved; it is not check acceptance or a merge waiver. The primary agent must verify the final push did not start Continuous, that PR #81 remains Draft and that PRs #76/#78/#80 remain unchanged and Draft.

The final staged-ledger validation, commit/tree and post-push state were still pending at this review point. They must be verified before hand-off. No merge, release, signing, deployment or distribution was performed by this reviewer or authorised by this review.
