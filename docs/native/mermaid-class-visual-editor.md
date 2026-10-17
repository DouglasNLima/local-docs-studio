# Native Mermaid Class Diagram Visual Editor

Engineering work in progress. This branch is not a certified preview.

## Baseline and architecture

The feature starts at consolidated `develop` revision
`5b732cd187f2c5d0742a0eaa3c978a2d95ba4815`, fetched on 18 October 2026.
The existing Flowchart, Sequence, State and ER editor windows and Core modules
are present. The browser/static post-merge run
[39080262674](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39080262674)
passed both browsers. Native post-merge run
[39080262652](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39080262652)
was still running installer lifecycle checks when work began; completion remains
to be recorded before baseline certification.

Class authoring will use a dedicated Core semantic/source adapter, exact source
patches, private occurrence identities and bounded history. The existing native
document ownership and single-Undo Apply contract remain authoritative. The
canonical Mermaider drawing supplies the canvas; WPF supplies interaction overlays.
No coordinates or private identity metadata enter portable Mermaid source.

## Pinned upstream discovery

Mermaider 0.15.1 identifies upstream revision
`71b64845570d633a94d29e85579c0a02786fece8` in its NuGet metadata.
The exact revision has been checked out separately for inspection:

- [ClassParser](https://github.com/nullean/mermaider/blob/71b64845570d633a94d29e85579c0a02786fece8/src/Mermaider/Parsing/ClassParser.cs)
- [Class model](https://github.com/nullean/mermaider/blob/71b64845570d633a94d29e85579c0a02786fece8/src/Mermaider/Models/ClassDiagram.cs)
- [Positioned model](https://github.com/nullean/mermaider/blob/71b64845570d633a94d29e85579c0a02786fece8/src/Mermaider/Models/PositionedClassDiagram.cs)
- [Class SVG renderer](https://github.com/nullean/mermaider/blob/71b64845570d633a94d29e85579c0a02786fece8/src/Mermaider/Rendering/ClassSvgRenderer.cs)

Critical observations: unrecognised global lines can be ignored; class-block
lines can be interpreted as broad attribute text; repeated declarations append
members to one class; attributes and methods occupy separate ordered lists.
Method flags are detected anywhere in the signature, but flags in return types
remain part of the displayed type. Attribute flags depend on the final marker.
Safe authoring must therefore use a bounded subset and preserve other forms.

Lollipop layout can introduce synthetic target classes. This requires explicit
protection rather than assuming a one-to-one source/layout class mapping.

## Verification status

Implementation, capability matrices, source examples, native acceptance,
screenshots, performance observations, final regression totals, hosted CI and
package provenance will be recorded from actual results as work proceeds.
No new editor, package or readiness claim is made by this initial report.
