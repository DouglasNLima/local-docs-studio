# Windows internal preview release notes

## Eleven native Mermaid editors — Expansion II

Timeline, User Journey and Quadrant join the existing eight normal editor families.
They reuse #40's source occurrences, source-group patching, private history, shared
native workspace and owner Apply session. No renderer/runtime upgrade, browser
fallback or LayoutExperiment is included. See the [syntax matrix](timeline-journey-quadrant-editors.md)
and [engineering receipt](expansion-ii-engineering-receipt.md) for source-labelled
verification, known protections and the Draft PR's final package/results record.
Stock Continuous and paused #38 retain their documented status. Internal packaging
and actual OS/installer acceptance are isolated; optional associations stay disabled.



## Eight native Mermaid editors — previous expansion

Gantt, Mind map and Pie join Flowchart, Sequence, State, ER and Class in the
ordinary native application. Each family uses its own source adapter, canonical
Mermaider 0.15.1 output, private history and the existing owner-document Apply
session. See the [supported/protected syntax and engineering record](gantt-mindmap-pie-editors.md).
The [engineering receipt](expanded-editor-engineering-receipt.md) and
[PR #40 delivery record](https://github.com/DouglasNLima/local-docs-studio/pull/40)
identify final-source checks, the fresh unsigned internal preview and limitations.
Historical receipts retain their original source scope.


This consolidation accepts the five-editor UX and bounded stock Continuous
improvements from PRs #36/#37. All five editors gain consistent toolbars,
full-name navigation, persistent inspector context, source panels and viewport/focus
improvements. Bounded frozen-drawing reuse improves measured repeat presentation;
cold results remain mixed and no general first-load or memory gain is claimed.

Continuous keeps table batching, adjacent-text coalescing, indexed anchors with a
complete fallback, owned theme palettes/stable brushes, selection retention and
source/version/appearance guards. The 250 ms goal remains incomplete with long
stock formatting/Input gaps. The `.749` first-launch incident remains unresolved.
Draft #38 is PAUSED at `31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`, with its achieved
layout/functional milestone and failing Input/INSUFFICIENT EVIDENCE verdict retained.
Its renderer, launcher and modified-runtime experiments are excluded from packages.
Stock Continuous remains the ordinary default. No migration commitment or deadline
is attached to the next Mermaid editor. See the source checkout's
`docs/native/accepted-native-consolidation.md` and the #37 integration receipt for
post-integration checks, actual source/version, package hashes and explicit skips.

Earlier editor capabilities and historical qualification bounds follow.
This candidate adds native Mermaid Class visual authoring as the fifth family alongside Flowchart, Sequence, State and ER. It retains the integrated workspace, interaction stability and canonical preview fidelity. The packaged `version.json` records this build's exact version, source revision, source kind and Actions run. Packages are unsigned and remain separate from the browser/PWA and legacy Windows shell.

- Class **Visual Edit** provides distinct class, attribute, method and relationship inspectors. **Insert → Class diagram visually…** opens a verified portable starter. Supported class IDs, simple generics/annotations, member names/types/parameters/visibility, static attributes and six primary relationship types use exact source patches and full pinned semantic validation.
- Class private occurrence identities distinguish duplicate members and parallel/self relationships. Reverse retains marker/cardinality ownership at the actual class; reconnection has native pointer and keyboard alternatives. Canonical Mermaider positions determine class placement. Private Undo/Redo, one-step document Apply, Save/reopen and Cancel retain ordinary portable source.
- Method static/abstract classifiers, abstract attributes, Lollipop authoring, namespace membership changes, note authoring, repeated declarations and ambiguous source remain protected. The pinned parser's ignored statements never grant editing permission. The offline Class guide and [Class delivery record](https://github.com/DouglasNLima/local-docs-studio/pull/35) identify exact capabilities, completed hosted gates, package provenance and limitations. A queued, skipped or failed gate is not certification.

- ER **Visual Edit** provides entity, attribute and relationship inspectors. **Insert → ER diagram visually…** creates a portable starter. Supported type/name, PK/FK/UK, quoted comment, row order, endpoint cardinality and identifying/non-identifying edits use exact expected-text patches and complete pinned semantic validation.
- ER source occurrence identities distinguish repeated attributes, parallel relationships and self-loops even when renderer order differs from source order. Unknown suffix tokens and statements remain protected and exact. Private Undo/Redo, one-step document Apply, Cancel and source revision checks preserve portable Mermaid. Key markers document meaning and do not enforce database constraints.
- ER qualification requires complete actual Windows authoring, Save/restart/reopen, normal and High Contrast acceptance, five window sizes, strict canonical pixels, existing editor regressions, resource observations and independent installed-package verification at the delivered source revision. Until those exact-head gates finish, this candidate is not certified. See `docs/native/mermaid-er-visual-editor.md` and Draft PR #34 for the complete evidence and limitations.

- State **Visual Edit** provides ordinary state and transition authoring through the canonical Mermaider drawing. **Insert → State diagram visually…** creates a starter. Edit labels, add states and connections, reconnect supported endpoints, and work with initial/final markers, self-transitions and cycles in simple diagrams.
- State operations use exact source patches, distinct transition occurrence identities, scoped pseudo-states, complete semantic/render validation and bounded private Undo/Redo. Apply changes the owning document in one Undo step; Cancel preserves its source. Temporary drag positions never enter saved Mermaid.
- Composite hierarchy, choice/fork/join, notes and styling remain readable and protected against unsafe structural changes. Proven independent label edits remain available. Unknown dependencies, duplicate declarations and ambiguous mappings require source editing. The inspector explains individual capabilities.
- State qualification includes real native authoring, document Undo/Redo, Save/restart/reopen, normal/dark appearance, actual Windows High Contrast, five window sizes, canonical pixel fidelity and bounded process performance. Exact-head installer lifecycle and package verification must complete before the corresponding build is treated as certified. See `docs/native/mermaid-state-visual-editor.md` in the source checkout and the Draft PR delivery record for source-specific evidence, hashes and genuine limitations.

- Sequence **Visual Edit** provides participant/actor, message, note and simple activation authoring through the existing canonical Mermaider drawing. **Insert → Sequence diagram visually…** creates a starter. Native lists and an inspector provide keyboard alternatives to canvas selection; participant and interaction order follow portable source semantics.
- Exact source patches, complete candidate correspondence, private Undo/Redo and the owning document's version/container checks protect unrelated Mermaid. Basic activation edits retain existing standalone syntax. Unknown dependencies, structural rewrites and ambiguous canvas regions stay protected with an exact-list/source alternative.
- Sequence qualification covers normal appearance, actual Windows High Contrast, five window sizes, repeated messages, implicit participants, protected blocks and the full edit/Apply/document Undo/Redo/Save/reopen workflow. Physical higher-DPI and production trust certification remain separate. See the source checkout's `docs/native/mermaid-sequence-visual-editor.md` for exact source-specific evidence and limitations.

- Recognised Flowcharts offer **Visual Edit** beside their native preview, including protected source. **Edit source** remains separate. **Insert → Flowchart visually…** creates a new diagram. The visual canvas uses the same Mermaider drawing as the native preview. Protected diagrams preserve their original layout for inspection; supported nodes, connections, nested subgraphs and styles retain native WPF controls and keyboard alternatives.
- The editor keeps a private source draft with Undo/Redo, source validation, zoom/PAN and temporary positioning. Apply replaces only the tracked diagram in one document Undo transaction; Cancel retains the original. Conflicting source, unsafe wrappers and unsupported grammar are protected.
- Standalone `.mmd`/`.mermaid` files retain raw source. Existing Mermaid rendering/export services validate every accepted visual operation. See the offline Native guide's **Mermaid visual authoring** topic for supported bounds and commands.

- Empty drafts offer optional native workspace/document/help actions and brief saving, image and export guidance. Typing hides the strip without changing the document.
- Explicit Continuous displays quiet guidance for the same source-character/mapped-element characteristics that select Pages in Automatic. It remains the user's choice, preserves source and selection, and makes no performance prediction.
- Preview packaging requires clean committed source, excludes development symbols, includes this guide and notes, and records branch-source versus PR merge-checkout provenance.
- Independent package checks verify exact version/source, checksums, update metadata, bundled .NET/WPF components and payload hygiene. Hosted native and web gates include the readiness branch and PR target.

The supported editing, portable workspace/image, Mermaid, recovery, DOCX/PDF export, installer lifecycle and fail-closed update contracts remain in place. The experimental Continuous viewer in Draft PR 22 is **NO-GO** and is excluded.

Known limits remain: unsigned publisher/SmartScreen warnings, no trusted unsigned in-app updates, missing genuinely clean Windows certification, incomplete physical accessibility/compatibility, bounded Mermaid/import/export fidelity, Continuous delays above 250 ms and the unresolved 587.9933 ms details delay. Previous real Word acceptance has its original scope; Excel first-run settings and a disposable Azure DevOps destination remain blocked. No production signing, public release or tag movement is authorised.

Read `PREVIEW-GUIDE.md` for evaluation instructions and `docs/native/mermaid-state-visual-editor.md` in the source checkout for the State scope and certification requirements. Earlier Flowchart, Sequence, readiness and performance records retain their original scope. Use the exact build's delivery record for its final verdict and package provenance. No byte-for-byte build reproducibility or production readiness is claimed.
