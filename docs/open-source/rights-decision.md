# Exact first-party MIT decision proposal

Goal #58 prepares an owner decision; **no licence is adopted**. The
[path ledger](evidence/goal58/rights-ledger.csv) names every tracked/proposed path
at the final source freeze. It is a rights-review classification, not an ownership
certificate. The final PR receipt binds it to a commit/tree. Git author names,
public visibility, generated files and successful tests establish no copyright title.

## Decisions to approve or correct together

| ID | Exact proposed boundary | Decision and evidence needed |
|---|---|---|
| R1 | Project implementation: `assets/scripts/**`, shell/style/config files, `src/native/**`, `src/windows/**`, `scripts/**`, `tests/**`, `installer/**`, `.github/**`, excluding the categories below | OWNER ACTION REQUIRED: identify the legal holder(s), employment/commission restrictions, contributor assignments or permissions, and copied fragments. Technical structure identifies project implementation only |
| R2 | First-party `README.md`, root policy documents and `docs/**`, with original examples, excluding preserved upstream text, screenshots and third-party quotations | OWNER ACTION REQUIRED: confirm documentation and example rights; approve the same MIT grant for the cleared files |
| R3 | `tests/fixtures/**`, `assets/scripts/registries/**` and examples within tests/docs | OWNER ACTION REQUIRED: confirm original/synthetic content or list upstream grants. Remove uncleared customer/private material from the adoption set; do not assume a synthetic file name proves permission |
| R4 | SVG/PNG/ICO and other artwork; `docs/native/images/**`, visible screenshots in `docs/native/evidence/**`, generated native icon and its SVG input | OWNER ACTION REQUIRED: identify creator/input permissions and subjects; approve copyright separately. Proposed default: exclude branding and screenshots from MIT until individually cleared |
| R5 | `docs/open-source/evidence/**` and historical machine evidence | OWNER ACTION REQUIRED: approve publishing permissions and the original report structure only; preserved upstream grants, captured content and metadata retain their separate status |
| R6 | `assets/vendor/**` except its project catalogue; `src/native/LensDocsStudio.Core/Rendering/PinnedMermaider/**`; `docs/native/licences/**`; NuGet/runtime/installer binaries | NOT APPLICABLE to first-party MIT: preserve upstream licences, copyrights, local-modification descriptions and font conditions. No blanket SPDX MIT headers |
| R7 | Customer/user-created or imported documents, private workspaces/templates, local state and exported user content | NOT APPLICABLE: ownership stays with the respective owners; using the app does not grant or change rights. No private workspace was inspected for this proposal |

Proposed copyright line for approval, **not a statement of verified title**:

```text
Copyright (c) 2026 DouglasNLima
```

`DouglasNLima` is the known public repository account, not a verified legal name.
The owner must either approve that exact public attribution as the rights holder,
replace it with the correct individual/entity name, or enumerate multiple holders.
Observed repository commit years are 2026 only; the owner must correct the year
range if pre-repository work or other rights require it. This observation does not
prove the date of creation or ownership. Keep the proposal non-operative until
both the copyright line and the exact cleared path set are approved.

The candidate grant is the unmodified MIT text already reproduced in
[licence-proposal.md](licence-proposal.md), with only its placeholder copyright
line replaced by the approved line above. The accompanying adoption scope file
must list the cleared path ledger and all R3–R7 exclusions. A root MIT file without
those boundaries would be misleading. There is no package licence metadata change
or new first-party SPDX identifier in this goal.

After approval, the separate adoption PR should add the operative root licence,
the approved scope/exclusion file, and narrowly applicable SPDX identifiers.
Keep upstream dual-licence choices and font terms intact. Use `NOASSERTION` for
unresolved binary/file conclusions. Do not label the whole installer MIT.

MIT would permit commercial reuse and resale by recipients. Free official project
distribution is not an extra licence restriction. Copyright approval grants no
trademark ownership, endorsement or permission to impersonate the project; branding
treatment needs an explicit owner decision and must not silently restrict MIT code.

Decision to return: approve/correct the holder and year line; identify cleared R1/R2
paths and exceptions; supply provenance for R3/R4; confirm R5 publication scope;
retain R6/R7 exclusions. **MIT adoption readiness: NO-GO until that decision.**
