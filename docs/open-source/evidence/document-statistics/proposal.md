# Document statistics: inactive provenance proposal

**PROPOSED — NOT APPROVED — NOT ACTIVE.**

Proposal `document-statistics-ten-v1`, canonical committed manifest SHA-256:
`7dd1bc0e1a4c1ccf798495cba950a422af06729ee3876d86a04e028f6141ec22`.

Baseline `2dbc6d4e61db431001b42bb93b02d8e503eb8096`; complete candidate `ed521c2bd7b30060580d3fee9967a5e307e0e89b`, tree
`c042138e54b80c61dcc20dc2fb17cbf40405693c`. Tested product/control commit `5b336179b5cbbb97d03880c0cf9674a2decaa000`
retains native subtree `5aeb8c1f09f16c2b790de1542e45dcd3b0e41649`.
The final preparation HEAD/tree will be recorded in the Draft PR receipt; the
proposal cannot embed its own future commit identity.

The [manifest](candidate-refresh.json) enumerates exactly ten old/new protected
SHA-256 pairs: five product files (Review service, Review window, Review action,
MainWindow XAML and initialisation) and five required native tracking documents.
Five added product/test files and all added documentation/evidence are separately
pinned. Historical PR #76 is reference only. Existing #81/#82/#83 approvals and
their bytes are preserved; no repeat approval is requested for them.

The owner may decide on bounded activation of these exact pairs and the successor
native subtree. Activation is limited to the seven `activationPathsOnly` paths.
A future implementation must pin this canonical manifest, the exact owner receipt,
all previous effective digests and current source/control identities, reject scope
expansion and retain prior approvals. This proposal implements no substitution.

Run `python -B docs/open-source/evidence/document-statistics/validate-proposal.py`
from a committed preparation tree. It reads raw Git blob bytes, checks index and
clean-filter working identities, compares the whole candidate delta, and rejects
mutated scope/digests/source/approval. LF/CRLF checkout conversion is allowed;
semantic JSON equivalence is not an approval identifier. Its PASS means only
inert proposal integrity, never ordinary active provenance PASS.

Ordinary governance must continue to reject this unapproved source. The refreshed
rights ledger grants no new rights; the MIT allowlist and R3–R7 exclusions remain
unchanged. Product/test review passes separately. Actual local High Contrast was
unavailable, and its future qualification is not waived. Historical failures,
including the aborted focused development control, remain recorded.

**Next decision: exact provenance activation only.** Hosted CI and merge need
separate authorisation. All 30 mandatory checks remain required. Continuous 250 ms
remains FAIL; no exception transfers and ADR 007 remains inactive. No signing,
release, tags, deployment, distribution or public/Native Stable acceptance.
