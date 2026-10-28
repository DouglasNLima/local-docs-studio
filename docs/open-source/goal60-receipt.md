# Goal #60 — Solo Maintainer Governance & Scoped MIT Adoption

Engineering decision packet for [tracking issue #60](https://github.com/DouglasNLima/local-docs-studio/issues/60).
Parents #56/#58 remain open. Target:
**SOLO_MAINTAINER_OPEN_SOURCE_GOVERNANCE_READY**, as a practical policy foundation,
not a claim that licensing, GitHub enforcement, private intake or production
release prerequisites are already operational.

## Source and risk

Freshly fetched origin/develop matches the requested baseline:
`4ffc8b1967aa58bff02aa65e2f3a6f4da986e5b0`, tree
`6a8d27461d76b3cd21aba98e4ed67dcf5ba6daf9`. PR #59 is merged and its source tree
matches. There is no baseline drift. Work uses the isolated
`feature/solo-maintainer-governance` branch and one Draft PR to develop.
The final PR body/terminal receipt supplies the final commit/tree without trying
to place a file's own future Git identity inside itself.

Scope is documentation, proposed administrative JSON, rights classification and
read-only compliance validation. Runtime, packaging, updater, production trust,
vendor bytes and package metadata are unchanged. Licence overclaiming and
unusable protection settings are the principal risks addressed. The new current
ledger corrects an upstream adapter outside the pinned folder and keeps historical
evidence separate; original Goal #58 evidence is preserved.

## Responsibilities and implemented policy

| Role | Human responsible |
|---|---|
| Project Owner | @DouglasNLima |
| Lead Developer / Committer | @DouglasNLima |
| Reviewer of external contributions | @DouglasNLima |
| Release Authoriser | @DouglasNLima |
| Security Maintainer | @DouglasNLima |
| Community and Support Maintainer | @DouglasNLima |

**IMPLEMENTED:** [governance](governance.md), CONTRIBUTING, CODEOWNERS, SECURITY,
conduct/support policies and signing proposals now describe one human holding
multiple roles. Owner changes require branch/PR, SHA/tree, risk assessment,
relevant checks/hosted CI and a recorded owner decision before merge, followed by
a separate public-release decision. External changes require human owner review.
AI review is labelled technical evidence. There is no fictional appeal contact,
unsupported SLA or second maintainer requirement.

## MIT scope and exact decision

The [R1–R7 packet](rights-decision.md) and
[current affected-file ledger](evidence/goal60/rights-ledger.csv) are authoritative
proposal inputs. R1 implementation and R2 original documentation are candidates;
R3 examples need individual clearance, R4 artwork/branding remains excluded,
R5 historical evidence retains provenance, R6 dependencies/adaptations preserve
upstream grants and R7 user content stays under its owners' rights. Embedded
examples/quotations remain excluded even inside R1/R2 files.

Proposed notice: **Copyright (c) 2026 DouglasNLima**. The public handle is not
assumed to be the legal copyright holder. The complete unmodified MIT text is
non-operative in [licence-proposal.md](licence-proposal.md). No root LICENSE,
package licence or new SPDX grant is introduced.

**OWNER APPROVAL REQUIRED:** approve/correct the exact public name, year/range,
additional holders and cleared scope against the frozen ledger. Personal
development/no employer/customer restrictions and free distribution are already
confirmed and need no reconfirmation. After approval a separate adoption change
adds LICENSE, LICENSING.md, suitable first-party SPDX and accurate metadata.

## Recommended versus enforced controls

[Exact rulesets/settings](governance-settings.md) propose required PRs, zero
approving reviews, no CODEOWNER/latest-push approval, exact observed CI contexts,
resolved conversations, no bypass/force push/deletion, merge commits only and
auto-merge disabled. Main gets only contexts that run on main PRs.

Actually observed: no protections/rulesets on either branch; auto-merge already
disabled; squash/rebase still allowed; private reporting disabled; only Pages
environment with a branch restriction and no reviewer. The current Pages route
can publish on a default-branch push; the separate owner publication decision
is policy, not an enforced environment gate. MFA is unverified. API observations
and a safe disposable validation plan are supplied; **no administrative write
or behavioural enforcement test has been performed**.

Continuous 250 ms **FAIL** remains unchanged as a production/release acceptance
blocker. It is intentionally excluded from ordinary integration protection, with
failure disclosure required in owner decisions. No threshold or workflow was
weakened. Other required check failures remain blocking.

## Security reporting and SignPath

**OWNER APPROVAL REQUIRED:** separately authorise private reporting enablement
and the controlled synthetic test in [the plan](private-reporting-plan.md).
The intended security recipient is @DouglasNLima. No real vulnerability is exposed;
the channel must not be advertised operational before delivery/access/privacy
verification. No synthetic report was submitted.

[SignPath findings](signpath-application-packet.md): published terms define
author/reviewer/approver responsibilities without explicitly requiring different
people. They do not explicitly guarantee single-maintainer acceptance.
@DouglasNLima is the proposed author, external reviewer, signing approver and
release authoriser; provider acceptance remains **BLOCKED**. No application or
enquiry was sent. Native public distribution evidence, component/font/apphost/Inno
interpretation and actual signing/trust validation remain unresolved.

RSA-PSS/SHA-256 manifest binding, approved Authenticode subject/fingerprint,
Windows trust/revocation, installer hashes, product/version/channel/asset binding,
manual first trusted installation, user consent and planned key/certificate
rotation are preserved. Future owner-reviewed environments leave Prevent
self-review disabled and administrator bypass disabled. No environment, secret,
signing request or publication workflow is activated.

## Validation and evidence

Local results before independent review: static integrity PASS; foundation
PASS (68 original notice references, 537 recorded native files, 522 exact native
matches, 2,448 paths and 135 local links); governance PASS (30 develop and two
main contexts); seven regression controls PASS. Clean maintenance-tool install
retained its existing deprecation warnings. Independent archive/source verification
passed for 1,559 installed files and 194 exact historical map sources; Highlight.js
reconstructed all 955,998 original bytes, SHA-256
`980aa9a979a537d5f05c76d313c5f8b48f0bccba293a75e3c271975fce968aa0`.
The first expanded link check rejected a literal README example as a live link;
fenced/inline-code handling was corrected and regression-tested. Final local
checks and git diff whitespace validation pass; CRLF checkout warnings are benign.

The [baseline CI observation](evidence/goal60/baseline-ci.json) distinguishes the
post-merge runs from this proposal. Web and OSS provenance succeeded.
Native job complex-er-preview failed during preview-loading UI Automation:
`Preview lifecycle check exceeded its bounded wait.` Cause is unproved.
Its original run/job/artefact digest is retained; this goal does not rerun or hide
that baseline failure. See the terminal PR receipt for the entire run outcome.

Focused validation uses static integrity, existing OSS provenance/reconstruction,
complete current-ledger coverage, local documentation links, proposed-setting
checks, negative controls for solo-approval deadlocks/bypass/inapplicable checks,
CODEOWNERS syntax/account resolution and unchanged trust/metadata hashes.
The [independent technical review](evidence/goal60/independent-review.md) and final
PR receipt record actual results; they are not human approval. Hosted workflows
must reach terminal outcomes for the final source, retaining failures and skips.
Expensive native installed-family suites are not run locally for these changes;
the existing hosted workflows remain unmodified and run when the PR triggers them.

Known inherited gaps: missing original dingbat notice, incomplete embedded
Mammoth/PDF.js closure, historical legacy notice remediation, uncertified
production signing/clean-machine/accessibility behaviour and Continuous 250 ms
FAIL. No successful test count converts these into acceptance.

## Separate readiness and owner decisions

| Area | Disposition | Exact next decision or dependency |
|---|---|---|
| Solo-maintainer governance | IMPLEMENTED as the reviewable policy foundation | Owner reviews exact final SHA/tree and records a merge/defer decision; this PR stays Draft |
| MIT adoption | OWNER APPROVAL REQUIRED | Exact notice/holders/year and cleared R1/R2 set with exclusions; R3/R4 additions only with evidence |
| GitHub protection | OWNER APPROVAL REQUIRED | Separately authorise the exact two rulesets/repository settings and safe enforcement validation; actual branches remain unprotected |
| Private vulnerability reporting | OWNER APPROVAL REQUIRED | Enablement, owner notification setup and private synthetic test; behavioural verification remains BLOCKED until authorised/tested |
| SignPath application | BLOCKED | Provider acceptance of combined roles/components, licence adoption, MFA evidence and native distribution prerequisite; submission is not authorised |
| Native Stable release | BLOCKED | Continuous FAIL plus real signing, trust bootstrap, complete rights/notices and production certification; no release authorised |
| First-party MIT on upstream/user content | NOT APPLICABLE | Preserve their rights rather than request a blanket grant |

No merge, tag, release, signing, trust activation, paid service or administrative
mutation is part of this delivery. Paused Draft PRs #38/#50 and parent #56 are not
modified. Only the actions requiring explicit owner decisions stop; the technical
packet, isolated commit/push, Draft PR, review and CI monitoring are completed.
