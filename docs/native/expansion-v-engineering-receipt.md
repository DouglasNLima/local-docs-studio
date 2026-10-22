# Native Mermaid expansion V engineering receipt

Status: compatibility implementation in progress; editor and acceptance gates pending.

## Source and baseline

- Goal started on 22 October 2026. Work branch: `feature/native-mermaid-architecture-c4-block-editors`.
- Actual fetched `develop`: `06a7d868d1b58c82f29c7c655dcc972f1ca77282`, tree `3c3de58afee4fbb410c4b60a9ec051694d2df0c5`. No later develop changes were present at the initial audit.
- PR #43 reviewed source: `9d0f6f7c026a13bc11f080ca392b84b1674f0d40`, same tree. The `.847` previews identify that feature source, not the merge.
- Post-merge push CI: [native Windows edition](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39468973608) and [static web validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39468973709), both completed successfully at the actual merge SHA.
- Original checkout remains on `main` at `0d100cc29dde2ba80755fb1e8af60ffd9d006b16`; its untracked attachment directory is preserved. Isolated checkout: `C:/Code/MarkdownReader-expansion-v`.
- PR #38 remains open, Draft and PAUSED at `31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`. Its source, packages and evidence remain untouched. Stock Continuous remains the ordinary default; historical timing acceptance remains incomplete and `.749` unresolved.

## Pinned compatibility milestone

Inspected existing upstream checkout `artifacts/pinned-mermaider` in the expansion IV worktree; its actual HEAD is `71b64845570d633a94d29e85579c0a02786fece8`. Inspection includes Architecture/C4/Block parsers and public models, Architecture layout, C4/Block renderer-owned geometry, icon registry, preprocessing/header dispatch, resource checks and matching upstream tests. Mermaider 0.15.1 and official .NET 10/WPF remain unchanged.

Added five representative fixtures: Architecture, each of C4Context/C4Container/C4Component, and flat Block. The independent `--topology-compatibility` harness exercises the ordinary application renderer, trusted SVG conversion, ordinary document preview, PNG and HTML/DOCX/PDF exports in Light and Dark. Its raw receipt records the compiled source revision and per-input hashes, including failures.

Audit expectations to verify with actual output:

- Architecture uses explicit `in` membership, a shared ID namespace, actual port anchors and arrowheads. Brace group endpoints are stripped but absent from the native edge box lookup; preserve/protect them rather than inventing routing. Six bundled default pictograms emit native vector geometry. Provider artwork emits embedded SVG images, rejected by the existing native image safety policy; no policy relaxation or remote icon registration is proposed.
- C4 has distinct positional slots, removes unquoted empty arguments, filters named dollar tokens, reverses `Rel_Back`, ignores directional hints and flushes unfinished boundaries at EOF. Duplicate aliases coalesce to the final visible element. Context, Container and Component require separate proof; Dynamic and Deployment remain source-only.
- Block recognises several tokens per line, first-wins IDs, repeatable spacers, rectangle/rounded forms and positive spans. It stops at an unparseable suffix and clamps over-wide spans visually. These behaviours must be represented/protected without rewriting original bytes.

Reproduction (run from the isolated checkout):

```powershell
dotnet build src/native/LensDocsStudio.Native.slnx -c Release -p:RestoreLockedMode=true
src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows/LensDocsStudio.Native.Validation.exe --topology-compatibility artifacts/native/expansion-v/compatibility .
```

## Retained infrastructure observations

Initial managed worktree creation failed with `No space left on device` while checking out tracked screenshots; C: had approximately 36 MB free. The tool removed its failed checkout. A separate sparse checkout allowed source inspection without altering earlier worktrees; sparse checkout was disabled once free space increased. No earlier artefacts or caches were deleted by this task.

Clean `16ffdeea3eda79220eaa1fcc7433bf69c78e3ccc` locked local build passed with zero warnings/errors in 4:09.84. Its first cold ordinary Architecture render failed the unchanged three-second resource deadline on this shared workstation. Raw failure remains at `artifacts/native/expansion-v/compatibility-16ffdee/`, including the exact source and compiled revision, and `logs/compatibility-16ffdee.log`. It is not a compatibility pass. No deadline, retry or fixture reduction was applied. A separately source-labelled disposable hosted compatibility workflow will establish the native result.

Before that hosted run, the explicit-membership geometry control connects its group junction to the member service: an unconnected junction could make the group bounding rectangle surround the unrelated service. The control now proves membership with unambiguous actual extents and retains both intended real connections; no rendered geometry is substituted.

The four preserved `.847` installer/portable hashes were independently rechecked against PR #43 and all match: `9e1dfe97dd87b8b3781189a4f516b09a5619365e3d4f91a2c872e6d309109adf`, `ca390641ceb1449754fd09c2a8e5140efba34b1a4069751f275041b66c5a430c`, `4be9b68cd27748fef6831ef786c8af068e2eb6097a529fcc97096f6eb7e4182a`, `528bd067820400c9fbbb2e0f70492eecfbeaea6e1baac8e21f1795b43b4fe79d`.

## Outstanding delivery gates

Compatibility execution, source-preserving authoring, grammar/capability documentation, all previous-family regressions, new-family normal/actual High Contrast UIA, owner/history/recovery, final clean-source ordinary/export/fidelity/browser/static gates, sixty installed authoring phases, fresh unsigned packages, independent payload/type/control inspection and final captures remain pending. No new package, screenshot, final-source acceptance or twenty-family implementation is claimed yet.

Installer execution and actual OS appearance mutation are restricted to disposable hosted Windows runners, with optional associations disabled. No merge, release, signing, updater publication, tag movement, Pages deployment or ordinary desktop installation is authorised.
