import { decodeZipText, readZipEntries, createCompressedZipBlob } from '../utils/zip.js';

export const WORD_TEMPLATE_PACK_VERSION = 3;

export const DEFAULT_WORD_STYLE_MAPPING = Object.freeze({
  documentTitle: 'Title',
  heading1: 'Heading1',
  heading2: 'Heading2',
  heading3: 'Heading3',
  heading4: 'Heading4',
  heading5: 'Heading5',
  heading6: 'Heading6',
  body: 'Normal',
  listParagraph: 'ListParagraph',
  table: 'TableGrid',
  code: 'NoSpacing',
  inlineCode: '',
  quote: 'Quote',
  caption: 'Caption',
});

const WORD_MAIN_NS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main';
const WORD_REL_NS = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships';
const PACKAGE_REL_NS = 'http://schemas.openxmlformats.org/package/2006/relationships';
const CONTENT_TYPES_NS = 'http://schemas.openxmlformats.org/package/2006/content-types';
const CORE_PROPERTIES_NS = 'http://schemas.openxmlformats.org/package/2006/metadata/core-properties';
const DC_NS = 'http://purl.org/dc/elements/1.1/';
const DCTERMS_NS = 'http://purl.org/dc/terms/';
const XML_NS = 'http://www.w3.org/XML/1998/namespace';

const OFFICE_REL_BASE = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships/';
const PACKAGE_REL_BASE = 'http://schemas.openxmlformats.org/package/2006/relationships/';

const REQUIRED_DOCUMENT_NAMESPACES = {
  w: WORD_MAIN_NS,
  r: WORD_REL_NS,
  wp: 'http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing',
  a: 'http://schemas.openxmlformats.org/drawingml/2006/main',
  pic: 'http://schemas.openxmlformats.org/drawingml/2006/picture',
};

const IMAGE_CONTENT_TYPES = {
  png: 'image/png',
  svg: 'image/svg+xml',
  jpg: 'image/jpeg',
  jpeg: 'image/jpeg',
  gif: 'image/gif',
  webp: 'image/webp',
};

const SETTINGS_CONTENT_TYPE = 'application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml';

export function analyseWordTemplatePackage(entries) {
  const mainDocumentPath = findMainDocumentPath(entries);
  const documentXml = decodeRequiredXml(entries, mainDocumentPath, 'main document');
  const documentRelationshipsPath = relationshipPartPath(mainDocumentPath);
  const documentRelationships = parseRelationships(entries.get(documentRelationshipsPath));
  const relationshipById = new Map(documentRelationships.map((relationship) => [relationship.id, relationship]));

  const stylesPath = findRelatedPartPath(documentRelationships, mainDocumentPath, 'styles')
    || findExistingPart(entries, 'word/styles.xml');
  const numberingPath = findRelatedPartPath(documentRelationships, mainDocumentPath, 'numbering')
    || findExistingPart(entries, 'word/numbering.xml');
  const settingsPath = findRelatedPartPath(documentRelationships, mainDocumentPath, 'settings')
    || findExistingPart(entries, 'word/settings.xml');
  const fontTablePath = findRelatedPartPath(documentRelationships, mainDocumentPath, 'fontTable')
    || findExistingPart(entries, 'word/fontTable.xml');
  const themePath = findRelatedPartPath(documentRelationships, mainDocumentPath, 'theme')
    || findExistingPart(entries, 'word/theme/theme1.xml');

  const styles = parseStyleInfo(entries.get(stylesPath));
  const styleMapping = buildSemanticStyleMapping(styles);
  const sections = extractDocumentSections(documentXml, relationshipById, mainDocumentPath, entries);
  const selectedSection = selectContentSection(sections);
  const numbering = parseNumbering(entries.get(numberingPath));
  const headingNumbering = buildHeadingNumbering(styleMapping, styles, numbering);
  const listNumbering = findListNumbering(numbering, new Set(
    Object.values(headingNumbering).filter((item) => item?.automatic).map((item) => item.numId),
  ));
  const tableResult = buildTablePrototype(documentXml, selectedSection, styles, styleMapping);
  const resolvedStyleMapping = {
    ...styleMapping,
    table: tableResult.styleId || styleMapping.table,
  };
  const headerFooterPartNames = collectHeaderFooterParts(sections);
  const corePropertiesPath = findRootRelatedPartPath(entries, 'metadata/core-properties')
    || findExistingPart(entries, 'docProps/core.xml');
  const documentTitle = detectDocumentTitle({
    entries,
    documentXml,
    corePropertiesPath,
    styleMapping: resolvedStyleMapping,
    headerFooterPartNames,
  });

  return {
    version: WORD_TEMPLATE_PACK_VERSION,
    mainDocumentPath,
    documentRelationshipsPath,
    stylesPath,
    numberingPath,
    settingsPath,
    fontTablePath,
    themePath,
    corePropertiesPath,
    sectionCount: sections.length,
    selectedSectionIndex: selectedSection.index,
    selectedSection: {
      sectPrXml: selectedSection.sectPrXml,
      usableWidthDxa: selectedSection.usableWidthDxa,
      usableHeightDxa: selectedSection.usableHeightDxa,
      titlePage: selectedSection.titlePage,
      headerFooterReferences: selectedSection.headerFooterReferences,
    },
    styles,
    styleIds: styles.map((style) => style.id),
    semanticStyleMapping: resolvedStyleMapping,
    headingNumbering,
    listNumbering,
    tablePrototype: tableResult.prototype,
    documentTitle,
    packagePartNames: [...entries.keys()].sort(),
  };
}

export async function prepareWordTemplateExport(templatePack) {
  const sourceBytes = toUint8Array(templatePack?.templateDocx);
  if (!sourceBytes?.byteLength) {
    throw new Error('The selected Word template no longer contains its source DOCX package. Import it again.');
  }

  const entries = await readZipEntries(sourceBytes);
  const analysedModel = analyseWordTemplatePackage(entries);
  const storedModel = templatePack?.manifest?.templateModel;
  const model = isCompatibleStoredModel(storedModel, analysedModel)
    ? { ...analysedModel, ...storedModel }
    : analysedModel;
  const documentRelationshipsXml = entries.get(model.documentRelationshipsPath)
    ? decodeZipText(entries.get(model.documentRelationshipsPath))
    : emptyRelationshipsXml();
  const documentRelationshipsDocument = parseRequiredXml(documentRelationshipsXml, 'document relationships');
  const relationshipRoot = documentRelationshipsDocument.documentElement;
  const usedRelationshipIds = new Set(
    [...relationshipRoot.getElementsByTagNameNS(PACKAGE_REL_NS, 'Relationship')]
      .map((relationship) => relationship.getAttribute('Id'))
      .filter(Boolean),
  );
  const usedPartNames = new Set(entries.keys());
  let nextRelationshipNumber = 1;
  let nextDocPrId = findMaximumDocPrId(entries) + 1;
  const generatedRelationships = [];
  const generatedParts = [];
  const numberingDocument = model.numberingPath && entries.has(model.numberingPath)
    ? parseRequiredXml(decodeZipText(entries.get(model.numberingPath)), 'numbering')
    : null;
  const usedNumberingIds = new Set(numberingDocument
    ? [...numberingDocument.getElementsByTagNameNS(WORD_MAIN_NS, 'num')]
      .map((numbering) => wordAttribute(numbering, 'numId'))
      .filter(Boolean)
    : []);
  let nextNumberingId = Math.max(0, ...[...usedNumberingIds].map((value) => Number(value) || 0)) + 1;
  let numberingChanged = false;

  function allocateRelationship(type, target, targetMode = '') {
    let id = `rIdLds${nextRelationshipNumber}`;
    while (usedRelationshipIds.has(id)) {
      nextRelationshipNumber += 1;
      id = `rIdLds${nextRelationshipNumber}`;
    }
    nextRelationshipNumber += 1;
    usedRelationshipIds.add(id);
    generatedRelationships.push({ id, type, target, targetMode });
    return id;
  }

  function allocateMediaPart(preferredName, data, mimeType) {
    const safeName = sanitisePackageFilename(preferredName) || 'image.png';
    const extension = getFilenameExtension(safeName) || extensionForMimeType(mimeType) || 'png';
    const stem = safeName.replace(/\.[^.]+$/, '') || 'image';
    let candidate = `word/media/lds-${stem}.${extension}`;
    let suffix = 2;
    while (usedPartNames.has(candidate)) {
      candidate = `word/media/lds-${stem}-${suffix}.${extension}`;
      suffix += 1;
    }
    usedPartNames.add(candidate);
    const target = relationshipTargetFromPart(model.mainDocumentPath, candidate);
    const relationshipId = allocateRelationship(`${OFFICE_REL_BASE}image`, target);
    const part = {
      name: candidate,
      data,
      mimeType: mimeType || IMAGE_CONTENT_TYPES[extension] || 'application/octet-stream',
    };
    generatedParts.push(part);
    return {
      relationshipId,
      target,
      partName: candidate,
      docPrId: nextDocPrId++,
    };
  }

  function allocateExternalHyperlink(target) {
    return allocateRelationship(`${OFFICE_REL_BASE}hyperlink`, target, 'External');
  }

  function allocateNumberingInstance(baseNumId, start = 1) {
    if (!numberingDocument || !baseNumId) return String(baseNumId || '');
    const source = [...numberingDocument.getElementsByTagNameNS(WORD_MAIN_NS, 'num')]
      .find((numbering) => wordAttribute(numbering, 'numId') === String(baseNumId));
    if (!source) return String(baseNumId);

    while (usedNumberingIds.has(String(nextNumberingId))) nextNumberingId += 1;
    const numId = String(nextNumberingId++);
    usedNumberingIds.add(numId);
    const clone = source.cloneNode(true);
    clone.setAttributeNS(WORD_MAIN_NS, 'w:numId', numId);

    let levelOverride = directChildren(clone, 'lvlOverride')
      .find((override) => numberAttribute(override, 'ilvl', -1) === 0);
    if (!levelOverride) {
      levelOverride = numberingDocument.createElementNS(WORD_MAIN_NS, 'w:lvlOverride');
      levelOverride.setAttributeNS(WORD_MAIN_NS, 'w:ilvl', '0');
      clone.appendChild(levelOverride);
    }
    let startOverride = directChild(levelOverride, 'startOverride');
    if (!startOverride) {
      startOverride = numberingDocument.createElementNS(WORD_MAIN_NS, 'w:startOverride');
      levelOverride.insertBefore(startOverride, levelOverride.firstChild);
    }
    startOverride.setAttributeNS(WORD_MAIN_NS, 'w:val', String(Math.max(1, Number(start) || 1)));
    numberingDocument.documentElement.appendChild(clone);
    numberingChanged = true;
    return numId;
  }

  return {
    entries,
    model,
    documentRelationshipsDocument,
    generatedRelationships,
    generatedParts,
    allocateMediaPart,
    allocateExternalHyperlink,
    allocateNumberingInstance,
    get numberingChanged() {
      return numberingChanged;
    },
    numberingDocument,
  };
}

export async function finaliseWordTemplateExport(context, { bodyXml, title }) {
  const { entries, model, documentRelationshipsDocument } = context;
  const outputEntries = new Map(entries);

  ensureSettingsPart(context, outputEntries);
  appendGeneratedRelationships(documentRelationshipsDocument, context.generatedRelationships);
  outputEntries.set(
    model.documentRelationshipsPath,
    encodeXml(documentRelationshipsDocument),
  );

  const sourceDocument = parseRequiredXml(
    decodeRequiredXml(outputEntries, model.mainDocumentPath, 'main document'),
    'main document',
  );
  replaceDocumentBody(sourceDocument, bodyXml, model.selectedSection.sectPrXml);
  outputEntries.set(model.mainDocumentPath, encodeXml(sourceDocument));

  if (context.numberingChanged && model.numberingPath && context.numberingDocument) {
    outputEntries.set(model.numberingPath, encodeXml(context.numberingDocument));
  }

  patchTemplateDocumentTitle(outputEntries, model.documentTitle, title);
  updateCoreProperties(outputEntries, model.corePropertiesPath, title);
  updateFieldRefreshSetting(outputEntries, model.settingsPath);

  context.generatedParts.forEach((part) => {
    outputEntries.set(part.name, part.data);
  });
  updateContentTypes(outputEntries, context.generatedParts, model.settingsPath);

  return await createCompressedZipBlob(
    [...outputEntries].map(([name, data]) => ({ name, data })),
    'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  );
}

export function resolveWordStyleMapping(manifest) {
  const model = manifest?.templateModel || manifest;
  const available = new Set(model?.styleIds || manifest?.styleIds || []);
  const mapping = {
    ...DEFAULT_WORD_STYLE_MAPPING,
    ...(model?.semanticStyleMapping || manifest?.semanticStyleMapping || {}),
  };

  Object.entries(mapping).forEach(([key, value]) => {
    if (!value) return;
    if (available.size && !available.has(value) && key !== 'table') {
      const fallback = DEFAULT_WORD_STYLE_MAPPING[key];
      mapping[key] = fallback || '';
    }
    if (key === 'table' && available.size && !available.has(value)) {
      mapping[key] = '';
    }
  });
  return mapping;
}

function findMainDocumentPath(entries) {
  const rootRelationships = parseRelationships(entries.get('_rels/.rels'));
  const officeDocument = rootRelationships.find((relationship) => relationship.type.endsWith('/officeDocument'));
  const related = officeDocument ? resolveRelationshipTarget('', officeDocument.target) : '';
  if (related && entries.has(related)) return related;
  if (entries.has('word/document.xml')) return 'word/document.xml';
  throw new Error('The Word template package does not contain a readable main document part.');
}

function findRootRelatedPartPath(entries, typeSuffix) {
  const relationship = parseRelationships(entries.get('_rels/.rels'))
    .find((item) => item.type.endsWith(`/${typeSuffix}`) || item.type.endsWith(typeSuffix));
  if (!relationship || relationship.targetMode === 'External') return '';
  const path = resolveRelationshipTarget('', relationship.target);
  return entries.has(path) ? path : '';
}

function findRelatedPartPath(relationships, sourcePart, typeSuffix) {
  const relationship = relationships.find((item) => item.type.endsWith(`/${typeSuffix}`));
  if (!relationship || relationship.targetMode === 'External') return '';
  return resolveRelationshipTarget(sourcePart, relationship.target);
}

function findExistingPart(entries, name) {
  return entries.has(name) ? name : '';
}

function parseRelationships(bytes) {
  if (!bytes) return [];
  const document = parseXml(decodeZipText(bytes));
  if (!document) return [];
  return [...document.getElementsByTagNameNS(PACKAGE_REL_NS, 'Relationship')]
    .map((relationship) => ({
      id: relationship.getAttribute('Id') || '',
      type: relationship.getAttribute('Type') || '',
      target: relationship.getAttribute('Target') || '',
      targetMode: relationship.getAttribute('TargetMode') || '',
    }))
    .filter((relationship) => relationship.id && relationship.target);
}

function parseStyleInfo(stylesBytes) {
  if (!stylesBytes) return [];
  const document = parseXml(decodeZipText(stylesBytes));
  if (!document) return [];

  return [...document.getElementsByTagNameNS(WORD_MAIN_NS, 'style')]
    .map((style) => {
      const nameElement = firstDescendant(style, 'name');
      const basedOnElement = firstDescendant(style, 'basedOn');
      const outlineLevel = firstDescendant(style, 'outlineLvl');
      const numPr = firstDescendant(style, 'numPr');
      const numId = numPr ? firstDescendant(numPr, 'numId') : null;
      const ilvl = numPr ? firstDescendant(numPr, 'ilvl') : null;
      return {
        id: wordAttribute(style, 'styleId'),
        name: wordAttribute(nameElement, 'val'),
        type: wordAttribute(style, 'type'),
        basedOn: wordAttribute(basedOnElement, 'val'),
        default: wordAttribute(style, 'default') === '1',
        custom: wordAttribute(style, 'customStyle') === '1',
        outlineLevel: numberAttribute(outlineLevel, 'val', null),
        numId: numberAttribute(numId, 'val', null),
        ilvl: numberAttribute(ilvl, 'val', null),
      };
    })
    .filter((style) => style.id);
}

function buildSemanticStyleMapping(styles) {
  const byId = new Map(styles.map((style) => [normaliseStyleKey(style.id), style]));
  const byName = new Map(styles.map((style) => [normaliseStyleKey(style.name), style]));
  const find = (type, ...candidates) => candidates
    .map((candidate) => byId.get(normaliseStyleKey(candidate)) || byName.get(normaliseStyleKey(candidate)))
    .find((style) => style && (!type || style.type === type))?.id || '';
  const findHeadingByOutline = (level) => styles
    .find((style) => style.type === 'paragraph' && style.outlineLevel === level - 1)?.id || '';
  const tableStyle = find('table', 'TableGrid', 'Table Grid')
    || styles.find((style) => style.type === 'table' && style.default)?.id
    || styles.find((style) => style.type === 'table' && !/normal\s*table/i.test(style.name))?.id
    || '';

  return {
    documentTitle: find('paragraph', 'Title', 'DocumentTitle', 'Document Title') || DEFAULT_WORD_STYLE_MAPPING.documentTitle,
    heading1: find('paragraph', 'Heading1', 'Heading 1') || findHeadingByOutline(1) || DEFAULT_WORD_STYLE_MAPPING.heading1,
    heading2: find('paragraph', 'Heading2', 'Heading 2') || findHeadingByOutline(2) || DEFAULT_WORD_STYLE_MAPPING.heading2,
    heading3: find('paragraph', 'Heading3', 'Heading 3') || findHeadingByOutline(3) || DEFAULT_WORD_STYLE_MAPPING.heading3,
    heading4: find('paragraph', 'Heading4', 'Heading 4') || findHeadingByOutline(4) || DEFAULT_WORD_STYLE_MAPPING.heading4,
    heading5: find('paragraph', 'Heading5', 'Heading 5') || findHeadingByOutline(5) || DEFAULT_WORD_STYLE_MAPPING.heading5,
    heading6: find('paragraph', 'Heading6', 'Heading 6') || findHeadingByOutline(6) || DEFAULT_WORD_STYLE_MAPPING.heading6,
    body: find('paragraph', 'Normal', 'BodyText', 'Body Text') || DEFAULT_WORD_STYLE_MAPPING.body,
    listParagraph: find('paragraph', 'ListParagraph', 'List Paragraph') || DEFAULT_WORD_STYLE_MAPPING.listParagraph,
    table: tableStyle,
    code: find('paragraph', 'Code', 'CodeBlock', 'Code Block', 'NoSpacing', 'No Spacing') || DEFAULT_WORD_STYLE_MAPPING.code,
    inlineCode: find('character', 'CodeChar', 'Code Char', 'InlineCode', 'Inline Code'),
    quote: find('paragraph', 'Quote', 'IntenseQuote', 'Intense Quote') || DEFAULT_WORD_STYLE_MAPPING.quote,
    caption: find('paragraph', 'Caption') || DEFAULT_WORD_STYLE_MAPPING.caption,
  };
}

function extractDocumentSections(documentXml, relationshipById, mainDocumentPath, entries) {
  const document = parseRequiredXml(documentXml, 'main document');
  const body = firstDescendant(document, 'body');
  if (!body) throw new Error('The Word template main document does not contain a body.');
  const sections = [];
  let blockNodes = [];

  [...body.childNodes].filter((node) => node.nodeType === Node.ELEMENT_NODE).forEach((child) => {
    if (child.localName === 'sectPr') {
      sections.push(buildSectionRecord(sections.length, child, blockNodes, relationshipById, mainDocumentPath, entries));
      blockNodes = [];
      return;
    }

    blockNodes.push(child);
    const paragraphSectPr = child.localName === 'p'
      ? [...child.getElementsByTagNameNS(WORD_MAIN_NS, 'sectPr')][0]
      : null;
    if (paragraphSectPr) {
      sections.push(buildSectionRecord(sections.length, paragraphSectPr, blockNodes, relationshipById, mainDocumentPath, entries));
      blockNodes = [];
    }
  });

  if (blockNodes.length || !sections.length) {
    const fallbackSectPr = parseRequiredXml(
      `<w:sectPr xmlns:w="${WORD_MAIN_NS}" xmlns:r="${WORD_REL_NS}"><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="840" w:right="840" w:bottom="960" w:left="840" w:header="720" w:footer="720" w:gutter="0"/></w:sectPr>`,
      'default section properties',
    ).documentElement;
    sections.push(buildSectionRecord(sections.length, fallbackSectPr, blockNodes, relationshipById, mainDocumentPath, entries));
  }

  return sections;
}

function buildSectionRecord(index, sectPr, blockNodes, relationshipById, mainDocumentPath, entries) {
  const clone = sectPr.cloneNode(true);
  const headerFooterReferences = [];
  [...clone.childNodes]
    .filter((node) => node.nodeType === Node.ELEMENT_NODE && ['headerReference', 'footerReference'].includes(node.localName))
    .forEach((reference) => {
      const relationshipId = relationshipAttribute(reference, 'id');
      const relationship = relationshipById.get(relationshipId);
      const partName = relationship && relationship.targetMode !== 'External'
        ? resolveRelationshipTarget(mainDocumentPath, relationship.target)
        : '';
      if (!relationship || !partName || !entries.has(partName)) {
        reference.remove();
        return;
      }
      headerFooterReferences.push({
        kind: reference.localName === 'headerReference' ? 'header' : 'footer',
        type: wordAttribute(reference, 'type') || 'default',
        relationshipId,
        partName,
      });
    });
  [...clone.getElementsByTagNameNS(WORD_MAIN_NS, 'sectPrChange')].forEach((change) => change.remove());

  const pgSz = firstDescendant(clone, 'pgSz');
  const pgMar = firstDescendant(clone, 'pgMar');
  const width = numberAttribute(pgSz, 'w', 11906);
  const height = numberAttribute(pgSz, 'h', 16838);
  const left = numberAttribute(pgMar, 'left', 840);
  const right = numberAttribute(pgMar, 'right', 840);
  const top = numberAttribute(pgMar, 'top', 840);
  const bottom = numberAttribute(pgMar, 'bottom', 960);
  const gutter = numberAttribute(pgMar, 'gutter', 0);
  const textLength = blockNodes.reduce((total, node) => total + getWordText(node).length, 0);
  const substantiveBlocks = blockNodes.filter((node) => {
    if (node.localName === 'tbl') return true;
    return node.localName === 'p' && getWordText(node).trim().length > 0;
  }).length;

  return {
    index,
    blockNodes: [...blockNodes],
    sectPrXml: serialiseXml(clone),
    headerFooterReferences,
    usableWidthDxa: Math.max(1, width - left - right - gutter),
    usableHeightDxa: Math.max(1, height - top - bottom),
    titlePage: Boolean(firstDescendant(clone, 'titlePg')),
    substantiveBlocks,
    textLength,
  };
}

function selectContentSection(sections) {
  return sections.reduce((best, section) => {
    const defaultBoundaries = section.headerFooterReferences.filter((item) => item.type === 'default').length;
    const score = Math.min(section.substantiveBlocks, 40) * 3
      + Math.min(section.textLength / 500, 20)
      + defaultBoundaries * 20
      + section.headerFooterReferences.length * 5
      - (section.titlePage ? 4 : 0)
      + section.index * 0.25;
    return !best || score >= best.score ? { section, score } : best;
  }, null)?.section || sections[sections.length - 1];
}

function collectHeaderFooterParts(sections) {
  return [...new Set(sections.flatMap((section) => section.headerFooterReferences.map((item) => item.partName)))];
}

function parseNumbering(numberingBytes) {
  if (!numberingBytes) return { abstracts: new Map(), nums: new Map() };
  const document = parseXml(decodeZipText(numberingBytes));
  if (!document) return { abstracts: new Map(), nums: new Map() };
  const abstracts = new Map();
  const nums = new Map();

  [...document.getElementsByTagNameNS(WORD_MAIN_NS, 'abstractNum')].forEach((abstractNum) => {
    const abstractNumId = wordAttribute(abstractNum, 'abstractNumId');
    const levels = [...abstractNum.getElementsByTagNameNS(WORD_MAIN_NS, 'lvl')].map((level) => ({
      ilvl: numberAttribute(level, 'ilvl', 0),
      start: numberAttribute(firstDescendant(level, 'start'), 'val', 1),
      numFmt: wordAttribute(firstDescendant(level, 'numFmt'), 'val') || 'decimal',
      levelText: wordAttribute(firstDescendant(level, 'lvlText'), 'val') || '',
      paragraphStyleId: wordAttribute(firstDescendant(level, 'pStyle'), 'val') || '',
    }));
    if (abstractNumId) abstracts.set(abstractNumId, { id: abstractNumId, levels });
  });

  [...document.getElementsByTagNameNS(WORD_MAIN_NS, 'num')].forEach((num) => {
    const numId = wordAttribute(num, 'numId');
    const abstractNumId = wordAttribute(firstDescendant(num, 'abstractNumId'), 'val');
    if (numId && abstractNumId) nums.set(numId, { id: numId, abstractNumId });
  });
  return { abstracts, nums };
}

function buildHeadingNumbering(styleMapping, styles, numbering) {
  const stylesById = new Map(styles.map((style) => [style.id, style]));
  const result = {};
  ['heading1', 'heading2', 'heading3', 'heading4', 'heading5', 'heading6'].forEach((key, index) => {
    const styleId = styleMapping[key];
    const inherited = resolveStyleNumbering(styleId, stylesById);
    let numId = inherited?.numId != null ? String(inherited.numId) : '';
    let ilvl = inherited?.ilvl != null ? inherited.ilvl : index;
    let abstract = numId ? numbering.abstracts.get(numbering.nums.get(numId)?.abstractNumId) : null;

    if (!abstract && styleId) {
      for (const [candidateNumId, num] of numbering.nums) {
        const candidateAbstract = numbering.abstracts.get(num.abstractNumId);
        const linkedLevel = candidateAbstract?.levels.find((level) => level.paragraphStyleId === styleId);
        if (linkedLevel) {
          numId = candidateNumId;
          ilvl = linkedLevel.ilvl;
          abstract = candidateAbstract;
          break;
        }
      }
    }

    const level = abstract?.levels.find((item) => item.ilvl === ilvl) || abstract?.levels[index] || abstract?.levels[0];
    const automatic = Boolean(
      numId
      && numId !== '0'
      && level
      && level.numFmt !== 'none'
      && level.numFmt !== 'bullet'
      && /%\d/.test(level.levelText),
    );
    result[key] = {
      styleId,
      automatic,
      numId,
      ilvl,
      numFmt: level?.numFmt || '',
      levelText: level?.levelText || '',
      start: level?.start ?? 1,
    };
  });
  return result;
}

function resolveStyleNumbering(styleId, stylesById, visited = new Set()) {
  if (!styleId || visited.has(styleId)) return null;
  visited.add(styleId);
  const style = stylesById.get(styleId);
  if (!style) return null;
  if (style.numId != null) {
    return { numId: style.numId, ilvl: style.ilvl };
  }
  return resolveStyleNumbering(style.basedOn, stylesById, visited);
}

function findListNumbering(numbering, excludedNumIds) {
  let bullet = null;
  let ordered = null;
  for (const [numId, num] of numbering.nums) {
    if (excludedNumIds.has(numId)) continue;
    const abstract = numbering.abstracts.get(num.abstractNumId);
    if (!abstract?.levels.length) continue;
    const bulletLevels = abstract.levels.filter((level) => level.numFmt === 'bullet').length;
    const orderedLevels = abstract.levels.filter((level) => !['bullet', 'none'].includes(level.numFmt)).length;
    if (bulletLevels && (!bullet || bulletLevels > bullet.levelCount)) {
      bullet = { numId, maxLevel: Math.max(...abstract.levels.map((level) => level.ilvl)), levelCount: bulletLevels };
    }
    if (orderedLevels && (!ordered || orderedLevels > ordered.levelCount)) {
      ordered = { numId, maxLevel: Math.max(...abstract.levels.map((level) => level.ilvl)), levelCount: orderedLevels };
    }
  }
  return {
    bullet: bullet ? { numId: bullet.numId, maxLevel: bullet.maxLevel } : null,
    ordered: ordered ? { numId: ordered.numId, maxLevel: ordered.maxLevel } : null,
  };
}

function buildTablePrototype(documentXml, selectedSection, styles, styleMapping) {
  const document = parseRequiredXml(documentXml, 'main document');
  const selectedTables = selectedSection.blockNodes.filter((node) => node.localName === 'tbl');
  const allTables = [...document.getElementsByTagNameNS(WORD_MAIN_NS, 'tbl')];
  const candidates = selectedTables.length ? selectedTables : allTables;
  const table = candidates
    .map((candidate) => ({ candidate, score: scoreTable(candidate) }))
    .sort((left, right) => right.score - left.score)[0]?.candidate;

  if (!table) {
    return { styleId: styleMapping.table || '', prototype: null };
  }

  const tblPr = directChild(table, 'tblPr');
  const tblGrid = directChild(table, 'tblGrid');
  const rows = directChildren(table, 'tr');
  const headerRow = rows[0] || null;
  const bodyRow = rows[1] || rows[0] || null;
  const styleId = wordAttribute(directChild(tblPr, 'tblStyle'), 'val');
  const availableTableStyles = new Set(styles.filter((style) => style.type === 'table').map((style) => style.id));

  return {
    styleId: availableTableStyles.has(styleId) ? styleId : styleMapping.table || '',
    prototype: {
      source: styleId && availableTableStyles.has(styleId) ? 'style' : 'direct',
      styleId: availableTableStyles.has(styleId) ? styleId : '',
      tblPrXml: tblPr ? serialiseXml(tblPr) : '',
      gridWidths: directChildren(tblGrid, 'gridCol').map((column) => numberAttribute(column, 'w', 0)).filter((width) => width > 0),
      columnCount: Math.max(...rows.map((row) => directChildren(row, 'tc').length), 1),
      headerRow: extractTableRowPrototype(headerRow),
      bodyRow: extractTableRowPrototype(bodyRow),
    },
  };
}

function scoreTable(table) {
  const rows = directChildren(table, 'tr');
  const columns = rows.reduce((maximum, row) => Math.max(maximum, directChildren(row, 'tc').length), 0);
  const tblPr = directChild(table, 'tblPr');
  const style = wordAttribute(directChild(tblPr, 'tblStyle'), 'val');
  const header = rows[0];
  const headerFormatting = header && (
    Boolean(firstDescendant(header, 'shd'))
    || Boolean(firstDescendant(header, 'b'))
    || Boolean(firstDescendant(header, 'tblHeader'))
  );
  return rows.length * Math.max(columns, 1) + (style ? 8 : 0) + (headerFormatting ? 6 : 0) - (rows.length === 1 ? 5 : 0);
}

function extractTableRowPrototype(row) {
  if (!row) return null;
  const cells = directChildren(row, 'tc');
  return {
    trPrXml: innerXml(directChild(row, 'trPr')),
    cells: cells.map((cell) => {
      const paragraph = firstDescendant(cell, 'p');
      const run = paragraph ? firstDescendant(paragraph, 'r') : null;
      return {
        tcPrXml: sanitiseTableCellProperties(directChild(cell, 'tcPr')),
        pPrXml: innerXml(directChild(paragraph, 'pPr')),
        rPrXml: innerXml(directChild(run, 'rPr')),
      };
    }),
  };
}

function sanitiseTableCellProperties(tcPr) {
  if (!tcPr) return '';
  const clone = tcPr.cloneNode(true);
  directChildren(clone).forEach((child) => {
    if (['tcW', 'gridSpan', 'vMerge', 'hMerge'].includes(child.localName)) child.remove();
  });
  return innerXml(clone);
}

function detectDocumentTitle({ entries, documentXml, corePropertiesPath, styleMapping, headerFooterPartNames }) {
  const document = parseRequiredXml(documentXml, 'main document');
  const body = document.getElementsByTagNameNS(WORD_MAIN_NS, 'body')[0];
  const titleParagraph = directChildren(body, 'p')
    .find((paragraph) => wordAttribute(firstDescendant(directChild(paragraph, 'pPr'), 'pStyle'), 'val') === styleMapping.documentTitle
      && getWordText(paragraph).trim());
  const coreTitle = readCoreTitle(entries.get(corePropertiesPath));
  const bodyTitle = getWordText(titleParagraph).trim();
  const candidates = [...new Set([coreTitle, bodyTitle].filter(Boolean))];
  let selected = null;

  for (const candidate of candidates) {
    const replacements = [];
    headerFooterPartNames.forEach((partName) => {
      const bytes = entries.get(partName);
      const part = bytes ? parseXml(decodeZipText(bytes)) : null;
      if (!part) return;
      [...part.getElementsByTagNameNS(WORD_MAIN_NS, 'p')].forEach((paragraph, paragraphIndex) => {
        const text = getWordText(paragraph);
        if (text.includes(candidate)) {
          replacements.push({ partName, paragraphIndex, sourceText: candidate });
        }
      });
    });
    if (replacements.length) {
      selected = { value: candidate, replacements };
      break;
    }
  }

  return {
    value: selected?.value || bodyTitle || coreTitle,
    source: selected?.value === coreTitle ? 'core-properties' : titleParagraph ? 'title-style' : coreTitle ? 'core-properties' : '',
    replacements: selected?.replacements || [],
  };
}

function readCoreTitle(bytes) {
  if (!bytes) return '';
  const document = parseXml(decodeZipText(bytes));
  return document?.getElementsByTagNameNS(DC_NS, 'title')[0]?.textContent?.trim() || '';
}

function replaceDocumentBody(document, bodyXml, sectPrXml) {
  const body = document.getElementsByTagNameNS(WORD_MAIN_NS, 'body')[0];
  if (!body) throw new Error('The Word template main document body could not be replaced.');
  while (body.firstChild) body.removeChild(body.firstChild);

  const fragment = parseRequiredXml(
    `<root ${namespaceDeclarations(REQUIRED_DOCUMENT_NAMESPACES)}>${bodyXml || ''}</root>`,
    'generated Word body',
  );
  [...fragment.documentElement.childNodes].forEach((node) => {
    body.appendChild(document.importNode(node, true));
  });

  const section = parseRequiredXml(
    `<root ${namespaceDeclarations(REQUIRED_DOCUMENT_NAMESPACES)}>${sectPrXml}</root>`,
    'selected section properties',
  ).documentElement.firstElementChild;
  if (!section || section.localName !== 'sectPr') {
    throw new Error('The selected Word template section properties are invalid.');
  }
  body.appendChild(document.importNode(section, true));
}

function patchTemplateDocumentTitle(entries, documentTitle, replacementTitle) {
  if (!documentTitle?.value || !replacementTitle || documentTitle.value === replacementTitle) return;
  documentTitle.replacements.forEach((target) => {
    const bytes = entries.get(target.partName);
    if (!bytes) return;
    const document = parseXml(decodeZipText(bytes));
    if (!document) return;
    const paragraph = document.getElementsByTagNameNS(WORD_MAIN_NS, 'p')[target.paragraphIndex];
    if (!paragraph || !getWordText(paragraph).includes(target.sourceText)) return;
    if (replaceTextAcrossWordNodes(paragraph, target.sourceText, replacementTitle)) {
      entries.set(target.partName, encodeXml(document));
    }
  });
}

function replaceTextAcrossWordNodes(scope, sourceText, replacementText) {
  const textNodes = [...scope.getElementsByTagNameNS(WORD_MAIN_NS, 't')];
  const combined = textNodes.map((node) => node.textContent || '').join('');
  const start = combined.indexOf(sourceText);
  if (start < 0) return false;
  const end = start + sourceText.length;
  let offset = 0;
  const affected = [];

  textNodes.forEach((node) => {
    const value = node.textContent || '';
    const nodeStart = offset;
    const nodeEnd = offset + value.length;
    offset = nodeEnd;
    if (nodeEnd <= start || nodeStart >= end) return;
    affected.push({
      node,
      value,
      sourceStart: Math.max(start, nodeStart),
      sourceEnd: Math.min(end, nodeEnd),
      nodeStart,
    });
  });
  if (!affected.length) return false;

  let replacementOffset = 0;
  affected.forEach((item, index) => {
    const sourceSegmentLength = item.sourceEnd - item.sourceStart;
    const nextReplacementOffset = index === affected.length - 1
      ? replacementText.length
      : Math.round((item.sourceEnd - start) * replacementText.length / sourceText.length);
    const replacementSegment = replacementText.slice(replacementOffset, nextReplacementOffset);
    replacementOffset = nextReplacementOffset;
    const localStart = item.sourceStart - item.nodeStart;
    const localEnd = item.sourceEnd - item.nodeStart;
    item.node.textContent = `${item.value.slice(0, localStart)}${replacementSegment}${item.value.slice(localEnd)}`;
    item.node.setAttributeNS(XML_NS, 'xml:space', 'preserve');
    if (replacementText.length > sourceText.length && replacementSegment) {
      applyRunFitText(item.node, sourceSegmentLength, index + 1);
    }
  });
  return true;
}

function applyRunFitText(textNode, sourceCharacterCount, fitId) {
  let run = textNode.parentElement;
  while (run && !(run.namespaceURI === WORD_MAIN_NS && run.localName === 'r')) run = run.parentElement;
  if (!run) return;
  let runProperties = directChild(run, 'rPr');
  if (!runProperties) {
    runProperties = run.ownerDocument.createElementNS(WORD_MAIN_NS, 'w:rPr');
    run.insertBefore(runProperties, run.firstChild);
  }
  let fitText = directChild(runProperties, 'fitText');
  if (!fitText) {
    fitText = run.ownerDocument.createElementNS(WORD_MAIN_NS, 'w:fitText');
    runProperties.appendChild(fitText);
  }
  fitText.setAttributeNS(WORD_MAIN_NS, 'w:val', String(Math.max(120, sourceCharacterCount * 120)));
  fitText.setAttributeNS(WORD_MAIN_NS, 'w:id', String(fitId));
}

function updateCoreProperties(entries, corePropertiesPath, title) {
  if (!corePropertiesPath || !entries.has(corePropertiesPath)) return;
  const document = parseXml(decodeZipText(entries.get(corePropertiesPath)));
  if (!document) return;
  let titleElement = document.getElementsByTagNameNS(DC_NS, 'title')[0];
  if (!titleElement) {
    titleElement = document.createElementNS(DC_NS, 'dc:title');
    document.documentElement.appendChild(titleElement);
  }
  titleElement.textContent = title;
  const modified = document.getElementsByTagNameNS(DCTERMS_NS, 'modified')[0];
  if (modified) modified.textContent = new Date().toISOString();
  entries.set(corePropertiesPath, encodeXml(document));
}

function ensureSettingsPart(context, entries) {
  const { model } = context;
  if (model.settingsPath && entries.has(model.settingsPath)) return;
  const directory = model.mainDocumentPath.includes('/')
    ? model.mainDocumentPath.slice(0, model.mainDocumentPath.lastIndexOf('/') + 1)
    : '';
  let settingsPath = `${directory}settings.xml`;
  let suffix = 2;
  while (entries.has(settingsPath)) {
    settingsPath = `${directory}settings-${suffix}.xml`;
    suffix += 1;
  }
  model.settingsPath = settingsPath;
  entries.set(settingsPath, new TextEncoder().encode(
    `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:settings xmlns:w="${WORD_MAIN_NS}"><w:updateFields w:val="true"/></w:settings>`,
  ));
  context.generatedRelationships.push({
    id: allocateRelationshipIdFromDocument(context.documentRelationshipsDocument, context.generatedRelationships),
    type: `${OFFICE_REL_BASE}settings`,
    target: relationshipTargetFromPart(model.mainDocumentPath, settingsPath),
    targetMode: '',
  });
}

function updateFieldRefreshSetting(entries, settingsPath) {
  if (!settingsPath || !entries.has(settingsPath)) return;
  const document = parseXml(decodeZipText(entries.get(settingsPath)));
  if (!document) return;
  let updateFields = document.getElementsByTagNameNS(WORD_MAIN_NS, 'updateFields')[0];
  if (!updateFields) {
    updateFields = document.createElementNS(WORD_MAIN_NS, 'w:updateFields');
    document.documentElement.appendChild(updateFields);
  }
  updateFields.setAttributeNS(WORD_MAIN_NS, 'w:val', 'true');
  entries.set(settingsPath, encodeXml(document));
}

function allocateRelationshipIdFromDocument(document, pending) {
  const used = new Set([
    ...[...document.getElementsByTagNameNS(PACKAGE_REL_NS, 'Relationship')].map((item) => item.getAttribute('Id')),
    ...pending.map((item) => item.id),
  ]);
  let index = 1;
  while (used.has(`rIdLds${index}`)) index += 1;
  return `rIdLds${index}`;
}

function appendGeneratedRelationships(document, relationships) {
  const root = document.documentElement;
  relationships.forEach((relationship) => {
    const element = document.createElementNS(PACKAGE_REL_NS, 'Relationship');
    element.setAttribute('Id', relationship.id);
    element.setAttribute('Type', relationship.type);
    element.setAttribute('Target', relationship.target);
    if (relationship.targetMode) element.setAttribute('TargetMode', relationship.targetMode);
    root.appendChild(element);
  });
}

function updateContentTypes(entries, generatedParts, settingsPath) {
  const contentTypesBytes = entries.get('[Content_Types].xml');
  if (!contentTypesBytes) throw new Error('The Word template package is missing [Content_Types].xml.');
  const document = parseRequiredXml(decodeZipText(contentTypesBytes), 'content types');
  const root = document.documentElement;
  const existingDefaults = new Set(
    [...document.getElementsByTagNameNS(CONTENT_TYPES_NS, 'Default')]
      .map((item) => (item.getAttribute('Extension') || '').toLowerCase()),
  );
  generatedParts.forEach((part) => {
    const extension = getFilenameExtension(part.name);
    if (!extension || existingDefaults.has(extension)) return;
    const element = document.createElementNS(CONTENT_TYPES_NS, 'Default');
    element.setAttribute('Extension', extension);
    element.setAttribute('ContentType', part.mimeType || IMAGE_CONTENT_TYPES[extension] || 'application/octet-stream');
    root.appendChild(element);
    existingDefaults.add(extension);
  });

  if (settingsPath && !hasContentTypeOverride(document, settingsPath)) {
    const override = document.createElementNS(CONTENT_TYPES_NS, 'Override');
    override.setAttribute('PartName', `/${settingsPath}`);
    override.setAttribute('ContentType', SETTINGS_CONTENT_TYPE);
    root.appendChild(override);
  }
  entries.set('[Content_Types].xml', encodeXml(document));
}

function hasContentTypeOverride(document, partName) {
  return [...document.getElementsByTagNameNS(CONTENT_TYPES_NS, 'Override')]
    .some((item) => item.getAttribute('PartName') === `/${partName}`);
}

function findMaximumDocPrId(entries) {
  let maximum = 0;
  entries.forEach((bytes, name) => {
    if (!/\.xml$/i.test(name) || !/^(word\/document\.xml|word\/(header|footer)\d+\.xml)$/i.test(name)) return;
    const document = parseXml(decodeZipText(bytes));
    if (!document) return;
    [...document.getElementsByTagNameNS(REQUIRED_DOCUMENT_NAMESPACES.wp, 'docPr')].forEach((docPr) => {
      maximum = Math.max(maximum, Number(docPr.getAttribute('id')) || 0);
    });
  });
  return maximum;
}

function relationshipPartPath(partName) {
  const slash = partName.lastIndexOf('/');
  const directory = slash >= 0 ? partName.slice(0, slash + 1) : '';
  const filename = slash >= 0 ? partName.slice(slash + 1) : partName;
  return `${directory}_rels/${filename}.rels`;
}

function relationshipTargetFromPart(sourcePart, targetPart) {
  const sourceDirectory = sourcePart.includes('/') ? sourcePart.slice(0, sourcePart.lastIndexOf('/') + 1) : '';
  if (targetPart.startsWith(sourceDirectory)) return targetPart.slice(sourceDirectory.length);
  return relativePackagePath(sourceDirectory, targetPart);
}

function relativePackagePath(fromDirectory, targetPath) {
  const from = fromDirectory.replace(/\/$/, '').split('/').filter(Boolean);
  const target = targetPath.split('/').filter(Boolean);
  while (from.length && target.length && from[0] === target[0]) {
    from.shift();
    target.shift();
  }
  return `${'../'.repeat(from.length)}${target.join('/')}`;
}

function resolveRelationshipTarget(sourcePart, target) {
  const cleanedTarget = String(target || '').replace(/\\/g, '/');
  if (!cleanedTarget) return '';
  if (cleanedTarget.startsWith('/')) return normalisePackagePath(cleanedTarget);
  const directory = sourcePart.includes('/') ? sourcePart.slice(0, sourcePart.lastIndexOf('/') + 1) : '';
  return normalisePackagePath(`${directory}${cleanedTarget}`);
}

function normalisePackagePath(value) {
  const segments = [];
  String(value || '').replace(/^\/+/, '').split('/').forEach((segment) => {
    if (!segment || segment === '.') return;
    if (segment === '..') segments.pop();
    else segments.push(segment);
  });
  return segments.join('/');
}

function emptyRelationshipsXml() {
  return `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="${PACKAGE_REL_NS}"/>`;
}

function namespaceDeclarations(namespaces) {
  return Object.entries(namespaces).map(([prefix, value]) => `xmlns:${prefix}="${value}"`).join(' ');
}

function parseXml(xml) {
  try {
    const document = new DOMParser().parseFromString(xml, 'application/xml');
    return document.getElementsByTagName('parsererror').length ? null : document;
  } catch {
    return null;
  }
}

function parseRequiredXml(xml, description) {
  const document = parseXml(xml);
  if (!document) throw new Error(`The Word template ${description} XML is malformed.`);
  return document;
}

function decodeRequiredXml(entries, partName, description) {
  const bytes = entries.get(partName);
  if (!bytes) throw new Error(`The Word template is missing its ${description} part.`);
  return decodeZipText(bytes);
}

function encodeXml(document) {
  return new TextEncoder().encode(serialiseXml(document));
}

function serialiseXml(node) {
  return new XMLSerializer().serializeToString(node);
}

function directChildren(element, localName = '') {
  if (!element) return [];
  return [...element.childNodes].filter((node) => node.nodeType === Node.ELEMENT_NODE && (!localName || node.localName === localName));
}

function directChild(element, localName) {
  return directChildren(element, localName)[0] || null;
}

function firstDescendant(element, localName) {
  if (!element) return null;
  if (element.getElementsByTagNameNS) {
    return element.getElementsByTagNameNS(WORD_MAIN_NS, localName)[0] || null;
  }
  return null;
}

function innerXml(element) {
  return element ? [...element.childNodes].map((node) => serialiseXml(node)).join('') : '';
}

function getWordText(element) {
  return element ? [...element.getElementsByTagNameNS(WORD_MAIN_NS, 't')].map((node) => node.textContent || '').join('') : '';
}

function wordAttribute(element, localName) {
  if (!element) return '';
  return element.getAttributeNS(WORD_MAIN_NS, localName) || element.getAttribute(`w:${localName}`) || element.getAttribute(localName) || '';
}

function relationshipAttribute(element, localName) {
  if (!element) return '';
  return element.getAttributeNS(WORD_REL_NS, localName) || element.getAttribute(`r:${localName}`) || '';
}

function numberAttribute(element, localName, fallback) {
  const rawValue = wordAttribute(element, localName);
  if (rawValue === '') return fallback;
  const value = Number(rawValue);
  return Number.isFinite(value) ? value : fallback;
}

function normaliseStyleKey(value) {
  return String(value || '').toLowerCase().replace(/[\s_-]+/g, '');
}

function readContentTypeDefaults(entries) {
  const bytes = entries.get('[Content_Types].xml');
  const document = bytes ? parseXml(decodeZipText(bytes)) : null;
  return document ? [...document.getElementsByTagNameNS(CONTENT_TYPES_NS, 'Default')] : [];
}

function isCompatibleStoredModel(storedModel, analysedModel) {
  return storedModel?.version === WORD_TEMPLATE_PACK_VERSION
    && storedModel.mainDocumentPath === analysedModel.mainDocumentPath
    && storedModel.selectedSection?.sectPrXml;
}

function toUint8Array(value) {
  if (value instanceof Uint8Array) return value;
  if (value instanceof ArrayBuffer) return new Uint8Array(value);
  if (ArrayBuffer.isView(value)) return new Uint8Array(value.buffer, value.byteOffset, value.byteLength);
  return null;
}

function sanitisePackageFilename(value) {
  return String(value || '')
    .split(/[\\/]/)
    .pop()
    .replace(/[^a-z0-9._-]+/gi, '-')
    .replace(/^-+|-+$/g, '');
}

function getFilenameExtension(value) {
  return String(value || '').match(/\.([a-z0-9]+)$/i)?.[1]?.toLowerCase() || '';
}

function extensionForMimeType(mimeType) {
  const entry = Object.entries(IMAGE_CONTENT_TYPES).find(([, value]) => value === mimeType);
  return entry?.[0] || '';
}
