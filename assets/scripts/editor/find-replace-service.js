import { buildSearchRegex, findTextMatches, replaceAllText } from '../utils/search.js';

export function createFindReplaceService({ editor, dom, callbacks }) {
  const {
    findReplaceDialog,
    findReplaceTitle,
    findReplaceFindInput,
    findReplaceReplaceInput,
    findReplaceRegexToggle,
    findReplaceCaseToggle,
    findReplaceCount,
    findReplacePrevButton,
    findReplaceNextButton,
    findReplaceCurrentButton,
    findReplaceAllButton,
    findReplaceCloseButton,
  } = dom;
  const {
    closeOpenMenus,
    replaceEditorRange,
    setStatus,
  } = callbacks;

  const state = {
    matches: [],
    activeIndex: -1,
  };

  function installFindReplaceHandlers() {
    findReplaceDialog?.addEventListener('cancel', closeFindReplace);
    findReplaceCloseButton?.addEventListener('click', closeFindReplace);
    findReplaceFindInput?.addEventListener('input', refreshMatches);
    findReplaceRegexToggle?.addEventListener('change', refreshMatches);
    findReplaceCaseToggle?.addEventListener('change', refreshMatches);
    findReplaceFindInput?.addEventListener('keydown', handleFindKeydown);
    findReplaceReplaceInput?.addEventListener('keydown', handleFindKeydown);
    findReplacePrevButton?.addEventListener('click', () => goToMatch(-1));
    findReplaceNextButton?.addEventListener('click', () => goToMatch(1));
    findReplaceCurrentButton?.addEventListener('click', replaceCurrent);
    findReplaceAllButton?.addEventListener('click', replaceAll);
  }

  function openFindReplace({ replace = false } = {}) {
    if (!findReplaceDialog) return;
    closeOpenMenus?.();
    findReplaceDialog.classList.toggle('replace-open', replace);
    findReplaceTitle.textContent = replace ? 'Find and replace' : 'Find in editor';
    findReplaceReplaceInput.hidden = !replace;
    findReplaceCurrentButton.hidden = !replace;
    findReplaceAllButton.hidden = !replace;
    const selected = editor.value.slice(editor.selectionStart, editor.selectionEnd);
    if (selected && !selected.includes('\n')) {
      findReplaceFindInput.value = selected;
    }
    findReplaceDialog.showModal();
    findReplaceFindInput.focus();
    findReplaceFindInput.select();
    refreshMatches();
  }

  function closeFindReplace(event) {
    event?.preventDefault?.();
    findReplaceDialog?.close();
    state.matches = [];
    state.activeIndex = -1;
    editor.focus();
  }

  function refreshMatches() {
    const query = findReplaceFindInput.value;
    const { error } = buildSearchRegex(query, getOptions());
    if (error) {
      state.matches = [];
      state.activeIndex = -1;
      updateCount(`Invalid regex: ${error.message}`);
      return;
    }
    state.matches = findTextMatches(editor.value, query, getOptions());
    if (!state.matches.length) {
      state.activeIndex = -1;
      updateCount();
      return;
    }

    const selectionStart = editor.selectionStart;
    const nextIndex = state.matches.findIndex((match) => match.index + match.length >= selectionStart);
    state.activeIndex = nextIndex === -1 ? 0 : nextIndex;
    selectActiveMatch();
  }

  function goToMatch(step) {
    if (!state.matches.length) return;
    state.activeIndex = (state.activeIndex + step + state.matches.length) % state.matches.length;
    selectActiveMatch();
  }

  function selectActiveMatch() {
    const match = state.matches[state.activeIndex];
    if (!match) {
      updateCount();
      return;
    }
    editor.focus();
    editor.setSelectionRange(match.index, match.index + match.length);
    updateCount();
  }

  function replaceCurrent() {
    const match = state.matches[state.activeIndex];
    if (!match) return;
    const replacement = buildReplacement(match);
    replaceEditorRange(match.index, match.index + match.length, replacement, match.index, match.index + replacement.length);
    refreshMatches();
    setStatus('Replaced current match.', 'ok');
  }

  function replaceAll() {
    const query = findReplaceFindInput.value;
    const replacement = findReplaceReplaceInput.value;
    const result = replaceAllText(editor.value, query, replacement, getOptions());
    if (result.error) {
      updateCount(`Invalid regex: ${result.error.message}`);
      return;
    }
    replaceEditorRange(0, editor.value.length, result.text, 0, 0);
    refreshMatches();
    setStatus(`Replaced ${result.count} match${result.count === 1 ? '' : 'es'}.`, 'ok');
  }

  function handleFindKeydown(event) {
    if (event.key === 'Escape') {
      closeFindReplace(event);
      return;
    }
    if (event.key === 'Enter') {
      event.preventDefault();
      if (event.ctrlKey || event.metaKey) {
        replaceCurrent();
      } else {
        goToMatch(event.shiftKey ? -1 : 1);
      }
    }
  }

  function buildReplacement(match) {
    const replacement = findReplaceReplaceInput.value;
    if (!findReplaceRegexToggle.checked) return replacement;
    return replacement.replace(/\$(\d+)/g, (_, index) => match.groups?.[Number(index)] ?? '');
  }

  function getOptions() {
    return {
      regex: findReplaceRegexToggle.checked,
      caseSensitive: findReplaceCaseToggle.checked,
    };
  }

  function updateCount(message = '') {
    if (message) {
      findReplaceCount.textContent = message;
      return;
    }
    const total = state.matches.length;
    findReplaceCount.textContent = total && state.activeIndex >= 0 ? `${state.activeIndex + 1}/${total}` : `0/${total}`;
    findReplacePrevButton.disabled = total < 2;
    findReplaceNextButton.disabled = total < 2;
    findReplaceCurrentButton.disabled = total < 1;
    findReplaceAllButton.disabled = total < 1;
  }

  return {
    installFindReplaceHandlers,
    openFindReplace,
    closeFindReplace,
    refreshMatches,
  };
}
