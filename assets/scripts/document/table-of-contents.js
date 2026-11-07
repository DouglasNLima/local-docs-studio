import { slugify } from '../utils/format.js';

// Used by the live outline and exported Docs Sites. Existing identifiers are
// retained so copying a TOC cannot rename a target or break a saved link.
export function collectDocumentHeadings(root) {
  const used = new Set();
  return [...root.querySelectorAll('h1, h2, h3, h4, h5, h6')].map((heading, index) => {
    const text = heading.textContent.trim() || `Section ${index + 1}`;
    const base = heading.id || slugify(text) || `section-${index + 1}`;
    let id = base;
    for (let suffix = 2; used.has(id); suffix++) id = `${base}-${suffix}`;
    used.add(id); heading.id = id;
    return { id, text, level: Number(heading.tagName.slice(1)), heading, line: Number(heading.dataset.line) };
  });
}

export function buildTableOfContents(headings, { startLevel = 1, endLevel = 6, style = 'bullets', normalise = true, details = false } = {}) {
  if (!Number.isInteger(startLevel) || !Number.isInteger(endLevel) || startLevel < 1 || endLevel > 6 || startLevel > endLevel)
    throw new Error('Choose heading levels from 1 to 6, with the start no greater than the end.');
  if (!['bullets', 'numbered', 'plain'].includes(style)) throw new Error('Choose a supported table of contents style.');
  const levels = [];
  const rows = headings.filter(item => item.level >= startLevel && item.level <= endLevel).map(item => {
    while (levels.length && levels.at(-1) >= item.level) levels.pop();
    const depth = normalise ? levels.length : item.level - 1;
    levels.push(item.level);
    const label = item.text.replace(/\s+/g, ' ').trim();
    const escaped = label.replace(/[\\[\]`*_<>]/g, '\\$&');
    const entry = style === 'plain' ? label : `${style === 'numbered' ? '1.' : '-'} [${escaped}](#${encodeURIComponent(item.id)})`;
    return `${(style === 'numbered' ? '   ' : '  ').repeat(depth)}${entry}`;
  });
  if (!rows.length) return '';
  const body = rows.join('\n');
  return details ? `<details>\n<summary>Table of Contents</summary>\n\n${body}\n\n</details>` : body;
}
