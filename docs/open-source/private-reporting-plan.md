# Private vulnerability reporting activation plan

**OWNER APPROVAL REQUIRED. Disabled and not operationally verified.**
The sole security maintainer/intended recipient is **@DouglasNLima**.
No second responder, email address or service level is invented.

GitHub's [configuration guide](https://docs.github.com/en/code-security/how-tos/report-and-fix-vulnerabilities/configure-vulnerability-reporting/configure-for-a-repository)
documents Settings → Security and quality → Advanced Security → Private
vulnerability reporting → Enable. Verify current UI labels. The API equivalent is
`PUT /repos/DouglasNLima/local-docs-studio/private-vulnerability-reporting`
without a body. Do not execute before separate authorisation.

The [exact administrative decision packet](goal62-github-receipt.md) records
this bodyless PUT separately from branch/repository settings and from any test
report. Rollback, if specifically authorised, is the bodyless DELETE on the same
endpoint; preserve any existing private advisories and never publish them.

After approval:

1. Recheck settings and owner access/MFA. Configure the owner's security-alert
   notifications; no additional maintainer is needed.
2. Enable reporting and require GET readback `enabled: true`.
3. Inspect Security → Advisories → Report a vulnerability from a genuine
   non-maintainer session. Without an authorised tester/session, record behavioural
   verification as BLOCKED. Do not invent an account or second maintainer.
4. Obtain permission covering submission of a private synthetic report titled
   `Synthetic reporting-channel verification — no vulnerability`. Use inert text
   only, no exploit, real vulnerability or personal data. Request no CVE and
   publish no advisory. Verify notification, owner access and private
   acknowledgement visible to the reporter.
5. Confirm a signed-out session cannot read the report. Retain only date, tested
   roles, enabled status, delivery/acknowledgement and privacy result publicly.
   Keep report IDs/URLs and account details private. Close/dismiss the synthetic
   report through the available private workflow; never publish it to finish.
6. If only the API enablement is verified, record **ENABLED / EXTERNAL-FLOW
   UNVERIFIED**. Actual alerts, reporter access and private delivery need evidence.
   Advertise the route as operational in SECURITY.md only after success. Failed
   delivery, access or privacy checks leave it unverified.

The intended entry is [Security Advisories](https://github.com/DouglasNLima/local-docs-studio/security/advisories).
The /security/advisories/new route must be tested as a non-maintainer; an
administrator's advisory-creation form is not proof of researcher access.
Until verification, SECURITY.md offers only a public request for private contact
without sensitive details. One volunteer may be unavailable; there is no SLA
or independent project escalation service.
