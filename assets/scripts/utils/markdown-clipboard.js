import { escapeHtml } from './format.js';

const markdownImagePattern = /!\[([^\]\r\n]*)\]\(\s*(<[^>\r\n]*>|(?:\\.|[^)\s])*)(?:\s+((?:"[^"]*"|'[^']*'|\([^)]*\))))?\s*\)/g;

/**
 * Find Markdown image tokens without entering fenced code blocks.
 *
 * The returned offsets refer to the original string so callers can replace
 * only the image payload while leaving every other Markdown character alone.
 */
export function findMarkdownImageTokens(markdown) {
  const source = String(markdown ?? '');
  if (!source) return [];

  const fencedRanges = getFencedCodeRanges(source);
  return [...source.matchAll(new RegExp(markdownImagePattern))]
    .filter((match) => !fencedRanges.some((range) => match.index >= range.start && match.index + match[0].length <= range.end))
    .map((match) => {
      const destination = match[2] || '';
      const href = destination.startsWith('<') && destination.endsWith('>')
        ? destination.slice(1, -1)
        : destination;
      const title = match[3] || '';
      return {
        start: match.index,
        end: match.index + match[0].length,
        raw: match[0],
        alt: match[1] || '',
        href,
        title,
        titleText: title.length > 1 ? title.slice(1, -1) : '',
      };
    });
}

/**
 * Compose the rich clipboard representation used by Copy Markdown with
 * images. Markdown remains literal text; only data-backed image tokens become
 * HTML images so rich paste targets can attach them.
 */
export function composeMarkdownClipboardHtml(markdown) {
  const source = String(markdown ?? '');
  const parts = ['<span data-markdown-clipboard="source">'];
  let cursor = 0;
  let imageIndex = 0;

  findMarkdownImageTokens(source).forEach((token) => {
    parts.push(encodeMarkdownClipboardText(source.slice(cursor, token.start)));

    if (isClipboardImageDataUrl(token.href)) {
      const titleAttribute = token.titleText
        ? ` title="${escapeHtml(token.titleText)}"`
        : '';
      parts.push(`<img data-markdown-clipboard-image="${imageIndex}" src="${escapeHtml(token.href)}" alt="${escapeHtml(token.alt)}"${titleAttribute}>`);
      imageIndex += 1;
    } else {
      parts.push(encodeMarkdownClipboardText(token.raw));
    }

    cursor = token.end;
  });

  parts.push(encodeMarkdownClipboardText(source.slice(cursor)), '</span>');
  return parts.join('');
}

/**
 * Encode literal Markdown for the HTML clipboard carrier.
 *
 * HTML source newlines are whitespace and may be collapsed by the paste
 * destination, so each logical source line boundary is represented by an
 * explicit BR element after escaping the source text.
 */
export function encodeMarkdownClipboardText(value) {
  return escapeHtml(value).replace(/\r\n|\r|\n/g, '<br>');
}

export function isClipboardImageDataUrl(value) {
  return /^data:image\/(?:png|jpe?g|gif|webp);base64,/i.test(String(value || '').trim());
}

function getFencedCodeRanges(source) {
  const ranges = [];
  const lines = getSourceLines(source);
  let opening = null;

  lines.forEach((line) => {
    if (opening) {
      if (isFenceClose(line.text, opening)) {
        ranges.push({ start: opening.start, end: line.end });
        opening = null;
      }
      return;
    }

    const match = line.text.match(/^[ \t]{0,3}(`{3,}|~{3,})/);
    if (match) {
      opening = {
        start: line.start,
        char: match[1][0],
        length: match[1].length,
      };
    }
  });

  if (opening) ranges.push({ start: opening.start, end: source.length });
  return ranges;
}

function getSourceLines(source) {
  const lines = [];
  let start = 0;

  while (start < source.length) {
    const nextBreak = source.slice(start).search(/\r\n|\n|\r/);
    if (nextBreak === -1) {
      lines.push({ text: source.slice(start), start, end: source.length });
      break;
    }

    const end = start + nextBreak;
    const eol = source.slice(end, end + 2) === '\r\n' ? '\r\n' : source[end];
    lines.push({ text: source.slice(start, end), start, end });
    start = end + eol.length;
  }

  return lines;
}

function isFenceClose(line, fence) {
  const pattern = fence.char === '`'
    ? new RegExp(`^[ \\t]{0,3}\`{${fence.length},}[ \\t]*$`)
    : new RegExp(`^[ \\t]{0,3}~{${fence.length},}[ \\t]*$`);
  return pattern.test(line);
}
