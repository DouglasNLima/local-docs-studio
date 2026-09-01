import { readZipEntries } from '../utils/zip.js';
import { idbRequest, idbTransactionDone, openObjectStoreDb } from '../utils/idb.js';
import { escapeHtml } from '../utils/format.js';
import {
  WORD_TEMPLATE_PACK_VERSION,
  analyseWordTemplatePackage,
} from './word-template-package.js';

export const DEFAULT_WORD_TEMPLATE_ID = '';
export const WORD_TEMPLATE_SELECTION_KEY = 'lensDocs.wordExport.templateId';

const WORD_TEMPLATE_DB_NAME = 'local-docs-studio-word-templates';
const WORD_TEMPLATE_DB_VERSION = 1;
const WORD_TEMPLATE_STORE = 'templates';
const DOCX_MIME = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';

export function createWordTemplateService({
  dom,
  callbacks,
}) {
  const {
    wordTemplateInput,
    wordTemplateManageButton,
    wordTemplateImportButton,
    wordTemplateSelect,
    wordTemplateSummary,
    wordTemplateManagementDialog,
    wordTemplateManagementForm,
    wordTemplateManagementList,
    wordTemplateManagementImportButton,
    wordTemplateManagementCloseButton,
  } = dom;
  const {
    closeOpenMenus,
    promptForText,
    confirmAction = async () => false,
    setStatus,
  } = callbacks;

  let templates = [];
  let selectedTemplateId = readStoredSelection();
  let managementReturnFocus = null;
  let managementBusy = false;

  async function initWordTemplates() {
    templates = await listStoredTemplates();
    if (selectedTemplateId && !hasTemplate(selectedTemplateId)) {
      setSelectedTemplateId(DEFAULT_WORD_TEMPLATE_ID, { render: false });
    }
    renderWordTemplateUi();
  }

  function installWordTemplateHandlers() {
    wordTemplateManageButton?.addEventListener('click', openWordTemplateManagement);
    wordTemplateImportButton?.addEventListener('click', openWordTemplateFilePicker);
    wordTemplateManagementImportButton?.addEventListener('click', openWordTemplateFilePicker);
    wordTemplateManagementCloseButton?.addEventListener('click', closeWordTemplateManagement);

    wordTemplateInput?.addEventListener('change', async () => {
      const [file] = [...(wordTemplateInput.files || [])];
      wordTemplateInput.value = '';
      if (!file) return;
      await importWordTemplateFromFile(file);
    });

    wordTemplateSelect?.addEventListener('change', () => {
      selectWordTemplate(wordTemplateSelect.value, { announce: true });
    });

    wordTemplateManagementForm?.addEventListener('submit', (event) => {
      event.preventDefault();
    });
    wordTemplateManagementDialog?.addEventListener('cancel', (event) => {
      event.preventDefault();
      closeWordTemplateManagement();
    });
    wordTemplateManagementDialog?.addEventListener('click', (event) => {
      if (event.target === wordTemplateManagementDialog || event.target.closest('[data-word-template-management-cancel]')) {
        closeWordTemplateManagement();
      }
    });
    wordTemplateManagementList?.addEventListener('click', handleWordTemplateManagementAction);
  }

  function openWordTemplateFilePicker() {
    wordTemplateInput?.click();
  }

  function openWordTemplateManagement() {
    if (!wordTemplateManagementDialog) return;

    managementReturnFocus = document.activeElement;
    renderWordTemplateManagement();
    closeOpenMenus?.();

    if (!wordTemplateManagementDialog.open) {
      if (typeof wordTemplateManagementDialog.showModal === 'function') {
        wordTemplateManagementDialog.showModal();
      } else {
        wordTemplateManagementDialog.setAttribute('open', '');
      }
    }

    window.requestAnimationFrame(() => {
      const firstAction = wordTemplateManagementList?.querySelector('button');
      (firstAction || wordTemplateManagementCloseButton)?.focus({ preventScroll: true });
    });
  }

  function closeWordTemplateManagement() {
    if (wordTemplateManagementDialog?.open && typeof wordTemplateManagementDialog.close === 'function') {
      wordTemplateManagementDialog.close();
    } else {
      wordTemplateManagementDialog?.removeAttribute('open');
    }

    const returnFocus = [
      managementReturnFocus?.closest?.('details')?.querySelector('summary'),
      managementReturnFocus,
      wordTemplateManageButton?.closest?.('details')?.querySelector('summary'),
      wordTemplateManageButton,
    ].find(isVisibleFocusTarget);
    managementReturnFocus = null;
    returnFocus?.focus?.({ preventScroll: true });
  }

  async function importWordTemplateFromFile(file, options = {}) {
    if (!isDocxFile(file)) {
      setStatus('Choose a .docx file to import as a Word template.', 'warning');
      return null;
    }

    try {
      const fallbackName = getTemplateDisplayName(file.name);
      let displayName = options.displayName;
      if (displayName === undefined) {
        displayName = typeof promptForText === 'function'
          ? await promptForText({
            title: 'Import Word template',
            message: 'Save this .docx as a reusable local Word export template.',
            label: 'Template name',
            value: fallbackName,
            confirmLabel: 'Import template',
            validate: (value) => value ? '' : 'Enter a template name.',
          })
          : fallbackName;
        if (displayName === null || displayName === undefined) return null;
      }

      displayName = normaliseDisplayName(displayName);
      if (!displayName) {
        setStatus('Enter a template name.', 'warning');
        return null;
      }

      const pack = await extractWordTemplatePack(file, { displayName });
      await saveWordTemplatePack(pack);
      templates = await listStoredTemplates();
      setSelectedTemplateId(pack.manifest.id, { render: false });
      renderWordTemplateUi();
      setStatus(`Imported Word template "${pack.manifest.displayName}".`, 'ok');
      closeOpenMenus?.();
      return pack;
    } catch (error) {
      setStatus('Word template import failed.', 'danger');
      console.error(error);
      return null;
    }
  }

  async function getSelectedWordTemplatePack() {
    if (!selectedTemplateId) return null;

    let record = null;
    try {
      record = await getStoredTemplate(selectedTemplateId);
    } catch (error) {
      console.warn('The selected Word template could not be read. Falling back to default Word export.', error);
      repairMissingSelection();
      return null;
    }

    if (!record) {
      templates = templates.filter((template) => template?.manifest?.id !== selectedTemplateId);
      repairMissingSelection();
      return null;
    }

    const sourceBytes = toUint8Array(record.templateDocx);
    if (!sourceBytes?.byteLength) {
      repairMissingSelection('The selected Word template is unavailable; using default Word export.');
      return null;
    }

    try {
      const entries = await readZipEntries(sourceBytes);
      analyseWordTemplatePackage(entries);
    } catch (error) {
      console.warn('The selected Word template package is unavailable. Falling back to default Word export.', error);
      repairMissingSelection('The selected Word template is unavailable; using default Word export.');
      return null;
    }

    return record;
  }

  function getWordTemplateSummaries() {
    return templates.map((template) => template.manifest);
  }

  function selectWordTemplate(templateId, options = {}) {
    const nextId = normaliseTemplateId(templateId);
    const resolvedId = nextId && hasTemplate(nextId) ? nextId : DEFAULT_WORD_TEMPLATE_ID;
    setSelectedTemplateId(resolvedId, { render: true });

    if (options.announce) {
      const selected = findTemplate(resolvedId);
      setStatus(
        selected
          ? `Word export will use "${selected.manifest.displayName}".`
          : 'Word export will use the default styling.',
        'ok',
      );
    }
    return resolvedId;
  }

  async function renameWordTemplate(templateId) {
    const template = findTemplate(templateId);
    if (!template) return false;

    const result = typeof promptForText === 'function'
      ? await promptForText({
        title: 'Rename Word template',
        message: 'Rename the saved template. Its original file and Word package will be kept unchanged.',
        label: 'Template name',
        value: template.manifest.displayName,
        confirmLabel: 'Save name',
        validate: (value) => value ? '' : 'Enter a template name.',
      })
      : template.manifest.displayName;
    if (result === null || result === undefined) return false;

    const displayName = normaliseDisplayName(result);
    if (!displayName) {
      setStatus('Enter a template name.', 'warning');
      return false;
    }

    try {
      const updated = await updateStoredTemplateDisplayName(templateId, displayName);
      if (!updated) {
        templates = await listStoredTemplates();
        renderWordTemplateUi();
        setStatus('The Word template is no longer available.', 'warning');
        return false;
      }
      templates = await listStoredTemplates();
      renderWordTemplateUi();
      setStatus(`Renamed Word template to "${displayName}".`, 'ok');
      return true;
    } catch (error) {
      setStatus('Could not rename Word template.', 'danger');
      console.error(error);
      return false;
    }
  }

  async function deleteWordTemplate(templateId) {
    const template = findTemplate(templateId);
    if (!template) return false;

    const confirmed = await confirmAction(
      `Delete “${template.manifest.displayName}”? This removes the saved template from Local Docs Studio. The original DOCX file on your computer will not be affected.`,
      {
        title: 'Delete Word template',
        kicker: 'Word template',
        confirmLabel: 'Delete',
        danger: true,
      },
    );
    if (!confirmed) return false;

    try {
      await deleteStoredTemplate(templateId);
      templates = templates.filter((item) => item?.manifest?.id !== templateId);
      const wasSelected = selectedTemplateId === templateId;
      if (wasSelected) {
        setSelectedTemplateId(DEFAULT_WORD_TEMPLATE_ID, { render: false });
      }
      renderWordTemplateUi();
      setStatus(
        wasSelected
          ? `Deleted Word template "${template.manifest.displayName}". Default Word export is now selected.`
          : `Deleted Word template "${template.manifest.displayName}".`,
        'ok',
      );
      return true;
    } catch (error) {
      setStatus('Could not delete Word template.', 'danger');
      console.error(error);
      return false;
    }
  }

  function renderWordTemplateUi() {
    renderWordTemplateOptions();
    renderWordTemplateManagement();
  }

  function renderWordTemplateOptions() {
    const selected = findTemplate(selectedTemplateId);

    if (wordTemplateSelect) {
      wordTemplateSelect.innerHTML = [
        '<option value="">Default</option>',
        ...templates.map(({ manifest }) => `<option value="${escapeHtml(manifest.id)}">${escapeHtml(manifest.displayName)}</option>`),
      ].join('');
      wordTemplateSelect.value = selected ? selected.manifest.id : DEFAULT_WORD_TEMPLATE_ID;
    }

    if (wordTemplateSummary) {
      wordTemplateSummary.textContent = selected
        ? `Selected for Word export: ${selected.manifest.displayName}. ${describeTemplateCapabilities(selected.manifest.capabilities)}`
        : 'No template selected. Default Word export uses the built-in document styling.';
    }
  }

  function renderWordTemplateManagement() {
    if (!wordTemplateManagementList) return;

    wordTemplateManagementList.replaceChildren();
    wordTemplateManagementList.appendChild(createManagementEntry({
      manifest: {
        id: DEFAULT_WORD_TEMPLATE_ID,
        displayName: 'No template / Default Word export',
      },
      isDefault: true,
    }));

    if (!templates.length) {
      const empty = document.createElement('p');
      empty.className = 'word-template-empty';
      empty.textContent = 'No Word templates imported yet. Import a DOCX template to preserve its styles, headers, footers, and page layout during Word export.';
      wordTemplateManagementList.appendChild(empty);
      return;
    }

    templates.forEach((template) => {
      if (template?.manifest) wordTemplateManagementList.appendChild(createManagementEntry(template));
    });
  }

  function createManagementEntry(template) {
    const manifest = template.manifest;
    const isDefault = Boolean(template.isDefault);
    const isSelected = isDefault ? !selectedTemplateId : selectedTemplateId === manifest.id;
    const entry = document.createElement('article');
    entry.className = `word-template-entry${isSelected ? ' is-selected' : ''}${isDefault ? ' is-default' : ''}`;
    entry.dataset.wordTemplateEntry = 'true';
    entry.dataset.templateId = manifest.id;
    entry.setAttribute('role', 'listitem');
    entry.setAttribute('aria-current', String(isSelected));

    const heading = document.createElement('div');
    heading.className = 'word-template-entry-heading';
    const title = document.createElement('h3');
    title.textContent = manifest.displayName;
    title.title = manifest.displayName;
    const state = document.createElement('span');
    state.className = `word-template-entry-state${isSelected ? ' is-selected' : ''}`;
    state.textContent = isSelected ? 'Selected' : isDefault ? 'Default' : 'Available';
    heading.append(title, state);

    const metadata = document.createElement('div');
    metadata.className = 'word-template-entry-metadata';
    if (isDefault) {
      metadata.textContent = 'Uses the built-in document styling. No imported template is stored for this option.';
    } else {
      const originalFileName = getOriginalFileName(manifest);
      const importedAt = formatImportedAt(manifest.importedAt || manifest.createdAt);
      if (originalFileName) metadata.appendChild(createMetadataLine('Original file', originalFileName));
      if (importedAt) metadata.appendChild(createMetadataLine('Imported', importedAt));
    }

    const actions = document.createElement('div');
    actions.className = 'word-template-entry-actions';
    const selectButton = createManagementActionButton(
      isSelected ? 'Selected' : isDefault ? 'Use default' : 'Use template',
      'select',
      manifest.id,
    );
    selectButton.setAttribute('aria-pressed', String(isSelected));
    selectButton.title = isDefault ? 'Use default Word export' : `Use ${manifest.displayName}`;
    actions.appendChild(selectButton);

    if (!isDefault) {
      const renameButton = createManagementActionButton('Rename', 'rename', manifest.id);
      renameButton.title = `Rename ${manifest.displayName}`;
      const deleteButton = createManagementActionButton('Delete', 'delete', manifest.id);
      deleteButton.classList.add('danger');
      deleteButton.title = `Delete ${manifest.displayName}`;
      actions.append(renameButton, deleteButton);
    }

    entry.append(heading, metadata, actions);
    return entry;
  }

  function createMetadataLine(label, value) {
    const line = document.createElement('span');
    const strong = document.createElement('strong');
    strong.textContent = `${label}:`;
    line.append(strong, document.createTextNode(` ${value}`));
    return line;
  }

  function createManagementActionButton(label, action, templateId) {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = label;
    button.dataset.wordTemplateAction = action;
    button.dataset.templateId = templateId;
    return button;
  }

  async function handleWordTemplateManagementAction(event) {
    const button = event.target.closest('[data-word-template-action]');
    if (!button || !wordTemplateManagementList?.contains(button) || managementBusy) return;

    const action = button.dataset.wordTemplateAction;
    const templateId = button.dataset.templateId || DEFAULT_WORD_TEMPLATE_ID;
    managementBusy = true;
    try {
      if (action === 'select') selectWordTemplate(templateId, { announce: true });
      if (action === 'rename') await renameWordTemplate(templateId);
      if (action === 'delete') await deleteWordTemplate(templateId);
    } finally {
      managementBusy = false;
    }
  }

  function repairMissingSelection(message = '') {
    setSelectedTemplateId(DEFAULT_WORD_TEMPLATE_ID, { render: true });
    if (message) setStatus(message, 'warning');
  }

  function setSelectedTemplateId(templateId, options = {}) {
    selectedTemplateId = normaliseTemplateId(templateId);
    persistSelectedTemplateId(selectedTemplateId);
    if (options.render !== false) renderWordTemplateUi();
  }

  function findTemplate(templateId) {
    const id = normaliseTemplateId(templateId);
    return id ? templates.find((template) => template?.manifest?.id === id) || null : null;
  }

  function hasTemplate(templateId) {
    return Boolean(findTemplate(templateId));
  }

  return {
    initWordTemplates,
    installWordTemplateHandlers,
    openWordTemplateManagement,
    importWordTemplateFromFile,
    getSelectedWordTemplatePack,
    getWordTemplateSummaries,
    selectWordTemplate,
    renameWordTemplate,
    deleteWordTemplate,
  };
}

export async function extractWordTemplatePack(file, options = {}) {
  const sourceBytes = new Uint8Array(await file.arrayBuffer());
  const entries = await readZipEntries(sourceBytes);
  const sourceName = file.name || 'word-template.docx';
  const displayName = normaliseDisplayName(options.displayName || getTemplateDisplayName(sourceName));
  if (!displayName) throw new Error('The Word template needs a display name.');
  const id = normaliseTemplateId(options.id) || createTemplateId();
  const importedAt = options.importedAt || new Date().toISOString();
  const templateModel = analyseWordTemplatePackage(entries);
  const boundaryReferences = templateModel.selectedSection.headerFooterReferences || [];
  const capabilities = {
    styles: Boolean(templateModel.stylesPath),
    numbering: Boolean(templateModel.numberingPath),
    theme: Boolean(templateModel.themePath),
    settings: Boolean(templateModel.settingsPath),
    fontTable: Boolean(templateModel.fontTablePath),
    headers: boundaryReferences.some((reference) => reference.kind === 'header'),
    footers: boundaryReferences.some((reference) => reference.kind === 'footer'),
    media: [...entries.keys()].some((name) => /^word\/media\//i.test(name)),
    sections: templateModel.sectionCount,
    tablePrototype: Boolean(templateModel.tablePrototype),
  };

  const manifest = {
    id,
    templateId: id,
    displayName,
    originalFileName: sourceName,
    sourceFilename: sourceName,
    importedAt,
    createdAt: importedAt,
    version: WORD_TEMPLATE_PACK_VERSION,
    capabilities,
    styleIds: templateModel.styleIds,
    styles: templateModel.styles,
    semanticStyleMapping: templateModel.semanticStyleMapping,
    templateModel,
  };

  return {
    manifest,
    templateDocx: sourceBytes,
  };
}

async function listStoredTemplates() {
  let db = null;
  try {
    db = await openTemplateDb();
    const transaction = db.transaction(WORD_TEMPLATE_STORE, 'readonly');
    const store = transaction.objectStore(WORD_TEMPLATE_STORE);
    const [records, keys] = await Promise.all([
      idbRequest(store.getAll()),
      idbRequest(store.getAllKeys()),
    ]);
    db.close();
    db = null;

    const migrated = records
      .map((record, index) => ({ ...migrateStoredTemplateRecord(record), oldKey: keys[index] }))
      .filter((entry) => entry.record);
    const changed = migrated.filter((entry) => entry.changed);
    if (changed.length) {
      try {
        await persistMigratedTemplates(changed);
      } catch (error) {
        console.warn('Word template metadata migration could not be persisted yet.', error);
      }
    }

    return migrated
      .map(({ record }) => record)
      .sort((left, right) => String(left.manifest.displayName).localeCompare(String(right.manifest.displayName)));
  } catch {
    try { db?.close(); } catch { /* ignore storage cleanup failures */ }
    return [];
  }
}

async function persistMigratedTemplates(entries) {
  const db = await openTemplateDb();
  try {
    const transaction = db.transaction(WORD_TEMPLATE_STORE, 'readwrite');
    const store = transaction.objectStore(WORD_TEMPLATE_STORE);
    entries.forEach(({ record, oldKey }) => {
      if (oldKey !== undefined && oldKey !== record.manifest.id) store.delete(oldKey);
      store.put(record);
    });
    await idbTransactionDone(transaction);
  } finally {
    db.close();
  }
}

function migrateStoredTemplateRecord(record) {
  if (!record || typeof record !== 'object' || !record.manifest || typeof record.manifest !== 'object') {
    return { record: null, changed: false };
  }

  const sourceManifest = record.manifest;
  const manifest = { ...sourceManifest };
  let changed = false;
  const setIfDifferent = (key, value) => {
    if (value === undefined || value === null || manifest[key] === value) return;
    manifest[key] = value;
    changed = true;
  };

  const originalFileName = firstNonEmpty(
    manifest.originalFileName,
    manifest.sourceFilename,
    manifest.sourceFileName,
    manifest.originalFilename,
    manifest.filename,
    manifest.fileName,
  );
  const existingId = firstNonEmpty(manifest.id, manifest.templateId);
  const displayName = normaliseDisplayName(
    firstNonEmpty(manifest.displayName, manifest.name)
      || (originalFileName ? getTemplateDisplayName(originalFileName) : '')
      || existingId,
  );
  const id = existingId || createLegacyTemplateId({ ...manifest, displayName, originalFileName }, record);
  const importedAt = firstNonEmpty(manifest.importedAt, manifest.createdAt);

  setIfDifferent('id', id);
  setIfDifferent('templateId', id);
  setIfDifferent('displayName', displayName || 'Word template');
  if (originalFileName) {
    setIfDifferent('originalFileName', originalFileName);
    setIfDifferent('sourceFilename', originalFileName);
  }
  if (importedAt) {
    setIfDifferent('importedAt', importedAt);
    setIfDifferent('createdAt', importedAt);
  }

  return {
    record: changed ? { ...record, manifest } : record,
    changed,
  };
}

async function getStoredTemplate(id) {
  const db = await openTemplateDb();
  try {
    const transaction = db.transaction(WORD_TEMPLATE_STORE, 'readonly');
    const record = await idbRequest(transaction.objectStore(WORD_TEMPLATE_STORE).get(id));
    return record || null;
  } finally {
    db.close();
  }
}

async function saveWordTemplatePack(pack) {
  const db = await openTemplateDb();
  try {
    const transaction = db.transaction(WORD_TEMPLATE_STORE, 'readwrite');
    transaction.objectStore(WORD_TEMPLATE_STORE).put(pack);
    await idbTransactionDone(transaction);
  } finally {
    db.close();
  }
}

async function updateStoredTemplateDisplayName(id, displayName) {
  const record = await getStoredTemplate(id);
  if (!record) return null;
  const updated = {
    ...record,
    manifest: {
      ...record.manifest,
      id,
      templateId: id,
      displayName,
    },
  };
  await saveWordTemplatePack(updated);
  return updated;
}

async function deleteStoredTemplate(id) {
  const db = await openTemplateDb();
  try {
    const transaction = db.transaction(WORD_TEMPLATE_STORE, 'readwrite');
    transaction.objectStore(WORD_TEMPLATE_STORE).delete(id);
    await idbTransactionDone(transaction);
  } finally {
    db.close();
  }
}

function openTemplateDb() {
  return openObjectStoreDb(WORD_TEMPLATE_DB_NAME, WORD_TEMPLATE_DB_VERSION, [
    {
      name: WORD_TEMPLATE_STORE,
      keyPath: 'manifest.id',
      indexes: [
        { name: 'displayName', keyPath: 'manifest.displayName' },
        { name: 'createdAt', keyPath: 'manifest.createdAt' },
      ],
    },
  ]);
}

function isDocxFile(file) {
  return /\.docx$/i.test(file?.name || '') || file?.type === DOCX_MIME;
}

function getTemplateDisplayName(filename) {
  return String(filename || 'Word template')
    .replace(/\.[^.]+$/, '')
    .replace(/\s+/g, ' ')
    .trim() || 'Word template';
}

function normaliseDisplayName(value) {
  return String(value || '').replace(/\s+/g, ' ').trim();
}

function createTemplateId() {
  const uuid = globalThis.crypto?.randomUUID?.();
  if (uuid) return `template-${uuid}`;
  return `template-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

function createLegacyTemplateId(manifest, record) {
  const bytes = toUint8Array(record?.templateDocx);
  const source = `${manifest.displayName}|${manifest.originalFileName || ''}|${manifest.createdAt || ''}|${bytes?.byteLength || 0}`;
  let hash = 2166136261;
  for (let index = 0; index < source.length; index += 1) {
    hash ^= source.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  if (bytes) {
    for (let index = 0; index < bytes.length; index += 1) {
      hash ^= bytes[index];
      hash = Math.imul(hash, 16777619);
    }
  }
  return `legacy-${(hash >>> 0).toString(36)}`;
}

function normaliseTemplateId(value) {
  return String(value || '').trim();
}

function readStoredSelection() {
  try {
    return normaliseTemplateId(localStorage.getItem(WORD_TEMPLATE_SELECTION_KEY));
  } catch {
    return DEFAULT_WORD_TEMPLATE_ID;
  }
}

function persistSelectedTemplateId(id) {
  try {
    if (id) localStorage.setItem(WORD_TEMPLATE_SELECTION_KEY, id);
    else localStorage.removeItem(WORD_TEMPLATE_SELECTION_KEY);
  } catch {
    // The in-memory selection still keeps this session safe when localStorage is unavailable.
  }
}

function firstNonEmpty(...values) {
  return values.map((value) => String(value || '').trim()).find(Boolean) || '';
}

function getOriginalFileName(manifest) {
  return firstNonEmpty(manifest.originalFileName, manifest.sourceFilename, manifest.sourceFileName);
}

function formatImportedAt(value) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  try {
    return new Intl.DateTimeFormat('en-GB', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    }).format(date);
  } catch {
    return date.toISOString().slice(0, 10);
  }
}

function toUint8Array(value) {
  if (value instanceof Uint8Array) return value;
  if (value instanceof ArrayBuffer) return new Uint8Array(value);
  if (ArrayBuffer.isView(value)) return new Uint8Array(value.buffer, value.byteOffset, value.byteLength);
  return null;
}

function isVisibleFocusTarget(element) {
  if (!element || !document.contains(element)) return false;
  const style = window.getComputedStyle(element);
  return style.display !== 'none'
    && style.visibility !== 'hidden'
    && element.getClientRects().length > 0;
}

function describeTemplateCapabilities(capabilities = {}) {
  const labels = [];
  if (capabilities.styles) labels.push('styles');
  if (capabilities.numbering) labels.push('numbering');
  if (capabilities.theme) labels.push('theme');
  if (capabilities.headers) labels.push('headers');
  if (capabilities.footers) labels.push('footers');
  if (capabilities.media) labels.push('media');
  if (capabilities.fontTable) labels.push('font table');
  if (capabilities.settings) labels.push('settings');
  if (capabilities.sections) labels.push('page layout');
  if (capabilities.tablePrototype) labels.push('table prototype');
  return labels.length
    ? `Template includes ${labels.join(', ')}.`
    : 'Template imported without optional Word identity parts.';
}
