# Preview reflow: corrected canonical three-file proposal

**PROPOSED — NOT APPROVED — NOT ACTIVE.**

Proposal `preview-reflow-integration-three-v2` supersedes the unapproved v1
proposal. The exact committed Git blob of [candidate-refresh.json](candidate-refresh.json)
has SHA-256 `340745a23758a94cb50b9aadeb761b817697777481a39762dbe0465ef64a8681`.
Only that canonical representation is eligible for a future owner decision.
The v1 checkout digest `1cecd8b708ea165e1bb73eb98382efb7799bd5dddd21cad3e5d46c586e3db9eb`
is invalid as an approval identifier. V1 and its original observations are
preserved unchanged, not approved or retrospectively repaired.

| Source | Commit | Complete tree |
| --- | --- | --- |
| Accepted develop | `3cd08bc2de31564e06973c5211e1aa231731f9cb` | `d5d07c445a12803f64dd36276a86511143a73648` |
| Starting PR #83 | `16b4b6864fd6cd72e3e7cf2bfb42d25af713ea85` | `14cec47913bce3732e23eac585963362f8d93eea` |
| Corrected final product and controls | `932335027314fe710671d54266367a008fca5547` | `c58468fc41222f56eb38f277950486686e820553` |

Native subtree: `4deeb6f0b54fd2e982525968500fbbe94030563d`.
Focused control subtree: `a04548454b40bb63f954d240e02cea5ac0b0ec7d`.
The final preparation HEAD and complete tree are supplied in the delivery
receipt and must be named in the owner decision. Preparation adds only the
corrected inert verifier, documentation, evidence and regenerable inventory;
the verifier requires these exact product/control subtrees at HEAD. A commit
cannot embed its own hash.

The product correction is confined to the already-added
`MainWindow.PreviewReflow.cs`: a hidden reader may change to Pages in Settings,
so incompatible or obsolete scroll anchors are discarded before starting an
owned restoration/loading request. Valid rendered content and its existing
status are retained. No renderer, deadline, export or launch changes are made.

| Protected file under `src/native/LensDocsStudio.Native/` | Previous effective approved SHA-256 | Proposed SHA-256 |
| --- | --- | --- |
| `MainWindow.Find.cs` | `9e8bcd8b2fbbcdf7d073a61f98b500e54c2c58dd6341e9dc506fab5fa6e7ac7e` | `30bb76cb045afd2b27be472a560a22d86eaa176d9649b0eb2e663e0564b7c974` |
| `MainWindow.Synchronisation.cs` | `fb55e56f82805ec4d3de82dc5fa132db50639d89eb61b4182e4765ab2a087ec9` | `8a7e197f2e850c9eebf4d2e14d312e87cb20a1568a462ab44b5d4cb63b504907` |
| `MainWindow.xaml.cs` | `50888389971d81a765bc00b3607baa26cc5e9982f5679779e3d87cbebbfa4585` | `65781f39bb3e41508960f53f510b503e64dcc09724ea98a2f51656431ab325c1` |

These three pairs remain unchanged from v1. Nine added product/control/fixture
paths are separately pinned, including the corrected reflow component and new
Pages transition control. The new files are outside the unchanged MIT allowlist;
no copyright, fixture, artwork or R3–R7 rights decision is inferred. Previous
approved amendments, owner receipts, protected files and historical evidence
remain intact.

The canonical verifier reads the committed object through binary `git cat-file`
output. It verifies the index and working file separately, allowing only checkout
line-ending differences and also requiring Git's path-specific clean object to
match. JSON is never parsed and re-serialised to manufacture an identity.
Missing, modified, substituted or staged content remains rejected. Native and
fixture working-source clean-filter verification is retained.

```powershell
python -B docs/open-source/evidence/preview-reflow-integration-v2/test-canonical-manifest.py
python -B docs/open-source/evidence/preview-reflow-integration-v2/validate-proposal.py
```

The [correction report](../../../native/native-preview-reflow-blockers.md)
records the focused results. Original local dense ER failures remain FAIL after
104 checks, with their close assertion not reached. The single corrected-source
shown lifecycle execution reaches 106 checks, including dense ER and closure;
this does not attribute the earlier failures or waive eventual hosted checks.

The [independent read-only review](independent-review.md) passed both corrections
without a new blocking finding. Recommended owner decision: approve only bounded
activation of `preview-reflow-integration-three-v2`, identifying the canonical
manifest SHA-256 above, exact product commit/tree and final preparation HEAD/tree
from the delivery receipt. Limit activation to the seven `activationPathsOnly`
entries, applying the three exact substitutions after the unchanged PR #82
successor and validating every pinned source/history identity. No activation is
implemented by this proposal.

All 30 mandatory develop contexts remain required. One normal hosted
qualification follows only subsequent authorisation and bounded activation.
The existing six-second dense ER deadline and historical Continuous 250 ms FAIL
remain unchanged. No performance-policy migration, merge, signing, release,
deployment, public distribution or Native Stable certification is authorised.
