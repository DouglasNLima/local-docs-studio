# Native Stable certification gates

This milestone ends at the licence audit and signing preparation requested by the
owner. It does not complete every workstream in [Goal #56](https://github.com/DouglasNLima/local-docs-studio/issues/56).
The issue stays open. Existing unsigned internal-preview CI remains separate.

| Criterion | Implementation / test status | Certification and next requirement |
|---|---|---|
| Immutable source/dependency inventory | IMPLEMENTED / TESTED | Baseline source evidence only; ownership and remaining bundle provenance BLOCKED |
| Native notice retention | IMPLEMENTED / TESTED | New candidate package inspection required; historical omissions remain recorded |
| Community/security/licence policies | Scoped first-party MIT adopted on develop; production solo-maintainer protections ACTIVE AND VERIFIED; PVR enabled | External reporting flow UNVERIFIED; default-branch policy promotion and publication remain separate decisions; see [current controls and receipts](governance-settings.md) |
| SignPath eligibility/governance | Matrix and adapter plan IMPLEMENTED | Acceptance, role/MFA verification, released native distribution and actual CI integration BLOCKED |
| Native manifest/channel controls | IMPLEMENTED / TESTED with ephemeral keys and inert bytes | No production identity or Windows signing certification |
| Planned manifest rollover / RC precedence | TESTED in Core | Real certificate rollover, overlap and emergency recovery BLOCKED |
| Stable/RC package builder | NOT IMPLEMENTED in this milestone | Current builder accepts preview only; future separate builder must enforce the adapter contract |
| Exact final-source native/web gates | Recorded per delivery receipt | A green historical baseline does not certify a later source |
| 24 native installed authoring families / UIA / themes | Existing hosted preview suites | New receipt must bind actual run/source; not SDK-free or physical-reader certification |
| SDK-free Windows 10/11, supported revisions | BLOCKED / NOT RUN | Authorised clean environments, verified first install/restart/uninstall |
| Historical preview→Stable migration | BLOCKED / NOT RUN | Original immutable old installation and real state, assets, drafts, sidecars and interrupted-save cases |
| Signed Stable A→B | BLOCKED / NOT RUN | Actual approved signer, RSA manifest, Windows revocation and restart evidence |
| Repair, rollback, failed install, immutable release assets | BLOCKED for production | Disposable signed lifecycle; no silent downgrade or user-data rollback |
| Physical DPI 125/150/200%, multi-monitor, screen reader | BLOCKED / NOT RUN here | Dedicated hardware/operator evidence; do not substitute geometry or UIA |
| Independent Continuous 250 ms requirement | Existing FAIL retained | Separate owner decision; no threshold changed or unrelated pass substituted |
| Formal build/release security review | BLOCKED / pending independent review | Review exact permissions, approvers, provenance and secret boundaries before enabling signing |

Technical distribution readiness: **NO-GO**. Audit/signing-design review: **READY
FOR REVIEW with recorded findings**. SignPath eligibility: **NOT ESTABLISHED**.
Production publication: **NOT AUTHORISED**. Scoped first-party MIT is operative
on develop at `709564dda30a57cf1e98ba07566f510c1636e05c`; main promotion remains a
separate, target-specific publication decision. No production trust pins,
signing request, tag, release, account application or merge occurs in this phase.
