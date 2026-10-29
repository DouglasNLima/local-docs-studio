# Goal #62 — Stage B: Solo Maintainer GitHub controls

Status: **PREPARED AND LOCALLY VALIDATED / OWNER APPROVAL REQUIRED**.
No administrative mutation or disposable enforcement experiment was performed.

## Observed state and proposal

The [29 October read-only snapshot](evidence/goal62/governance-observation.json)
records public visibility, owner admin permission, no branch protections (both
GETs return 404 Branch not protected), no rulesets/effective rules, no CODEOWNERS
errors on develop, merge/squash/rebase enabled, auto-merge disabled and PVR
`enabled:false`. Only github-pages exists, with no reviewer and admin bypass
available. MFA is **UNVERIFIED**, not assumed disabled.

[GitHub availability](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets)
supports branch rulesets in public Free repositories; no paid-plan assumption is
needed. [Current rule schemas](evidence/goal62/github-rule-schema.json) explicitly
allow zero approving reviews. API/schema validation is not behavioural enforcement.

Both proposed rulesets require PRs, zero human approvals, no CODEOWNER/last-pusher
approval, resolved conversations, blocked deletion/force push and no bypass actors.
Repository settings retain merge commits and disable squash/rebase/auto-merge.
[Observed checks](evidence/goal62/check-contexts.json) bind all 30 develop and two
main contexts to GitHub Actions app 15368 on PR #61's head. Native checks do not
run on main PRs and are not required there. Continuous 250 ms remains inherited
**FAIL**, excluded from ordinary integration rules but blocking release unchanged.
Other required failures block integration. Skipped jobs are never reported as passes.

## Exact approval packet and rollback

The machine-readable [activation plan](configuration/admin-activation-plan.json)
binds each canonical-LF payload SHA-256, HTTP method and exact repository target:

| Decision | Target and payload | Rollback scope |
|---|---|---|
| B1 | POST repository rulesets with develop-ruleset.json and main-ruleset.json | DELETE only each ID returned by these approved POSTs, after matching name/ref/body; no unrelated rule deletion |
| B2 | PATCH repository with repository-settings.json | PATCH only the five recorded settings using repository-settings-rollback.json |
| B3 — separate | Bodyless PUT private-vulnerability-reporting; recipient @DouglasNLima | Separately approved bodyless DELETE of intake setting, preserving private advisories |
| B4 — separate | Owner's private MFA/status and alert configuration verification | Status-only evidence; no secrets, recovery codes or unapproved account changes |
| B5 — separate | Authorised genuine external reporter and exact synthetic private test | Private closure only; never publish an advisory or real vulnerability |

Re-read all state before approval/application. Drift invalidates the recorded
before-state and rollback; prepare a revised packet. Production application must
follow a separately approved safe disposable test. No administrative write,
including rollback, is pre-authorised by this source change.

## Disposable enforcement validation still awaiting target-specific permission

Proposed isolated target: a new public, empty
`DouglasNLima/local-docs-studio-governance-sandbox-62` repository. This name is
only a proposed target, not an existing resource or creation permission. No
production branch or historical release is used for destructive tests.

The [exact sandbox transaction](configuration/sandbox-transaction.json) includes
the repository-create body, encoded inert workflow, ref/commit/PR templates,
copy of both rulesets, merge settings, expected rejected requests and identity-bound
cleanup. The [readable inert workflow](configuration/sandbox-workflow.yml.txt)
runs no product code and has no signing, secrets or deployment. It is stored
outside .github/workflows and is not active. Returned object IDs/SHAs are the only
runtime substitutions; targets and test content are fixed. Creation must abort
if that repository already exists or its initial default branch is not main.
The force-update probe runs only after the target advances from its initial SHA,
so a no-op cannot be mistaken for enforcement. Production payloads and this sandbox plan
require separate explicit approval, including sandbox merges and cleanup.

Required outcomes: reject direct and force pushes/deletion; block absent/failing
checks and unresolved conversations; stale checks after a new push; allow an
owner-authored PR merge with passing checks, resolved conversations and zero
approvals. Read effective rules including inherited rules and confirm empty
bypass. Retain every API error. Abort on mismatch or loss of admin recovery; do
not invent a bypass. Creation/deletion and a sandbox merge each need the exact
approved test transaction. Dry-run success is not proof of enforcement.

After separately authorised production activation, read back rules, settings and
effective branch rules and inspect an innocuous PR. Any production merge is a
separate owner decision. PVR requires enabled readback, owner alerts and actual
external reporter evidence; without a tester report **ENABLED / EXTERNAL-FLOW
UNVERIFIED**, never operational.

## Validation and publication boundary

Local governance controls check exact contexts/app IDs, branch scopes, zero-review
semantics, no bypass and merge-only settings. Negative tests reject second-person
deadlocks, wrong/unobserved app contexts, native-on-main and Continuous-in-integration.
The final PR receipt supplies executed counts and terminal CI.

Pages still deploys on a default-branch push without a manual reviewer. This
stage does not redesign or activate that boundary. The sole workflow delta is
delivery of licence/scope/notices with static assets; triggers, permissions,
environment and deployment steps are unchanged. A separate owner publication
decision remains necessary before any main merge.
