# SignPath eligibility review

See the [Goal #58 application decision packet](signpath-application-packet.md) for
the current evidence, owner classifications, exact binary selectors and unanswered
provider decisions. The observations below are retained as the Goal #56 baseline.

Checked 28 October 2026 against the official [conditions](https://signpath.org/terms.html),
[Foundation description](https://signpath.org/) and [application page](https://signpath.org/apply).
The conditions page itself is labelled Draft; acceptance and interpretation belong
to SignPath. No application was submitted. The form fields were not exposed by
the page reader, so no complete application-form checklist is claimed.

| Criterion | Assessment | Evidence / next action |
|---|---|---|
| OSI-approved licence | BLOCKED | No root licence; MIT is a proposal awaiting rights clearance and approval |
| Component eligibility | ACTION REQUIRED | Native permissive package metadata; font terms, copied adaptations and toolchain boundaries need review. Legacy Windows App SDK has separate Microsoft terms |
| Active maintenance | PASS, observable activity only | Current develop and successful post-merge runs at `5c61d17`; not a reputation judgement |
| Existing released native product | BLOCKED | Five public `v0.1.0-dev*` releases are `LensDocsStudio.Windows`; no `native-v*` release. Actions previews do not establish public native distribution |
| Verifiable reputation/control | UNKNOWN | Provider decides; source activity and legacy releases do not prove acceptance |
| Documented functionality | PASS | Root README and native/web user guides exist |
| No harmful functionality | ACTION REQUIRED | Documentation studio intent; formal build/dependency security review remains required |
| Own binary boundaries | ACTION REQUIRED | Allowlist native apphost/installer only; ask provider about adapted source, apphost and Inno/uninstaller boundaries; never sign upstream DLLs as project code |
| Privacy, system changes, uninstall | ACTION REQUIRED | Existing user-controlled workflows and uninstall; publish/approve disclosure and installer presentation |
| MFA and named roles | UNKNOWN | No proof of all collaborators' MFA or signing roles |
| Public signing policy | ACTION REQUIRED | Draft prepared; credit and memberships withheld until approved |
| Source/build origin | ACTION REQUIRED | Existing hosted checks; no configured SignPath trusted build integration |
| Metadata restrictions | ACTION REQUIRED | Product/version constraints required; preview naming is not final Stable metadata |
| Manual signing approval | BLOCKED | No SignPath project/policy or protected release environment |

The Foundation's certificate identifies the Foundation as publisher and its
service can suspend signing or revoke the certificate. It is not a certificate
issued to the repository owner. OSS upstream libraries may be included unsigned
under the published terms; they must not be indiscriminately re-signed. System
library exceptions do not automatically make every proprietary SDK eligible.

Prepare an owner-reviewed application packet containing project/download URLs,
the approved licence and notices, actual native released-distribution evidence,
maintenance/reputation evidence, named roles/MFA, signing/privacy policies,
source-to-binary process and proposed binary boundaries. Ask for a decision on
uncertain components and signer rollover before choosing pins. Do not publish an
unsigned native release simply to fill the distribution gap without permission.

## Fallback decision

[Microsoft's signing guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)
states that Store MSIX submissions are re-signed by the Store after certification;
Store MSI/EXE submissions still need publisher Authenticode signing. Evaluate MSIX
identity, account eligibility, certification, file associations, per-user state,
desktop integration and migration before selecting it. Store-managed updates would
need a separate product path; do not run both updaters against the same installation.
No account, MSIX project, payment or store submission was created. The current
Inno/GitHub route remains intact. No zero-cost acceptance or SmartScreen outcome is
guaranteed for a proposed binary.
