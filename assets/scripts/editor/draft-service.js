import { idbRequest, idbTransactionDone, idbSupported, openObjectStoreDb } from '../utils/idb.js';

const DB_NAME = 'md-mmd-renderer-drafts';
const DB_VERSION = 1;
const STORE_NAME = 'drafts';
const SAVE_DEBOUNCE_MS = 650;

export function createDraftService({ state, dom, callbacks }) {
  const {
    editor,
    recoveryDialog,
    recoveryDiffSaved,
    recoveryDiffDraft,
    recoverySummary,
    recoveryKeepSavedButton,
    recoveryRestoreDraftButton,
    recoveryDeleteDraftButton,
    lossProtectionDialog,
    lossProtectionSummary,
    lossProtectionUndoButton,
    lossProtectionSaveAnywayButton,
    lossProtectionCancelButton,
  } = dom;
  const {
    setStatus,
    renderFileList,
    updateActiveFileLabel,
    updateSaveButton,
    resetEditorHistory,
    renderPreview,
  } = callbacks;

  let dbPromise = null;
  let saveTimer = 0;
  let recoveryResolve = null;
  let lossResolve = null;

  function initDraftStore() {
    if (!idbSupported()) {
      setStatus('Local draft recovery is unavailable in this browser.', 'warning');
      return;
    }

    dbPromise = openObjectStoreDb(DB_NAME, DB_VERSION, [{
      name: STORE_NAME,
      keyPath: 'key',
      indexes: [
        { name: 'workspaceKey', keyPath: 'workspaceKey' },
        { name: 'path', keyPath: 'path' },
      ],
    }]).catch((error) => {
      console.warn('Draft store unavailable.', error);
      setStatus('Local draft recovery could not start. Editing still works normally.', 'warning');
      return null;
    });
  }

  function installDraftHandlers() {
    recoveryDialog?.addEventListener('cancel', (event) => {
      event.preventDefault();
      closeRecoveryDialog({ action: 'keep' });
    });
    recoveryKeepSavedButton?.addEventListener('click', () => closeRecoveryDialog({ action: 'keep' }));
    recoveryRestoreDraftButton?.addEventListener('click', () => closeRecoveryDialog({ action: 'restore' }));
    recoveryDeleteDraftButton?.addEventListener('click', () => closeRecoveryDialog({ action: 'delete' }));
    recoveryDialog?.addEventListener('click', (event) => {
      if (event.target.closest('[data-recovery-keep-saved]')) closeRecoveryDialog({ action: 'keep' });
    });

    lossProtectionDialog?.addEventListener('cancel', (event) => {
      event.preventDefault();
      closeLossDialog(false);
    });
    lossProtectionCancelButton?.addEventListener('click', () => closeLossDialog(false));
    lossProtectionUndoButton?.addEventListener('click', () => {
      const saved = state.savedContentCache.get(state.activePath);
      if (typeof saved === 'string') {
        editor.value = saved;
        state.fileCache.set(state.activePath, saved);
        state.dirtyPaths.delete(state.activePath);
        resetEditorHistory();
        renderFileList();
        updateActiveFileLabel();
        updateSaveButton();
        renderPreview();
        setStatus('Large deletion undone. Review the document before saving again.', 'ok');
      }
      closeLossDialog(false);
    });
    lossProtectionSaveAnywayButton?.addEventListener('click', () => closeLossDialog(true));
  }

  function updateWorkspaceDraftKey() {
    const paths = state.files.map((file) => file.path).sort().join('|');
    state.draftWorkspaceKey = `${state.folderName || 'workspace'}::${paths}`;
  }

  function scheduleDraftSave() {
    if (!state.activePath) return;
    window.clearTimeout(saveTimer);
    saveTimer = window.setTimeout(() => saveActiveDraft(), SAVE_DEBOUNCE_MS);
  }

  async function saveActiveDraft() {
    const record = state.files.find((item) => item.path === state.activePath);
    if (!record || record.readOnly) return;
    const saved = state.savedContentCache.get(record.path);
    if (typeof saved === 'string' && saved === editor.value) {
      await deleteDraft(record.path);
      return;
    }
    await putDraft(record.path, editor.value, saved ?? '');
  }

  async function afterActiveFileLoaded(record, content) {
    state.savedContentCache.set(record.path, content);
    state.deletionOverridePaths.delete(record.path);

    const draft = await getDraft(record.path);
    if (!draft || draft.text === content || record.readOnly) {
      return { content, dirty: false };
    }

    const decision = await showRecoveryDialog(record, content, draft.text);
    if (decision.action === 'restore') {
      setStatus(`Restored local draft for ${record.name}. Save when ready.`, 'warning');
      return { content: draft.text, dirty: true };
    }

    if (decision.action === 'delete' || decision.action === 'keep') {
      await deleteDraft(record.path);
      setStatus(decision.action === 'delete' ? `Deleted local draft for ${record.name}.` : `Kept saved version of ${record.name}.`, 'ok');
    }
    return { content, dirty: false };
  }

  async function beforeSaveActiveFile(record, content) {
    const saved = state.savedContentCache.get(record.path);
    if (state.deletionOverridePaths.has(record.path)) {
      state.deletionOverridePaths.delete(record.path);
      return true;
    }

    if (!isLargeDeletion(saved, content)) return true;
    const deleted = String(saved).length - String(content).length;
    const percent = Math.round((deleted / Math.max(String(saved).length, 1)) * 100);
    const approved = await showLossProtectionDialog(record, { deleted, percent });
    if (approved) {
      state.deletionOverridePaths.add(record.path);
      return true;
    }
    return false;
  }

  async function afterSaveActiveFile(record, content, oldPath = '') {
    state.savedContentCache.set(record.path, content);
    state.deletionOverridePaths.delete(record.path);
    await deleteDraft(record.path);
    if (oldPath && oldPath !== record.path) {
      state.savedContentCache.delete(oldPath);
      state.deletionOverridePaths.delete(oldPath);
      await deleteDraft(oldPath);
    }
  }

  async function clearWorkspaceDrafts() {
    const db = await getDb();
    if (!db || !state.draftWorkspaceKey) return;
    const transaction = db.transaction(STORE_NAME, 'readwrite');
    const index = transaction.objectStore(STORE_NAME).index('workspaceKey');
    const request = index.openCursor(IDBKeyRange.only(state.draftWorkspaceKey));
    request.onsuccess = () => {
      const cursor = request.result;
      if (!cursor) return;
      cursor.delete();
      cursor.continue();
    };
    await idbTransactionDone(transaction);
  }

  async function putDraft(path, text, savedText) {
    const db = await getDb();
    if (!db) return;
    const draft = {
      key: getDraftKey(path),
      workspaceKey: state.draftWorkspaceKey || '',
      path,
      text,
      savedText,
      updatedAt: Date.now(),
      appVersion: 'drafts-v1',
    };
    const transaction = db.transaction(STORE_NAME, 'readwrite');
    transaction.objectStore(STORE_NAME).put(draft);
    await idbTransactionDone(transaction);
  }

  async function getDraft(path) {
    const db = await getDb();
    if (!db) return null;
    const transaction = db.transaction(STORE_NAME, 'readonly');
    return await idbRequest(transaction.objectStore(STORE_NAME).get(getDraftKey(path)));
  }

  async function deleteDraft(path) {
    const db = await getDb();
    if (!db) return;
    const transaction = db.transaction(STORE_NAME, 'readwrite');
    transaction.objectStore(STORE_NAME).delete(getDraftKey(path));
    await idbTransactionDone(transaction);
  }

  async function getDb() {
    if (!dbPromise) return null;
    return await dbPromise;
  }

  function getDraftKey(path) {
    return `${state.draftWorkspaceKey || 'workspace'}::${path}`;
  }

  function showRecoveryDialog(record, savedText, draftText) {
    if (!recoveryDialog?.showModal) {
      return Promise.resolve(window.confirm(`A local draft exists for ${record.name}. Restore it?`)
        ? { action: 'restore' }
        : { action: 'keep' });
    }

    recoverySummary.textContent = `${record.path} has a newer browser-local draft. Compare it with the saved file before choosing.`;
    recoveryDiffSaved.innerHTML = renderDiffColumn(savedText, draftText, 'saved');
    recoveryDiffDraft.innerHTML = renderDiffColumn(draftText, savedText, 'draft');
    recoveryDialog.showModal();
    return new Promise((resolve) => {
      recoveryResolve = resolve;
    });
  }

  function closeRecoveryDialog(result) {
    recoveryDialog?.close();
    recoveryResolve?.(result);
    recoveryResolve = null;
  }

  function showLossProtectionDialog(record, { deleted, percent }) {
    if (!lossProtectionDialog?.showModal) {
      return Promise.resolve(window.confirm(`${record.name} lost ${deleted} characters (${percent}%). Save anyway?`));
    }
    lossProtectionSummary.textContent = `${record.path} is ${deleted} characters shorter than the last saved version (${percent}% removed).`;
    lossProtectionDialog.showModal();
    return new Promise((resolve) => {
      lossResolve = resolve;
    });
  }

  function closeLossDialog(approved) {
    lossProtectionDialog?.close();
    lossResolve?.(approved);
    lossResolve = null;
  }

  function isLargeDeletion(saved, content) {
    if (typeof saved !== 'string') return false;
    const deleted = saved.length - String(content || '').length;
    return deleted > 50 && deleted / Math.max(saved.length, 1) > 0.2;
  }

  function renderDiffColumn(leftText, rightText, side) {
    const left = String(leftText || '').split('\n');
    const right = String(rightText || '').split('\n');
    const total = Math.max(left.length, right.length);
    const rows = [];
    for (let index = 0; index < total; index += 1) {
      const value = left[index] ?? '';
      const changed = value !== (right[index] ?? '');
      rows.push(`<div class="diff-line ${changed ? `diff-${side}` : ''}"><span>${index + 1}</span><code>${escapeHtml(value || ' ')}</code></div>`);
    }
    return rows.join('');
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
    initDraftStore,
    installDraftHandlers,
    updateWorkspaceDraftKey,
    scheduleDraftSave,
    saveActiveDraft,
    afterActiveFileLoaded,
    beforeSaveActiveFile,
    afterSaveActiveFile,
    clearWorkspaceDrafts,
  };
}
