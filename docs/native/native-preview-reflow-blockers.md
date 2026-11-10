# PR #83: two blocking findings corrected

The corrected product rejects incompatible retained scroll anchors before
starting a loading request. The replacement inert provenance proposal uses
canonical committed Git bytes and independently checks working-source integrity.
Independent review of these corrections is pending; provenance remains inactive.

Starting source: `16b4b6864fd6cd72e3e7cf2bfb42d25af713ea85`, complete tree
`14cec47913bce3732e23eac585963362f8d93eea`, against accepted develop
`3cd08bc2de31564e06973c5211e1aa231731f9cb`. Local and remote identities were
verified before editing. Corrected product/control commit:
`932335027314fe710671d54266367a008fca5547`, complete tree
`c58468fc41222f56eb38f277950486686e820553`, native subtree
`4deeb6f0b54fd2e982525968500fbbe94030563d`.

## Pages transition

Before: Continuous → Editor → Settings: Pages → Preview retains a scroll anchor.
The original caller starts loading, the scroll-only helper returns false, and
valid visible Pages content is labelled `Failed` / `Preview unavailable`.
Both the initial anchor assertion and the settled failure observation are
retained as FAIL records in [the evidence](evidence/preview-reflow-blockers/results.json).

After: the caller checks current source/document ownership and effective Scroll
mode before creating a CTS or loading ticket. Incompatible or obsolete anchors
are cleared without changing the valid document or publishing Ready/Failed.
The existing source, focus, caret, selection, zoom, Find and cancellation paths
remain unchanged. No sleeps, forced layout loops or restoration retries were
added to production.

The new shown WPF/UIA control opens the real Settings window and executes its
Apply command through the production callback. It covers direct Preview and
Split returns, Preview → Split, return to Continuous and rapid document/layout
replacement. It checks effective mode, source version, active/rendered identity,
visible UIA text, preservation of editor/reader state and absence of obsolete
restoration or inappropriate failure. The existing 30-second test observation
bound is unchanged.

```powershell
dotnet run --project tests/native/preview-reflow/PreviewReflowControls.csproj -c Release -- --pages-transition tests/fixtures/native-preview-reflow artifacts/pages-fresh
```

For actual Windows High Contrast, enable it through Settings first, use a fresh
output directory and add `--expect-high-contrast`. The control asserts Win32 and
WPF flags; it does not alter OS appearance or simulate hosted-runner identity.
The original Windows None setting was restored after Aquatic controls.

## Focused validation

| Check | Corrected-source result |
| --- | --- |
| New shown Pages transition | 31/31 normal and 31/31 actual High Contrast |
| Existing reflow controls | 11/11: seven normal, four actual High Contrast |
| Find routing | 48 checks pass |
| Selection navigation | 16 cases pass |
| Core | 2,320 passed, zero failed/skipped |
| PR #82 offline presentation | 11/11 controls pass |
| Unchanged shown WPF/UIA loading lifecycle | One execution, 106 checks pass; dense ER and application closure reached |
| Canonical manifest controls | 8 tests pass, including fresh LF/CRLF Windows Git checkouts and altered/missing/staged/committed content |
| Original local dense ER executions | Both remain FAIL after 104 checks; close assertion NOT REACHED |
| Hosted qualification | NOT RUN; all 30 mandatory contexts remain required |
| Historical Continuous 250 ms | FAIL unchanged; not rerun |

The new lifecycle PASS is a separate observation on corrected source. It does
not establish why the original local baseline and candidate hit the unchanged
six-second dense ER deadline, nor replace the mandatory hosted
`complex-er-preview` qualification. No deadline extension, skipped scenario,
repeated candidate lifecycle attempt or performance campaign was used.

These are owned-window WPF/UIA checks, with shown normal/HC transition controls
and two shown existing reflow controls. They do not certify physical display,
injected keyboard/mouse journeys or screen readers. The previously reported
desktop helper identity limitation is separate; production window identity and
launch infrastructure were not modified. Browser suites were not rerun because
web source/assets and workflows are unchanged.

## Canonical proposal

[Proposal v2](../open-source/evidence/preview-reflow-integration-v2/proposal.md)
supersedes the unapproved v1 without changing its retained files. Canonical
manifest SHA-256:
`340745a23758a94cb50b9aadeb761b817697777481a39762dbe0465ef64a8681`.
The exact three protected pairs are unchanged; the corrected added-file digests
and native/control trees are recalculated. Nine added source/control/fixture
paths are pinned. All prior approved amendments, historical FAIL outcomes,
licence exclusions and MIT scope are preserved.

The verifier reads raw committed Git blob bytes. Separate checks bind the index,
working manifest and Git-clean source objects; line-ending conversion alone is
allowed, while different spacing or JSON semantics cannot inherit the pin.
The original v1 checkout digest is not reused as an approval identifier.

Preparation commits carry `[skip ci]`; PR #83 remains Draft. Independent review
must pass before recommending an exact owner provenance decision. Activation,
hosted CI and merge have not been authorised by this correction task. PRs #76,
#78 and #80, all workflow bytes, the six-second deadline and performance policy
remain unchanged. No signing, release, deployment, distribution or Native
Stable certification is performed.
