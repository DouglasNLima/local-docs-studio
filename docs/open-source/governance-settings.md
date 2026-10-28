# Minimal governance configuration for owner approval

The [API observation](evidence/goal58/governance-observation.json), dated 28 October
2026, records a public personal repository, unprotected `develop` and `main`, no
rulesets, private vulnerability reporting disabled, and only a `github-pages`
environment with a branch policy. MFA and role membership are unverified.
**No repository administration was changed. Documentation is not enforcement.**

Source changes prepared here are `.github/CODEOWNERS` and the read-only `OSS
provenance` check. CODEOWNERS routes to the existing repository account
`@DouglasNLima`; it does not appoint an independent reviewer, prove copyright or
grant signing authority. It is not effective on `develop`/`main` until accepted
there. Draft PRs do not automatically request code-owner reviews.

## Exact recommended branch settings

OWNER ACTION REQUIRED: approve two active branch rules, matching `develop` and
`main` exactly, including administrators, with an empty bypass list. Require PRs,
one approving review from a person other than the author, code-owner review,
dismissal of stale approvals, approval of the most recent reviewable push by
someone other than its pusher, resolution of review conversations, and required
status checks against an up-to-date base. Block force pushes and deletions. Do not
require linear history: the project uses merge commits. Do not enable auto-merge.
Keep ordinary push permissions to the smallest approved collaborator set.

| Branch | Required check contexts after they have run on that branch/PR |
|---|---|
| `develop` | `Test static app`; `OSS provenance`; `native`; `diagram-arrangement`; `arrangement-responsiveness`; `complex-er-preview`; `state-installed-authoring`; `er-installed-authoring`; `class-installed-authoring`; all 21 `companion-installed-authoring (Family)` matrix contexts |
| `main` | `Test static app`; `OSS provenance`. Native promotion additionally requires the exact accepted develop source/tree and complete native receipt; current native workflows do not trigger for PRs targeting main, so do not configure an impossible missing native status on main |

The 21 exact family suffixes are `Pie`, `Mindmap`, `Flowchart`, `Gantt`, `Sankey`,
`Sequence`, `Quadrant`, `Timeline`, `Journey`, `Xy`, `Requirement`, `Radar`, `Block`,
`C4`, `Kanban`, `Architecture`, `GitGraph`, `TreeView`, `Packet`, `Treemap`, `Venn`.
Select the GitHub Actions app as the expected status source and verify the exact
contexts from the final PR's check-runs API; do not type hypothetical future jobs.
Additional existing native UX/interaction suites remain acceptance evidence even
where this minimum protected-branch set does not enumerate every context.

**Continuous 250 ms policy:** retain its real FAIL and original 250 ms threshold.
It remains a separate release acceptance blocker. Audit-only integration may be
CONDITIONAL GO only under an explicit owner decision recording the exact source,
failed runs and limited scope, as for PR #57. That exception authorises no Stable
release, hides no result and is not a security-risk waiver. Do not change the
workflow, mark it successful or ignore it in the receipt. Before making it a
mandatory branch check, the owner must choose either to block all merging until
it is fixed, or explicitly retain this bounded audit-only process. No such setting
is silently selected or applied here.

After approval, validate protection through API readback and a disposable test
branch/PR: direct push without bypass is denied; missing/failed checks block merge;
self-approval is insufficient; a new source push invalidates approval; unresolved
reviews block merge. Do not perform a destructive test push to `main` or `develop`.
Verify code-owner resolution through GitHub's CODEOWNERS errors API and the PR UI.

## Releases, identities and confidential reports

OWNER ACTION REQUIRED: protect future `native-v*` and existing release tag patterns
against update/deletion; restrict creation to the named release actor after approval.
Inventory existing tags first. No tag rules or tags are changed here. Prepare a
`native-production` publication environment with named required reviewers, prevent
self-review, disable administrator bypass, and allow only the agreed protected
release branch/tag patterns. No production workflow or secret is created here.
Do not grant a broad `contents: write` permission to PR jobs. Build/verify with read
permissions; future signing and publication must be separate, manually approved jobs.

The owner must appoint real Author, independent Reviewer, signing Approver,
publisher, security responder and conduct/appeal contacts. One account may own the
repository but cannot satisfy a claim of independent non-self-approval alone.
Until a second eligible reviewer is appointed, that proposed governance is BLOCKED.
Record collaborator access and MFA verification privately, not recovery codes or
personal identifiers in this repository. Require MFA on GitHub and SignPath, no
shared accounts, access removal on departure, and owner-approved recovery contacts.
A personal repository has no organisation-wide MFA enforcement policy; manual
verified membership checks or an approved move to an organisation are alternatives.

GitHub documents protected branches/CODEOWNERS for public repositories on Free,
and environment reviewers for public repositories on Free/Pro/Team. The public
repository status therefore does not establish a paid-plan blocker. Actual owner
settings must still be read back. Environment reviewers require only **one** of the
listed reviewers, not all of them; use distinct protected jobs if two approvals
are required. If the account rejects a proposed control, record the actual error
and block the affected release step; do not substitute a policy document for it.

OWNER ACTION REQUIRED: select security recipients and enable GitHub private
vulnerability reporting. Then verify API `enabled: true`, the report form from a
non-maintainer account, a clearly marked synthetic private report, recipient
notifications/access and a private acknowledgement. Confirm the report is not
publicly visible, remove synthetic personal data and retain only a redacted test
receipt. Do not post a real vulnerability or submit this test before those settings
and recipients are approved. Confirm an alternate confidential route and response
targets; none is invented here. Until then, the route remains BLOCKED for launch.

Security escalation: an appointed responder triages privately, involves only
necessary maintainers, preserves evidence and coordinates patch/advisory timing.
Provider/signing compromise escalates to the signing approver and provider support;
freeze signing/publication until trust is reviewed. Conduct escalation uses the
appointed confidential contact, recusal for conflicts, proportionate action and an
independent appeal contact. If the owner is involved, use the independent contact;
its absence is a staffing blocker, not permission to disclose incident details.

The seven existing community/privacy drafts were reviewed. Their disabled-channel,
unassigned-role, unsigned-preview and network disclosures remain accurate. This
proposal adds no security exclusion, accepted risk or severity suppression.

Official references checked 28 October 2026:
[branch protection](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches),
[CODEOWNERS](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners),
[environments](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments),
[private reporting](https://docs.github.com/en/code-security/how-tos/report-and-fix-vulnerabilities/configure-vulnerability-reporting/configure-for-a-repository).
