// Deterministic, first-party calibration workloads. Never rewrites historical fixtures.
import { mkdirSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
const root = fileURLToPath(new URL('../../../tests/fixtures/native-ux/', import.meta.url));
mkdirSync(root, { recursive: true });
const hash = b => createHash('sha256').update(b).digest('hex');
const header = '# ReadableStart\n\nNeedleOne and NeedleOne. Café 😀 é.\n\n';
const end = '\n\n## Distant target\n\nReadableEnd\n';
const rows = (n, fn) => Array.from({ length: n }, (_, i) => fn(i + 1)).join('\n');
const fixtures = [
  ['small', 'Small prose', rows(12, i => `ProseToken${i} is **strong** and *emphasised*.\n`), 12, 0, 0],
  ['technical', 'Typical technical documentation', rows(80, i => `## Section ${i}\n\nTechnicalToken${i} documents a local workflow.\n\n- First step\n- Second step\n`), 80, 160, 0],
  ['long', 'Long prose', rows(1200, i => `LongToken${i} preserves **formatting**, Unicode Café and complete content.\n`), 1200, 0, 0],
  ['table', 'Large table', '| Row | Value |\n|---|---|\n' + rows(1000, i => `| TableToken${i} | Value${i} |`), 1000, 0, 1001],
  ['nested-list', 'Long and nested lists', rows(1000, i => `- ListToken${i}\n  - Nested${i}\n    - Deep${i}\n- Peer${i}`), 1000, 4000, 0],
  ['mermaid', 'Complex Mermaid', '```mermaid\nflowchart TD\n' + rows(60, i => `N${i}[Node ${i}] --> N${i + 1}[Node ${i + 1}]`) + '\n```', 0, 0, 0],
  ['mixed', 'Mixed text, table, diagram and local image', rows(150, i => `- MixedToken${i}`) + '\n\n| Row | Value |\n|---|---|\n' + rows(100, i => `| Row${i} | Value${i} |`) + '\n\n![Local pixel](pixel.png)\n\n```mermaid\nflowchart LR\nA[Source] --> B[Preview]\n```', 150, 150, 101]
];
const manifest = fixtures.map(([id, category, body, tokens, listItems, tableRows], i) => {
  const source = (header + body + end).replaceAll('\n', i % 2 ? '\r\n' : '\n');
  const bytes = Buffer.from(source, 'utf8');
  writeFileSync(root + `${id}.md`, bytes);
  const prefix = { small: 'ProseToken', technical: 'TechnicalToken', long: 'LongToken', table: 'TableToken', 'nested-list': 'ListToken', mixed: 'MixedToken' }[id];
  return { id, category, file: `${id}.md`, sha256: hash(bytes), bytes: bytes.length, lineEnding: i % 2 ? 'CRLF' : 'LF',
    expectedTokens: ['ReadableStart', 'ReadableEnd', ...Array.from({ length: tokens }, (_, j) => prefix + (j + 1))], listItems, tableRows,
    diagrams: ['mermaid', 'mixed'].includes(id) ? 1 : 0, images: id === 'mixed' ? 1 : 0 };
});
const pixel = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=', 'base64');
writeFileSync(root + 'pixel.png', pixel);
writeFileSync(root + 'manifest.json', JSON.stringify({ schema: 1, assetSha256: hash(pixel), fixtures: manifest }, null, 2) + '\n');
