# Goal #58 foundation decision receipt

Historical receipt. PR #59 was subsequently merged at
`4ffc8b1967aa58bff02aa65e2f3a6f4da986e5b0`, tree
`6a8d27461d76b3cd21aba98e4ed67dcf5ba6daf9`.
[Goal #60](goal60-receipt.md) supersedes the earlier staffing/governance proposal:
the owner has confirmed personal development and one human maintainer. A second
reviewer is not required for ordinary owner integration. Original evidence and
unresolved provenance/performance limitations below are retained.

Target outcome: **OPEN_SOURCE_FOUNDATION_READY_FOR_OWNER_DECISION**. This is an
audit/provenance/governance proposal, not production certification. The final
Draft PR receipt supplies the frozen source commit/tree, independent-review
findings/resolutions, terminal hosted runs and resulting technical verdict.

## Accepted integration baseline

PR #57 was explicitly authorised and merged as an audit stage. Its accepted head
`48535ee6b876dbcef71064ab10020279ed0f7360` and new `develop` merge
`22d434a923a03e86ebee237e71c55ef89dd996ed` have the identical tree
`133cc2e59515aec1e44691c0e2333aa4f47d3ef4`. The first parent is the previous develop
`5c61d17966befee665856a7993cb174252df5214`. Fresh fetch verified the source before
creating `feature/oss-foundation-closeout` in an isolated worktree.

[PR #57 integration receipt](https://github.com/DouglasNLima/local-docs-studio/pull/57#issuecomment-6480387564):
web run 40759577983 succeeded (135 Chromium + 135 Edge tests); native run
40759577875 attempt 1 had 27 successful jobs, one responsiveness failure and two
conditional skips. Its sole failed job was repeated once on unchanged source and
passed; attempt 2 ended SUCCESS, reusing the other results. The original
`Candidate generation did not finish` timeout has **unproved cause** and is retained.
All 24 installed authoring families passed. Core 2,298/2,298 and 20 preview installer
lifecycle checks passed. Skipped historical/diagnostic, optional associations and
production/clean-machine/physical accessibility certifications remain unproved.

The separate Continuous 250 ms failure in run 40739070222 remains FAIL. It is not
triggered by develop pushes and is neither suppressed nor replaced by other passes.
The Goal #58 PR's own applicable performance results must also be retained.

## Delivered technical scope

- Exact existing Highlight.js reconstruction, pinned tool lock and verified upstream
  source/map/archive evidence; no vendor programme replacement or runtime dependency upgrade.
- Cached web notice pack with original bytes, a manifest entry and service-worker
  cache v71. No rendering/editor/export logic changed.
- Read-only binary SBOM generator, source-bound baseline candidate table and installer
  hashes. The native workflow generates fresh per-candidate `binary-sbom.json` files
  beside both ZIPs and retains them in `native-validation`; it changes no packaging
  bytes, test gates, signer, secret or publication permission.
- Complete path-level rights review ledger and a non-operative exact MIT proposal.
- CODEOWNERS review routing; read-only, pinned-action OSS provenance CI; exact proposed
  branch/environment/private-reporting/MFA settings and validation procedures.
- SignPath eligibility/selector/trust packet, without application or trust activation.

The [third-party report](third-party-closeout.md), [rights decision](rights-decision.md),
[governance settings](governance-settings.md) and
[application packet](signpath-application-packet.md) contain the detailed evidence.
The old Goal #56 inventory remains an immutable historical record. No native C#,
installer definition, update trust, paused PR, historical release or tag was changed.

## Consolidated owner hand-off

| Decision | Classification | Required next action |
|---|---|---|
| Technical preparation and one Draft PR | APPROVED | Authorised by the owner steer; acceptance/merge of this PR is still a separate decision |
| Exact MIT holder/year and cleared paths | OWNER ACTION REQUIRED | Approve or correct `2026 DouglasNLima`, title/assignment evidence, R1–R5 scope and R6/R7 exclusions |
| Embedded Mammoth/PDF.js attribution closure | BLOCKED | Resolve the documented payload-specific gaps before redistribution clearance; do not infer incompatibility from missing proof |
| Historical legacy notice remediation | OWNER ACTION REQUIRED | Approve a separate correction strategy; immutable bytes stay unchanged |
| Branch/release/environment/private-reporting settings | OWNER ACTION REQUIRED | Approve the exact proposed settings and recipients, then validate actual enforcement |
| Independent reviewer, signer/publisher roles and MFA | OWNER ACTION REQUIRED | Appoint real people and verify accounts; CODEOWNERS alone does not satisfy separation |
| Font/apphost/Inno and other provider interpretation | BLOCKED | Obtain provider interpretation only after approval to contact/apply |
| Existing public native distribution evidence | BLOCKED | Agree a legitimate future milestone with the owner/provider; do not publish a beta to manufacture eligibility |
| First-party MIT over upstream/user/customer content | NOT APPLICABLE | Retain separate rights and notices |
| Production signing, trust, tags, RC/Stable publication | BLOCKED | Later separately authorised milestones and real signed/clean-machine validation |

MIT adoption, SignPath submission and Stable publication remain **NO-GO**. The
next authorised planning milestone is owner-approved MIT adoption and SignPath
preparation, not automatic Stable packaging or publication. Goals #56 and #58 stay
open for their outstanding decisions; this PR remains Draft and is not merged.

## Verification method

`python scripts/compliance/check_foundation.py` checks exact vendor evidence,
original notice bytes and caching, SBOM references, rights-path coverage and local
documentation links. The separate reconstruction verifies upstream bytes and
rebuilds the complete original bundle. Static and Chromium/Edge checks cover the
cache change. Native source is unchanged; the full affected hosted native pipeline
validates the additive SBOM step and all existing preview checks on the final source.
The final receipt identifies actual results, failed attempts and inherited skips.
An independent agent review is engineering evidence, not a human GitHub approval
or proof that repository protections are enforced.
