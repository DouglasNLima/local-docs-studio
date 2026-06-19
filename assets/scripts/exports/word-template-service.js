import { readZipEntries, decodeZipText } from '../utils/zip.js';
import { idbRequest, idbTransactionDone, openObjectStoreDb } from '../utils/idb.js';
import { escapeHtml, escapeXml, sanitiseFileName, slugify } from '../utils/format.js';

export const DEFAULT_WORD_TEMPLATE_ID = '';

const WORD_TEMPLATE_DB_NAME = 'local-docs-studio-word-templates';
const WORD_TEMPLATE_DB_VERSION = 1;
const WORD_TEMPLATE_STORE = 'templates';
const WORD_TEMPLATE_SELECTION_KEY = 'lensDocs.wordExport.templateId';
const WORD_TEMPLATE_PACK_VERSION = 1;

const WORD_REL_NS = 'http://schemas.openxmlformats.org/package/2006/relationships';
const WORD_MAIN_NS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main';

const TEMPLATE_PART_CONTENT_TYPES = {
  'word/styles.xml': 'application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml',
  'word/numbering.xml': 'application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml',
  'word/settings.xml': 'application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml',
  'word/theme/theme1.xml': 'application/vnd.openxmlformats-officedocument.theme+xml',
};

const HEADER_CONTENT_TYPE = 'application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml';
const FOOTER_CONTENT_TYPE = 'application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml';

const STYLE_MAPPING_KEYS = [
  'documentTitle',
  'heading1',
  'heading2',
  'heading3',
  'body',
  'table',
  'code',
  'quote',
  'caption',
];

const DEFAULT_STYLE_MAPPING = {
  documentTitle: 'Title',
  heading1: 'Heading1',
  heading2: 'Heading2',
  heading3: 'Heading3',
  body: 'Normal',
  table: 'TableGrid',
  code: 'NoSpacing',
  quote: 'Quote',
  caption: 'Caption',
};

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
  const parts = {};
  const sourceName = file.name || 'word-template.docx';
  const displayName = (options.displayName || getTemplateDisplayName(sourceName)).trim();
  const id = options.id || createTemplateId(displayName);

  const directParts = [
    'word/styles.xml',
    'word/numbering.xml',
    'word/theme/theme1.xml',
    'word/settings.xml',
  ];

  directParts.forEach((name) => copyPart(entries, parts, name));
  copyMatchingParts(entries, parts, /^word\/header\d+\.xml$/i);
  copyMatchingParts(entries, parts, /^word\/footer\d+\.xml$/i);
  copyMatchingParts(entries, parts, /^word\/_rels\/header\d+\.xml\.rels$/i);
  copyMatchingParts(entries, parts, /^word\/_rels\/footer\d+\.xml\.rels$/i);
  copyReferencedHeaderFooterMedia(entries, parts);

  const styleInfo = parseStyleInfo(parts['word/styles.xml']);
  const capabilities = {
    styles: Boolean(parts['word/styles.xml']),
    numbering: Boolean(parts['word/numbering.xml']),
    theme: Boolean(parts['word/theme/theme1.xml']),
    headers: Object.keys(parts).some((name) => /^word\/header\d+\.xml$/i.test(name)),
    footers: Object.keys(parts).some((name) => /^word\/footer\d+\.xml$/i.test(name)),
    media: Object.keys(parts).some((name) => /^word\/media\//i.test(name)),
  };

  const manifest = {
    id,
    displayName,
    createdAt: new Date().toISOString(),
    sourceFilename: sourceName,
    version: WORD_TEMPLATE_PACK_VERSION,
    capabilities,
    styleIds: styleInfo.map((style) => style.id),
    styles: styleInfo,
    semanticStyleMapping: buildSemanticStyleMapping(styleInfo),
  };

  return {
    manifest,
    templateDocx: sourceBytes,
    parts,
  };
}

export function buildWordTemplatePackageAdditions(templatePack) {
  if (!templatePack?.manifest || !templatePack.parts) {
    return {
      files: [],
      contentTypeOverrides: [],
      documentRelationshipEntries: [],
      sectPrReferences: [],
      styleMapping: { ...DEFAULT_STYLE_MAPPING },
    };
  }

  const parts = templatePack.parts;
  const files = Object.entries(parts).map(([name, data]) => ({ name, data }));
  const contentTypeOverrides = buildTemplateContentTypeOverrides(parts);
  const documentRelationshipEntries = [];
  const sectPrReferences = [];

  if (parts['word/styles.xml']) {
    documentRelationshipEntries.push(buildDocumentRelationship('rIdTemplateStyles', 'styles', 'styles.xml'));
  }
  if (parts['word/numbering.xml']) {
    documentRelationshipEntries.push(buildDocumentRelationship('rIdTemplateNumbering', 'numbering', 'numbering.xml'));
  }
  if (parts['word/settings.xml']) {
    documentRelationshipEntries.push(buildDocumentRelationship('rIdTemplateSettings', 'settings', 'settings.xml'));
  }
  if (parts['word/theme/theme1.xml']) {
    documentRelationshipEntries.push(buildDocumentRelationship('rIdTemplateTheme', 'theme', 'theme/theme1.xml'));
  }

  collectTemplateBoundaryParts(parts, 'header').forEach((partName, index) => {
    const relationshipId = `rIdTemplateHeader${index + 1}`;
    documentRelationshipEntries.push(buildDocumentRelationship(relationshipId, 'header', partName.replace(/^word\//, '')));
    sectPrReferences.push(`<w:headerReference w:type="${index === 0 ? 'default' : 'first'}" r:id="${relationshipId}"/>`);
  });

  collectTemplateBoundaryParts(parts, 'footer').forEach((partName, index) => {
    const relationshipId = `rIdTemplateFooter${index + 1}`;
    documentRelationshipEntries.push(buildDocumentRelationship(relationshipId, 'footer', partName.replace(/^word\//, '')));
    sectPrReferences.push(`<w:footerReference w:type="${index === 0 ? 'default' : 'first'}" r:id="${relationshipId}"/>`);
  });

  return {
    files,
    contentTypeOverrides,
    documentRelationshipEntries,
    sectPrReferences,
    styleMapping: resolveStyleMapping(templatePack.manifest),
  };
}

export function resolveStyleMapping(manifest) {
  const available = new Set(manifest?.styleIds || []);
  const mapping = { ...DEFAULT_STYLE_MAPPING, ...(manifest?.semanticStyleMapping || {}) };
  STYLE_MAPPING_KEYS.forEach((key) => {
    if (!mapping[key] || (available.size && !available.has(mapping[key]) && !isStandardFallbackStyle(mapping[key]))) {
      mapping[key] = DEFAULT_STYLE_MAPPING[key];
    }
  });
  return mapping;
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

function copyPart(entries, parts, name) {
  const bytes = entries.get(name);
  if (bytes) parts[name] = bytes;
}

function copyMatchingParts(entries, parts, pattern) {
  entries.forEach((bytes, name) => {
    if (pattern.test(name)) parts[name] = bytes;
  });
}

function copyReferencedHeaderFooterMedia(entries, parts) {
  Object.entries(parts)
    .filter(([name]) => /^word\/_rels\/(header|footer)\d+\.xml\.rels$/i.test(name))
    .forEach(([, bytes]) => {
      const relsXml = decodeZipText(bytes);
      const targets = collectRelationshipTargets(relsXml);
      targets.forEach((target) => {
        if (target.mode === 'External') return;
        const mediaPath = normaliseWordRelationshipTarget(target.target);
        if (!/^word\/media\//i.test(mediaPath)) return;
        copyPart(entries, parts, mediaPath);
      });
    });
}

function collectRelationshipTargets(xml) {
  const document = parseXml(xml);
  if (!document) return [];
  return [...document.getElementsByTagNameNS(WORD_REL_NS, 'Relationship')]
    .map((relationship) => ({
      target: relationship.getAttribute('Target') || '',
      mode: relationship.getAttribute('TargetMode') || '',
    }))
    .filter((relationship) => relationship.target);
}

function normaliseWordRelationshipTarget(target) {
  const cleaned = target.replace(/\\/g, '/').replace(/^\/+/, '');
  if (cleaned.startsWith('../')) return `word/${cleaned.replace(/^\.\.\//, '')}`;
  if (cleaned.startsWith('word/')) return cleaned;
  return `word/${cleaned}`;
}

function parseStyleInfo(stylesBytes) {
  if (!stylesBytes) return [];
  const document = parseXml(decodeZipText(stylesBytes));
  if (!document) return [];

  return [...document.getElementsByTagNameNS(WORD_MAIN_NS, 'style')]
    .map((style) => {
      const nameElement = style.getElementsByTagNameNS(WORD_MAIN_NS, 'name')[0];
      return {
        id: style.getAttributeNS(WORD_MAIN_NS, 'styleId') || style.getAttribute('w:styleId') || '',
        name: nameElement?.getAttributeNS(WORD_MAIN_NS, 'val') || nameElement?.getAttribute('w:val') || '',
        type: style.getAttributeNS(WORD_MAIN_NS, 'type') || style.getAttribute('w:type') || '',
      };
    })
    .filter((style) => style.id);
}

function buildSemanticStyleMapping(styles) {
  const byId = new Map(styles.map((style) => [normaliseStyleKey(style.id), style.id]));
  const byName = new Map(styles.map((style) => [normaliseStyleKey(style.name), style.id]));
  const find = (...candidates) => candidates
    .map((candidate) => byId.get(normaliseStyleKey(candidate)) || byName.get(normaliseStyleKey(candidate)))
    .find(Boolean);

  return {
    documentTitle: find('Title', 'DocumentTitle') || DEFAULT_STYLE_MAPPING.documentTitle,
    heading1: find('Heading1', 'Heading 1') || DEFAULT_STYLE_MAPPING.heading1,
    heading2: find('Heading2', 'Heading 2') || DEFAULT_STYLE_MAPPING.heading2,
    heading3: find('Heading3', 'Heading 3') || DEFAULT_STYLE_MAPPING.heading3,
    body: find('Normal', 'BodyText', 'Body Text') || DEFAULT_STYLE_MAPPING.body,
    table: find('TableGrid', 'Table Grid') || DEFAULT_STYLE_MAPPING.table,
    code: find('Code', 'NoSpacing', 'No Spacing') || DEFAULT_STYLE_MAPPING.code,
    quote: find('Quote', 'IntenseQuote', 'Intense Quote') || DEFAULT_STYLE_MAPPING.quote,
    caption: find('Caption') || DEFAULT_STYLE_MAPPING.caption,
  };
}

function normaliseStyleKey(value) {
  return String(value || '').toLowerCase().replace(/[\s_-]+/g, '');
}

function parseXml(xml) {
  try {
    const document = new DOMParser().parseFromString(xml, 'application/xml');
    return document.querySelector('parsererror') ? null : document;
  } catch {
    return null;
  }
}

function buildTemplateContentTypeOverrides(parts) {
  const overrides = [];
  Object.keys(TEMPLATE_PART_CONTENT_TYPES).forEach((partName) => {
    if (parts[partName]) {
      overrides.push(`<Override PartName="/${partName}" ContentType="${TEMPLATE_PART_CONTENT_TYPES[partName]}"/>`);
    }
  });
  collectTemplateBoundaryParts(parts, 'header').forEach((partName) => {
    overrides.push(`<Override PartName="/${partName}" ContentType="${HEADER_CONTENT_TYPE}"/>`);
  });
  collectTemplateBoundaryParts(parts, 'footer').forEach((partName) => {
    overrides.push(`<Override PartName="/${partName}" ContentType="${FOOTER_CONTENT_TYPE}"/>`);
  });
  return overrides;
}

function collectTemplateBoundaryParts(parts, type) {
  const pattern = new RegExp(`^word/${type}\\d+\\.xml$`, 'i');
  return Object.keys(parts)
    .filter((name) => pattern.test(name))
    .sort((left, right) => left.localeCompare(right, undefined, { numeric: true }));
}

function buildDocumentRelationship(id, type, target) {
  return `  <Relationship Id="${id}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/${type}" Target="${escapeXml(target)}"/>`;
}

function isStandardFallbackStyle(styleId) {
  return Object.values(DEFAULT_STYLE_MAPPING).includes(styleId);
}

function describeTemplateCapabilities(capabilities) {
  const labels = [];
  if (capabilities.styles) labels.push('styles');
  if (capabilities.numbering) labels.push('numbering');
  if (capabilities.theme) labels.push('theme');
  if (capabilities.headers) labels.push('headers');
  if (capabilities.footers) labels.push('footers');
  if (capabilities.media) labels.push('media');
  return labels.length
    ? `Template includes ${labels.join(', ')}.`
    : 'Template imported without optional Word identity parts.';
}

export function normaliseWordTemplatePartName(value) {
  return sanitiseFileName(value || '').replace(/\\/g, '/');
}
