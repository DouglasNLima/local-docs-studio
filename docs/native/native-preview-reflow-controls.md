# Focused native preview reflow controls

Run from the repository root with .NET 10 on Windows:

```powershell
dotnet run --project tests/native/preview-reflow/PreviewReflowControls.csproj -c Release -- --controlled-layout tests/fixtures/native-preview-reflow artifacts/reflow-fresh --shown
```

Use a fresh output directory for each process. Optional `--zoom=75`, `--zoom=200`,
`--mode=Automatic` and `--mode=Pages` select the existing reader settings.
`--expect-displacement` is a baseline negative control: exit zero means the original
empty-text viewport was reproduced, not product acceptance.

The fixture is the unchanged PR #80 mixed document and local pixel image from
`22c223c6f56fa41fda2a36102e1eed0af44974ec`. Markdown SHA-256:
`6b7cd92a7dcf1dd88ff31e68fb196bde2ab76655f99e9f9fee93d82bf57280a0`.
It contains 150 list items, 101 table rows, responsive media and a distant mapped
text position. Preserve its canonical LF bytes when comparing that digest.

The control is extracted from PR #80's `LayoutControl.cs`, with its 30-second bound,
natural WPF layout and mapped-position assertions retained. It adds current
document/generation/loading evidence, window render PNGs, source-edit cancellation,
Find-state checks and shown-window focus preservation. Each process owns isolated
state and recovery directories. It does not modify the fixture or the ordinary
user's session. No historical tracing or performance campaign is imported.

`--shown` displays the owned test window. Otherwise it is off-screen. PNGs are
WPF window render captures, not physical desktop scan-out. UIA TextPattern visible
ranges and arranged source geometry are asserted separately from logical content.
No desktop input is injected. The existing layout command closes the Find bar;
its query and match state must remain unchanged.

For actual High Contrast, enable it through Windows first, then add
`--expect-high-contrast`. The test requires both Win32 and WPF flags to match;
it never changes OS appearance or fakes hosted-runner flags. Restore the original
Windows appearance after validation. Hosted input, screen readers and the 30
mandatory integration contexts remain separate qualifications.
