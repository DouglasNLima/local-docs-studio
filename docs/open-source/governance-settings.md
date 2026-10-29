# Active GitHub controls for one maintainer

**Active and independently verified on 29 October 2026.** The owner authorised
the three production changes and, separately, PVR enablement. This document is a
current status guide; the original plans, payloads and historical receipts remain
unchanged. No further administrative mutation is authorised by this document.

- [Production activation receipt](https://github.com/DouglasNLima/local-docs-studio/issues/62#issuecomment-6499115737):
  develop ruleset `26127883` (30 checks), main ruleset `26127901` (two checks),
  both active, zero approvals, no bypass, strict checks from GitHub Actions app
  `15368`, resolved conversations, deletion and force-push protection.
- Merge commits enabled; squash, rebase and automatic merging disabled.
- [PVR execution receipt](https://github.com/DouglasNLima/local-docs-studio/issues/62#issuecomment-6501362732):
  `enabled: true`; `PVR_ENABLED_EXTERNAL_FLOW_UNVERIFIED`.
- [Disposable test and cleanup](https://github.com/DouglasNLima/local-docs-studio/issues/62#issuecomment-6498331447):
  expected controls exercised; main deletion was blocked by the default-branch
  safeguard, so isolated attribution to its deletion rule remains unverified.
  No destructive production test or demonstration merge was performed.
- Sole-maintainer use requires no second human reviewer. Recorded owner self-review
  remains a human procedure; MFA, external reporting and signing are separate gaps.

The default branch's missing `OSS provenance` workflow still needs the separately
reviewed main promotion PR and real passing required checks. Active rules alone
do not prove that the older main source produces both required contexts.

## Historical pre-activation observation

[Read-only evidence](evidence/goal62/governance-observation.json) records a public
personal repository, both branches unprotected (HTTP 404: Branch not protected),
no rulesets, private vulnerability reporting disabled, auto-merge disabled, and
merge/squash/rebase all allowed. @DouglasNLima resolves with admin access.
CODEOWNERS on develop has no API errors. Only github-pages exists, with a branch
policy and no required reviewer. MFA is unverified. No settings were changed.

The [Stage B receipt](goal62-github-receipt.md) binds exact payload SHA-256 values,
rollback and separate activation approvals. [GitHub's current availability
documentation](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets)
supports branch rulesets for this public personal repository even on GitHub Free.
The execution receipts above supersede this historical observation for current status.

## Approved configuration and original activation instructions

The completed activation used the [develop payload](configuration/develop-ruleset.json)
and [main payload](configuration/main-ruleset.json) with
`POST /repos/DouglasNLima/local-docs-studio/rulesets`.
They match `refs/heads/develop` and `refs/heads/main` exactly and use active
enforcement with empty bypass lists. Administrators have no hidden bypass.
Re-read settings first; preserve later unrelated rules.

| Setting | develop | main |
|---|---|---|
| Pull request required | Yes | Yes |
| Required approving reviews | 0 | 0 |
| Mandatory CODEOWNER approval | No | No |
| Latest-push approval by another person | No | No |
| Passing CI with up-to-date base | Yes | Yes |
| Resolve review conversations | Yes | Yes |
| Force push / branch deletion | Blocked | Blocked |
| Bypass actors | None | None |
| Merge methods | Merge commit only | Merge commit only |
| Automatic merge | Disabled repository-wide | Disabled repository-wide |

The pull_request rule requires a PR independently of
`required_approving_review_count: 0`. Separate flags disable CODEOWNER and
last-push approval. No linear-history rule is added.
`dismiss_stale_reviews_on_push: true` dismisses stale external approvals but,
with zero mandatory reviews, does not enforce the owner's decision procedure.
Every changed SHA still requires renewed owner self-review.

The applied [repository settings payload](configuration/repository-settings.json),
sent by `PATCH /repos/DouglasNLima/local-docs-studio`, retains merge commits,
disables squash/rebase and auto-merge, and disables automatic merged-branch
deletion. Protected-branch deletion is separately blocked by the rulesets.

### Exact status checks

All selected names were observed from GitHub Actions, integration ID **15368**,
on PR #61's exact head; see [check-context evidence](evidence/goal62/check-contexts.json).

- Both branches: `Test static app` and `OSS provenance`. Develop uses a browser
  matrix aggregate; the older main workflow runs both browsers in one job.
- Develop also: `native`, `diagram-arrangement`, `arrangement-responsiveness`,
  `complex-er-preview`, `state-installed-authoring`, `er-installed-authoring`,
  `class-installed-authoring`, and all 21 exact
  `companion-installed-authoring (Family)` names in its payload.
- Main: no native context, because current native workflows do not run on PRs to
  main. Native promotion needs the accepted develop SHA/tree and full native
  receipt in the owner decision.

Pages validation runs on all PRs/pushes. Develop's OSS provenance workflow runs on
PRs to develop/main and pushes to develop. The older main tree still needs its own
workflow bootstrap; a workflow's presence on develop does not make it run for a
main-based PR. It need not run after main pushes to gate main PRs.
Native validation runs on develop PRs/pushes without path filters. Do not select
deployment, historical replay or conditional diagnostic jobs as required checks.

GitHub can treat a skipped/neutral check conclusion as satisfying a required
check. Therefore the owner must verify that the expected acceptance jobs actually
ran successfully on the reviewed SHA, not merely that the merge box is green.
The web aggregate rejects an unsuccessful browser matrix. Native installed jobs
can be skipped after a prerequisite failure; the required native prerequisite
must also succeed. Record every skip and do not call it a test pass.

The controlled Continuous 250 ms replications are deliberately excluded from
ordinary integration protection. Their **FAIL** remains visible and blocks native
production/release acceptance; no threshold, result or workflow is changed.
Other selected failures still block integration; no native failure is excused.
This intentional treatment was part of the approved activation. Other suites
remain receipt evidence even when not minimum mandatory contexts.

### Original validation procedure and completed execution

The sandbox and administrative steps below were separately authorised and
completed; their receipts and the main deletion qualification are linked above.
They are retained as procedure, not permission to repeat a write or a test.

1. Re-read settings, rulesets, protections and check names/app IDs; retain a dated
   snapshot and confirm each required context executes for its target branch.
2. Reproduce the rules in an owner-approved disposable repository/test refs.
   Do not assume evaluate mode is available. Test refs need a separately approved
   inert workflow emitting test contexts because production branch filters do not
   run there.
3. On disposable refs only, verify direct/force pushes and deletion are rejected;
   missing/failing checks and unresolved conversations block merge; and an owner
   PR with passing checks/resolved conversations can merge without an approving
   review after a recorded decision. A new push must stale earlier check results.
   Do not test destructive writes on main/develop.
4. Apply only the approved payloads, read back enforcement, bypass lists, merge
   settings and effective branch rules, and inspect a harmless production-target
   PR in the UI. Merging that PR needs a separate owner decision.
5. Record any API error or behavioural gap. Production write/readback and disposable
   enforcement evidence now exist; preserve their qualifications. Never add bypass.

## Publication and future environments

Direct unreviewed publication is prohibited by policy. Current Pages deployment
automatically follows a default-branch push without a reviewer. Active PR rules
provide a reviewed route but cannot enforce the separate publication decision.
Before publication, record explicit owner authorisation; separately approve and
verify a manual github-pages gate if technical publication enforcement is required.
This current gap is not represented as implemented protection.

A future native-production environment would require reviewer @DouglasNLima,
**Prevent self-review disabled**, administrator bypass disabled and only approved
release refs. GitHub needs one listed reviewer; the owner can approve their own
initiated deployment. This is manual owner approval, not independent approval.
Build validation, signing and publication retain separate decisions. No environment,
secret, signing or publication workflow is activated. Tag rules need a later exact
proposal and approval; no tag change is made here.

Use the [private reporting plan](private-reporting-plan.md) for completed enablement
and the remaining external verification requiring separate approval. Verify MFA
through account settings with status-only evidence after owner authorisation.

## Official semantics checked 29 October 2026

[Rules REST API](https://docs.github.com/en/rest/repos/rules),
[available rules](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets),
[branch protection API](https://docs.github.com/en/rest/branches/branch-protection),
[status-check conclusions](https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/troubleshooting-required-status-checks),
[repository settings API](https://docs.github.com/en/rest/repos/repos#update-a-repository),
[CODEOWNERS](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners)
and [environments](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments).
Published schemas verify the proposed fields; live GET responses verify current
state. Neither substitutes for the gated write/readback/behavioural test.
The observation also records the SHA-256 of GitHub's published OpenAPI document
used to inspect the exact rule fields, zero-review minimum and merge-method enum.
