export function normaliseDevOpsMermaidBlocks(source) {
  const text = String(source ?? '');
  const lines = text.split(/\r?\n/);
  const output = [];
  let changed = false;
  let codeFence = null;

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];

    if (codeFence) {
      output.push(line);
      if (isFenceClose(line, codeFence)) {
        codeFence = null;
      }
      continue;
    }

    const fence = readFenceOpen(line);
    if (fence) {
      codeFence = fence;
      output.push(line);
      continue;
    }

    if (!isDevOpsMermaidOpen(line)) {
      output.push(line);
      continue;
    }

    const block = [];
    let closeIndex = -1;
    for (let cursor = index + 1; cursor < lines.length; cursor += 1) {
      if (isDevOpsBlockClose(lines[cursor])) {
        closeIndex = cursor;
        break;
      }
      block.push(lines[cursor]);
    }

    if (closeIndex === -1) {
      output.push(line, ...block);
      index += block.length;
      continue;
    }

    output.push('```mermaid', ...block, '```');
    changed = true;
    index = closeIndex;
  }

  return changed ? output.join('\n') : text;
}

export function formatMarkdownForDevOpsBundle(source, fileName = '') {
  if (!/\.(md|markdown)$/i.test(fileName)) return String(source ?? '');
  return convertMermaidFencesToDevOps(normaliseDevOpsMermaidBlocks(source));
}

export function countMarkdownMermaidBlocks(source) {
  const normalised = normaliseDevOpsMermaidBlocks(source);
  const lines = normalised.split(/\r?\n/);
  let count = 0;
  let codeFence = null;

  for (const line of lines) {
    if (codeFence) {
      if (isFenceClose(line, codeFence)) {
        codeFence = null;
      }
      continue;
    }

    const fence = readFenceOpen(line);
    if (!fence) continue;
    if (isMermaidFenceLanguage(fence.language)) {
      count += 1;
    }
    codeFence = fence;
  }

  return count;
}

export function isPositionInsideDevOpsMermaidBlock(source, position) {
  const text = String(source ?? '').slice(0, Math.max(Number(position) || 0, 0));
  const lines = text.split(/\r?\n/);
  let codeFence = null;
  let inDevOpsMermaid = false;

  for (const line of lines) {
    if (codeFence) {
      if (isFenceClose(line, codeFence)) {
        codeFence = null;
      }
      continue;
    }

    if (inDevOpsMermaid) {
      if (isDevOpsBlockClose(line)) {
        inDevOpsMermaid = false;
      }
      continue;
    }

    const fence = readFenceOpen(line);
    if (fence) {
      codeFence = fence;
      continue;
    }

    if (isDevOpsMermaidOpen(line)) {
      inDevOpsMermaid = true;
    }
  }

  return inDevOpsMermaid;
}

function convertMermaidFencesToDevOps(source) {
  const text = String(source ?? '');
  const lines = text.split(/\r?\n/);
  const output = [];
  let changed = false;
  let codeFence = null;

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];

    if (codeFence) {
      output.push(line);
      if (isFenceClose(line, codeFence)) {
        codeFence = null;
      }
      continue;
    }

    const fence = readFenceOpen(line);
    if (!fence) {
      output.push(line);
      continue;
    }

    if (!isMermaidFenceLanguage(fence.language)) {
      codeFence = fence;
      output.push(line);
      continue;
    }

    const block = [];
    let closeIndex = -1;
    for (let cursor = index + 1; cursor < lines.length; cursor += 1) {
      if (isFenceClose(lines[cursor], fence)) {
        closeIndex = cursor;
        break;
      }
      block.push(lines[cursor]);
    }

    if (closeIndex === -1) {
      output.push(line);
      codeFence = fence;
      continue;
    }

    output.push('::: mermaid', ...normaliseMermaidSourceForDevOps(block.join('\n')).split('\n'), ':::');
    changed = true;
    index = closeIndex;
  }

  return changed ? output.join('\n') : text;
}

function normaliseMermaidSourceForDevOps(source) {
  let converted = false;
  return String(source ?? '').split('\n').map((line) => {
    if (converted || !line.trim()) return line;
    converted = true;
    return line.replace(/^(\s*)flowchart(\s+(?:TB|TD|BT|RL|LR)\s*;?\s*)?$/i, (_match, indent, direction = '') => `${indent}graph${direction}`);
  }).join('\n');
}

function isDevOpsMermaidOpen(line) {
  return /^[ \t]*:::[ \t]*mermaid[ \t]*$/i.test(line);
}

function isDevOpsBlockClose(line) {
  return /^[ \t]*:::[ \t]*$/.test(line);
}

function readFenceOpen(line) {
  const match = String(line ?? '').match(/^[ \t]{0,3}(`{3,}|~{3,})[ \t]*([^`~\s]*)?/);
  if (!match) return null;
  return {
    marker: match[1],
    char: match[1][0],
    length: match[1].length,
    language: String(match[2] || '').toLowerCase(),
  };
}

function isFenceClose(line, fence) {
  if (!fence) return false;
  const pattern = fence.char === '`'
    ? new RegExp(`^[ \\t]{0,3}\`{${fence.length},}[ \\t]*$`)
    : new RegExp(`^[ \\t]{0,3}~{${fence.length},}[ \\t]*$`);
  return pattern.test(line);
}

function isMermaidFenceLanguage(language) {
  return ['mermaid', 'mmd'].includes(String(language || '').toLowerCase());
}
