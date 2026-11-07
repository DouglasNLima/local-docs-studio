import { createRenderingService } from '../rendering/render-service.js';
import { collectDocumentHeadings } from './table-of-contents.js';

// Two presentation slots share the existing document records and active command
// surface. The inactive surface is a view of fileCache, never a writable buffer.
export function createComparisonService({ state, editor, preview, workspace, setEditorLayout, selectFile, find, capturePreviewFind, restorePreviewFind, onPreviewClick, setStatus }) {
  const app = document.querySelector('#app');
  const sessions = new WeakMap();
  let slots = null; let active = 0; let switching = false; let originalLayout = 'split'; let request = 0;
  const host = document.createElement('section'); host.className = 'comparison-host'; host.hidden = true;
  host.setAttribute('aria-label', 'Document comparison');
  host.innerHTML = `<div class="comparison-controls"><strong>Compare documents</strong><label><input type="checkbox" class="comparison-sync"> Synchronise scroll</label><label>Pane widths <input class="comparison-width" type="range" min="25" max="75" value="50" aria-label="Comparison pane widths"></label><button type="button" class="comparison-close">Close comparison</button></div><div class="comparison-panes"></div>`;
  workspace.before(host);
  const panes = host.querySelector('.comparison-panes');
  const sync = host.querySelector('.comparison-sync');
  const views = [0, 1].map(index => {
    const element = document.createElement('section'); element.className = 'comparison-pane'; element.dataset.pane = index;
    element.innerHTML = `<header><select aria-label="Document in ${index ? 'right' : 'left'} pane"></select><select aria-label="Mode in ${index ? 'right' : 'left'} pane"><option value="editor">Editor</option><option value="preview">Preview</option></select><button type="button" class="comparison-activate">Activate</button></header><div class="comparison-body"></div>`;
    panes.append(element);
    const [documentChoice, modeChoice] = element.querySelectorAll('select'); const body = element.querySelector('.comparison-body');
    documentChoice.addEventListener('change', () => void choose(index, documentChoice.value));
    modeChoice.addEventListener('change', () => { if (!slots) return; slots[index].mode = modeChoice.value; if (index === active) setEditorLayout(modeChoice.value, { persist: false }); else void paintInactive(); });
    element.querySelector('button').addEventListener('click', () => void activate(index));
    return { element, documentChoice, modeChoice, body };
  });
  const passiveEditor = document.createElement('textarea'); passiveEditor.className = 'comparison-passive-editor';
  passiveEditor.readOnly = true; passiveEditor.spellcheck = false;
  const passivePreview = document.createElement('article'); passivePreview.className = 'preview comparison-passive-preview'; passivePreview.tabIndex = 0;
  const passiveState = Object.create(state);
  Object.assign(passiveState, { activePath: '', fileName: '', renderId: 0, debounceId: 0, studioMode: false, docsPreview: false, diagramZoom: 1 });
  Object.defineProperty(passiveState, 'mermaidRenderChain', { get: () => state.mermaidRenderChain, set: value => { state.mermaidRenderChain = value; } });
  const control = () => document.createElement('button');
  const renderer = createRenderingService({ state: passiveState, instanceId: 'comparison',
    dom: { editor: passiveEditor, preview: passivePreview, diagramCount: control(), mermaidThemeSelect: control(),
      zoomOutButton: control(), zoomInButton: control(), fitZoomButton: control(), resetZoomButton: control(), zoomValue: control() },
    callbacks: { setStatus: () => {}, setExportTrust: () => {}, updatePreviewOutline: () => {
      for (const heading of collectDocumentHeadings(passivePreview)) heading.heading.id = `comparison-${heading.id}`;
    },
      getExportFileStem: () => passiveState.fileName, getDocTitleFromPath: path => path } });
  let paintedRecord = null; let paintedSource = null; let paintedTheme = null; let scrollLock = false; let previewPointer = false;
  function selectionRange(root) {
    const selection = window.getSelection();
    if (!selection?.rangeCount || selection.isCollapsed || !root.contains(selection.anchorNode) || !root.contains(selection.focusNode)) return null;
    const range = selection.getRangeAt(0); const before = document.createRange(); before.selectNodeContents(root); before.setEnd(range.startContainer, range.startOffset);
    return { start: before.toString().length, length: range.toString().length };
  }
  function restoreSelection(root, saved) {
    if (!saved) return;
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT); let position = 0; let start = null; let end = null;
    while (walker.nextNode()) {
      const node = walker.currentNode; const next = position + node.length;
      if (!start && saved.start >= position && saved.start <= next) start = [node, saved.start - position];
      if (saved.start + saved.length >= position && saved.start + saved.length <= next) { end = [node, saved.start + saved.length - position]; break; }
      position = next;
    }
    if (start && end) { const range = document.createRange(); range.setStart(...start); range.setEnd(...end); const selection = window.getSelection(); selection.removeAllRanges(); selection.addRange(range); }
  }
  function currentRecord() { return state.files.find(record => record.path === state.activePath); }
  function capture(record = currentRecord()) {
    if (!record) return;
    sessions.set(record, { start: editor.selectionStart, end: editor.selectionEnd, direction: editor.selectionDirection, editorTop: editor.scrollTop,
      editorLeft: editor.scrollLeft, previewTop: preview.scrollTop, find: find.captureSession(), previewFind: capturePreviewFind(),
      selection: selectionRange(preview), source: editor.value });
  }
  function restore(record) {
    const session = sessions.get(record) || {};
    editor.setSelectionRange(session.start || 0, session.end || 0, session.direction || 'none');
    editor.scrollTop = session.editorTop || 0; editor.scrollLeft = session.editorLeft || 0;
    preview.scrollTop = session.previewTop || 0; find.restoreSession(session.find); restorePreviewFind(session.previewFind);
    if (session.source === editor.value) restoreSelection(preview, session.selection);
  }
  function refreshHeaders() {
    if (!slots) return;
    views.forEach((view, index) => {
      const selected = slots[index].record;
      const signature = state.files.map(record => record.path).join('\n');
      if (view.documentChoice.dataset.files !== signature) {
        view.documentChoice.replaceChildren(...state.files.map(record => new Option(record.path, record.path)));
        view.documentChoice.dataset.files = signature;
      }
      view.documentChoice.value = selected.path;
      view.modeChoice.value = slots[index].mode;
      view.element.classList.toggle('comparison-active', index === active);
      view.element.setAttribute('aria-label', `${index === active ? 'Active' : 'Secondary'} document: ${selected.name}`);
      const button = view.element.querySelector('button'); button.textContent = index === active ? 'Active' : 'Activate';
      button.setAttribute('aria-pressed', String(index === active));
      button.disabled = switching;
    });
  }
  async function paintInactive() {
    if (!slots) return;
    const index = 1 - active; const slot = slots[index]; const source = state.fileCache.get(slot.record.path);
    // Open beside first uses selectFile to load/recover the document through its
    // existing lifecycle. No passive renderer reads files or persists anything.
    if (typeof source !== 'string') return;
    passiveState.activePath = slot.record.path; passiveState.fileName = slot.record.name;
    const session = sessions.get(slot.record) || {};
    passiveEditor.value = source;
    const appearance = getComputedStyle(editor);
    for (const property of ['fontSize', 'fontFamily', 'lineHeight']) passiveEditor.style[property] = appearance[property];
    passiveEditor.setAttribute('aria-label', `${slot.record.name} source; focus to activate this document`);
    passivePreview.setAttribute('aria-label', `${slot.record.name} preview`);
    views[index].body.replaceChildren(slot.mode === 'editor' ? passiveEditor : passivePreview);
    passiveEditor.setSelectionRange(session.start || 0, session.end || 0, session.direction || 'none');
    passiveEditor.scrollTop = session.editorTop || 0; passiveEditor.scrollLeft = session.editorLeft || 0;
    if (slot.mode !== 'preview') { passiveState.renderId++; return; }
    const theme = `${state.theme}|${state.mermaidTheme}`;
    if (paintedRecord !== slot.record || paintedSource !== source || paintedTheme !== theme || !passivePreview.childNodes.length) {
      if (paintedRecord !== slot.record) passivePreview.replaceChildren(document.createTextNode('Loading comparison preview…'));
      paintedRecord = slot.record; paintedSource = source; paintedTheme = theme;
      await renderer.renderPreview();
    }
    if (slots?.[1 - active] === slot) passivePreview.scrollTop = session.previewTop || 0;
  }
  function attach() {
    if (!slots) return;
    views[active].body.replaceChildren(workspace);
    setEditorLayout(slots[active].mode, { persist: false });
    refreshHeaders(); void paintInactive();
  }
  async function activate(index, focus = true) {
    if (!slots || switching || index === active) return;
    const target = slots[index].record; const generation = ++request; switching = true; refreshHeaders();
    host.setAttribute('aria-busy', 'true'); capture();
    try {
      await selectFile(target.path);
      if (!slots || generation !== request || currentRecord() !== target) return;
      if (slots[index].mode === 'editor') sessions.set(target, { ...(sessions.get(target) || {}), start: passiveEditor.selectionStart,
        end: passiveEditor.selectionEnd, direction: passiveEditor.selectionDirection, editorTop: passiveEditor.scrollTop, editorLeft: passiveEditor.scrollLeft });
      active = index; attach(); restore(target);
      if (focus) (slots[index].mode === 'editor' ? editor : preview).focus({ preventScroll: true });
    } finally { switching = false; host.removeAttribute('aria-busy'); refreshHeaders(); }
  }
  async function choose(index, path) {
    if (switching) return;
    if (!slots) return;
    if (slots[1 - index].record.path === path) { await activate(1 - index); refreshHeaders(); return; }
    if (index !== active) await activate(index, false);
    if (!slots || active !== index) return;
    await selectFile(path);
  }
  async function open() {
    if (slots) { views[1 - active].documentChoice.focus(); return; }
    const primary = currentRecord(); const secondary = state.files.find(record => record !== primary);
    if (!primary || !secondary) { setStatus('Open at least two workspace documents to compare them.', 'warning'); return; }
    originalLayout = state.editorLayout; capture(primary);
    slots = [{ record: primary, mode: originalLayout === 'preview' ? 'preview' : 'editor' }, { record: secondary, mode: 'preview' }];
    active = 0; host.hidden = false; app.classList.add('comparing-documents'); attach();
    await activate(1);
    // Opening the second file only loads or recovers through the existing service.
    // Keep the original document active and retain the second file's live buffer.
    if (slots && currentRecord() === secondary) await activate(0);
    if (slots && !state.fileCache.has(secondary.path)) close();
  }
  function close() {
    if (!slots) return;
    capture(); request++; slots = null; passiveState.renderId++; paintedRecord = null; paintedSource = null;
    host.before(workspace); host.hidden = true; app.classList.remove('comparing-documents');
    passivePreview.replaceChildren(); passiveEditor.value = ''; setEditorLayout(originalLayout, { persist: false });
    preview.tabIndex = 0;
    (originalLayout === 'preview' ? preview : editor).focus({ preventScroll: true });
  }
  editor.addEventListener('lens-document-leaving', () => { if (slots) capture(); });
  editor.addEventListener('lens-document-ready', event => {
    if (!slots || switching) return;
    if (slots.some(slot => !state.files.includes(slot.record))) { close(); return; }
    const record = event.detail.record;
    if (slots[1 - active].record === record) active = 1 - active;
    else slots[active].record = record;
    attach(); restore(record);
  });
  document.addEventListener('lens-document-state', () => {
    if (!slots || switching) return;
    if (slots.some(slot => !state.files.includes(slot.record))) close();
    else { refreshHeaders(); const secondary = slots[1 - active].record; if (passiveEditor.value !== state.fileCache.get(secondary.path)) void paintInactive(); }
  });
  document.addEventListener('lens-layout-changed', () => { if (slots) { slots[active].mode = state.editorLayout === 'preview' ? 'preview' : 'editor'; views[active].modeChoice.value = slots[active].mode; } });
  passiveEditor.addEventListener('focus', () => { if (slots && !switching) void activate(1 - active); });
  passivePreview.addEventListener('pointerdown', () => { previewPointer = true; });
  passivePreview.addEventListener('blur', () => { previewPointer = false; });
  passivePreview.addEventListener('focus', () => { if (!previewPointer && slots && !switching) void activate(1 - active); });
  passivePreview.addEventListener('click', async event => {
    if (!slots || switching) return;
    const target = event.target; const actionable = target.closest('a,button');
    if (target.closest('summary')) return;
    const anchor = target.closest('a[href]');
    if (!anchor || anchor.dataset.docPath || anchor.dataset.wikilinkTarget || anchor.getAttribute('href').startsWith('#')) event.preventDefault();
    const index = 1 - active;
    const record = slots[index].record; const session = sessions.get(record) || {};
    sessions.set(record, { ...session, selection: selectionRange(passivePreview), source: passiveEditor.value, previewTop: passivePreview.scrollTop });
    await activate(index, false);
    if (slots && active === index) {
      preview.tabIndex = 0; preview.focus({ preventScroll: true });
      if (actionable) await onPreviewClick({ target, preventDefault() {}, stopPropagation() {} });
    }
  });
  function scroll(source, secondary) {
    if (!slots || scrollLock) return;
    const record = slots[secondary ? 1 - active : active].record;
    const session = sessions.get(record) || {};
    session[source === editor || source === passiveEditor ? 'editorTop' : 'previewTop'] = source.scrollTop; sessions.set(record, session);
    if (!sync.checked) return;
    const target = secondary ? (slots[active].mode === 'editor' ? editor : preview) : (slots[1 - active].mode === 'editor' ? passiveEditor : passivePreview);
    scrollLock = true; target.scrollTop = source.scrollTop / Math.max(1, source.scrollHeight - source.clientHeight) * Math.max(0, target.scrollHeight - target.clientHeight);
    requestAnimationFrame(() => { scrollLock = false; });
  }
  for (const element of [editor, preview, passiveEditor, passivePreview]) element.addEventListener('scroll', () => scroll(element, element === passiveEditor || element === passivePreview));
  host.querySelector('.comparison-close').addEventListener('click', close);
  host.querySelector('.comparison-width').addEventListener('input', event => panes.style.setProperty('--comparison-left', `${event.target.value}%`));
  new MutationObserver(() => { if (slots && paintedTheme !== `${state.theme}|${state.mermaidTheme}`) void paintInactive(); }).observe(app, { attributes: true, attributeFilter: ['class'] });
  document.querySelector('#openBesideButton').addEventListener('click', () => void open());
  return { open, close };
}
