import { test } from 'node:test';
import assert from 'node:assert/strict';
import { collectImageTokens, inspectImageBytes, relativeImageReference, resolveImageReference, rewriteImageReferences, serialiseImage } from '../../assets/scripts/utils/image-references.js';

const png = new Uint8Array(Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP432H3HwAHFALF2h7vpgAAAABJRU5ErkJggg==', 'base64'));

test('image identities stay document-relative, case-sensitive and bounded by the authorised root', () => {
  assert.equal(resolveImageReference('../assets/architecture.png', 'docs/start.md').path, 'assets/architecture.png');
  assert.equal(resolveImageReference('./architecture.png', 'docs/start.md').path, 'docs/architecture.png');
  assert.equal(resolveImageReference('assets\\Architecture.png', 'start.md').path, 'assets/Architecture.png');
  assert.equal(resolveImageReference('../../escape.png', 'docs/start.md').kind, 'invalid');
  assert.equal(resolveImageReference('../escape.png', 'pack/start.md', { root: 'pack' }).kind, 'invalid');
  for (const path of ['C:\\secret.png', '//host/secret.png', '/secret.png', 'file:///secret.png', 'blob:abc', '../%2fsecret.png', '%5csecret.png', '%00.png']) assert.equal(resolveImageReference(path, 'start.md').kind, 'invalid', path);
});

test('special filename characters survive exactly one URI encoding and decoding pass', () => {
  const path = 'assets/ação (v1) # 100% %2e.png';
  const reference = relativeImageReference('docs/start.md', path);
  assert.equal(reference, '../assets/a%C3%A7%C3%A3o%20%28v1%29%20%23%20100%25%20%252e.png');
  assert.equal(resolveImageReference(reference, 'docs/start.md').path, path);
  assert.equal(resolveImageReference('../'+path, 'docs/start.md', { literal: true }).path, path);
  assert.equal(resolveImageReference('%252e%252e/inside.png', 'start.md').path, '%2e%2e/inside.png');
  assert.equal(resolveImageReference('https://example.test/a\\b.png', 'start.md').reference, 'https://example.test/a\\b.png');
  assert.equal(resolveImageReference('javascript:alert(1)').kind, 'invalid');
  assert.equal(resolveImageReference('https://lens-docs-studio.local/assets/capture.png').kind, 'invalid');
});

test('Markdown images and figure sources are collected without code or prose substitutions', () => {
  const source = '# Test\n\n![One](../assets/a.png)\n\n<figure data-image-figure>\n<img src="../assets/a.png" alt="One">\n<figcaption>Caption</figcaption>\n</figure>\n\n```md\n![Code](../assets/a.png)\n<img src="../assets/a.png">\n```\n\n`![Inline](../assets/a.png)`\n\n    ![Indented](../assets/a.png)\n\nPlain ../assets/a.png and [link](../assets/a.png).';
  const tokens = collectImageTokens(source);
  assert.equal(tokens.length, 2);
  assert.deepEqual(tokens.map((token) => resolveImageReference(token.href, 'docs/start.md').path), ['assets/a.png','assets/a.png']);
  const rewritten = rewriteImageReferences(source, 'docs/start.md', (path) => path === 'assets/a.png' ? 'assets/images/a-2.png' : '');
  assert.equal(collectImageTokens(rewritten).every((token) => token.href === '../assets/images/a-2.png'), true);
  assert.ok(rewritten.includes('```md\n![Code](../assets/a.png)\n<img src="../assets/a.png">\n```'));
  assert.ok(rewritten.includes('`![Inline](../assets/a.png)`'));
  assert.ok(rewritten.includes('Plain ../assets/a.png and [link](../assets/a.png).'));
  assert.ok(rewritten.includes('<figcaption>Caption</figcaption>'));
});

test('images in table cells, lists, references and balanced destinations retain their exact identity', () => {
  const source = '![Reference][capture]\n\n[capture]: assets/a%20b.png "Title"\n\n- ![List](assets/a(b).png)\n\n| Image | Other |\n|---|---|\n| ![Cell](assets/c.png) | `![Code](assets/c.png)` |';
  assert.deepEqual(collectImageTokens(source).map((token) => token.href), ['assets/a%20b.png', 'assets/a(b).png', 'assets/c.png']);
  assert.ok(rewriteImageReferences(source, 'start.md', () => 'assets/new.png').includes('![Reference](assets/new.png "Title")'));
});

test('captions are truly optional and escaped consistently with alternative text', () => {
  assert.equal(serialiseImage({ reference: 'assets/a.png', alt: 'Capture first' }), '![Capture first](assets/a.png)');
  assert.equal(serialiseImage({ reference: 'assets/a.png', alt: 'Capture', caption: '  ' }), '![Capture](assets/a.png)');
  assert.ok(serialiseImage({ reference: 'assets/a.png', alt: '"<>', caption: 'A & B' }).includes('<figcaption>A &amp; B</figcaption>'));
});

test('image byte validation checks signature, size, extension and declared MIME', () => {
  assert.equal(inspectImageBytes(png, { name: 'a.png', mimeType: 'image/png' }).mimeType, 'image/png');
  for (const props of [{ name: 'a.svg' }, { name: 'a.jpg' }, { mimeType: 'image/gif' }]) assert.throws(() => inspectImageBytes(png, props));
  assert.throws(() => inspectImageBytes(new Uint8Array([1,2,3]), { name: 'a.png' }));
  assert.throws(() => inspectImageBytes(new Uint8Array(21*1024*1024), { name: 'a.png' }));
});
