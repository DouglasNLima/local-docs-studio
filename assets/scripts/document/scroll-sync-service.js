import { storageKeys } from '../state/config.js';

export function createScrollSyncService({ state, dom }) {
  const {
    app,
    editor,
    preview,
    scrollSyncToggle,
  } = dom;

  let suppressSource = '';
  let suppressTimer = 0;
  let renderSnapshot = null;

  function installScrollSyncHandlers() {
    if (scrollSyncToggle) {
      scrollSyncToggle.checked = state.scrollSyncEnabled;
      scrollSyncToggle.addEventListener('change', () => {
        state.scrollSyncEnabled = scrollSyncToggle.checked;
        localStorage.setItem(storageKeys.scrollSync, String(state.scrollSyncEnabled));
      });
    }

    editor.addEventListener('scroll', () => {
      rememberScrollPosition();
    }, { passive: true });

    preview.addEventListener('scroll', () => {
      rememberScrollPosition();
    }, { passive: true });
  }

  function beforePreviewRender() {
    rememberScrollPosition();
    renderSnapshot = getCurrentScrollSnapshot();
  }

  function afterPreviewRender() {
    if (!renderSnapshot || renderSnapshot.path !== state.activePath) return;

    setPreviewScroll(renderSnapshot.previewRatio * maxScrollable(preview), 'restore');

    renderSnapshot = null;
  }

  function rememberScrollPosition(path = state.activePath) {
    if (!path) return;
    state.scrollPositions.set(path, getCurrentScrollSnapshot(path));
  }

  function restoreScrollPosition(path = state.activePath) {
    if (!path) return;
    const saved = state.scrollPositions.get(path);

    if (!saved) {
      setEditorScroll(0, 'restore');
      setPreviewScroll(0, 'restore');
      return;
    }

    setEditorScroll(saved.editorRatio * maxScrollable(editor), 'restore');
    setPreviewScroll(saved.previewRatio * maxScrollable(preview), 'restore');
  }

  function resetActiveScrollPosition(path = state.activePath) {
    if (path) {
      state.scrollPositions.delete(path);
    }
    setEditorScroll(0, 'restore');
    setPreviewScroll(0, 'restore');
  }

  function clearScrollPositions() {
    state.scrollPositions.clear();
  }

  function setEditorScroll(top, source) {
    setScrollTop(editor, top, source);
  }

  function setPreviewScroll(top, source) {
    setScrollTop(preview, top, source);
  }

  function setScrollTop(element, top, source) {
    const target = clamp(top, 0, maxScrollable(element));
    if (Math.abs(element.scrollTop - target) < 2) return;
    suppressSource = source;
    window.clearTimeout(suppressTimer);
    element.scrollTop = target;
    suppressTimer = window.setTimeout(() => {
      suppressSource = '';
      rememberScrollPosition();
    }, 140);
  }

  function getCurrentScrollSnapshot(path = state.activePath) {
    return {
      path,
      editorTop: editor.scrollTop,
      previewTop: preview.scrollTop,
      editorRatio: scrollRatio(editor),
      previewRatio: scrollRatio(preview),
    };
  }

  function scrollRatio(element) {
    const max = maxScrollable(element);
    return max ? clamp(element.scrollTop / max, 0, 1) : 0;
  }

  function maxScrollable(element) {
    return Math.max(0, element.scrollHeight - element.clientHeight);
  }

  function clamp(value, min, max) {
    return Math.min(max, Math.max(min, Number.isFinite(value) ? value : min));
  }

  return {
    installScrollSyncHandlers,
    beforePreviewRender,
    afterPreviewRender,
    rememberScrollPosition,
    restoreScrollPosition,
    resetActiveScrollPosition,
    clearScrollPositions,
  };
}
