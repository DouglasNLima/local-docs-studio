import DOMPurify from '../../vendor/dompurify-3.4.5.es.js';

const renderedHtmlAttributes = [
  'target',
  'rel',
  'aria-label',
  'data-code-action',
  'data-code-language',
  'data-table-action',
  'data-progress-bar',
  'data-progress-colour',
  'data-progress-label',
  'data-progress-value',
  'data-status-badge',
  'data-status-kind',
  'data-details-block',
  'data-image-figure',
  'data-shortcut',
  'data-doc-anchor',
  'data-diagram-action',
  'data-diagram-index',
  'data-diagram-total',
  'data-diagram-source',
  'data-diagram-file-stem',
  'data-managed-asset-path',
  'data-line',
  'data-doc-path',
  'data-wikilink-target',
];

const forbiddenHtmlTags = [
  'base',
  'embed',
  'form',
  'iframe',
  'link',
  'meta',
  'object',
  'script',
];

const forbiddenHtmlAttributes = [
  'srcdoc',
  'style',
];

export function sanitizeRenderedHtml(dirtyHtml) {
  const cleanHtml = DOMPurify.sanitize(dirtyHtml, {
    ADD_ATTR: renderedHtmlAttributes,
    FORBID_TAGS: forbiddenHtmlTags,
    FORBID_ATTR: forbiddenHtmlAttributes,
  });
  const template = document.createElement('template');
  template.innerHTML = cleanHtml;
  scrubUnsafeUrls(template.content);
  return template.innerHTML;
}

export function sanitizeMermaidSvg(svg) {
  const cleanSvg = DOMPurify.sanitize(replaceForeignObjectLabels(normaliseSvgVoidElements(svg)), {
    USE_PROFILES: { svg: true, svgFilters: true },
    FORBID_TAGS: ['foreignObject', 'script'],
  });
  const template = document.createElement('template');
  template.innerHTML = cleanSvg;
  scrubUnsafeUrls(template.content, { allowDataImages: false });
  template.content.querySelectorAll('*').forEach((element) => {
    [...element.attributes].forEach((attribute) => {
      if (/^on/i.test(attribute.name) || attribute.name === 'srcdoc') {
        element.removeAttribute(attribute.name);
      }
    });
  });
  return template.innerHTML;
}

function normaliseSvgVoidElements(svg) {
  return String(svg || '').replace(
    /<(br|hr|img|input|meta|link|area|base|col|embed|param|source|track|wbr)\b([^<>]*)>/gi,
    (match, tagName, attributes = '') => {
      if (/\/\s*$/.test(attributes)) return match;
      return `<${tagName}${attributes} />`;
    }
  );
}

function replaceForeignObjectLabels(svg) {
  const document = new DOMParser().parseFromString(svg, 'image/svg+xml');
  document.querySelectorAll('foreignObject').forEach((foreignObject) => {
    const sourceLines = getForeignObjectTextLines(foreignObject);
    if (!sourceLines.length) {
      foreignObject.remove();
      return;
    }

    const x = parseFloat(foreignObject.getAttribute('x')) || 0;
    const y = parseFloat(foreignObject.getAttribute('y')) || 0;
    const width = parseFloat(foreignObject.getAttribute('width')) || 1;
    const height = parseFloat(foreignObject.getAttribute('height')) || 1;
    const { lines, fontSize, lineHeight } = fitForeignObjectTextLines(sourceLines, width, height);
    const svgText = document.createElementNS('http://www.w3.org/2000/svg', 'text');

    svgText.setAttribute('x', String(x + width / 2));
    svgText.setAttribute('text-anchor', 'middle');
    svgText.setAttribute('dominant-baseline', 'middle');
    svgText.setAttribute('font-size', String(fontSize));
    svgText.setAttribute('fill', '#333333');
    svgText.setAttribute('y', String(y + height / 2 - ((lines.length - 1) * lineHeight) / 2));

    lines.forEach((line, index) => {
      const tspan = document.createElementNS('http://www.w3.org/2000/svg', 'tspan');
      tspan.setAttribute('x', String(x + width / 2));
      if (index > 0) tspan.setAttribute('dy', String(lineHeight));
      tspan.textContent = line;
      svgText.appendChild(tspan);
    });

    foreignObject.replaceWith(svgText);
  });

  return new XMLSerializer().serializeToString(document.documentElement);
}

function fitForeignObjectTextLines(sourceLines, width, height) {
  const availableWidth = Math.max(width - 10, 24);
  const availableHeight = Math.max(height, 12);
  let fontSize = 16;
  let lines = [];
  let lineHeight = getLineHeight(fontSize);

  while (fontSize >= 7) {
    lines = wrapTextLines(sourceLines, availableWidth, fontSize);
    lineHeight = getLineHeight(fontSize);
    if (getMaxSvgLineWidth(lines, fontSize) <= availableWidth + 2 && lines.length * lineHeight <= availableHeight + 2) {
      return { lines, fontSize, lineHeight };
    }
    fontSize -= 1;
  }

  lines = wrapTextLines(sourceLines, availableWidth, 7);
  lineHeight = Math.max(7, Math.min(getLineHeight(7), availableHeight / Math.max(lines.length, 1)));
  return {
    lines,
    fontSize: Math.max(6, Math.min(7, lineHeight * .86)),
    lineHeight,
  };
}

function wrapTextLines(sourceLines, maxWidth, fontSize) {
  return sourceLines.flatMap((line) => wrapTextLine(line, maxWidth, fontSize));
}

function wrapTextLine(line, maxWidth, fontSize) {
  const words = String(line).split(/\s+/).filter(Boolean);
  const lines = [];
  let current = '';

  words.forEach((word) => {
    const next = current ? `${current} ${word}` : word;
    if (!current || measureSvgText(next, fontSize) <= maxWidth) {
      current = next;
      return;
    }

    lines.push(current);
    current = word;
  });

  if (current) lines.push(current);
  return lines.length ? lines : [''];
}

function getMaxSvgLineWidth(lines, fontSize) {
  return lines.reduce((max, line) => Math.max(max, measureSvgText(line, fontSize)), 0);
}

function measureSvgText(text, fontSize) {
  return [...String(text)].reduce((sum, char) => sum + getSvgCharWidth(char, fontSize), 0);
}

function getSvgCharWidth(char, fontSize) {
  if (/\s/.test(char)) return fontSize * .32;
  if (/[A-Z]/.test(char)) return fontSize * .66;
  if (/[il.,'`:;]/.test(char)) return fontSize * .28;
  if (/[mwMW@#%&]/.test(char)) return fontSize * .82;
  if (/[-_/\\|()[\]{}+]/.test(char)) return fontSize * .38;
  return fontSize * .56;
}

function getLineHeight(fontSize) {
  return fontSize * 1.18;
}

function getForeignObjectTextLines(foreignObject) {
  const clone = foreignObject.cloneNode(true);
  clone.querySelectorAll('br').forEach((breakElement) => {
    breakElement.replaceWith(clone.ownerDocument.createTextNode('\n'));
  });

  const lines = clone.textContent
    .split(/\n+/)
    .map((line) => line.replace(/\s+/g, ' ').trim())
    .filter(Boolean);

  if (lines.length) return lines;

  const fallbackText = foreignObject.textContent.replace(/\s+/g, ' ').trim();
  return fallbackText ? [fallbackText] : [];
}

export function buildCspMeta(policy) {
  return `<meta http-equiv="Content-Security-Policy" content="${escapeHtmlAttribute(policy)}">`;
}

export function buildAppCsp() {
  return [
    "default-src 'self'",
    "script-src 'self'",
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob: https:",
    "font-src 'self'",
    "connect-src 'self'",
    "worker-src 'self' blob:",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'none'",
  ].join('; ');
}

export function buildDocsSiteCsp() {
  return [
    "default-src 'self'",
    "script-src 'self'",
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob:",
    "font-src 'self'",
    "connect-src 'self'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'none'",
  ].join('; ');
}

export function buildStandaloneExportCsp(nonce) {
  return [
    "default-src 'none'",
    `script-src 'nonce-${nonce}'`,
    "style-src 'unsafe-inline'",
    "img-src data: blob:",
    "font-src data:",
    "connect-src 'none'",
    "object-src 'none'",
    "base-uri 'none'",
    "form-action 'none'",
  ].join('; ');
}

export function buildPrintExportCsp() {
  return [
    "default-src 'none'",
    "style-src 'unsafe-inline'",
    "img-src data: blob:",
    "font-src data:",
    "object-src 'none'",
    "base-uri 'none'",
    "form-action 'none'",
  ].join('; ');
}

export function createCspNonce() {
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  return btoa(String.fromCharCode(...bytes)).replace(/[+/=]/g, '');
}

function scrubUnsafeUrls(root, { allowDataImages = true } = {}) {
  root.querySelectorAll('[href], [xlink\\:href]').forEach((element) => {
    for (const name of ['href', 'xlink:href']) {
      if (!element.hasAttribute(name)) continue;
      if (!isSafeLinkUrl(element.getAttribute(name) || '')) element.removeAttribute(name);
    }
  });

  root.querySelectorAll('img[src]').forEach((image) => {
    if (!isSafeImageUrl(image.getAttribute('src') || '', allowDataImages)) image.remove();
  });

  root.querySelectorAll('[src]:not(img)').forEach((element) => {
    if (!isSafeResourceUrl(element.getAttribute('src') || '')) element.removeAttribute('src');
  });
}

function isSafeLinkUrl(value) {
  const url = String(value || '').trim();
  if (!url) return false;
  return /^(https?:|mailto:|#|\.?\.?\/|[^:?#/][^:]*$)/i.test(url);
}

function isSafeImageUrl(value, allowDataImages) {
  const url = String(value || '').trim();
  if (!url) return false;
  if (/^data:image\/svg\+xml/i.test(url)) return false;
  if (/^data:/i.test(url)) return allowDataImages && /^data:image\/(png|jpe?g|gif|webp);base64,/i.test(url);
  return /^(https?:|blob:|#|\.?\.?\/|[^:?#/][^:]*$)/i.test(url);
}

function isSafeResourceUrl(value) {
  const url = String(value || '').trim();
  if (!url) return false;
  return /^(https?:|blob:|\.?\.?\/|[^:?#/][^:]*$)/i.test(url);
}

function escapeHtmlAttribute(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('"', '&quot;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;');
}
