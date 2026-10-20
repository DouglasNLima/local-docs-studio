# Timeline, User Journey and Quadrant compatibility

Implementation in progress on `feature/native-mermaid-timeline-journey-quadrant-editors`,
from develop `546b27adf2e0bc615e0f8cab23fe47f3e8f5cfc3`. This is a source authoring
contract, not a claim of completed native or installed acceptance.

The ordinary adapter remains authoritative: Mermaider 0.15.1, upstream source
[`71b6484`](https://github.com/nullean/mermaider/tree/71b64845570d633a94d29e85579c0a02786fece8).
Matching `Parsing/{Timeline,Journey,Quadrant}Parser.cs`, `DiagramHeaderOptions.cs`,
`Rendering/{Timeline,Journey,Quadrant}SvgRenderer.cs` and their parser/renderer tests
establish the pinned grammar. No replacement renderer or private engine API is used.

| Family | Editable first scope | Preserved or protected |
| --- | --- | --- |
| Timeline | Effective title; named and unsectioned groups; ordered periods and colon-separated events, including continuation groups | Empty source sections retained; unknown lines and orphan continuations protect ambiguous structural operations; colon/newline inputs rejected without invented escaping |
| User Journey | Effective title; groups; ordered tasks; integer score corrections 1–5; ordered actor occurrences scoped to one task | Original score lexemes, including clamped scores; absent versus empty suffix; duplicate actors and untouched whitespace; ignored lines protect crossing moves; new delimiter-bearing names rejected |
| Quadrant | Effective title; X left/right and Y bottom/top labels; numbered quadrant labels; ordered point occurrences; finite new coordinates in [0,1] | Original precision and clamped lexemes; repeated declarations retained with last occurrence precedence; one-sided axes retained; unknown directives protect crossing moves |

Title body declarations override header titles, with the last body declaration winning.
Timeline and Quadrant body directives are case sensitive; Journey directives are case
insensitive. Header recognition is case insensitive. Repeated axis declarations replace
both ends, including clearing the opposite end for a later one-sided declaration.

The actual Quadrant renderer numbers quadrants 1 top right, 2 top left, 3 bottom left,
4 bottom right. Its canonical panel rectangles establish the plot extent, including
the centre gap. X grows rightwards and Y grows upwards; pointer correspondence uses
the emitted point shapes and SVG viewBox. Coincident points remain separate list entries.

All operations must retain authoritative source, stable source keys and bounded private
history. Structural attachment follows #40: immediately preceding same-indent comments
attach to the following source group; blank lines stop attachment. Unproved trailing
comments, ignored directives or unterminated groups protect affected moves/removals.
Invalid input remains pending; Apply must revalidate the exact current draft and owner.

Native compatibility evidence is produced with `--quadrant-compatibility`: ordinary
adapter, panel/point geometry, trusted SVG conversion, PNG and HTML/DOCX/PDF exports
in Light/Dark. Later engineering receipts will identify exact source and actual results.
