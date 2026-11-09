# Preview reflow: three-file provenance proposal

**PROPOSED — NOT APPROVED — NOT ACTIVE.** No protected substitution is activated.

Proposal `preview-reflow-integration-three-v1` requests a bounded owner decision
on [candidate-refresh.json](candidate-refresh.json), canonical SHA-256
`1cecd8b708ea165e1bb73eb98382efb7799bd5dddd21cad3e5d46c586e3db9eb`.
The owner decision must identify this manifest and the final preparation
commit/tree supplied with the Draft PR. Changes to the pairs, source identities,
exclusions or activation scope require a new review and decision.

## Exact identities

| Role | Commit | Tree |
| --- | --- | --- |
| Accepted develop | `3cd08bc2de31564e06973c5211e1aa231731f9cb` | `d5d07c445a12803f64dd36276a86511143a73648` |
| Tested product and controls | `2003c1f24de3fa8c7b98e18ec543655cec281cf0` | `540eae1bff9effcede132793c14847d985bf8202` |
| Candidate with fixture byte preservation | `752b2c26e6c309b77fe23b812c6f3d1a5114d369` | `9835e2b16bdd113683effbed85a10416cd636b39` |
| Historical PR #80 | `22c223c6f56fa41fda2a36102e1eed0af44974ec` | `89e012f468801ac7f445113b811ca2fbe041c8d9` |

The candidate adds only a fixture-local `.gitattributes` to the tested source.
Product and control code are identical. The native subtree is
`fe3d78249f388df1488bf09c1941f147363e63c1`. Later preparation changes consist of
new documentation, retained evidence and the regenerable current rights ledger;
the final commit cannot embed its own identity.

## Exact protected substitutions

Digests are SHA-256 of canonical Git blobs, not CRLF working-copy hashes.

| Protected path | Previous effective approved digest | Proposed digest |
| --- | --- | --- |
| `src/native/LensDocsStudio.Native/MainWindow.Find.cs` | `9e8bcd8b2fbbcdf7d073a61f98b500e54c2c58dd6341e9dc506fab5fa6e7ac7e` | `30bb76cb045afd2b27be472a560a22d86eaa176d9649b0eb2e663e0564b7c974` |
| `src/native/LensDocsStudio.Native/MainWindow.Synchronisation.cs` | `fb55e56f82805ec4d3de82dc5fa132db50639d89eb61b4182e4765ab2a087ec9` | `8a7e197f2e850c9eebf4d2e14d312e87cb20a1568a462ab44b5d4cb63b504907` |
| `src/native/LensDocsStudio.Native/MainWindow.xaml.cs` | `50888389971d81a765bc00b3607baa26cc5e9982f5679779e3d87cbebbfa4585` | `65781f39bb3e41508960f53f510b503e64dcc09724ea98a2f51656431ab325c1` |

`MainWindow.PreviewReflow.cs` and `Rendering/PreviewViewportAnchor.cs` are new
files, pinned in the added-file inventory and successor native subtree. They
are not substitutions for an existing protected file. The eight added source,
control and fixture paths are enumerated in the manifest.

The three substitutions respectively cancel obsolete Find/reflow competition,
cancel and clear synchronisation-owned anchors, and capture/restore layout with
render and keyboard cancellation. The [technical report](../../../native/native-preview-reflow-integration.md)
describes the extraction and its loading integration.

## Preserved authority and activation mechanism

The frozen Goal #62 preservation record, MIT allowlist, previous find/caret and
selection amendments, and all approved PR #81/#82 manifests and owner receipts
remain unchanged. All 2,421 original protected paths are enumerated by the
read-only verifier; exactly three have proposed new bytes. The baseline passes
the active governance check and all 49 governance controls.

No licence grant is added. Existing enumerated original portions retain their
current scope. Added files remain outside the unchanged MIT allowlist; fixture,
artwork, evidence, upstream and other R3–R7 exclusions remain in force. A
protected-byte approval does not establish rights to every added fragment.

After explicit approval, a separate amendment may change **only** the manifest's
`activationPathsOnly`: the existing `licence_scope.py` mechanism, its governance
controls, a new pinned owner receipt, approved refresh, activation validation,
independent activation review and the regenerable rights ledger. It must apply
the three pairs after the unchanged PR #82 successor, verify the prior effective
hashes, pin this manifest and the owner's decision, and validate the exact native
and fixture trees and canonical working source. It must reject missing or extra
paths, changed digests and absent source history. No predecessor evidence or
approved substitution may be rewritten.

Run the inert proposal verifier with:

```powershell
python -B docs/open-source/evidence/preview-reflow-integration/validate-proposal.py
```

The [result](validation-results.json) includes six rejected mutations. This is
proposal verification, **not** an OSS-provenance PASS: the active candidate
governance check correctly rejects the unapproved native subtree.

## Integration limits

The candidate and exact develop both fail the existing shown lifecycle suite's
dense ER completion assertion after 104 successful checks because of the
unchanged six-second renderer deadline. Both observations are retained. The
source-position controls pass, including actual Windows High Contrast, but no
deadline is extended and no lifecycle failure is waived.

Independent technical review remains pending. No independent approval is
claimed by the implementing agent. The ordinary app's computer-use window
identity failure also prevents a separate manual-input qualification; the
owned-window WPF/UIA evidence is explicitly scoped in the report.

All 30 live mandatory develop contexts remain required. The separate historical
Continuous 250 ms FAIL stays FAIL. No PR #81/#82 personal-use exception extends
to this proposal. Hosted CI requires a subsequent explicit owner authorisation
after provenance approval and candidate readiness. No merge, release, signing,
deployment, binary distribution or Native Stable certification is authorised.
