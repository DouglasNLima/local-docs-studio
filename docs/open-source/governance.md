# Solo-maintainer governance

Policy prepared for [Goal #60](https://github.com/DouglasNLima/local-docs-studio/issues/60),
28 October 2026. One human, **@DouglasNLima**, holds all current responsibilities.
There is no second maintainer or independent human reviewer. Automated checks and
separate-agent reviews are technical evidence, never independent human approval.

The owner has confirmed an entirely personal project: no code was created for
employers/customers and no employer/customer contractual restrictions apply.
The project will remain free, without subscriptions or paid feature tiers.
The owner approved the exact MIT notice and restricted R1/R2 [scope](rights-decision.md).
[LICENSE](../../LICENSE) now grants the enumerated original portions in this source
copy; [LICENSING.md](../../LICENSING.md) preserves all exclusions.

| Role | Responsible |
|---|---|
| Project Owner | @DouglasNLima |
| Lead Developer / Committer | @DouglasNLima |
| Reviewer of external contributions | @DouglasNLima |
| Release Authoriser | @DouglasNLima |
| Security Maintainer | @DouglasNLima |
| Community and Support Maintainer | @DouglasNLima |

These are multiple responsibilities held by one person, without separation of
duties. CODEOWNERS routes every path to this account; it does not prove copyright
or create a signing identity. No independent appeal contact exists.

## Owner self-review and external contributions

1. Use a focused feature branch and PR to `develop`; promotion to `main` also
   requires a PR. State the outcome, changed boundaries and exclusions.
2. Record source commit/tree and a change/risk assessment covering user data,
   dependencies, licence scope, workflows, trust and publication effects.
3. Inspect the actual diff and relevant tests. Record hosted run URLs, attempts,
   failures and skips against that source. Refresh affected checks and the
   decision whenever source changes.
4. Record an explicit human owner decision in the PR before merge: SHA/tree,
   reviewed risk, validation, limitations and merge/defer decision. An agent must
   not write this as if it came from the owner. Self-review is not a GitHub
   approving review.
5. Use a merge commit after that decision and applicable checks. No auto-merge.
   A separate owner decision is required before public release. For a main merge
   that triggers Pages publication, record that separate publication decision
   before merging.

Suggested owner record, not an approval: `Reviewed <SHA>/<tree>; risk <assessment>;
checks <URLs/outcomes>; unresolved <limitations>; decision <merge/defer>.`
A release decision additionally names version/channel, candidate hashes,
signer/trust verification and distribution scope.

External contributions require the human maintainer's review of source,
permissions, dependency provenance and tests before integration. An AI review
cannot substitute. Contributor rights and original notices must be checked;
new paths require explicit scope review; see CONTRIBUTING.md for inbound MIT
contributions and separately identified exceptions.

## Controls and limitations

The [active configuration](governance-settings.md), verified on 29 October 2026, requires PRs and CI with
**zero** mandatory approving reviews, no mandatory CODEOWNER/last-push approval
and no bypass actors. GitHub cannot verify the meaning of a decision comment:
recorded self-review is an auditable human procedure, not a technical approval gate.

Continuous 250 ms **FAIL** remains a production/release acceptance blocker at the
unchanged threshold. The active integration checks deliberately exclude that
performance gate so ordinary technical work can continue, with its failed runs
disclosed in any integration decision. No failure is waived or reclassified.

MFA is required for the sole GitHub maintainer and future SignPath access. Current
verification is unknown; a null API field does not mean disabled. Verify account
settings and retain only account/date/enabled status, never passwords, recovery
codes or private credentials. Keep recovery information privately, use no shared
accounts, review future appointments and remove obsolete access.

Follow [SECURITY.md](../../SECURITY.md) for the enabled, externally unverified private reporting
route and emergency signing/publication suspension.
[Conduct policy](../../CODE_OF_CONDUCT.md) states the lack of independent appeal.
Provider acceptance and [release gates](release-gates.md) remain separate.
