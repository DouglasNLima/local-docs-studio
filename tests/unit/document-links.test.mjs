import assert from 'node:assert/strict';
import test from 'node:test';
import {
  classifyDocumentHref,
  generateDocumentLink,
  resolveWorkspaceDocumentLink,
} from '../../assets/scripts/utils/document-links.js';

const files = [
  { path: 'Documentation Pack/Onboarding.md', name: 'Onboarding.md' },
  { path: 'Documentation Pack/docs/Guide.md', name: 'Guide.md' },
  { path: 'Documentation Pack/docs/Café Guide.md', name: 'Café Guide.md' },
  { path: 'Documentation Pack/reference/Guide.md', name: 'Guide.md' },
  { path: 'Documentation Pack/reference/Sources #1 (EU) [final] %.md', name: 'Sources #1 (EU) [final] %.md' },
  { path: 'Other Pack/docs/Guide.md', name: 'Guide.md' },
];

function resolve(href, fromPath = 'Documentation Pack/Onboarding.md', overrides = {}) {
  return resolveWorkspaceDocumentLink({
    href,
    fromPath,
    files,
    workspaceKind: 'zip',
    workspaceRoot: 'Documentation Pack',
    ...overrides,
  });
}

test('classifies permitted external links without treating Markdown suffixes as workspace paths', () => {
  assert.equal(classifyDocumentHref('https://example.com/guide.md').kind, 'external');
  assert.equal(classifyDocumentHref('http://example.com').kind, 'external');
  assert.equal(classifyDocumentHref('mailto:docs@example.com').kind, 'external');
  assert.equal(classifyDocumentHref('tel:+3531234567').kind, 'external');
  assert.equal(classifyDocumentHref('//cdn.example.com/guide.md').kind, 'external');
});

test('resolves exact relative paths inside an outer ZIP folder', () => {
  assert.equal(resolve('docs/Guide.md').targetPath, 'Documentation Pack/docs/Guide.md');
  assert.equal(resolve('./Caf%C3%A9%20Guide.md', 'Documentation Pack/docs/Guide.md').targetPath, 'Documentation Pack/docs/Café Guide.md');
  const sibling = resolve('../reference/Guide.md?view=reader#details', 'Documentation Pack/docs/Guide.md');
  assert.equal(sibling.targetPath, 'Documentation Pack/reference/Guide.md');
  assert.equal(sibling.query, 'view=reader');
  assert.equal(sibling.fragment, 'details');
});

test('resolves folder workspaces from their relative root without adding or removing a wrapper', () => {
  const folderFiles = [
    { path: 'Onboarding.md', name: 'Onboarding.md' },
    { path: 'docs/Guide.md', name: 'Guide.md' },
    { path: 'reference/Sources.md', name: 'Sources.md' },
  ];
  const child = resolveWorkspaceDocumentLink({
    href: './docs/Guide.md',
    fromPath: 'Onboarding.md',
    files: folderFiles,
    workspaceKind: 'folder',
  });
  const sibling = resolveWorkspaceDocumentLink({
    href: '../reference/Sources.md#authority',
    fromPath: 'docs/Guide.md',
    files: folderFiles,
    workspaceKind: 'folder',
  });
  assert.equal(child.targetPath, 'docs/Guide.md');
  assert.equal(sibling.targetPath, 'reference/Sources.md');
  assert.equal(sibling.fragment, 'authority');
  assert.equal(resolveWorkspaceDocumentLink({
    href: '../../outside.md',
    fromPath: 'docs/Guide.md',
    files: folderFiles,
    workspaceKind: 'folder',
  }).reason, 'outside-workspace-root');
});

test('keeps explicit Markdown paths case-sensitive and does not choose duplicate basenames', () => {
  const wrongCase = resolve('docs/guide.md');
  assert.equal(wrongCase.kind, 'unresolved');
  assert.equal(wrongCase.reason, 'document-not-found');
  assert.equal(wrongCase.suggestionPath, 'Documentation Pack/docs/Guide.md');

  const basenameOnly = resolve('Guide.md');
  assert.equal(basenameOnly.kind, 'unresolved');
  assert.equal(basenameOnly.targetPath, 'Documentation Pack/Guide.md');
});

test('rejects traversal, absolute paths, dangerous schemes, controls, and ambiguous encoding', () => {
  assert.equal(resolve('../Outside.md').reason, 'outside-workspace-root');
  assert.equal(resolve('../../../Other Pack/docs/Guide.md', 'Documentation Pack/docs/Guide.md').reason, 'outside-workspace-root');
  assert.equal(classifyDocumentHref('/docs/Guide.md').reason, 'absolute-path');
  assert.equal(classifyDocumentHref('C:\\Docs\\Guide.md').reason, 'absolute-path');
  assert.equal(classifyDocumentHref('\\\\server\\share\\Guide.md').reason, 'absolute-path');
  assert.equal(classifyDocumentHref('javascript:alert(1)').reason, 'blocked-scheme');
  assert.equal(classifyDocumentHref('file:///C:/Docs/Guide.md').reason, 'blocked-scheme');
  assert.equal(classifyDocumentHref('data:text/plain,hello').reason, 'blocked-scheme');
  assert.equal(classifyDocumentHref('docs/%2FGuide.md').reason, 'encoded-separator');
  assert.equal(classifyDocumentHref('docs/%252FGuide.md').reason, 'ambiguous-encoding');
  assert.equal(classifyDocumentHref('docs/%00Guide.md').reason, 'encoded-separator');
  assert.equal(classifyDocumentHref('docs/%E0%A4%A.md').reason, 'malformed-encoding');
});

test('requires shared path context for loose files', () => {
  const result = resolveWorkspaceDocumentLink({
    href: 'Guide.md',
    fromPath: 'Onboarding.md',
    files: [{ path: 'Onboarding.md' }, { path: 'Guide.md' }],
    workspaceKind: '',
  });
  assert.equal(result.kind, 'unresolved');
  assert.equal(result.reason, 'missing-relative-context');
});

test('does not cross file origins merely because an exact relative path is present', () => {
  const source = {
    path: 'Documentation Pack/docs/Source.md',
    hasRelativePathContext: true,
    pathContextId: 'pack-a',
  };
  const target = {
    path: 'Documentation Pack/reference/Other.md',
    hasRelativePathContext: true,
    pathContextId: 'pack-b',
  };
  const result = resolveWorkspaceDocumentLink({
    href: '../reference/Other.md',
    fromPath: source.path,
    files: [source, target],
    workspaceKind: 'zip',
    workspaceRoot: 'Documentation Pack',
  });
  assert.equal(result.kind, 'unresolved');
  assert.equal(result.reason, 'different-origin');
  assert.equal(generateDocumentLink({
    fromPath: source.path,
    toPath: target.path,
    fromRecord: source,
    toRecord: target,
    workspaceKind: 'zip',
    workspaceRoot: 'Documentation Pack',
  }).reason, 'different-origin');
});

test('generates encoded Markdown that resolves to exactly the selected document and section', () => {
  const targetPath = 'Documentation Pack/reference/Sources #1 (EU) [final] %.md';
  const generated = generateDocumentLink({
    fromPath: 'Documentation Pack/docs/Guide.md',
    toPath: targetPath,
    fragment: 'p03 café',
    label: 'Source [authority]',
    workspaceKind: 'zip',
    workspaceRoot: 'Documentation Pack',
  });
  assert.equal(generated.ok, true);
  assert.equal(generated.markdown, '[Source \\[authority\\]](../reference/Sources%20%231%20%28EU%29%20%5Bfinal%5D%20%25.md#p03%20caf%C3%A9)');

  const roundTrip = resolve(generated.href, 'Documentation Pack/docs/Guide.md');
  assert.equal(roundTrip.kind, 'resolved');
  assert.equal(roundTrip.targetPath, targetPath);
  assert.equal(roundTrip.fragment, 'p03 café');
});

test('generates fragment-only links for the current document and rejects an empty self target', () => {
  const section = generateDocumentLink({
    fromPath: files[0].path,
    toPath: files[0].path,
    fragment: 'minha-seção',
    label: 'Minha seção',
    workspaceKind: 'zip',
    workspaceRoot: 'Documentation Pack',
  });
  assert.equal(section.href, '#minha-se%C3%A7%C3%A3o');
  assert.equal(resolve(section.href).targetPath, files[0].path);
  assert.equal(generateDocumentLink({
    fromPath: files[0].path,
    toPath: files[0].path,
    workspaceKind: 'zip',
  }).reason, 'empty-same-document-target');
});
