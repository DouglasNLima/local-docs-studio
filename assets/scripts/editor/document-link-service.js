import {
  generateDocumentLink,
  recordHasDocumentContext,
  recordsShareDocumentContext,
} from '../utils/document-links.js';
import { isSupportedFile } from '../utils/files.js';

export function createDocumentLinkService({ state, editor, dom, callbacks }) {
  const {
    dialog,
    form,
    searchInput,
    results,
    linkTextInput,
    sectionSelect,
    generatedMarkdown,
    relativePath,
    validation,
    contextNote,
    applyButton,
    cancelButton,
  } = dom;
  const {
    replaceEditorRange,
    setStatus,
    readRecordText,
    inspectMarkdownDocument,
    isActiveReadOnly,
    getDocTitleFromPath,
  } = callbacks;

  const metadataCache = new Map();
  let candidates = [];
  let selectedPath = '';
  let origin = null;
  let returnFocus = null;
  let linkTextTouched = false;
  let dialogRunId = 0;
  let applying = false;

  function installDocumentLinkHandlers() {
    form?.addEventListener('submit', applyDocumentLink);
    searchInput?.addEventListener('input', renderResults);
    searchInput?.addEventListener('keydown', handleSearchKeydown);
    results?.addEventListener('click', handleResultClick);
    linkTextInput?.addEventListener('input', () => {
      linkTextTouched = true;
      updateGeneratedMarkdown();
    });
    sectionSelect?.addEventListener('change', updateGeneratedMarkdown);
    cancelButton?.addEventListener('click', closeDocumentLinkDialog);
    dialog?.querySelectorAll('[data-document-link-cancel]').forEach((button) => {
      button.addEventListener('click', closeDocumentLinkDialog);
    });
    dialog?.addEventListener('cancel', (event) => {
      event.preventDefault();
      closeDocumentLinkDialog();
    });
    dialog?.addEventListener('click', (event) => {
      if (event.target === dialog) closeDocumentLinkDialog();
    });
  }

  async function openDocumentLinkDialog() {
    if (isActiveReadOnly?.()) {
      setStatus('Document link insertion is unavailable for read-only documents. Navigation remains available.', 'warning');
      return;
    }
    const activeRecord = state.files.find((record) => record.path === state.activePath);
    if (!activeRecord) {
      setStatus('Open an editable document before inserting a document link.', 'warning');
      return;
    }

    const runId = ++dialogRunId;
    applying = false;
    returnFocus = document.activeElement;
    origin = {
      record: activeRecord,
      path: activeRecord.path,
      value: editor.value,
      selectionStart: editor.selectionStart,
      selectionEnd: editor.selectionEnd,
      selectionText: editor.value.slice(editor.selectionStart, editor.selectionEnd).trim(),
    };
    linkTextTouched = Boolean(origin.selectionText);
    const hasRelativeContext = recordHasDocumentContext(activeRecord, state.workspaceKind);
    candidates = state.files
      .filter((record) => isSupportedFile(record.name || record.path) && !record.generatedMap)
      .filter((record) => record.path === origin.path
        || recordsShareDocumentContext(activeRecord, record, state.workspaceKind))
      .map((record) => ({
        record,
        title: getDocTitleFromPath(record.path),
        sections: [],
      }));
    selectedPath = origin.path;
    searchInput.value = '';
    linkTextInput.value = origin.selectionText || getDocTitleFromPath(origin.path);
    contextNote.textContent = hasRelativeContext
      ? 'Paths are calculated from the current document inside this workspace.'
      : 'Only this document is available because loose files have no shared relative path. Open a folder or import the complete ZIP pack to link other documents.';
    renderResults();
    renderSections({ preserveSelection: false });
    updateGeneratedMarkdown();
    dialog.showModal();
    searchInput.focus();

    const selected = candidates.find((candidate) => candidate.record.path === selectedPath);
    if (selected) await hydrateCandidate(selected, runId);
    void Promise.all(candidates
      .filter((candidate) => candidate !== selected)
      .map((candidate) => hydrateCandidate(candidate, runId)));
  }

  function closeDocumentLinkDialog(event) {
    event?.preventDefault?.();
    if (dialog?.open) dialog.close();
    const target = returnFocus;
    returnFocus = null;
    origin = null;
    applying = false;
    if (target?.isConnected && typeof target.focus === 'function') target.focus({ preventScroll: true });
  }

  async function hydrateCandidate(candidate, runId = dialogRunId) {
    try {
      const metadata = await getDocumentMetadata(candidate.record);
      if (runId !== dialogRunId || !dialog.open) return;
      candidate.title = metadata.title || getDocTitleFromPath(candidate.record.path);
      candidate.sections = metadata.sections;
      renderResults();
      if (candidate.record.path === selectedPath) {
        if (!origin.selectionText && !linkTextTouched) linkTextInput.value = candidate.title;
        renderSections();
        updateGeneratedMarkdown();
      }
    } catch {
      if (runId === dialogRunId && dialog.open && candidate.record.path === selectedPath) {
        validation.textContent = `Could not read ${candidate.record.path} without activating it.`;
      }
    }
  }

  async function getDocumentMetadata(record) {
    const text = await readRecordText(record);
    const cached = metadataCache.get(record.path);
    if (cached?.record === record && cached.text === text) return cached.metadata;
    const inspected = await inspectMarkdownDocument(text, record.name || record.path);
    const metadata = {
      title: inspected.title || getDocTitleFromPath(record.path),
      sections: inspected.sections || [],
    };
    metadataCache.set(record.path, { record, text, metadata });
    return metadata;
  }

  function renderResults() {
    const query = String(searchInput?.value || '').trim().toLowerCase();
    const filtered = candidates.filter((candidate) => {
      const haystack = `${candidate.title} ${candidate.record.name || ''} ${candidate.record.path}`.toLowerCase();
      return !query || haystack.includes(query);
    });
    results.innerHTML = '';
    filtered.forEach((candidate) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'document-link-result';
      button.dataset.documentPath = candidate.record.path;
      button.setAttribute('role', 'option');
      button.setAttribute('aria-selected', String(candidate.record.path === selectedPath));

      const title = document.createElement('strong');
      title.textContent = candidate.title || candidate.record.name;
      const path = document.createElement('span');
      path.textContent = candidate.record.path;
      button.append(title, path);
      results.appendChild(button);
    });
    if (!filtered.length) {
      const empty = document.createElement('p');
      empty.className = 'document-link-empty';
      empty.textContent = 'No loaded document matches this search.';
      results.appendChild(empty);
    }
  }

  async function handleResultClick(event) {
    const button = event.target.closest('[data-document-path]');
    if (!button) return;
    await selectCandidate(button.dataset.documentPath);
  }

  async function handleSearchKeydown(event) {
    if (event.key !== 'Enter' || event.isComposing) return;
    const first = results.querySelector('[data-document-path]');
    if (!first) return;
    event.preventDefault();
    await selectCandidate(first.dataset.documentPath);
    linkTextInput.focus();
    linkTextInput.select();
  }

  async function selectCandidate(path) {
    const candidate = candidates.find((item) => item.record.path === path);
    if (!candidate) return;
    selectedPath = candidate.record.path;
    renderResults();
    if (!origin.selectionText && !linkTextTouched) linkTextInput.value = candidate.title;
    renderSections({ preserveSelection: false });
    updateGeneratedMarkdown();
    await hydrateCandidate(candidate);
  }

  function renderSections({ preserveSelection = true } = {}) {
    const candidate = candidates.find((item) => item.record.path === selectedPath);
    const previous = preserveSelection ? sectionSelect.value : '';
    sectionSelect.innerHTML = '';
    sectionSelect.appendChild(new Option('Start of document', ''));
    (candidate?.sections || []).forEach((section) => {
      const prefix = section.kind === 'heading' ? `H${section.level}` : 'Anchor';
      sectionSelect.appendChild(new Option(`${prefix} — ${section.label}`, section.id));
    });
    if ([...sectionSelect.options].some((option) => option.value === previous)) {
      sectionSelect.value = previous;
    }
  }

  function updateGeneratedMarkdown() {
    const candidate = candidates.find((item) => item.record.path === selectedPath);
    if (!origin || !candidate) {
      setGeneratedState({ ok: false, message: 'Choose a document.' });
      return;
    }
    const generated = generateDocumentLink({
      fromPath: origin.path,
      toPath: candidate.record.path,
      fragment: sectionSelect.value,
      label: linkTextInput.value || candidate.title || candidate.record.name,
      workspaceKind: state.workspaceKind,
      workspaceRoot: state.workspaceRootPath,
      fromRecord: origin.record,
      toRecord: candidate.record,
    });
    if (!generated.ok) {
      const message = generated.reason === 'empty-same-document-target'
        ? 'Choose a section when linking to the current document.'
        : generated.reason === 'missing-relative-context'
          ? 'Open a folder or import the complete ZIP pack to calculate a relative path.'
          : 'A safe relative document link cannot be generated for this selection.';
      setGeneratedState({ ok: false, message });
      return;
    }
    setGeneratedState({ ok: true, generated });
  }

  function setGeneratedState({ ok, generated = null, message = '' }) {
    generatedMarkdown.textContent = generated?.markdown || 'No link generated.';
    relativePath.textContent = generated?.href ? `Relative target: ${generated.href}` : '';
    validation.textContent = message;
    applyButton.disabled = !ok;
  }

  async function applyDocumentLink(event) {
    event.preventDefault();
    const candidate = candidates.find((item) => item.record.path === selectedPath);
    if (!origin || !candidate || applying) return;
    const submittedOrigin = origin;
    const runId = dialogRunId;
    const currentOrigin = state.files.find((record) => record.path === submittedOrigin.path);
    if (state.activePath !== submittedOrigin.path || currentOrigin !== submittedOrigin.record || editor.value !== submittedOrigin.value) {
      validation.textContent = 'The source document changed while this dialogue was open. Close it and try again from the current selection.';
      applyButton.disabled = true;
      return;
    }
    const currentTarget = state.files.find((record) => record.path === candidate.record.path);
    if (currentTarget !== candidate.record) {
      validation.textContent = 'The selected document is no longer available at that exact path.';
      applyButton.disabled = true;
      return;
    }
    applying = true;
    applyButton.disabled = true;

    let metadata;
    try {
      metadata = await getDocumentMetadata(currentTarget);
    } catch {
      if (runId === dialogRunId && origin === submittedOrigin && dialog.open) {
        validation.textContent = `Could not read ${currentTarget.path} without activating it. The link was not inserted.`;
        applying = false;
        applyButton.disabled = false;
      }
      return;
    }
    if (runId !== dialogRunId || origin !== submittedOrigin || !dialog.open) return;
    if (state.activePath !== submittedOrigin.path
      || state.files.find((record) => record.path === submittedOrigin.path) !== submittedOrigin.record
      || editor.value !== submittedOrigin.value) {
      validation.textContent = 'The source document changed while this dialogue was open. Close it and try again from the current selection.';
      applyButton.disabled = true;
      return;
    }
    if (state.files.find((record) => record.path === currentTarget.path) !== currentTarget) {
      validation.textContent = 'The selected document is no longer available at that exact path.';
      applyButton.disabled = true;
      return;
    }
    if (currentTarget.path !== submittedOrigin.path
      && !recordsShareDocumentContext(submittedOrigin.record, currentTarget, state.workspaceKind)) {
      validation.textContent = 'The selected document no longer shares a known relative workspace context with the source.';
      applyButton.disabled = true;
      return;
    }
    candidate.title = metadata.title || getDocTitleFromPath(currentTarget.path);
    candidate.sections = metadata.sections;
    const section = sectionSelect.value;
    if (section && !metadata.sections.some((item) => item.id === section)) {
      validation.textContent = 'The selected section changed or is no longer available. Choose the section again.';
      applying = false;
      renderSections({ preserveSelection: false });
      updateGeneratedMarkdown();
      return;
    }

    const generated = generateDocumentLink({
      fromPath: submittedOrigin.path,
      toPath: currentTarget.path,
      fragment: section,
      label: linkTextInput.value || metadata.title || currentTarget.name,
      workspaceKind: state.workspaceKind,
      workspaceRoot: state.workspaceRootPath,
      fromRecord: submittedOrigin.record,
      toRecord: currentTarget,
    });
    if (!generated.ok) {
      applying = false;
      updateGeneratedMarkdown();
      return;
    }

    const start = submittedOrigin.selectionStart;
    const end = submittedOrigin.selectionEnd;
    dialog.close();
    returnFocus = null;
    origin = null;
    applying = false;
    replaceEditorRange(start, end, generated.markdown, start + generated.markdown.length, start + generated.markdown.length);
    setStatus(`Inserted document link to ${currentTarget.path}${section ? `#${section}` : ''}.`, 'ok');
  }

  return {
    installDocumentLinkHandlers,
    openDocumentLinkDialog,
  };
}
