import {
  classifyDocumentHref,
  getTrustedPreviewInternalLink,
  resolveWorkspaceDocumentLink,
} from '../utils/document-links.js';
import { findDocumentSection } from './document-sections.js';
import { resolveWikilinkTarget } from '../utils/wikilinks.js';

const historyLimit = 100;

export function createDocumentNavigationService({ state, dom, callbacks }) {
  const {
    editor,
    preview,
    backButton,
    forwardButton,
    additionalBackButtons = [],
    additionalForwardButtons = [],
  } = dom;
  const backButtons = [backButton, ...additionalBackButtons].filter(Boolean);
  const forwardButtons = [forwardButton, ...additionalForwardButtons].filter(Boolean);
  const {
    selectFile,
    setStatus,
    rememberScrollPosition,
  } = callbacks;

  let entries = [];
  let index = -1;
  let replaying = false;
  let navigationRequestId = 0;
  let pendingNavigationRequestId = 0;

  function installDocumentNavigationHandlers() {
    backButtons.forEach((button) => button.addEventListener('click', () => {
      void replayHistory(-1);
    }));
    forwardButtons.forEach((button) => button.addEventListener('click', () => {
      void replayHistory(1);
    }));
    preview?.addEventListener('click', handlePreviewAnchorEvent);
    preview?.addEventListener('auxclick', handlePreviewAnchorEvent);
    updateHistoryControls();
  }

  function resetWorkspaceHistory() {
    navigationRequestId += 1;
    pendingNavigationRequestId = 0;
    entries = [];
    index = -1;
    replaying = false;
    updateHistoryControls();
  }

  function beforeFileSelection({ options = {} } = {}) {
    if (!options.navigationRequestId) options.navigationRequestId = beginNavigation();
    if (['manual', 'skip'].includes(options.historyMode)) return null;
    return captureContext();
  }

  function afterFileSelection({ options = {}, previousContext = null } = {}) {
    if (['manual', 'skip'].includes(options.historyMode)) {
      updateHistoryControls();
      return;
    }
    commitSuccessfulNavigation(previousContext, captureContext());
  }

  async function handlePreviewAnchorEvent(event) {
    if (event.type === 'auxclick' && event.button !== 1) return;
    if (event.type === 'click' && event.button !== 0) return;
    const anchor = event.target.closest('a');
    if (!anchor || !preview.contains(anchor)) return;
    if (anchor.hasAttribute('download')) return;

    const trustedInternalLink = getTrustedPreviewInternalLink(anchor);
    if (trustedInternalLink) {
      const shouldOwnModifiedNavigation = event.type === 'auxclick' || event.ctrlKey || event.metaKey || event.shiftKey;
      if (!shouldOwnModifiedNavigation) return;
      event.preventDefault();
      event.stopImmediatePropagation();
      const target = trustedInternalLink.kind === 'document'
        ? state.files.find((record) => record.path === trustedInternalLink.target)
        : resolveWikilinkTarget(trustedInternalLink.target, state.files, state.activePath);
      if (!target) {
        setStatus('No exact loaded document matches that internal link.', 'warning');
        return;
      }
      const requestId = beginNavigation({ pending: true });
      try {
        const selected = await selectFile(target.path, {
          navigationKind: 'modified-internal-link',
          navigationRequestId: requestId,
        });
        if (selected?.ok && isCurrentNavigation(requestId, target.path)) setStatus(`Opened ${target.path}.`, 'ok');
      } finally {
        finishNavigation(requestId);
      }
      return;
    }

    const literal = anchor.getAttribute('href') || '';
    const classification = classifyDocumentHref(literal);
    if (classification.kind === 'external') return;

    event.preventDefault();
    event.stopImmediatePropagation();
    await navigateLiteral(literal);
  }

  async function navigateLiteral(literal) {
    const requestId = beginNavigation({ pending: true });
    try {
      const resolution = resolveWorkspaceDocumentLink({
        href: literal,
        fromPath: state.activePath,
        files: state.files,
        workspaceKind: state.workspaceKind,
        workspaceRoot: state.workspaceRootPath,
      });

      if (resolution.kind !== 'resolved') {
        reportResolutionFailure(resolution);
        return false;
      }

      const previousContext = captureContext();
      if (!resolution.sameDocument) {
        const selected = await selectFile(resolution.targetPath, {
          historyMode: 'manual',
          navigationKind: 'document-link',
          navigationRequestId: requestId,
          scrollMode: 'start',
        });
        if (!selected?.ok) return false;
      }
      if (!isCurrentNavigation(requestId, resolution.targetPath)) return false;

      let sectionFound = true;
      if (resolution.fragment) {
        sectionFound = await jumpToSection(resolution.fragment, {
          guard: () => isCurrentNavigation(requestId, resolution.targetPath),
        });
        if (sectionFound === null) return false;
      } else {
        await waitForStableLayout();
        if (!isCurrentNavigation(requestId, resolution.targetPath)) return false;
        setPreviewScroll(0);
        if (!resolution.sameDocument) editor.scrollTop = 0;
        await waitForStableLayout();
        if (!isCurrentNavigation(requestId, resolution.targetPath)) return false;
        setPreviewScroll(0);
      }

      rememberScrollPosition?.(state.activePath);
      const destination = captureContext({
        sectionId: sectionFound ? resolution.fragment : '',
        requestedFragment: resolution.fragment,
      });
      commitSuccessfulNavigation(previousContext, destination);

      if (!sectionFound) {
        setPreviewScroll(0);
        setStatus(`Opened ${resolution.targetPath}, but section #${resolution.fragment} was not found. Showing the start of the document.`, 'warning');
        return true;
      }

      setStatus(resolution.fragment
        ? `Opened ${resolution.targetPath} at #${resolution.fragment}.`
        : `Opened ${resolution.targetPath}.`, 'ok');
      return true;
    } finally {
      finishNavigation(requestId);
    }
  }

  async function navigateToSection(fragment, { source = 'section' } = {}) {
    const id = String(fragment || '');
    if (!state.activePath || !id) return false;
    const requestId = beginNavigation({ pending: true });
    const path = state.activePath;
    const previousContext = captureContext();
    try {
      const found = await jumpToSection(id, {
        guard: () => isCurrentNavigation(requestId, path),
      });
      if (found === null) return false;
      if (!found) {
        setStatus(`Section #${id} was not found in ${path}.`, 'warning');
        return false;
      }
      rememberScrollPosition?.(state.activePath);
      commitSuccessfulNavigation(previousContext, captureContext({ sectionId: id }));
      if (source !== 'outline') setStatus(`Opened section #${id}.`, 'ok');
      return true;
    } finally {
      finishNavigation(requestId);
    }
  }

  async function replayHistory(direction) {
    if (replaying) return false;
    const requestId = beginNavigation();
    const targetIndex = index + direction;
    if (targetIndex < 0 || targetIndex >= entries.length) return false;
    const targetContext = entries[targetIndex];
    const targetRecord = state.files.find((record) => record.path === targetContext.path);
    if (!targetRecord) {
      setStatus(`Cannot restore ${targetContext.path}; that exact document is no longer loaded.`, 'warning');
      updateHistoryControls();
      return false;
    }

    if (index >= 0 && state.activePath) entries[index] = captureContext();
    replaying = true;
    updateHistoryControls();
    try {
      if (state.activePath !== targetContext.path) {
        const selected = await selectFile(targetContext.path, {
          historyMode: 'skip',
          navigationKind: direction < 0 ? 'history-back' : 'history-forward',
          navigationRequestId: requestId,
          scrollMode: 'start',
        });
        if (!selected?.ok) return false;
      }
      if (!isCurrentNavigation(requestId, targetContext.path)) return false;
      const restored = await restoreContext(targetContext, {
        guard: () => isCurrentNavigation(requestId, targetContext.path),
      });
      if (!restored) return false;
      index = targetIndex;
      setStatus(`${direction < 0 ? 'Back' : 'Forward'} to ${targetContext.path}${targetContext.sectionId ? ` at #${targetContext.sectionId}` : ''}.`, 'ok');
      return true;
    } finally {
      replaying = false;
      updateHistoryControls();
    }
  }

  function commitSuccessfulNavigation(previousContext, destinationContext) {
    if (!destinationContext?.path) return;

    if (previousContext?.path) {
      if (index < 0) {
        entries = [previousContext];
        index = 0;
      } else if (entries[index]?.path === previousContext.path) {
        entries[index] = previousContext;
      } else {
        if (index < entries.length - 1) entries = entries.slice(0, index + 1);
        entries.push(previousContext);
        index = entries.length - 1;
      }
    }

    if (index < entries.length - 1) entries = entries.slice(0, index + 1);
    if (index >= 0 && contextsEquivalent(entries[index], destinationContext)) {
      entries[index] = destinationContext;
    } else {
      entries.push(destinationContext);
      if (entries.length > historyLimit) entries = entries.slice(-historyLimit);
      index = entries.length - 1;
    }
    updateHistoryControls();
  }

  function captureContext(overrides = {}) {
    if (!state.activePath) return null;
    return {
      path: state.activePath,
      sectionId: overrides.sectionId ?? getCurrentReadingSectionId(),
      requestedFragment: overrides.requestedFragment || '',
      previewTop: Math.max(0, Math.round(preview.scrollTop)),
      editorTop: Math.max(0, Math.round(editor.scrollTop)),
      selectionStart: editor.selectionStart,
      selectionEnd: editor.selectionEnd,
    };
  }

  async function restoreContext(context, { guard = null } = {}) {
    if (!context || context.path !== state.activePath) return;
    await waitForStableLayout();
    if (guard && !guard()) return false;
    if (context.sectionId) {
      const sectionResult = await jumpToSection(context.sectionId, { settle: false, guard });
      if (sectionResult === null) return false;
    }
    if (guard && !guard()) return false;
    setPreviewScroll(context.previewTop);
    editor.scrollTop = Math.max(0, Number(context.editorTop) || 0);
    const length = editor.value.length;
    const start = clamp(Number(context.selectionStart) || 0, 0, length);
    const end = clamp(Number(context.selectionEnd) || start, start, length);
    editor.setSelectionRange(start, end);
    await waitForStableLayout();
    if (guard && !guard()) return false;
    setPreviewScroll(context.previewTop);
    editor.scrollTop = Math.max(0, Number(context.editorTop) || 0);
    rememberScrollPosition?.(state.activePath);
    return true;
  }

  async function jumpToSection(fragment, { settle = true, guard = null } = {}) {
    if (settle) await waitForStableLayout();
    if (guard && !guard()) return null;
    const root = getContentRoot();
    const target = findDocumentSection(root, fragment);
    if (!target) return false;
    const previewRect = preview.getBoundingClientRect();
    const targetRect = target.getBoundingClientRect();
    setPreviewScroll(preview.scrollTop + targetRect.top - previewRect.top - 12);
    if (settle) {
      await waitForStableLayout();
      if (guard && !guard()) return null;
      const nextPreviewRect = preview.getBoundingClientRect();
      const nextTargetRect = target.getBoundingClientRect();
      setPreviewScroll(preview.scrollTop + nextTargetRect.top - nextPreviewRect.top - 12);
    }
    return true;
  }

  function beginNavigation({ pending = false } = {}) {
    navigationRequestId += 1;
    pendingNavigationRequestId = pending ? navigationRequestId : 0;
    updateHistoryControls();
    return navigationRequestId;
  }

  function finishNavigation(requestId) {
    if (requestId !== navigationRequestId || pendingNavigationRequestId !== requestId) return;
    pendingNavigationRequestId = 0;
    updateHistoryControls();
  }

  function isCurrentNavigation(requestId, expectedPath = '') {
    return Number(requestId) === navigationRequestId
      && (!expectedPath || state.activePath === expectedPath);
  }

  function getCurrentReadingSectionId() {
    const root = getContentRoot();
    const candidates = [...root.querySelectorAll('h1[id], h2[id], h3[id], h4[id], h5[id], h6[id], a[id], [data-doc-anchor][id]')];
    if (!candidates.length) return '';
    const threshold = preview.getBoundingClientRect().top + 24;
    let active = candidates[0];
    candidates.forEach((candidate) => {
      if (candidate.getBoundingClientRect().top <= threshold) active = candidate;
    });
    return active?.id || '';
  }

  function getContentRoot() {
    return preview.querySelector('.docs-site-content') || preview;
  }

  function setPreviewScroll(value) {
    const maximum = Math.max(0, preview.scrollHeight - preview.clientHeight);
    preview.scrollTop = clamp(Number(value) || 0, 0, maximum);
  }

  function updateHistoryControls() {
    const previous = index > 0 ? entries[index - 1] : null;
    backButtons.forEach((button) => {
      button.disabled = replaying || Boolean(pendingNavigationRequestId) || index <= 0;
      button.title = previous ? `Back to ${previous.path}` : 'Back';
      button.setAttribute('aria-label', previous ? `Back to ${previous.path}` : 'Back');
    });
    const next = index >= 0 && index < entries.length - 1 ? entries[index + 1] : null;
    forwardButtons.forEach((button) => {
      button.disabled = replaying || Boolean(pendingNavigationRequestId) || index < 0 || index >= entries.length - 1;
      button.title = next ? `Forward to ${next.path}` : 'Forward';
      button.setAttribute('aria-label', next ? `Forward to ${next.path}` : 'Forward');
    });
  }

  function reportResolutionFailure(result) {
    const source = state.activePath || 'the current document';
    if (result.reason === 'document-not-found') {
      const suggestion = result.suggestionPath ? ` Capitalisation must match exactly; did you mean ${result.suggestionPath}?` : '';
      setStatus(`Could not open ${result.targetPath || 'that document'} from ${source}; the exact file is not loaded.${suggestion}`, 'warning');
      return;
    }
    if (result.reason === 'missing-relative-context') {
      setStatus('This document has no shared relative workspace context. Open a folder or import the complete ZIP pack first.', 'warning');
      return;
    }
    if (result.reason === 'different-origin') {
      setStatus(`The exact target from ${source} belongs to a different loaded origin. Open the complete folder or ZIP pack to establish a safe relative path.`, 'warning');
      return;
    }
    if (['outside-workspace-root', 'source-outside-root', 'absolute-path'].includes(result.reason)) {
      setStatus(`The link from ${source} is outside the authorised workspace root and was not opened.`, 'warning');
      return;
    }
    if (result.kind === 'unsupported') {
      setStatus('That link target is not a supported workspace document and was not opened.', 'warning');
      return;
    }
    setStatus(`The link from ${source} is invalid or ambiguously encoded and was not opened.`, 'warning');
  }

  function contextsEquivalent(left, right) {
    return Boolean(left && right
      && left.path === right.path
      && left.sectionId === right.sectionId
      && left.previewTop === right.previewTop
      && left.editorTop === right.editorTop
      && left.selectionStart === right.selectionStart
      && left.selectionEnd === right.selectionEnd);
  }

  function waitForStableLayout() {
    return new Promise((resolve) => {
      requestAnimationFrame(() => requestAnimationFrame(resolve));
    });
  }

  function clamp(value, minimum, maximum) {
    return Math.min(maximum, Math.max(minimum, Number.isFinite(value) ? value : minimum));
  }

  return {
    installDocumentNavigationHandlers,
    resetWorkspaceHistory,
    beforeFileSelection,
    afterFileSelection,
    navigateLiteral,
    navigateToSection,
    updateHistoryControls,
  };
}
