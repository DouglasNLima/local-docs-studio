import DOMPurify from '../../vendor/dompurify-3.4.5.es.js';
import { formatMarkdownTable } from './markdown-table.js';

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

export function htmlToMarkdown(html, options = {}) {
  if (!html?.trim()) return '';
  const document = parseHtmlDocument(html, options);
  return cleanMarkdownDocument(markdownFromChildren(document.body, options));
}

export function htmlToPlainText(html) {
  if (!html) return '';
  const document = parseHtmlDocument(html, { sanitize: true });
  return document.body.textContent || '';
}

export function parseHtmlTable(input) {
  if (!input) return null;

  let table = null;
  if (typeof Element !== 'undefined' && input instanceof Element) {
    table = input.matches('table') ? input : input.querySelector('table');
  } else {
    const html = String(input || '');
    if (!/<table[\s>]/i.test(html)) return null;
    const document = new DOMParser().parseFromString(html, 'text/html');
    table = document.querySelector('table');
  }

  if (!table) return null;

  const rows = [];
  const rowspans = [];
  let flattened = false;

  table.querySelectorAll('tr').forEach((tableRow) => {
    const row = [];
    let columnIndex = 0;

    columnIndex = fillActiveRowspans(row, rowspans, columnIndex);

    tableRow.querySelectorAll('th,td').forEach((cell) => {
      columnIndex = fillActiveRowspans(row, rowspans, columnIndex);

      const colspan = Math.max(parseInt(cell.getAttribute('colspan') || '1', 10) || 1, 1);
      const rowspan = Math.max(parseInt(cell.getAttribute('rowspan') || '1', 10) || 1, 1);
      flattened = flattened || colspan > 1 || rowspan > 1;

      row[columnIndex] = normaliseCellText(cell.textContent || '');
      for (let offset = 0; offset < colspan; offset += 1) {
        if (offset > 0) row[columnIndex + offset] = '';
        if (rowspan > 1) rowspans[columnIndex + offset] = Math.max(rowspans[columnIndex + offset] || 0, rowspan - 1);
      }
      columnIndex += colspan;
    });

    fillTrailingRowspans(row, rowspans, columnIndex);
    rows.push(row);
  });

  return isUsefulTable(rows) ? { rows, flattened } : null;
}

export function isUsefulTable(rows) {
  if (!rows.length) return false;
  const width = Math.max(...rows.map((row) => row.length), 0);
  return width > 1 && rows.some((row) => row.some((cell) => String(cell || '').trim()));
}

export function buildCodeBlock(value, language = '') {
  const text = normaliseLineEndings(value);
  const longestFence = Math.max(2, ...[...text.matchAll(/`+/g)].map((match) => match[0].length));
  const fence = '`'.repeat(Math.max(3, longestFence + 1));
  const info = String(language || '').trim().replace(/[`\s].*$/g, '');
  return `${fence}${info}\n${text}${text.endsWith('\n') ? '' : '\n'}${fence}`;
}

export function buildQuoteBlock(value) {
  const lines = normaliseLineEndings(value).replace(/\n+$/, '').split('\n');
  return lines.map((line) => line.trim() ? `> ${line}` : '>').join('\n');
}

export function cleanMarkdownDocument(value) {
  return normaliseLineEndings(value)
    .split('\n')
    .map((line) => line.replace(/[ \t]+$/g, ''))
    .join('\n')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
}

export function normaliseLineEndings(value) {
  return String(value || '').replace(/\r\n?/g, '\n');
}

function parseHtmlDocument(html, { sanitize = true } = {}) {
  const source = sanitize ? sanitizeHtml(html) : html;
  const document = new DOMParser().parseFromString(source, 'text/html');
  scrubUnsafeUrls(document.body);
  return document;
}

function sanitizeHtml(html) {
  return DOMPurify.sanitize(html, {
    ADD_ATTR: ['start', 'colspan', 'rowspan'],
    FORBID_TAGS: forbiddenHtmlTags,
    FORBID_ATTR: forbiddenHtmlAttributes,
  });
}

function scrubUnsafeUrls(root) {
  root.querySelectorAll('a[href]').forEach((link) => {
    if (!isSafeLinkUrl(link.getAttribute('href') || '')) link.removeAttribute('href');
  });

  root.querySelectorAll('img[src]').forEach((image) => {
    if (!isSafeImageUrl(image.getAttribute('src') || '')) image.removeAttribute('src');
  });

  root.querySelectorAll('[src]:not(img)').forEach((element) => {
    element.removeAttribute('src');
  });
}

function isSafeLinkUrl(value) {
  const url = String(value || '').trim();
  if (!url) return false;
  return /^(https?:|mailto:|#|\.?\.?\/|[^:?#/][^:]*$)/i.test(url);
}

function isSafeImageUrl(value) {
  const url = String(value || '').trim();
  if (!url) return false;
  if (/^data:image\/svg\+xml/i.test(url)) return false;
  if (/^data:/i.test(url)) return /^data:image\/(png|jpe?g|gif|webp);base64,/i.test(url);
  return /^(https?:|blob:|#|\.?\.?\/|[^:?#/][^:]*$)/i.test(url);
}

function markdownFromChildren(node, options) {
  return [...(node?.childNodes || [])].map((child) => markdownFromNode(child, options)).join('');
}

function markdownFromNode(node, options) {
  if (node.nodeType === 3) return normaliseInlineText(node.textContent || '');
  if (node.nodeType !== 1) return '';

  const element = node;
  const tag = element.tagName.toLowerCase();
  const children = () => markdownFromChildren(element, options);
  const text = () => cleanMarkdownDocument(children() || element.textContent || '');

  if (/^h[1-6]$/.test(tag)) {
    const level = Number(tag.slice(1));
    return block(`${'#'.repeat(level)} ${text()}`);
  }

  if (['p', 'div', 'section', 'article', 'header', 'footer', 'main', 'aside', 'figure', 'figcaption'].includes(tag)) {
    return block(text());
  }

  if (tag === 'br') return '\n';
  if (tag === 'strong' || tag === 'b') return `**${children()}**`;
  if (tag === 'em' || tag === 'i') return `*${children()}*`;
  if (tag === 's' || tag === 'del' || tag === 'strike') return `~~${children()}~~`;
  if (tag === 'sub' || tag === 'sup' || tag === 'span') return children();

  if (tag === 'input' && element.getAttribute('type') === 'checkbox') {
    return element.hasAttribute('checked') ? '[x] ' : '[ ] ';
  }

  if (tag === 'code' && element.closest('pre')) {
    return element.textContent || '';
  }

  if (tag === 'code') {
    return formatInlineCode(element.textContent || '');
  }

  if (tag === 'pre') {
    const code = element.querySelector('code');
    const source = code?.textContent || element.textContent || '';
    const language = getCodeLanguageFromElement(code);
    return source.trim() ? block(buildCodeBlock(source, language)) : '';
  }

  if (tag === 'blockquote') {
    return block(buildQuoteBlock(text()));
  }

  if (tag === 'ul' || tag === 'ol') {
    return renderList(element, tag === 'ol', options);
  }

  if (tag === 'table') {
    const table = parseHtmlTable(element);
    return table ? block(formatMarkdownTable(table.rows)) : '';
  }

  if (tag === 'a') {
    const href = element.getAttribute('href') || '';
    const label = cleanInlineMarkdown(children() || element.textContent || href);
    return href ? `[${label}](${href})` : label;
  }

  if (tag === 'img') {
    const src = element.getAttribute('src') || '';
    if (!src) return '';
    const alt = element.getAttribute('alt') || 'image';
    const resolved = options.resolveImageSource?.({ src, alt, element });
    if (resolved === null) return '';
    const imageSrc = resolved || src;
    return imageSrc ? `![${cleanInlineMarkdown(alt)}](${imageSrc})` : '';
  }

  if (tag === 'hr') return block('---');

  return children();
}

function renderList(element, ordered, options) {
  const start = Math.max(parseInt(element.getAttribute('start') || '1', 10) || 1, 1);
  const items = [...element.children]
    .filter((child) => child.tagName?.toLowerCase() === 'li')
    .map((item) => cleanMarkdownDocument(markdownFromChildren(item, options)) || normaliseInlineText(item.textContent || ''))
    .filter(Boolean);

  if (!items.length) return '';

  return block(items.map((item, index) => {
    const marker = ordered ? `${start + index}.` : '-';
    return `${marker} ${indentListContinuation(item)}`;
  }).join('\n'));
}

function indentListContinuation(value) {
  return normaliseLineEndings(value).replace(/\n/g, '\n  ');
}

function getCodeLanguageFromElement(element) {
  const className = element?.getAttribute('class') || '';
  const match = className.match(/(?:^|\s)language-([a-z0-9_-]+)/i);
  return match?.[1] || '';
}

function fillActiveRowspans(row, rowspans, columnIndex) {
  while (rowspans[columnIndex] > 0) {
    row[columnIndex] = '';
    rowspans[columnIndex] -= 1;
    columnIndex += 1;
  }
  return columnIndex;
}

function fillTrailingRowspans(row, rowspans, columnIndex) {
  for (let index = columnIndex; index < rowspans.length; index += 1) {
    if (rowspans[index] > 0) {
      row[index] = '';
      rowspans[index] -= 1;
    }
  }
}

function formatInlineCode(value) {
  const text = String(value || '');
  const longestFence = Math.max(0, ...[...text.matchAll(/`+/g)].map((match) => match[0].length));
  const fence = '`'.repeat(longestFence + 1 || 1);
  const padding = text.startsWith('`') || text.endsWith('`') ? ' ' : '';
  return `${fence}${padding}${text}${padding}${fence}`;
}

function block(value) {
  const text = cleanMarkdownDocument(value);
  return text ? `\n\n${text}\n\n` : '';
}

function cleanInlineMarkdown(value) {
  return normaliseInlineText(value).replace(/[\[\]]/g, '\\$&');
}

function normaliseInlineText(value) {
  return String(value || '').replace(/\s+/g, ' ');
}

function normaliseCellText(value) {
  return normaliseLineEndings(value)
    .replace(/\u00a0/g, ' ')
    .replace(/[ \t]+\n/g, '\n')
    .replace(/\n[ \t]+/g, '\n')
    .trim();
}
