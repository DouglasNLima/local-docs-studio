import { readZipEntries } from '../utils/zip.js';
import { idbRequest, idbTransactionDone, openObjectStoreDb } from '../utils/idb.js';
import { escapeHtml, slugify } from '../utils/format.js';
import {
  WORD_TEMPLATE_PACK_VERSION,
  analyseWordTemplatePackage,
} from './word-template-package.js';

export const DEFAULT_WORD_TEMPLATE_ID = '';

const WORD_TEMPLATE_DB_NAME = 'local-docs-studio-word-templates';
const WORD_TEMPLATE_DB_VERSION = 1;
const WORD_TEMPLATE_STORE = 'templates';
const WORD_TEMPLATE_SELECTION_KEY = 'lensDocs.wordExport.templateId';
export function createWordTemplateService({
  dom,
  callbacks,
}) {
  const {
    wordTemplateInput,
    wordTemplateImportButton,
    wordTemplateSelect,
    wordTemplateSummary,
  } = dom;
  const {
    closeOpenMenus,
    promptForText,
    setStatus,
  } = callbacks;

  let templates = [];
  let selectedTemplateId = localStorage.getItem(WORD_TEMPLATE_SELECTION_KEY) || DEFAULT_WORD_TEMPLATE_ID;

  async function initWordTemplates() {
    templates = await listStoredTemplates();
    if (selectedTemplateId && !templates.some((template) => template.manifest.id === selectedTemplateId)) {
      selectedTemplateId = DEFAULT_WORD_TEMPLATE_ID;
      localStorage.removeItem(WORD_TEMPLATE_SELECTION_KEY);
    }
    renderWordTemplateOptions();
  }

  function installWordTemplateHandlers() {
    wordTemplateImportButton?.addEventListener('click', () => {
      wordTemplateInput?.click();
    });

    wordTemplateInput?.addEventListener('change', async () => {
      const [file] = [...(wordTemplateInput.files || [])];
      wordTemplateInput.value = '';
      if (!file) return;
      await importWordTemplateFromFile(file);
    });

    wordTemplateSelect?.addEventListener('change', () => {
      selectedTemplateId = wordTemplateSelect.value || DEFAULT_WORD_TEMPLATE_ID;
      if (selectedTemplateId) {
        localStorage.setItem(WORD_TEMPLATE_SELECTION_KEY, selectedTemplateId);
      } else {
        localStorage.removeItem(WORD_TEMPLATE_SELECTION_KEY);
      }
      renderWordTemplateOptions();
    });
  }

  async function importWordTemplateFromFile(file, options = {}) {
    if (!isDocxFile(file)) {
      setStatus('Choose a .docx file to import as a Word template.', 'warning');
      return null;
    }

    try {
      const fallbackName = getTemplateDisplayName(file.name);
      const displayName = options.displayName || await promptForText?.({
        title: 'Import Word template',
        message: 'Save this .docx as a reusable local Word export template.',
        label: 'Template name',
        value: fallbackName,
        confirmLabel: 'Import template',
        validate: (value) => value ? '' : 'Enter a template name.',
      }) || fallbackName;

      if (!displayName) return null;

      const pack = await extractWordTemplatePack(file, { displayName });
      await saveWordTemplatePack(pack);
      templates = await listStoredTemplates();
      selectedTemplateId = pack.manifest.id;
      localStorage.setItem(WORD_TEMPLATE_SELECTION_KEY, selectedTemplateId);
      renderWordTemplateOptions();
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
    return await getStoredTemplate(selectedTemplateId);
  }

  function getWordTemplateSummaries() {
    return templates.map((template) => template.manifest);
  }

  function renderWordTemplateOptions() {
    if (!wordTemplateSelect) return;

    const previous = selectedTemplateId;
    wordTemplateSelect.innerHTML = [
      '<option value="">Default</option>',
      ...templates.map(({ manifest }) => `<option value="${escapeHtml(manifest.id)}">${escapeHtml(manifest.displayName)}</option>`),
    ].join('');
    wordTemplateSelect.value = previous && templates.some((template) => template.manifest.id === previous) ? previous : DEFAULT_WORD_TEMPLATE_ID;

    if (wordTemplateSummary) {
      const selected = templates.find((template) => template.manifest.id === wordTemplateSelect.value);
      wordTemplateSummary.textContent = selected
        ? describeTemplateCapabilities(selected.manifest.capabilities)
        : 'Default export uses the built-in document styling.';
    }
  }

  return {
    initWordTemplates,
    installWordTemplateHandlers,
    importWordTemplateFromFile,
    getSelectedWordTemplatePack,
    getWordTemplateSummaries,
  };
}

export async function extractWordTemplatePack(file, options = {}) {
  const sourceBytes = new Uint8Array(await file.arrayBuffer());
  const entries = await readZipEntries(sourceBytes);
  const sourceName = file.name || 'word-template.docx';
  const displayName = (options.displayName || getTemplateDisplayName(sourceName)).trim();
  const id = options.id || createTemplateId(displayName);
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
    displayName,
    createdAt: new Date().toISOString(),
    sourceFilename: sourceName,
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
  try {
    const db = await openTemplateDb();
    const transaction = db.transaction(WORD_TEMPLATE_STORE, 'readonly');
    const records = await idbRequest(transaction.objectStore(WORD_TEMPLATE_STORE).getAll());
    db.close();
    return records.sort((left, right) => left.manifest.displayName.localeCompare(right.manifest.displayName));
  } catch {
    return [];
  }
}

async function getStoredTemplate(id) {
  const db = await openTemplateDb();
  const transaction = db.transaction(WORD_TEMPLATE_STORE, 'readonly');
  const record = await idbRequest(transaction.objectStore(WORD_TEMPLATE_STORE).get(id));
  db.close();
  return record || null;
}

async function saveWordTemplatePack(pack) {
  const db = await openTemplateDb();
  const transaction = db.transaction(WORD_TEMPLATE_STORE, 'readwrite');
  transaction.objectStore(WORD_TEMPLATE_STORE).put(pack);
  await idbTransactionDone(transaction);
  db.close();
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
  return /\.docx$/i.test(file?.name || '') || file?.type === 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
}

function getTemplateDisplayName(filename) {
  return String(filename || 'Word template')
    .replace(/\.[^.]+$/, '')
    .replace(/[-_]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim() || 'Word template';
}

function createTemplateId(displayName) {
  const stem = slugify(displayName) || 'word-template';
  const suffix = Date.now().toString(36);
  return `${stem}-${suffix}`;
}

function describeTemplateCapabilities(capabilities) {
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
