const supportedWorkspaceKinds = new Set([
  'artefact-bundle',
  'bundle',
  'folder',
  'folder-fallback',
  'native-folder',
  'zip',
]);

const unsupportedAssetPattern = /\.(?:avif|bmp|csv|docx?|gif|html?|jpe?g|json|pdf|png|svg|webp|xlsx?|xml|zip)$/i;
const dangerousEncodedPathPattern = /%(?:00|2f|5c)/i;
const controlCharacterPattern = /[\u0000-\u001f\u007f]/;
const schemePattern = /^([a-z][a-z0-9+.-]*):/i;
const windowsDrivePattern = /^[a-z]:[\\/]/i;
const trustedPreviewLinks = new WeakMap();

export function trustPreviewInternalLink(anchor, { kind, target } = {}) {
  if (!anchor || !['document', 'wikilink'].includes(kind) || !target) return;
  trustedPreviewLinks.set(anchor, { kind, target: String(target) });
}

export function getTrustedPreviewInternalLink(anchor) {
  return anchor ? trustedPreviewLinks.get(anchor) || null : null;
}

export function hasWorkspacePathContext(workspaceKind) {
  return supportedWorkspaceKinds.has(String(workspaceKind || ''));
}

export function recordHasDocumentContext(record, workspaceKind) {
  if (typeof record?.hasRelativePathContext === 'boolean') return record.hasRelativePathContext;
  return hasWorkspacePathContext(workspaceKind);
}

export function recordsShareDocumentContext(source, target, workspaceKind) {
  if (!source || !target || !recordHasDocumentContext(source, workspaceKind)) return false;
  const hasExplicitContext = Object.prototype.hasOwnProperty.call(source, 'hasRelativePathContext')
    || Object.prototype.hasOwnProperty.call(target, 'hasRelativePathContext');
  if (!hasExplicitContext) return hasWorkspacePathContext(workspaceKind);
  return Boolean(source.hasRelativePathContext
    && target.hasRelativePathContext
    && source.pathContextId
    && source.pathContextId === target.pathContextId);
}

export function classifyDocumentHref(value) {
  const literal = String(value ?? '').trim();
  if (!literal) return invalidResult(literal, 'empty-target');
  if (controlCharacterPattern.test(literal)) return invalidResult(literal, 'control-character');

  if (literal.startsWith('//')) {
    return { kind: 'external', literal, scheme: 'protocol-relative' };
  }

  if (literal.startsWith('/') || literal.startsWith('\\') || windowsDrivePattern.test(literal)) {
    return unsupportedResult(literal, 'absolute-path');
  }

  const schemeMatch = literal.match(schemePattern);
  if (schemeMatch) {
    const scheme = schemeMatch[1].toLowerCase();
    if (['http', 'https', 'mailto', 'tel'].includes(scheme)) {
      return { kind: 'external', literal, scheme };
    }
    return unsupportedResult(literal, ['data', 'file', 'javascript'].includes(scheme)
      ? 'blocked-scheme'
      : 'unsupported-scheme', { scheme });
  }

  const parts = splitLiteralTarget(literal);
  const pathResult = decodeLinkPart(parts.path, { path: true });
  const queryResult = decodeLinkPart(parts.query);
  const fragmentResult = decodeLinkPart(parts.fragment);
  const invalid = [pathResult, queryResult, fragmentResult].find((result) => !result.ok);
  if (invalid) return invalidResult(literal, invalid.reason);

  const path = pathResult.value;
  if (path && (path.startsWith('/') || path.startsWith('\\') || windowsDrivePattern.test(path))) {
    return unsupportedResult(literal, 'absolute-path');
  }
  if (path.includes('\\')) return invalidResult(literal, 'ambiguous-separator');

  if (!path && parts.hasFragment) {
    return {
      kind: 'fragment',
      literal,
      path: '',
      query: queryResult.value,
      rawQuery: parts.query,
      fragment: fragmentResult.value,
      rawFragment: parts.fragment,
    };
  }

  if (path && unsupportedAssetPattern.test(path)) {
    return unsupportedResult(literal, 'unsupported-file-type', {
      path,
      query: queryResult.value,
      fragment: fragmentResult.value,
    });
  }

  return {
    kind: 'document',
    literal,
    path,
    query: queryResult.value,
    rawQuery: parts.query,
    fragment: fragmentResult.value,
    rawFragment: parts.fragment,
  };
}

export function resolveWorkspaceDocumentLink({
  href,
  fromPath = '',
  files = [],
  workspaceKind = '',
  workspaceRoot = '',
} = {}) {
  const classification = classifyDocumentHref(href);
  if (!['document', 'fragment'].includes(classification.kind)) return classification;

  const sourcePath = normaliseCanonicalPath(fromPath);
  const source = files.find((record) => normaliseCanonicalPath(record?.path) === sourcePath) || null;
  if (!sourcePath || !source) {
    return {
      ...classification,
      kind: 'unresolved',
      reason: 'missing-source-context',
      sourcePath,
    };
  }

  if (classification.kind === 'fragment' || !classification.path) {
    return {
      ...classification,
      kind: 'resolved',
      sourcePath,
      targetPath: sourcePath,
      target: source,
      sameDocument: true,
    };
  }

  if (!recordHasDocumentContext(source, workspaceKind)) {
    return {
      ...classification,
      kind: 'unresolved',
      reason: 'missing-relative-context',
      sourcePath,
    };
  }

  const pathResult = resolveRelativeDocumentPath({
    fromPath: sourcePath,
    targetPath: classification.path,
    workspaceRoot,
  });
  if (!pathResult.ok) {
    return {
      ...classification,
      kind: 'unresolved',
      reason: pathResult.reason,
      sourcePath,
    };
  }

  const exactPathRecords = files.filter((record) => normaliseCanonicalPath(record?.path) === pathResult.path);
  const target = exactPathRecords.find((record) => recordsShareDocumentContext(source, record, workspaceKind)) || null;
  if (!target) {
    const suggestion = files.find((record) => recordsShareDocumentContext(source, record, workspaceKind)
      && normaliseCanonicalPath(record?.path).toLowerCase() === pathResult.path.toLowerCase()) || null;
    return {
      ...classification,
      kind: 'unresolved',
      reason: exactPathRecords.length ? 'different-origin' : 'document-not-found',
      sourcePath,
      targetPath: pathResult.path,
      suggestionPath: suggestion?.path || '',
    };
  }

  return {
    ...classification,
    kind: 'resolved',
    sourcePath,
    targetPath: target.path,
    target,
    sameDocument: target.path === source.path,
  };
}

export function resolveRelativeDocumentPath({ fromPath = '', targetPath = '', workspaceRoot = '' } = {}) {
  const source = normaliseCanonicalPath(fromPath);
  const target = String(targetPath || '');
  const root = normaliseCanonicalPath(workspaceRoot).replace(/\/$/, '');
  if (!source) return { ok: false, reason: 'missing-source-context' };
  if (!target) return { ok: true, path: source };
  if (target.startsWith('/') || target.startsWith('\\') || target.includes('\\') || windowsDrivePattern.test(target)) {
    return { ok: false, reason: 'absolute-path' };
  }

  const rootParts = root ? root.split('/').filter(Boolean) : [];
  const sourceParts = source.split('/').filter(Boolean);
  if (rootParts.length && !startsWithSegments(sourceParts, rootParts)) {
    return { ok: false, reason: 'source-outside-root' };
  }

  const stack = sourceParts.slice(0, -1);
  const minimumDepth = rootParts.length;
  for (const segment of target.split('/')) {
    if (!segment || segment === '.') continue;
    if (segment === '..') {
      if (stack.length <= minimumDepth) return { ok: false, reason: 'outside-workspace-root' };
      stack.pop();
      continue;
    }
    if (segment === '.' || segment === '..') continue;
    if (controlCharacterPattern.test(segment)) return { ok: false, reason: 'control-character' };
    stack.push(segment);
  }

  if (!stack.length || (rootParts.length && !startsWithSegments(stack, rootParts))) {
    return { ok: false, reason: 'outside-workspace-root' };
  }
  return { ok: true, path: stack.join('/') };
}

export function generateDocumentLink({
  fromPath = '',
  toPath = '',
  fragment = '',
  label = '',
  workspaceKind = '',
  workspaceRoot = '',
  fromRecord = null,
  toRecord = null,
} = {}) {
  const source = normaliseCanonicalPath(fromPath);
  const target = normaliseCanonicalPath(toPath);
  const section = String(fragment || '');
  if (!source || !target) return { ok: false, reason: 'missing-source-context' };
  if (source !== target && (fromRecord || toRecord) && !recordsShareDocumentContext(fromRecord, toRecord, workspaceKind)) {
    return { ok: false, reason: recordHasDocumentContext(fromRecord, workspaceKind)
      ? 'different-origin'
      : 'missing-relative-context' };
  }

  let href = '';
  if (source === target) {
    if (!section) return { ok: false, reason: 'empty-same-document-target' };
    href = `#${encodeLinkComponent(section)}`;
  } else {
    if (!hasWorkspacePathContext(workspaceKind)) {
      return { ok: false, reason: 'missing-relative-context' };
    }
    const relative = makeRelativeDocumentPath(source, target, workspaceRoot);
    if (!relative.ok) return relative;
    href = encodeWorkspacePath(relative.path);
    if (section) href += `#${encodeLinkComponent(section)}`;
  }

  const safeLabel = escapeMarkdownLinkLabel(label || target.split('/').pop() || 'Document');
  return {
    ok: true,
    href,
    markdown: `[${safeLabel}](${href})`,
  };
}

export function makeRelativeDocumentPath(fromPath, toPath, workspaceRoot = '') {
  const source = normaliseCanonicalPath(fromPath);
  const target = normaliseCanonicalPath(toPath);
  const root = normaliseCanonicalPath(workspaceRoot).replace(/\/$/, '');
  if (!source || !target) return { ok: false, reason: 'missing-source-context' };

  const rootParts = root ? root.split('/').filter(Boolean) : [];
  const sourceParts = source.split('/').filter(Boolean);
  const targetParts = target.split('/').filter(Boolean);
  if (rootParts.length && (!startsWithSegments(sourceParts, rootParts) || !startsWithSegments(targetParts, rootParts))) {
    return { ok: false, reason: 'outside-workspace-root' };
  }

  const fromDirectory = sourceParts.slice(0, -1);
  let shared = 0;
  while (shared < fromDirectory.length && shared < targetParts.length && fromDirectory[shared] === targetParts[shared]) {
    shared += 1;
  }
  if (shared < rootParts.length) return { ok: false, reason: 'outside-workspace-root' };

  const path = [
    ...Array.from({ length: fromDirectory.length - shared }, () => '..'),
    ...targetParts.slice(shared),
  ].join('/');
  return path ? { ok: true, path } : { ok: false, reason: 'empty-target' };
}

export function encodeWorkspacePath(path) {
  return String(path || '').split('/').map((segment) => segment === '..' || segment === '.'
    ? segment
    : encodeLinkComponent(segment)).join('/');
}

export function encodeLinkComponent(value) {
  return encodeURIComponent(String(value || '')).replace(/[!'()*]/g, (character) => `%${character.charCodeAt(0).toString(16).toUpperCase()}`);
}

export function escapeMarkdownLinkLabel(value) {
  return String(value || '')
    .replace(/\r?\n/g, ' ')
    .replace(/\\/g, '\\\\')
    .replace(/([\[\]])/g, '\\$1')
    .trim();
}

export function normaliseCanonicalPath(value) {
  return String(value || '').replace(/\\/g, '/').replace(/^\/+|\/+$/g, '').replace(/\/{2,}/g, '/');
}

function splitLiteralTarget(literal) {
  const hashIndex = literal.indexOf('#');
  const beforeFragment = hashIndex >= 0 ? literal.slice(0, hashIndex) : literal;
  const queryIndex = beforeFragment.indexOf('?');
  return {
    path: queryIndex >= 0 ? beforeFragment.slice(0, queryIndex) : beforeFragment,
    query: queryIndex >= 0 ? beforeFragment.slice(queryIndex + 1) : '',
    fragment: hashIndex >= 0 ? literal.slice(hashIndex + 1) : '',
    hasFragment: hashIndex >= 0,
  };
}

function decodeLinkPart(value, { path = false } = {}) {
  const raw = String(value || '');
  if (path && dangerousEncodedPathPattern.test(raw)) return { ok: false, reason: 'encoded-separator' };
  let decoded;
  try {
    decoded = decodeURIComponent(raw);
  } catch {
    return { ok: false, reason: 'malformed-encoding' };
  }
  if (controlCharacterPattern.test(decoded)) return { ok: false, reason: 'control-character' };
  if (path && dangerousEncodedPathPattern.test(decoded)) return { ok: false, reason: 'ambiguous-encoding' };
  return { ok: true, value: decoded };
}

function startsWithSegments(value, prefix) {
  return prefix.every((segment, index) => value[index] === segment);
}

function invalidResult(literal, reason) {
  return { kind: 'invalid', literal, reason };
}

function unsupportedResult(literal, reason, extra = {}) {
  return { kind: 'unsupported', literal, reason, ...extra };
}
