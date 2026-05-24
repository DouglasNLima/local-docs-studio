import { buildSearchRegex, findTextMatches, fuzzyScore, getLineInfoAtIndex, summariseLineContext } from '../utils/search.js';

const MAX_CONTENT_RESULTS = 80;

export function createWorkspaceSearchService({ state, dom, callbacks }) {
  const {
    workspaceSearchDialog,
    workspaceSearchInput,
    workspaceSearchModeFiles,
    workspaceSearchModeContent,
    workspaceSearchRegexToggle,
    workspaceSearchCaseToggle,
    workspaceSearchResults,
    workspaceSearchCount,
    workspaceSearchCloseButton,
  } = dom;
  const {
    closeOpenMenus,
    selectFile,
    readRecordText,
    focusEditorAtLine,
    setStatus,
  } = callbacks;

  let mode = 'files';
  let searchRunId = 0;

  function installWorkspaceSearchHandlers() {
    workspaceSearchDialog?.addEventListener('cancel', closeWorkspaceSearch);
    workspaceSearchCloseButton?.addEventListener('click', closeWorkspaceSearch);
    workspaceSearchModeFiles?.addEventListener('click', () => setMode('files'));
    workspaceSearchModeContent?.addEventListener('click', () => setMode('content'));
    workspaceSearchInput?.addEventListener('input', runSearch);
    workspaceSearchRegexToggle?.addEventListener('change', runSearch);
    workspaceSearchCaseToggle?.addEventListener('change', runSearch);
    workspaceSearchResults?.addEventListener('click', handleResultClick);
    workspaceSearchInput?.addEventListener('keydown', handleSearchKeydown);
  }

  function openWorkspaceSearch(nextMode = 'files') {
    if (!workspaceSearchDialog) return;
    closeOpenMenus?.();
    setMode(nextMode, { run: false });
    workspaceSearchInput.value = nextMode === 'files' ? '' : (workspaceSearchInput.value || '');
    workspaceSearchDialog.showModal();
    workspaceSearchInput.focus();
    workspaceSearchInput.select();
    runSearch();
  }

  function closeWorkspaceSearch(event) {
    event?.preventDefault?.();
    workspaceSearchDialog?.close();
  }

  function setMode(nextMode, { run = true } = {}) {
    mode = nextMode === 'content' ? 'content' : 'files';
    workspaceSearchModeFiles?.classList.toggle('active', mode === 'files');
    workspaceSearchModeContent?.classList.toggle('active', mode === 'content');
    workspaceSearchModeFiles?.setAttribute('aria-pressed', String(mode === 'files'));
    workspaceSearchModeContent?.setAttribute('aria-pressed', String(mode === 'content'));
    workspaceSearchInput.placeholder = mode === 'files' ? 'Jump to file' : 'Search text in loaded files';
    workspaceSearchRegexToggle.disabled = mode === 'files';
    workspaceSearchCaseToggle.disabled = mode === 'files';
    if (run) runSearch();
  }

  async function runSearch() {
    const runId = ++searchRunId;
    const query = workspaceSearchInput.value.trim();
    workspaceSearchResults.innerHTML = '';
    workspaceSearchCount.textContent = '';

    if (mode === 'files') {
      renderFileResults(query);
      return;
    }

    if (!query) {
      workspaceSearchResults.appendChild(createEmptyState('Type to search file contents.'));
      return;
    }

    const { error } = buildSearchRegex(query, {
      regex: workspaceSearchRegexToggle.checked,
      caseSensitive: workspaceSearchCaseToggle.checked,
    });
    if (error) {
      workspaceSearchResults.appendChild(createEmptyState(`Invalid regex: ${error.message}`));
      workspaceSearchCount.textContent = '0 results';
      return;
    }

    workspaceSearchResults.appendChild(createEmptyState('Searching...'));
    const results = [];
    for (const record of state.files) {
      if (runId !== searchRunId) return;
      const text = await readRecordText(record);
      const matches = findTextMatches(text, query, {
        regex: workspaceSearchRegexToggle.checked,
        caseSensitive: workspaceSearchCaseToggle.checked,
      });
      matches.slice(0, MAX_CONTENT_RESULTS - results.length).forEach((match) => {
        const line = getLineInfoAtIndex(text, match.index);
        results.push({
          type: 'content',
          path: record.path,
          name: record.name,
          line: line.line,
          column: line.column,
          length: Math.max(match.length, 1),
          context: summariseLineContext(line.text, line.column),
        });
      });
      if (results.length >= MAX_CONTENT_RESULTS) break;
    }
    if (runId !== searchRunId) return;
    renderResults(results);
  }

  function renderFileResults(query) {
    const results = state.files
      .map((record) => ({ record, score: fuzzyScore(query, record.path) }))
      .filter(({ score }) => score > 0)
      .sort((left, right) => right.score - left.score || left.record.path.localeCompare(right.record.path))
      .slice(0, 40)
      .map(({ record }) => ({ type: 'file', path: record.path, name: record.name }));
    renderResults(results);
  }

  function renderResults(results) {
    workspaceSearchResults.innerHTML = '';
    workspaceSearchCount.textContent = `${results.length} result${results.length === 1 ? '' : 's'}`;
    if (!results.length) {
      workspaceSearchResults.appendChild(createEmptyState('No matches.'));
      return;
    }

    results.forEach((result, index) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'workspace-search-result';
      button.dataset.searchIndex = String(index);
      button.dataset.searchPath = result.path;
      button.dataset.searchLine = result.line ? String(result.line) : '';
      button.dataset.searchColumn = result.column ? String(result.column) : '';
      button.dataset.searchLength = result.length ? String(result.length) : '';
      button.innerHTML = result.type === 'file'
        ? `<strong>${escapeHtml(result.name)}</strong><span>${escapeHtml(result.path)}</span>`
        : `<strong>${escapeHtml(result.path)}:${result.line}</strong><span>${escapeHtml(result.context)}</span>`;
      workspaceSearchResults.appendChild(button);
    });

    workspaceSearchResults.querySelector('button')?.classList.add('active');
  }

  async function handleResultClick(event) {
    const button = event.target.closest('[data-search-path]');
    if (!button) return;
    await activateResult(button);
  }

  async function activateResult(button) {
    const path = button.dataset.searchPath;
    closeWorkspaceSearch();
    await selectFile(path);
    const line = Number(button.dataset.searchLine || '0');
    if (line > 0) {
      focusEditorAtLine(line, Number(button.dataset.searchColumn || '1'), Number(button.dataset.searchLength || '1'));
    }
  }

  function handleSearchKeydown(event) {
    if (event.key === 'Escape') {
      closeWorkspaceSearch(event);
      return;
    }
    const buttons = [...workspaceSearchResults.querySelectorAll('[data-search-path]')];
    if (!buttons.length) return;
    const currentIndex = Math.max(0, buttons.findIndex((button) => button.classList.contains('active')));
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const nextIndex = (currentIndex + (event.key === 'ArrowDown' ? 1 : -1) + buttons.length) % buttons.length;
      buttons.forEach((button, index) => button.classList.toggle('active', index === nextIndex));
      buttons[nextIndex].scrollIntoView({ block: 'nearest' });
    }
    if (event.key === 'Enter') {
      event.preventDefault();
      activateResult(buttons[currentIndex]);
    }
  }

  function createEmptyState(message) {
    const item = document.createElement('div');
    item.className = 'workspace-search-empty';
    item.textContent = message;
    return item;
  }

  function escapeHtml(value) {
    return String(value)
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#039;');
  }

  return {
    installWorkspaceSearchHandlers,
    openWorkspaceSearch,
    closeWorkspaceSearch,
  };
}
