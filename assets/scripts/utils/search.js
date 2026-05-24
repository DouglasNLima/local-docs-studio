export function escapeRegExp(value) {
  return String(value).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

export function buildSearchRegex(query, { regex = false, caseSensitive = false } = {}) {
  const source = regex ? String(query || '') : escapeRegExp(query || '');
  if (!source) return { regex: null, error: null };

  try {
    return { regex: new RegExp(source, caseSensitive ? 'g' : 'gi'), error: null };
  } catch (error) {
    return { regex: null, error };
  }
}

export function findTextMatches(source, query, options = {}) {
  const { regex, error } = buildSearchRegex(query, options);
  if (!regex || error) return [];

  const text = String(source || '');
  const matches = [];
  let match;
  while ((match = regex.exec(text))) {
    matches.push({
      index: match.index,
      length: match[0].length,
      text: match[0],
      groups: match,
    });
    if (match[0].length === 0) regex.lastIndex += 1;
  }
  return matches;
}

export function replaceAllText(source, query, replacement, options = {}) {
  const { regex, error } = buildSearchRegex(query, options);
  if (!regex || error) return { text: source, count: 0, error };

  let count = 0;
  const text = String(source || '').replace(regex, (...args) => {
    count += 1;
    if (!options.regex) return replacement;
    return String(replacement || '').replace(/\$(\d+)/g, (_, index) => args[Number(index)] ?? '');
  });
  return { text, count, error: null };
}

export function getLineOffsets(source) {
  const text = String(source || '');
  const offsets = [0];
  for (let index = 0; index < text.length; index += 1) {
    if (text[index] === '\n') offsets.push(index + 1);
  }
  return offsets;
}

export function getLineInfoAtIndex(source, index, offsets = getLineOffsets(source)) {
  const text = String(source || '');
  let low = 0;
  let high = offsets.length - 1;
  while (low <= high) {
    const mid = Math.floor((low + high) / 2);
    if (offsets[mid] <= index && (mid === offsets.length - 1 || offsets[mid + 1] > index)) {
      const end = text.indexOf('\n', offsets[mid]);
      const lineEnd = end === -1 ? text.length : end;
      return {
        line: mid + 1,
        column: index - offsets[mid] + 1,
        start: offsets[mid],
        end: lineEnd,
        text: text.slice(offsets[mid], lineEnd),
      };
    }
    if (offsets[mid] > index) high = mid - 1;
    else low = mid + 1;
  }
  return { line: 1, column: 1, start: 0, end: text.length, text };
}

export function fuzzyScore(query, candidate) {
  const q = String(query || '').trim().toLowerCase();
  const c = String(candidate || '').toLowerCase();
  if (!q) return 1;
  if (!c) return 0;
  if (c === q) return 1000;
  if (c.includes(q)) return 600 - c.indexOf(q);

  let score = 0;
  let lastIndex = -1;
  for (const char of q) {
    const nextIndex = c.indexOf(char, lastIndex + 1);
    if (nextIndex === -1) return 0;
    score += nextIndex === lastIndex + 1 ? 12 : 4;
    if (nextIndex === 0 || /[\/._ -]/.test(c[nextIndex - 1] || '')) score += 8;
    lastIndex = nextIndex;
  }
  return score - Math.max(0, c.length - q.length) * 0.05;
}

export function summariseLineContext(lineText, column, width = 120) {
  const text = String(lineText || '').replace(/\s+/g, ' ').trim();
  if (text.length <= width) return text;
  const center = Math.max(0, column - 1);
  const start = Math.max(0, center - Math.floor(width / 2));
  const end = Math.min(text.length, start + width);
  return `${start > 0 ? '...' : ''}${text.slice(start, end)}${end < text.length ? '...' : ''}`;
}
