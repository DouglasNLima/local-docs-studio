import { storageKeys } from '../state/config.js';

export function createTypewriterService({ state, dom, callbacks }) {
  const {
    app,
    editor,
    typewriterToggleButton,
  } = dom;
  const {
    closeOpenMenus,
    updateEditorChrome,
  } = callbacks;

  function installTypewriterHandlers() {
    typewriterToggleButton?.addEventListener('click', () => toggleTypewriterMode());
    editor.addEventListener('input', keepCaretCentred);
    editor.addEventListener('keyup', keepCaretCentred);
    editor.addEventListener('click', keepCaretCentred);
  }

  function restoreTypewriterPreference() {
    const on = localStorage.getItem(storageKeys.typewriterMode) === 'true';
    if (on) toggleTypewriterMode(true, { restore: true });
  }

  function toggleTypewriterMode(force, { restore = false } = {}) {
    const next = typeof force === 'boolean' ? force : !state.typewriterMode;
    if (next === state.typewriterMode && !restore) return;

    state.typewriterMode = next;
    app.classList.toggle('typewriter-mode', next);
    typewriterToggleButton?.setAttribute('aria-pressed', String(next));
    localStorage.setItem(storageKeys.typewriterMode, String(next));
    closeOpenMenus?.();
    updateEditorChrome?.();
    window.setTimeout(keepCaretCentred, 0);
  }

  function keepCaretCentred() {
    if (!state.typewriterMode || document.activeElement !== editor) return;
    const value = editor.value.slice(0, editor.selectionStart);
    const lineIndex = value.split('\n').length - 1;
    const lineHeight = parseFloat(getComputedStyle(editor).lineHeight) || 22;
    const target = Math.max(0, lineIndex * lineHeight - editor.clientHeight * 0.5);
    if (Math.abs(editor.scrollTop - target) > lineHeight * 2) {
      editor.scrollTop = target;
    }
  }

  return {
    installTypewriterHandlers,
    restoreTypewriterPreference,
    toggleTypewriterMode,
    keepCaretCentred,
  };
}
