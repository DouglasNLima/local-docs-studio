# Open-source and free Windows distribution readiness

Current guidance: [Goal #62 stage receipts](goal62-receipt.md),
[operative scoped MIT](../../LICENSING.md), [governance](governance.md),
[proposed settings](governance-settings.md) and [SignPath packet](signpath-application-packet.md).
The owner-approved first-party notice/scope is implemented in the Goal #62 branch.
Administrative activation, private reporting, provider contact and publication
remain separately gated. The Goal #60 receipt and the Goal #56/58 text below are
historical observations, not the current licence decision.

The [Goal #58 closeout and owner decision packet](goal58-closeout.md) continues
this audit from the authorised PR #57 merge. The receipt below retains its original
Goal #56 source and validation context.

Goal #56 audit/signing-preparation milestone, 28 October 2026.

The owner has chosen free, community maintained distribution with no subscriptions,
paid licences or feature tiers. **MIT remains a proposal.** This milestone delivers
an inspectable audit, policy drafts, a concrete native-notice correction and a
signing/trust design. It does not complete Stable packaging or production certification.

Start with the [licence audit](licence-audit.md), [MIT proposal](licence-proposal.md),
[SignPath eligibility matrix](signpath-readiness.md), [CI adapter plan](signpath-adapter-plan.md),
[trust lifecycle](trust-lifecycle.md) and [release gates](release-gates.md).
Public-facing proposals: [contribution guide](../../CONTRIBUTING.md),
[conduct](../../CODE_OF_CONDUCT.md), [security](../../SECURITY.md),
[support](../../SUPPORT.md), [governance](governance.md),
[Code signing policy](code-signing-policy.md) and [privacy](privacy.md).

## Source and evidence receipt

- Baseline: `5c61d17966befee665856a7993cb174252df5214`, tree
  `d995527c96141dca398a0adcff6c9d007edacde3`, fetched from `develop`.
- Branch: `feature/open-source-readiness`; one Draft PR targets `develop`.
  The PR body records final commit/tree and hosted run outcomes after commit;
  this document does not pretend to contain its own future Git identity.
- Verified baseline [native run 40719397894](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40719397894)
  and [web run 40719398004](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40719398004)
  are successful push runs at that baseline. They do not certify this new branch.
- Native baseline package artefact `12832551036` (`native-validation`), API digest
  `sha256:36d2556a78ee6549b29b08d0f1a7a4d0ac68ff54f76c157ba813f23f2cf738b7`,
  expires 11 November 2026 at 16:17:52 UTC. Downloaded through `gh run download`;
  the outer API digest is recorded, not independently recomputed from the extracted
  download. Measured inner ZIP/file hashes are in the distribution evidence.
- `.953` native pair reports the baseline source/tree, clean source and unsigned
  internal-preview mode. No downloaded installer or application was executed.
  Five legacy public ZIPs were also inspected as data; historical assets were not
  modified. [Release source metadata](evidence/legacy-release-sources.json) and
  [distribution file/hash evidence](evidence/distribution-evidence.json) are retained.
- [Inventory](evidence/inventory.json), [all tracked paths](evidence/tracked-files.csv),
  [npm archive comparisons](evidence/npm-archive-evidence.json) and
  [unmodified upstream notices](evidence/upstream-notices/index.json) are review inputs.

## Validation receipt

The original update-filtered suite passed **51/51**. Seven additional cases cover
manifest-key bridge/retirement, empty publisher bootstrap, RC→Stable ordering,
channel filtering, drafts and replay. The expanded update suite passed **58/58**
with ephemeral RSA keys and inert bytes. No production trust or actual signing is
implied. The existing signing-adapter suite passed **14/14** mock outcomes.

The new notice control script passed **5/5** outcomes, including missing declared
licence refusal. An actual restored-package check independently verified **14**
original notice files by SHA-256, including Sugiyama and Core/WPF runtime notices.
This is notice-generation validation, not installed-package certification.

Static checks passed (50 modules, 57 shell assets, 150 cached vendor assets and 56
runtime external-dependency scans). `npm run test:unit` was attempted but is not a
script on this `develop` baseline; it ran no tests. The correct scripts are
`test:static` and `test:browser`. Browser/final hosted results are in the PR receipt.

The first full Core run passed **2,297/2,298** and failed the existing ER
`complete-detailed` case at its unchanged six-second rendering deadline. A subsequent
isolated run passed **6/6** cases of that theory. Contention is not established as
the cause. The original failure remains evidence; later runs do not erase it or
waive the independent Continuous 250 ms gate. Final hosted results must be reviewed
separately. Raw local TRX files remain under ignored `artifacts/compliance/tests/`;
the retained compact [validation evidence](evidence/validation.json) records counts
and failure without personal filesystem paths.

## Owner decisions and continuation

Technical distribution readiness is **NO-GO**; the proposal is **ready for review**.
Remaining decisions are concrete: first-party ownership and MIT scope/holder/year;
logo/documentation terms; vendor and legacy notice/provenance remediation;
confidential reporting and conduct contacts; named MFA/review/signing roles and
protected release controls; and whether/when to apply to SignPath with the native
distribution gap explicitly disclosed.

After rights and provider decisions, separately authorise the Stable builder,
actual signing integration, public trust bootstrap, signed RC→signed upgrade and
clean-Windows certification. Candidate `1.0.0` is not a release commitment. Keep
paused Draft PRs #38/#50 unchanged and Goal #56 open. No tag, public release,
account application, production credential, trust activation, paid service or
automatic merge is authorised by this work.
