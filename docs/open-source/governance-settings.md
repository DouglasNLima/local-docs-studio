# Proposed GitHub controls for one maintainer

**OWNER APPROVAL REQUIRED before any administrative mutation.** The configuration
JSON files are reviewable payloads, not applied settings or active workflows.

## Observed state

[Read-only evidence](evidence/goal60/governance-observation.json) records a public
personal repository, both branches unprotected (HTTP 404: Branch not protected),
no rulesets, private vulnerability reporting disabled, auto-merge disabled, and
merge/squash/rebase all allowed. @DouglasNLima resolves with admin access.
CODEOWNERS on develop has no API errors. Only github-pages exists, with a branch
policy and no required reviewer. MFA is unverified. No settings were changed.

## Exact proposed configuration

After separate approval, use the [develop payload](configuration/develop-ruleset.json)
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

The [repository settings payload](configuration/repository-settings.json), for a
later `PATCH /repos/DouglasNLima/local-docs-studio`, retains merge commits,
disables squash/rebase and auto-merge, and disables automatic merged-branch
deletion. Protected-branch deletion is separately blocked by the rulesets.

### Exact status checks

All selected names were observed from GitHub Actions, integration ID **15368**,
on PR #59's exact head; see [check-context evidence](evidence/goal60/check-contexts.json).

- Both branches: `Test static app` (the aggregate fails unless both Chromium and
  Edge succeed) and `OSS provenance`.
- Develop also: `native`, `diagram-arrangement`, `arrangement-responsiveness`,
  `complex-er-preview`, `state-installed-authoring`, `er-installed-authoring`,
  `class-installed-authoring`, and all 21 exact
  `companion-installed-authoring (Family)` names in its payload.
- Main: no native context, because current native workflows do not run on PRs to
  main. Native promotion needs the accepted develop SHA/tree and full native
  receipt in the owner decision.

Pages validation runs on all PRs/pushes. OSS provenance runs on PRs to develop/main
and pushes to develop; it need not run after main pushes to gate main PRs.
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
This intentional treatment is part of the proposed owner approval. Other suites
remain receipt evidence even when not minimum mandatory contexts.

### Safe validation after separate authorisation

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
5. Record any API error or behavioural gap. No write has been sent in this goal:
   server acceptance and enforcement tests remain pending. Never quietly add bypass.

## Publication and future environments

Direct unreviewed publication is prohibited by policy. Current Pages deployment
automatically follows a default-branch push without a reviewer. Proposed PR rules
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

Use the [private reporting plan](private-reporting-plan.md) for separately authorised
enablement/testing. Verify MFA through account settings with status-only evidence.

## Official semantics checked 28 October 2026

[Rules REST API](https://docs.github.com/en/rest/repos/rules),
[available rules](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets),
[branch protection API](https://docs.github.com/en/rest/branches/branch-protection),
[repository settings API](https://docs.github.com/en/rest/repos/repos#update-a-repository),
[CODEOWNERS](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners)
and [environments](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments).
Published schemas verify the proposed fields; live GET responses verify current
state. Neither substitutes for the gated write/readback/behavioural test.
