# Community governance proposal

Goal #58 supplies [exact proposed settings and verification steps](governance-settings.md).
CODEOWNERS and a provenance check are prepared in source; no administration control
is claimed effective merely because its policy is written.

Status: review only, 28 October 2026. The owner has selected a free, community
maintained, open-source direction. There will be no subscription, paid licence
or feature tier. MIT is proposed, not adopted. Community participation does not
imply that a maintainer team or independent reviewers already exist.

The GitHub namespace belongs to `DouglasNLima`; the inspected Git history records
one author name. This establishes repository history, not copyright clearance or
SignPath role membership. Author, Reviewer, Approver, security responder and conduct
appeal roles must be explicitly assigned and published by the owner. SignPath
approval, if obtained, does not transfer project governance to its certificate
publisher.

Authors prepare source and tests. Reviewers inspect changes, dependency provenance
and CI definitions. Approvers inspect the exact source/run/candidate digests and
each signing request. Publication has a separate owner authorisation. Prefer an
independent reviewer and last-push approval; if staffing cannot provide this,
record the gap and agree governance with SignPath before activating signing.
Do not claim separation of duties while one person holds all roles.

Require MFA for all collaborators on GitHub and SignPath, remove obsolete access,
and keep a recoverable access procedure without shared accounts or tokens. MFA
enforcement and role assignments are currently UNKNOWN. A repository policy is
not evidence that MFA is enabled.

Read-only GitHub inspection found `develop` unprotected, no repository rulesets,
private vulnerability reporting disabled, and only a `github-pages` environment
with a branch rule. These are ACTION REQUIRED. Proposed controls are reviewed PRs,
required source-specific native/web checks, resolved reviews, no force-push or
deletion of release branches/tags, reviewed CODEOWNERS for CI/update trust, and a
protected release environment with named required reviewers and prevention of
self-approval. Confirm account support and actual enforced settings. No settings
were changed by this proposal.

Before community launch: confirm rights and licence boundaries; enable and test
private vulnerability reporting; appoint confidential conduct/appeal contacts;
approve the support/security expectations; publish real role membership; and
verify controls using an unauthorised test path. Do not promise a paid SLA or
create a subscription to satisfy these controls.
