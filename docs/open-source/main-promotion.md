# Main licence and security promotion review

This is a narrow proposal from main `0d100cc29dde2ba80755fb1e8af60ffd9d006b16`.
The PR engineering receipt supplies the final head/tree and actual check runs;
this source document cannot contain its own future commit identity. No merge or
publication is authorised by a passing check.

## Target-specific rights review

The [explicit manifest](main-scope.json) and [ledger](main-rights-ledger.json)
enumerate this tree, not develop's 1,077-path grant. Review inputs are the owner's
R1/R2 original-portion declaration in Goal #62, the actual main Git history,
path/content attribution scans and the earlier approved scope. Main-only document
navigation, image portability, Word-template, legacy-shell and test-server code
was assessed as original implementation/harness portions; embedded samples and
templates stay excluded. Prior path membership alone does not grant a different
version's embedded material. Git authorship and absence of licence markers are
supporting evidence, not independent proof of every fragment's originality.

The ledger pins current canonical bytes and each original-portion/excluded
decision. New paths remain blocked unless explicitly reviewed and enumerated.
Its own bytes are bound by the final Git tree to avoid self-reference. Regenerate
with `python scripts/compliance/main_provenance.py --write-ledger` only after
review; generation cannot grant rights. Verify with the same command without the
flag and run `python scripts/compliance/test_main_provenance.py`.

## Static packaging and required checks

The Pages artefact will include root licence/scope/security/notices plus the
existing docs and assets directories. PWA cache `lens-docs-studio-v68` adds those
documents, their local scope/notice dependencies and the known web notice pack.
The shell entry revision follows v68. Legacy static packaging copies the four
root policy files too, avoiding missing cache entries in the WinUI-hosted app.
No rendering/editor/import/export logic or existing vendor runtime asset is
replaced. Tests must run against this older main
application, independently of develop. A fresh offline cache must resolve the
documents, and a previous cache must be replaced using the existing lifecycle.

`OSS provenance` runs on real pull requests targeting main with contents-read
permission, pinned checkout/Python actions and standard-library-only checks. It
verifies exact MIT bytes, explicit scope, all path hashes, unchanged vendor bytes,
original notice ranges, local policy links and static/offline packaging. Negative
tests exercise tampered grants, exclusions, notices, paths and links. It does not
prove complete upstream provenance, legal title or release fitness. The separate
existing `Test static app` job runs main's unit/static/browser suites. Both exact
contexts must actually succeed under GitHub Actions app `15368`; missing/skipped
contexts are not accepted as execution evidence in the receipt.

## Publication and release impact

Production ruleset `26127901` requires both contexts, PRs, zero human approvals,
resolved conversations, strict status checks and no bypass. Develop ruleset
`26127883` retains its 30 checks. This PR does not change either or any repository,
account, security-reporting, Pages-approval or signing setting.

Main controls public GitHub Pages. A future owner-approved merge is a publication
decision: recheck notices/rights gaps, exact source and CI; explicitly authorise
the deployment; then verify the actual Pages URL, licence/security/notices,
subpath links, cache update and offline app after deployment. Current preparation
does not perform these post-deployment checks or trigger production deployment.
The draft remains unmerged pending that decision and resolution/disposition of
the recorded publication gaps. No temporary ruleset weakening is authorised.

No native WPF source, tag, release, signing environment/secret or binary is added.
Paused PRs #38/#50 and existing releases stay unchanged. Native previews remain
unsigned internal candidates; there is no public native WPF release or SignPath
acceptance. The revised provider enquiry is an unsent develop proposal requiring
separate owner approval. PVR is enabled but external submission, notification,
confidentiality and acknowledgement remain unverified. Continuous 250 ms **FAIL**
and historical UI Automation reliability findings remain unchanged; documentation
checks confer no native release certification.
