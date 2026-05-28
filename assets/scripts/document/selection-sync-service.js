const SELECTION_IGNORE_SELECTOR = [
  '.code-block-header',
  '.table-block-header',
  '.diagram-toolbar',
  '.diagram-error-actions',
  'button',
  'script',
  'style',
  'svg',
].join(',');

const MAX_SELECTION_LENGTH = 160;
const MIN_SELECTION_LENGTH = 2;

export function createSelectionSyncService({ state, dom }) {
  const {
    app,
    editor,
    preview,
    scrollSyncToggle,
    editorFindPanel,
  } = dom;

  let selectionTimer = 0;

  function installSelectionSyncHandlers() {
    editor.addEventListener('select', () => scheduleEditorSelectionSync());
    editor.addEventListener('mouseup', () => scheduleEditorSelectionSync());
    editor.addEventListener('keyup', () => scheduleEditorSelectionSync());
    editor.addEventListener('input', clearSelectionSync);

    scrollSyncToggle?.addEventListener('change', () => {
      if (!scrollSyncToggle.checked) {
        clearSelectionSync();
      }
    });
  }

  function scheduleEditorSelectionSync() {
    window.clearTimeout(selectionTimer);
    selectionTimer = window.setTimeout(syncEditorSelectionToPreview, 70);
  }

  function syncEditorSelectionToPreview() {
    if (!isSelectionSyncActive()) {
      clearSelectionSync();
      return;
    }

    const selected = editor.value.slice(editor.selectionStart, editor.selectionEnd);
    const query = normaliseSelectionQuery(selected);
    if (!isValidQuery(query) || !isEditorSelectionRenderable()) {
      clearSelectionSync();
      return;
    }

    clearSelectionSync();
    const match = findPreviewTextMatch(query, getEditorSelectionContext(query));
    if (!match) return;

    highlightPreviewRange(match.start, match.end);

    const hit = preview.querySelector('.selection-sync-hit');
    if (hit) {
      hit.scrollIntoView({ block: 'center', inline: 'nearest' });
    }
  }

  function clearSelectionSync() {
    window.clearTimeout(selectionTimer);
    preview.querySelectorAll('mark.selection-sync-hit').forEach((mark) => {
      mark.replaceWith(document.createTextNode(mark.textContent || ''));
    });
    preview.normalize();
  }

  function isSelectionSyncActive() {
    if (!state.scrollSyncEnabled) return false;
    if (state.editorLayout !== 'split') return false;
    if (editorFindPanel && !editorFindPanel.hidden) return false;
    if (app.classList.contains('preview-maximized')) return false;
    if (!editor.clientHeight || !preview.clientHeight) return false;
    return true;
  }

  function findPreviewTextMatch(query, context = {}) {
    const segments = collectPreviewTextSegments();
    const aggregate = segments.map((segment) => segment.text).join('');
    const indexed = normaliseWithMap(aggregate);
    const index = chooseOccurrence(findOccurrences(indexed.text, query), context, indexed.text.length);
    if (index === -1) return null;
    return {
      start: indexed.map[index],
      end: indexed.map[index + query.length - 1] + 1,
      segments,
    };
  }

  function highlightPreviewRange(start, end) {
    const segments = collectPreviewTextSegments();
    const overlaps = segments
      .map((segment) => ({
        segment,
        start: Math.max(start, segment.start),
        end: Math.min(end, segment.end),
      }))
      .filter((item) => item.start < item.end)
      .reverse();

    overlaps.forEach(({ segment, start: overlapStart, end: overlapEnd }) => {
      const localStart = overlapStart - segment.start;
      const localEnd = overlapEnd - segment.start;
      wrapTextNodeRange(segment.node, localStart, localEnd);
    });
  }

  function wrapTextNodeRange(node, start, end) {
    const text = node.textContent || '';
    const before = text.slice(0, start);
    const selected = text.slice(start, end);
    const after = text.slice(end);
    const fragment = document.createDocumentFragment();

    if (before) fragment.appendChild(document.createTextNode(before));
    if (selected) {
      const mark = document.createElement('mark');
      mark.className = 'selection-sync-hit';
      mark.textContent = selected;
      fragment.appendChild(mark);
    }
    if (after) fragment.appendChild(document.createTextNode(after));

    node.replaceWith(fragment);
  }

  // Returns segments of text nodes inside getContentRoot()
  function collectPreviewTextSegments() {
    const segments = [];
    let cursor = 0;
    const walker = document.createTreeWalker(getContentRoot(), NodeFilter.SHOW_TEXT, {
      acceptNode: (node) => {
        if (!node.textContent) return NodeFilter.FILTER_REJECT;
        const parent = node.parentElement;
        if (!parent || parent.closest(SELECTION_IGNORE_SELECTOR)) return NodeFilter.FILTER_REJECT;
        return NodeFilter.FILTER_ACCEPT;
      },
    });

    while (walker.nextNode()) {
      const node = walker.currentNode;
      const text = node.textContent || '';
      segments.push({ node, text, start: cursor, end: cursor + text.length });
      cursor += text.length;
    }

    return segments;
  }

  function getEditorSelectionContext(query) {
    const indexed = normaliseSourceWithMap(editor.value);
    const targetIndex = normalisedIndexForRawOffset(indexed.map, editor.selectionStart);
    const occurrences = findOccurrences(indexed.text, query);
    return {
      occurrenceIndex: findClosestOccurrenceIndex(occurrences, targetIndex),
      totalOccurrences: occurrences.length,
      targetRatio: indexed.text.length ? targetIndex / indexed.text.length : 0,
    };
  }

  function findOccurrences(text, query) {
    const occurrences = [];
    let index = text.indexOf(query);
    while (index !== -1) {
      occurrences.push(index);
      index = text.indexOf(query, index + Math.max(1, query.length));
    }
    return occurrences;
  }

  function chooseOccurrence(occurrences, context = {}, textLength = 0) {
    if (!occurrences.length) return -1;
    const target = Math.max(0, Math.min(1, Number(context.targetRatio) || 0)) * Math.max(1, textLength);
    const targetRatio = Math.max(0, Math.min(1, Number(context.targetRatio) || 0));
    const occurrenceIndex = Number.isInteger(context.occurrenceIndex) ? context.occurrenceIndex : null;
    const totalOccurrences = Number.isInteger(context.totalOccurrences) ? context.totalOccurrences : null;

    return occurrences.reduce((best, current, index) => {
      const bestIndex = occurrences.indexOf(best);
      const bestScore = scoreOccurrence(best, bestIndex, occurrences.length, target, targetRatio, occurrenceIndex, totalOccurrences, textLength);
      const currentScore = scoreOccurrence(current, index, occurrences.length, target, targetRatio, occurrenceIndex, totalOccurrences, textLength);
      return currentScore < bestScore ? current : best;
    }, occurrences[0]);
  }

  function scoreOccurrence(occurrence, index, numOccurrences, target, targetRatio, occurrenceIndex, totalOccurrences, textLength) {
    const ratioDistance = Math.abs((occurrence / Math.max(1, textLength)) - targetRatio);
    const positionDistance = Math.abs(occurrence - target) / Math.max(1, textLength);
    
    let occurrenceRatioDistance = 0;
    if (occurrenceIndex !== null && totalOccurrences !== null && totalOccurrences > 0 && numOccurrences > 0) {
      const contextRatio = totalOccurrences > 1 ? occurrenceIndex / (totalOccurrences - 1) : 0;
      const currentRatio = numOccurrences > 1 ? index / (numOccurrences - 1) : 0;
      occurrenceRatioDistance = Math.abs(currentRatio - contextRatio);
    }

    const occurrenceDistance = occurrenceIndex === null ? 0 : Math.abs(index - occurrenceIndex) * 0.035;
    const isPerfectOccurrenceMatch = occurrenceIndex === index && totalOccurrences === numOccurrences;
    const exactOccurrenceBonus = occurrenceIndex === index ? (isPerfectOccurrenceMatch ? 0.25 : 0.012) : 0;
    
    return ratioDistance + positionDistance + occurrenceRatioDistance * 0.4 + occurrenceDistance - exactOccurrenceBonus;
  }

  function findClosestOccurrenceIndex(occurrences, targetIndex) {
    if (!occurrences.length) return -1;
    let bestIndex = 0;
    let bestDistance = Math.abs(occurrences[0] - targetIndex);
    occurrences.forEach((occurrence, index) => {
      const distance = Math.abs(occurrence - targetIndex);
      if (distance < bestDistance) {
        bestDistance = distance;
        bestIndex = index;
      }
    });
    return bestIndex;
  }

  function normalisedIndexForRawOffset(map, rawOffset) {
    if (!map.length) return 0;
    const index = map.findIndex((value) => value >= rawOffset);
    return index === -1 ? map.length - 1 : index;
  }

  function isEditorSelectionRenderable() {
    const start = Math.min(editor.selectionStart, editor.selectionEnd);
    const end = Math.max(editor.selectionStart, editor.selectionEnd);
    if (start === end) return false;

    const value = editor.value;
    const lineStart = value.lastIndexOf('\n', Math.max(0, start - 1)) + 1;
    const lineEndIndex = value.indexOf('\n', start);
    const lineEnd = lineEndIndex === -1 ? value.length : lineEndIndex;
    const line = value.slice(lineStart, lineEnd);

    if (/^\s*```+/.test(line)) return false;
    if (isTableDelimiterLine(line)) return false;

    const selected = value.slice(start, end);
    if (!normaliseSelectionQuery(selected)) return false;

    return true;
  }

  function isTableDelimiterLine(line) {
    const trimmed = String(line || '').trim();
    if (!trimmed.includes('|') || !trimmed.includes('-')) return false;
    return /^\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?$/.test(trimmed);
  }

  function normaliseSelectionQuery(value) {
    return normaliseInlineMarkdown(value).replace(/\s+/g, ' ').trim().toLowerCase();
  }

  // Normalise inline markdown syntax
  function normaliseInlineMarkdown(value) {
    return String(value || '')
      .replace(/!\[([^\]]*)\]\([^)]+\)/g, '$1')
      .replace(/\[([^\]]+)\]\([^)]+\)/g, '$1')
      .replace(/`([^`]+)`/g, '$1')
      .replace(/[*_~#>\[\]()]/g, '');
  }

  function normaliseSourceWithMap(value) {
    const text = String(value || '');
    const output = [];
    const map = [];
    let previousWhitespace = false;

    for (let index = 0; index < text.length; index += 1) {
      const char = text[index];
      if (/[*_~#>`\[\]()]/.test(char)) continue;
      if (/\s/.test(char)) {
        if (previousWhitespace) continue;
        output.push(' ');
        map.push(index);
        previousWhitespace = true;
        continue;
      }

      output.push(char.toLowerCase());
      map.push(index);
      previousWhitespace = false;
    }

    return trimNormalisedMap(output, map);
  }

  function normaliseWithMap(value) {
    const text = String(value || '');
    const output = [];
    const map = [];
    let previousWhitespace = false;

    for (let index = 0; index < text.length; index += 1) {
      const char = text[index];
      if (/\s/.test(char)) {
        if (previousWhitespace) continue;
        output.push(' ');
        map.push(index);
        previousWhitespace = true;
        continue;
      }

      output.push(char.toLowerCase());
      map.push(index);
      previousWhitespace = false;
    }

    return trimNormalisedMap(output, map);
  }

  function trimNormalisedMap(output, map) {
    while (output[0] === ' ') {
      output.shift();
      map.shift();
    }
    while (output[output.length - 1] === ' ') {
      output.pop();
      map.pop();
    }
    return { text: output.join(''), map };
  }

  function isValidQuery(query) {
    return query.length >= MIN_SELECTION_LENGTH && query.length <= MAX_SELECTION_LENGTH;
  }

  function getContentRoot() {
    return preview.querySelector('.docs-site-content') || preview;
  }

  return {
    installSelectionSyncHandlers,
    clearSelectionSync,
  };
}
