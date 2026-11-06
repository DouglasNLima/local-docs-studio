export function readingTime(minutes) {
  return minutes < 60 ? `${minutes} min read` : `${Math.floor(minutes / 60)} hr ${minutes % 60} min read`;
}

export function createDocumentStatisticsService({ state, editor, button, reviewButton, stateLabel, onMetrics }) {
  let timer = 0;
  let worker = null;
  let generation = 0;
  let path;
  let source;
  let metrics = null;
  const decoder = document.createElement('textarea');
  const number = new Intl.NumberFormat('en-GB');
  button.addEventListener('click', () => { if (!state.documentReviewOpen) reviewButton.click(); });
  editor.addEventListener('input', schedule);
  editor.addEventListener('lens-buffer-changed', schedule);
  document.addEventListener('lens-document-state', schedule);
  window.addEventListener('pagehide', () => { clearTimeout(timer); worker?.terminate(); worker = null; });

  function updateState() {
    const record = state.files.find(item => item.path === state.activePath);
    stateLabel.textContent = record?.readOnly ? 'Read-only'
      : record?.persistenceState === 'saving' ? 'Saving…'
        : state.externalChangePaths.has(state.activePath) ? 'External change'
          : record?.persistenceState === 'error' ? 'Save failed'
        : state.dirtyPaths.has(state.activePath) ? 'Unsaved changes'
          : record?.persistenceState === 'saved' ? 'Saved'
            : record?.handle || record?.nativeHandleId ? 'Unchanged' : 'Local document';
  }
  function schedule() {
    updateState();
    if (path === state.activePath && source === editor.value) return;
    const changedDocument = path !== state.activePath;
    path = state.activePath;
    source = editor.value;
    generation++;
    metrics = null;
    clearTimeout(timer);
    // Termination cancels obsolete parsing and prevents an unbounded worker message queue.
    worker?.terminate(); worker = null;
    if (changedDocument) {
      button.textContent = 'Counting…';
      button.title = 'Counting current document statistics. Open Document Review.';
      button.setAttribute('aria-label', button.title);
    }
    button.setAttribute('aria-busy', 'true');
    timer = window.setTimeout(measure, 350);
  }
  function measure() {
    const request = generation;
    try {
      worker = new Worker(new URL('./document-statistics-worker.js', import.meta.url), { type: 'module' });
      worker.onmessage = ({ data }) => {
        if (request !== generation || data.generation !== generation) return;
        if (data.error) { failed(data.error); return; }
        // Decode entities in an inert textarea; escaped '<' can never become an element.
        decoder.innerHTML = data.text.replace(/</g, '&lt;');
        const readable = decoder.value.replace(/\s+/gu, ' ').trim();
        let words = 0;
        for (const match of readable.matchAll(/[\p{L}\p{N}]+(?:['-][\p{L}\p{N}]+)*/gu)) words++;
        let characters = 0;
        for (const character of readable) characters++;
        metrics = { words, characters, readingMinutes: Math.ceil(words / 220) };
        const summary = `${readingTime(metrics.readingMinutes)} · ${number.format(words)} words · ${number.format(characters)} chars`;
        button.replaceChildren();
        const primary = document.createElement('span'); primary.textContent = readingTime(metrics.readingMinutes);
        const secondary = document.createElement('span'); secondary.className = 'document-statistics-secondary';
        secondary.textContent = ` · ${number.format(words)} words · ${number.format(characters)} chars`;
        button.append(primary, secondary);
        button.title = `${summary}. Open Document Review.`;
        button.setAttribute('aria-label', `${summary}. Open Document Review`);
        button.removeAttribute('aria-busy');
        worker?.terminate(); worker = null;
        onMetrics?.(metrics);
      };
      worker.onerror = () => { if (request === generation) failed('Document statistics could not be calculated.'); };
      worker.postMessage({ generation, source, standalone: /\.(mmd|mermaid)$/i.test(path || '') });
    } catch { failed('Document statistics require browser worker support.'); }
  }
  function failed(message) {
    worker?.terminate(); worker = null;
    button.textContent = 'Statistics unavailable'; button.title = message;
    button.setAttribute('aria-label', `${message} Open Document Review`); button.removeAttribute('aria-busy');
  }
  return { schedule, getMetrics: () => metrics };
}
