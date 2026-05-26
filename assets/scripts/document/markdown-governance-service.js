import { getLineInfoAtIndex, getLineOffsets } from '../utils/search.js';
import { normaliseWikilinkTarget, resolveWikilinkTarget, splitWikilink } from '../utils/wikilinks.js';

export const governanceRuleLabels = {
  'heading-hierarchy': 'Heading hierarchy',
  'internal-links': 'Internal links',
  'alt-text': 'Alt text',
  tables: 'Tables',
  'british-english': 'British English',
  'release-markers': 'TODO/FIXME',
};

const assetTargetPattern = /\.(png|jpe?g|gif|webp|svg|pdf|zip)(?:[?#].*)?$/i;
const markdownLinkPattern = /!?\[([^\]\n]*)\]\(([^)\n]+)\)/g;
const wikilinkPattern = /\[\[([^\]\n]+?)\]\]/g;
const rawAnchorPattern = /<a\b[^>]*\bhref\s*=\s*(["'])(.*?)\1[^>]*>/gi;
const rawImagePattern = /<img\b[^>]*>/gi;

const britishEnglishTerms = new Map([
  ['analyze', 'analyse'],
  ['analyzed', 'analysed'],
  ['analyzing', 'analysing'],
  ['behavior', 'behaviour'],
  ['behaviors', 'behaviours'],
  ['catalog', 'catalogue'],
  ['center', 'centre'],
  ['centered', 'centred'],
  ['color', 'colour'],
  ['colors', 'colours'],
  ['customize', 'customise'],
  ['customized', 'customised'],
  ['customizing', 'customising'],
  ['favorite', 'favourite'],
  ['favorites', 'favourites'],
  ['gray', 'grey'],
  ['honor', 'honour'],
  ['honors', 'honours'],
  ['localization', 'localisation'],
  ['neighbor', 'neighbour'],
  ['neighbors', 'neighbours'],
  ['organize', 'organise'],
  ['organized', 'organised'],
  ['organizing', 'organising'],
  ['prioritize', 'prioritise'],
  ['prioritized', 'prioritised'],
  ['prioritizing', 'prioritising'],
  ['specialize', 'specialise'],
  ['specialized', 'specialised'],
  ['specializing', 'specialising'],
]);

const severityWeight = {
  danger: 0,
  warning: 1,
  info: 2,
};

export function analyzeMarkdownGovernance({ records = [], activePath = '' } = {}) {
  const documentRecords = records
    .filter((record) => record?.path)
    .map((record) => ({
      name: record.name || String(record.path).split('/').pop() || record.path,
      path: record.path,
      text: String(record.text || '').replace(/\r\n?/g, '\n'),
    }));
  const issues = [];

  documentRecords.forEach((record) => {
    const lineContexts = createLineContexts(record.text);
    const lineOffsets = getLineOffsets(record.text);
    const helpers = {
      records: documentRecords,
      lineContexts,
      lineOffsets,
      addIssue: (issue) => issues.push(normaliseIssue(issue, record)),
    };

    collectHeadingIssues(record, helpers);
    collectInternalLinkIssues(record, helpers);
    collectAltTextIssues(record, helpers);
    collectTableIssues(record, helpers);
    collectBritishEnglishIssues(record, helpers);
    collectReleaseMarkerIssues(record, helpers);
  });

  const sortedIssues = sortIssues(issues, documentRecords, activePath);
  return {
    issues: sortedIssues,
    summary: buildSummary(sortedIssues, documentRecords, activePath),
  };
}

function collectHeadingIssues(record, { lineContexts, addIssue }) {
  let previousLevel = 0;
  lineContexts.forEach((line) => {
    if (line.inCode) return;
    const match = line.text.match(/^(#{1,6})[ \t]+(.+?)(?:[ \t]+#+)?[ \t]*$/);
    if (!match) return;

    const level = match[1].length;
    if (previousLevel && level > previousLevel + 1) {
      addIssue({
        rule: 'heading-hierarchy',
        severity: 'warning',
        line: line.number,
        column: 1,
        length: match[1].length,
        message: `Heading level jumps from H${previousLevel} to H${level}.`,
        suggestion: `Insert an H${previousLevel + 1} heading before this section or lower it to H${previousLevel + 1}.`,
      });
    }
    previousLevel = level;
  });
}

function collectInternalLinkIssues(record, { records, lineContexts, lineOffsets, addIssue }) {
  const source = record.text;
  const scanLink = ({ raw, target, index, label, kind }) => {
    const lineInfo = getLineInfoAtIndex(source, index, lineOffsets);
    if (lineContexts[lineInfo.line - 1]?.inCode) return;

    const cleanTarget = cleanInternalTarget(target);
    if (!shouldValidateInternalTarget(cleanTarget)) return;
    if (resolveDocumentTarget(cleanTarget, records, record.path)) return;

    addIssue({
      rule: 'internal-links',
      severity: 'warning',
      line: lineInfo.line,
      column: lineInfo.column,
      length: raw.length,
      message: `${kind === 'wikilink' ? 'Broken wikilink' : 'Broken internal link'} "${label || cleanTarget}".`,
      suggestion: 'Load the target file or update the link target.',
    });
  };

  source.replace(wikilinkPattern, (raw, value, index) => {
    const link = splitWikilink(value);
    scanLink({ raw, target: link.target, label: link.label, index, kind: 'wikilink' });
    return raw;
  });

  source.replace(markdownLinkPattern, (raw, label, href, index) => {
    if (raw.startsWith('!')) return raw;
    scanLink({ raw, target: parseMarkdownHref(href), label, index, kind: 'markdown' });
    return raw;
  });

  source.replace(rawAnchorPattern, (raw, quote, href, index) => {
    scanLink({ raw, target: href, label: href, index, kind: 'html' });
    return raw;
  });
}

function collectAltTextIssues(record, { lineContexts, lineOffsets, addIssue }) {
  const source = record.text;
  source.replace(markdownLinkPattern, (raw, alt, href, index) => {
    if (!raw.startsWith('!')) return raw;
    const lineInfo = getLineInfoAtIndex(source, index, lineOffsets);
    if (lineContexts[lineInfo.line - 1]?.inCode) return raw;
    if (String(alt || '').trim()) return raw;

    addIssue({
      rule: 'alt-text',
      severity: 'warning',
      line: lineInfo.line,
      column: lineInfo.column,
      length: raw.length,
      message: 'Image is missing alt text.',
      suggestion: 'Add concise descriptive text inside ![...].',
    });
    return raw;
  });

  source.replace(rawImagePattern, (raw, index) => {
    const lineInfo = getLineInfoAtIndex(source, index, lineOffsets);
    if (lineContexts[lineInfo.line - 1]?.inCode) return raw;
    const alt = getRawImageAlt(raw);
    if (alt !== undefined && alt.trim()) return raw;

    addIssue({
      rule: 'alt-text',
      severity: 'warning',
      line: lineInfo.line,
      column: lineInfo.column,
      length: raw.length,
      message: 'HTML image is missing alt text.',
      suggestion: 'Add a meaningful alt attribute.',
    });
    return raw;
  });
}

function collectTableIssues(record, { lineContexts, addIssue }) {
  for (let index = 0; index < lineContexts.length - 1; index += 1) {
    const header = lineContexts[index];
    const separator = lineContexts[index + 1];
    if (header.inCode || separator.inCode) continue;
    if (!looksLikeTableRow(header.text) || !looksLikeTableRow(separator.text) || !looksLikeSeparatorCandidate(separator.text)) continue;

    const headerCells = parseMarkdownRow(header.text);
    const separatorCells = parseMarkdownRow(separator.text);
    const separatorValid = isValidSeparatorRow(separatorCells);

    if (!separatorValid) {
      addIssue({
        rule: 'tables',
        severity: 'warning',
        line: separator.number,
        column: 1,
        length: separator.text.length || 1,
        message: 'Markdown table separator row is invalid.',
        suggestion: 'Use at least three dashes per column, such as | --- | --- |.',
      });
    } else if (separatorCells.length !== headerCells.length) {
      addIssue({
        rule: 'tables',
        severity: 'warning',
        line: separator.number,
        column: 1,
        length: separator.text.length || 1,
        message: `Markdown table separator has ${separatorCells.length} columns but the header has ${headerCells.length}.`,
        suggestion: 'Keep the same number of cells in the header, separator, and body rows.',
      });
    }

    let endIndex = index + 1;
    for (let rowIndex = index + 2; rowIndex < lineContexts.length; rowIndex += 1) {
      const row = lineContexts[rowIndex];
      if (row.inCode || !looksLikeTableRow(row.text)) break;
      endIndex = rowIndex;
      const cells = parseMarkdownRow(row.text);
      if (cells.length !== headerCells.length) {
        addIssue({
          rule: 'tables',
          severity: 'warning',
          line: row.number,
          column: 1,
          length: row.text.length || 1,
          message: `Markdown table row has ${cells.length} columns but the header has ${headerCells.length}.`,
          suggestion: 'Add or remove cells so every row has the same column count.',
        });
      }
    }
    index = endIndex;
  }
}

function collectBritishEnglishIssues(record, { lineContexts, addIssue }) {
  lineContexts.forEach((line) => {
    if (line.inCode) return;
    const text = maskInlineCodeAndUrls(line.text);
    for (const [usTerm, ukTerm] of britishEnglishTerms.entries()) {
      const pattern = new RegExp(`\\b${escapeRegExp(usTerm)}\\b`, 'gi');
      let match;
      while ((match = pattern.exec(text))) {
        addIssue({
          rule: 'british-english',
          severity: 'info',
          line: line.number,
          column: match.index + 1,
          length: match[0].length,
          message: `Use British English spelling for "${match[0]}".`,
          suggestion: `Use "${matchCase(ukTerm, match[0])}".`,
        });
      }
    }
  });
}

function collectReleaseMarkerIssues(record, { lineContexts, addIssue }) {
  lineContexts.forEach((line) => {
    if (line.inCode) return;
    const text = maskInlineCodeAndUrls(line.text);
    const pattern = /\b(TODO|FIXME)\b/gi;
    let match;
    while ((match = pattern.exec(text))) {
      addIssue({
        rule: 'release-markers',
        severity: 'warning',
        line: line.number,
        column: match.index + 1,
        length: match[0].length,
        message: `${match[0].toUpperCase()} marker found before release.`,
        suggestion: 'Resolve it or move the follow-up into tracked work before release.',
      });
    }
  });
}

function createLineContexts(source) {
  const lines = String(source || '').split('\n');
  const contexts = [];
  let offset = 0;
  let inFence = false;
  let fenceMarker = '';
  let fenceLength = 0;

  lines.forEach((text, index) => {
    const fence = text.match(/^\s{0,3}(`{3,}|~{3,})/);
    const lineInCode = inFence || Boolean(fence);
    contexts.push({
      text,
      start: offset,
      number: index + 1,
      inCode: lineInCode,
    });

    if (fence) {
      const marker = fence[1][0];
      if (!inFence) {
        inFence = true;
        fenceMarker = marker;
        fenceLength = fence[1].length;
      } else if (marker === fenceMarker && fence[1].length >= fenceLength) {
        inFence = false;
        fenceMarker = '';
        fenceLength = 0;
      }
    }
    offset += text.length + 1;
  });

  return contexts;
}

function normaliseIssue(issue, record) {
  return {
    rule: issue.rule,
    severity: issue.severity || 'warning',
    path: issue.path || record.path,
    line: Math.max(1, Number(issue.line) || 1),
    column: Math.max(1, Number(issue.column) || 1),
    length: Math.max(1, Number(issue.length) || 1),
    message: issue.message,
    suggestion: issue.suggestion || '',
  };
}

function sortIssues(issues, records, activePath) {
  const pathOrder = new Map(records.map((record, index) => [record.path, index]));
  return [...issues].sort((left, right) => {
    const leftActive = left.path === activePath ? 0 : 1;
    const rightActive = right.path === activePath ? 0 : 1;
    return leftActive - rightActive
      || (severityWeight[left.severity] ?? 9) - (severityWeight[right.severity] ?? 9)
      || (pathOrder.get(left.path) ?? 9999) - (pathOrder.get(right.path) ?? 9999)
      || left.line - right.line
      || left.column - right.column;
  });
}

function buildSummary(issues, records, activePath) {
  const ruleCounts = new Map();
  issues.forEach((issue) => {
    const current = ruleCounts.get(issue.rule) || {
      rule: issue.rule,
      label: governanceRuleLabels[issue.rule] || issue.rule,
      count: 0,
      warningCount: 0,
      infoCount: 0,
    };
    current.count += 1;
    if (issue.severity === 'info') current.infoCount += 1;
    else current.warningCount += 1;
    ruleCounts.set(issue.rule, current);
  });

  return {
    fileCount: records.length,
    issueCount: issues.length,
    warningCount: issues.filter((issue) => issue.severity !== 'info').length,
    infoCount: issues.filter((issue) => issue.severity === 'info').length,
    activeIssueCount: issues.filter((issue) => issue.path === activePath).length,
    workspaceIssueCount: issues.filter((issue) => issue.path !== activePath).length,
    ruleCounts: [...ruleCounts.values()],
  };
}

function parseMarkdownHref(value) {
  const raw = String(value || '').trim();
  const bracketed = raw.match(/^<([^>]+)>/);
  const target = bracketed ? bracketed[1] : raw.split(/\s+/)[0];
  try {
    return decodeURIComponent(target);
  } catch {
    return target;
  }
}

function cleanInternalTarget(target) {
  return String(target || '').trim().split(/[?#]/)[0].replace(/^\.\/+/, '');
}

function shouldValidateInternalTarget(target) {
  if (!target || target.startsWith('#')) return false;
  if (/^(?:[a-z][a-z0-9+.-]*:|\/\/)/i.test(target)) return false;
  return !assetTargetPattern.test(target);
}

function resolveDocumentTarget(target, records, fromPath) {
  const resolved = resolveWikilinkTarget(target, records, fromPath);
  if (resolved) return resolved;

  const relativePath = normaliseRelativePath(target, fromPath).toLowerCase();
  const relativeNoExt = stripDocumentExtension(relativePath);
  return records.find((record) => {
    const path = normaliseWikilinkTarget(record.path).toLowerCase();
    const name = normaliseWikilinkTarget(record.name).toLowerCase();
    return path === relativePath
      || stripDocumentExtension(path) === relativeNoExt
      || name === relativePath
      || stripDocumentExtension(name) === relativeNoExt;
  }) || null;
}

function normaliseRelativePath(target, fromPath) {
  const activeDir = fromPath.includes('/') ? fromPath.slice(0, fromPath.lastIndexOf('/') + 1) : '';
  const raw = target.startsWith('/') ? target.slice(1) : `${activeDir}${target}`;
  const stack = [];
  raw.replace(/\\/g, '/').split('/').forEach((part) => {
    if (!part || part === '.') return;
    if (part === '..') stack.pop();
    else stack.push(part);
  });
  return stack.join('/');
}

function stripDocumentExtension(value) {
  return String(value || '').replace(/\.(md|markdown|mmd|mermaid)$/i, '');
}

function getRawImageAlt(tag) {
  const quoted = String(tag || '').match(/\balt\s*=\s*(["'])(.*?)\1/i);
  if (quoted) return quoted[2];
  const unquoted = String(tag || '').match(/\balt\s*=\s*([^\s>]+)/i);
  return unquoted ? unquoted[1] : undefined;
}

function looksLikeTableRow(line) {
  return /^\s*\|?.+\|.+\|?\s*$/.test(String(line || ''));
}

function looksLikeSeparatorCandidate(line) {
  const text = String(line || '').trim();
  return text.includes('|') && /-/.test(text) && /^[\s|:-]+$/.test(text);
}

function isValidSeparatorRow(cells) {
  return cells.length > 0 && cells.every((cell) => /^:?-{3,}:?$/.test(cell.trim()));
}

function parseMarkdownRow(line) {
  const raw = String(line || '').trim();
  const content = raw.startsWith('|') ? raw.slice(1) : raw;
  const trimmed = content.endsWith('|') ? content.slice(0, -1) : content;
  const cells = [];
  let current = '';
  let escaped = false;

  for (const char of trimmed) {
    if (escaped) {
      current += char;
      escaped = false;
      continue;
    }
    if (char === '\\') {
      escaped = true;
      continue;
    }
    if (char === '|') {
      cells.push(current.trim());
      current = '';
      continue;
    }
    current += char;
  }
  cells.push(current.trim());
  return cells;
}

function maskInlineCodeAndUrls(line) {
  return maskPattern(maskPattern(String(line || ''), /`[^`\n]*`/g), /\b(?:https?:\/\/|www\.)\S+/gi);
}

function maskPattern(value, pattern) {
  return value.replace(pattern, (match) => ' '.repeat(match.length));
}

function matchCase(replacement, original) {
  if (original === original.toUpperCase()) return replacement.toUpperCase();
  if (original[0] === original[0]?.toUpperCase()) {
    return `${replacement.charAt(0).toUpperCase()}${replacement.slice(1)}`;
  }
  return replacement;
}

function escapeRegExp(value) {
  return String(value).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
