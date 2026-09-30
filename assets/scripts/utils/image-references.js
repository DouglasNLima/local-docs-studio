import { marked } from '../../vendor/marked-16.4.2.esm.js';
import { escapeHtml } from './format.js';

export const IMAGE_MAX_BYTES = 20 * 1024 * 1024;
export const IMAGE_SESSION_MAX_BYTES = 100 * 1024 * 1024;
export const IMAGE_EXTENSIONS = /\.(png|jpe?g|gif|webp)$/i;

// Asset identity is a literal, case-sensitive workspace path. References are URIs
// relative to the document; decode once, never search by basename or root fallback.
export function resolveImageReference(value, documentPath = '', { literal = false, root = '' } = {}) {
  const source = String(value || '').trim();
  if (!source || /[\u0000-\u001f\u007f]/.test(source)) return invalid('Invalid image reference.');
  if (/^https?:\/\//i.test(source)) {
    try {
      const url = new URL(source);
      if (url.hostname === 'lens-docs-studio.local') return invalid('Use a workspace image path instead of a Windows virtual-host URL.');
      if (url.username || url.password || url.protocol !== 'https:') return invalid('Use an HTTPS image URL without credentials.');
      return { kind: 'external', reference: source };
    } catch { return invalid('Invalid image URL.'); }
  }
  if (/^data:image\/(png|jpe?g|gif|webp);base64,/i.test(source)) return { kind: 'embedded', reference: source };
  if (/^(?:[a-z][a-z\d+.-]*:|\/|\\)/i.test(source)) return invalid('Choose an image inside the authorised workspace or import a file.');
  let path = source.replaceAll('\\', '/');
  if (!literal) {
    // Query/fragment components are not filename characters in a stored URI.
    path = path.split(/[?#]/, 1)[0];
    if (/%(?:00|2f|5c)/i.test(path)) return invalid('Encoded separators are not allowed.');
    try { path = decodeURIComponent(path.replace(/%(?![\da-f]{2})/gi, '%25')); }
    catch { return invalid('Invalid image path encoding.'); }
  }
  if (/[:\u0000-\u001f\u007f]/.test(path) || path.startsWith('/')) return invalid('Invalid local image path.');
  const base = String(documentPath).replaceAll('\\', '/').split('/').slice(0, -1);
  const boundary = root ? root.replace(/\/$/, '').split('/').length : 0;
  for (const part of path.split('/')) {
    if (!part || part === '.') continue;
    if (part === '..') {
      if (base.length <= boundary) return invalid('Image path leaves the authorised workspace.');
      base.pop();
    } else base.push(part);
  }
  const identity = base.join('/');
  if (!IMAGE_EXTENSIONS.test(identity)) return invalid('Use PNG, JPEG, GIF, or WebP images.');
  return { kind: 'local', path: identity, reference: relativeImageReference(documentPath, identity) };
}

export function relativeImageReference(documentPath, assetPath) {
  const from = String(documentPath).replaceAll('\\', '/').split('/').slice(0, -1);
  const to = String(assetPath).replaceAll('\\', '/').split('/');
  while (from.length && to.length && from[0] === to[0]) { from.shift(); to.shift(); }
  return [...from.map(() => '..'), ...to.map(encodeImagePathComponent)].join('/');
}

export function encodeImagePathComponent(value) {
  return encodeURIComponent(value).replace(/[!'()*]/g, (char) => `%${char.charCodeAt(0).toString(16).toUpperCase()}`);
}

export function serialiseImage({ reference, alt = '', caption = '' }) {
  if (caption.trim()) return `<figure data-image-figure>\n  <img src="${escapeHtml(reference)}" alt="${escapeHtml(alt)}">\n  <figcaption>${escapeHtml(caption.trim())}</figcaption>\n</figure>`;
  return `![${String(alt).replaceAll('\\', '\\\\').replaceAll('[', '\\[').replaceAll(']', '\\]').replace(/\r?\n/g, ' ')}](${reference})`;
}

// Walk the same Markdown grammar used by the preview. Offsets are restricted to
// actual image/HTML tokens, so code, prose and unrelated links remain untouched.
export function collectImageTokens(source) {
  const text = String(source || '');
  const images = [];
  function walk(tokens, region, offset) {
    let cursor = 0;
    for (const token of tokens || []) {
      if (!token.raw) continue;
      const start = region.indexOf(token.raw, cursor);
      if (start < 0) continue;
      cursor = start + token.raw.length;
      const at = offset + start;
      if (token.type === 'image') images.push({ start: at, end: at + token.raw.length, href: token.href, alt: token.text, title: token.title, kind: 'markdown' });
      else if (token.type === 'html') {
        const html = token.raw.replace(/<!--[\s\S]*?-->/g, (match) => ' '.repeat(match.length));
        for (const match of html.matchAll(/<img\b[^>]*?\bsrc\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+))[^>]*>/gi)) {
          const attr = match[0].match(/\bsrc\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+))/i);
          const raw = attr[1] ?? attr[2] ?? attr[3];
          const valueStart = match.index + attr.index + attr[0].indexOf(raw, attr[0].indexOf('=') + 1);
          images.push({ start: at + valueStart, end: at + valueStart + raw.length, href: decodeHtmlAttribute(raw), kind: 'html' });
        }
      } else if (token.type !== 'code' && token.type !== 'codespan') {
        if (token.tokens) walk(token.tokens, token.raw, at);
        if (token.items) walk(token.items, token.raw, at);
        if (token.type === 'table') {
          let cellCursor = 0;
          for (const cell of [...token.header, ...token.rows.flat()]) {
            const cellAt = token.raw.indexOf(cell.text, cellCursor);
            if (cellAt < 0) continue;
            cellCursor = cellAt + cell.text.length;
            walk(cell.tokens, cell.text, at + cellAt);
          }
        }
      }
    }
  }
  walk(marked.lexer(text), text, 0);
  return images.sort((a, b) => a.start - b.start);
}

export function rewriteImageReferences(source, documentPath, mapPath, options = {}) {
  let result = String(source);
  for (const token of collectImageTokens(source).reverse()) {
    const resolved = options.resolveReference?.(token.href, documentPath) || resolveImageReference(token.href, documentPath, options);
    if (resolved.kind !== 'local') continue;
    const path = mapPath(resolved.path);
    if (!path || path === resolved.path && !resolved.legacy) continue;
    const reference = relativeImageReference(documentPath, path);
    const replacement = token.kind === 'html' ? escapeHtml(reference)
      : `${serialiseImage({ reference, alt: token.alt }) .slice(0, -1)}${token.title ? ` "${token.title.replaceAll('"', '&quot;')}"` : ''})`;
    result = result.slice(0, token.start) + replacement + result.slice(token.end);
  }
  return result;
}

export function inspectImageBytes(bytes, { name = '', mimeType = '' } = {}) {
  if (!bytes.length || bytes.length > IMAGE_MAX_BYTES) throw new Error('Choose an image up to 20 MB.');
  const prefix = [...bytes.slice(0, 12)];
  let type = '';
  if (prefix.slice(0, 8).join(',') === '137,80,78,71,13,10,26,10' && bytes.length >= 33) type = 'png';
  else if (prefix[0] === 255 && prefix[1] === 216 && prefix[2] === 255 && bytes.length >= 12) type = 'jpeg';
  else if (/^GIF8[79]a/.test(String.fromCharCode(...prefix)) && bytes.length >= 14) type = 'gif';
  else if (String.fromCharCode(...prefix.slice(0, 4)) === 'RIFF' && String.fromCharCode(...prefix.slice(8, 12)) === 'WEBP' && bytes.length >= 20) type = 'webp';
  if (!type) throw new Error('The file bytes are not a supported raster image. SVG images are not imported.');
  const extension = name.match(/\.([^.]+)$/)?.[1]?.toLowerCase();
  if (extension && !(extension === type || (type === 'jpeg' && extension === 'jpg'))) throw new Error('Image extension does not match its bytes.');
  if (mimeType && mimeType !== 'application/octet-stream' && mimeType !== `image/${type}` && !(type === 'jpeg' && mimeType === 'image/jpg')) throw new Error('Image MIME type does not match its bytes.');
  return { mimeType: `image/${type}`, extension: type === 'jpeg' ? 'jpg' : type };
}

function decodeHtmlAttribute(value) {
  return value.replace(/&(?:amp|quot|apos|lt|gt|#\d+|#x[\da-f]+);/gi, (entity) => {
    const map = { '&amp;': '&', '&quot;': '"', '&apos;': "'", '&lt;': '<', '&gt;': '>' };
    const normalised = entity.toLowerCase();
    if (map[normalised]) return map[normalised];
    const hex = normalised.startsWith('&#x');
    const code = parseInt(normalised.slice(hex ? 3 : 2, -1), hex ? 16 : 10);
    return Number.isFinite(code) && code >= 0 && code <= 0x10ffff ? String.fromCodePoint(code) : '\uFFFD';
  });
}

function invalid(message) { return { kind: 'invalid', message }; }
