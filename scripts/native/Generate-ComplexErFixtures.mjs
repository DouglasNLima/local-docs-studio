// Deterministic synthetic graphs: no customer names or source material.
import { mkdirSync, writeFileSync } from 'node:fs';
const root = new URL('../../tests/fixtures/native-complex-er/', import.meta.url);
mkdirSync(root, { recursive: true });
const cases = [
  ['complete-relationships', 39, 115, 0], ['complete-detailed', 39, 115, 284],
  ['operations', 22, 68, 195], ['monitoring', 13, 46, 138],
  ['workflow-assignment', 11, 21, 82], ['workflow-definitions', 12, 20, 97],
];
const manifest = [];
for (const [name, entities, relationships, attributes] of cases) {
  const lines = ['erDiagram'];
  for (let n = 0; n < entities; n++) {
    const count = Math.floor(attributes / entities) + (n < attributes % entities ? 1 : 0);
    lines.push(`    ENTITY_${n}` + (count ? ' {' : ''));
    for (let a = 0; a < count; a++) lines.push(`        ${a ? 'string' : 'int'} field_${a}${a ? '' : ' PK'} "Synthetic attribute ${a}"`);
    if (count) lines.push('    }');
  }
  const cardinalities = ['||--o{', '|o..||', '}o--o{', '||..|{'];
  for (let e = 0; e < relationships; e++) {
    // A spanning chain, followed by dense cross-links, exact parallel endpoints and self-links.
    const from = e < entities - 1 ? e : (e * 7) % entities;
    const to = e < entities - 1 ? e + 1 : e % 13 === 0 ? from : e % 9 === 0 ? (from + 1) % entities : (e * 11 + 3) % entities;
    const label = e % 17 === 0 ? '' : e % 11 === 0 ? `relationship_${e}_with_a_long_descriptive_label` : `relationship_${e}`;
    lines.push(`    ENTITY_${from} ${cardinalities[e % cardinalities.length]} ENTITY_${to} : "${label}"`);
  }
  writeFileSync(new URL(name + '.mmd', root), lines.join('\n') + '\n');
  manifest.push({ name, entities, relationships, attributes });
}
writeFileSync(new URL('manifest.json', root), JSON.stringify(manifest, null, 2) + '\n');
