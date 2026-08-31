import { base64ToUint8Array } from '../utils/binary.js';
import { escapeXml } from '../utils/format.js';
import { createCompressedZipBlob } from '../utils/zip.js';
import {
  DEFAULT_WORD_STYLE_MAPPING,
  finaliseWordTemplateExport,
  prepareWordTemplateExport,
  resolveWordStyleMapping,
} from './word-template-package.js';

const APP_NAME = 'Lens Docs Studio';
const DOCX_MIME = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
const WORD_MAIN_NS = 'http://schemas.openxmlformats.org/wordprocessingml/2006/main';

export async function createWordDocument({ root, images, fallbackTitle, templatePack = null }) {
  const title = getMarkdownDocumentTitle(root) || fallbackTitle || 'rendered-document';
  const templateContext = templatePack ? await prepareWordTemplateExport(templatePack) : null;
  const context = createRenderContext(templateContext);

  const layoutImages = await prepareWordImagesForLayout(root, images, context);
  context.imageRelationships = layoutImages.map((image, index) => prepareImageRelationship(image, index, context));
  const bodyXml = buildWordBodyXml(root, context);

  if (templateContext) {
    return await finaliseWordTemplateExport(templateContext, { bodyXml, title });
  }
  return await createDefaultWordPackage({ bodyXml, title, context });
}

export function getMarkdownDocumentTitle(root) {
  return root?.querySelector?.('h1')?.textContent?.replace(/\s+/g, ' ').trim() || '';
}

function createRenderContext(templateContext) {
  const model = templateContext?.model || null;
  const styleMapping = model
    ? resolveWordStyleMapping({ templateModel: model })
    : { ...DEFAULT_WORD_STYLE_MAPPING };
  const availableStyleIds = new Set(model?.styleIds || []);
  const defaultRelationships = [];
  const hyperlinkIds = new Map();
  let nextDefaultRelationshipId = 1;
  let nextDefaultDocPrId = 1;

  function allocateDefaultRelationship(type, target, targetMode = '') {
    const relationship = {
      id: `rId${nextDefaultRelationshipId++}`,
      type,
      target,
      targetMode,
    };
    defaultRelationships.push(relationship);
    return relationship.id;
  }

  return {
    templateContext,
    model,
    isTemplate: Boolean(templateContext),
    styleMapping,
    availableStyleIds,
    headingNumbering: model?.headingNumbering || {},
    listNumbering: model?.listNumbering || {},
    tablePrototype: model?.tablePrototype || null,
    usableWidthDxa: model?.selectedSection?.usableWidthDxa || 10226,
    usableHeightDxa: model?.selectedSection?.usableHeightDxa || 15038,
    defaultRelationships,
    imageRelationships: [],
    hasTemplateStyle(styleId) {
      return Boolean(templateContext && styleId && availableStyleIds.has(styleId));
    },
    allocateMedia(image, index) {
      const data = base64ToUint8Array(image.base64);
      if (templateContext) {
        return templateContext.allocateMediaPart(image.name || `image-${index + 1}.png`, data, image.mimeType);
      }
      const name = sanitiseMediaName(image.name || `image-${index + 1}.png`);
      const target = `media/${name}`;
      return {
        relationshipId: allocateDefaultRelationship(
          'http://schemas.openxmlformats.org/officeDocument/2006/relationships/image',
          target,
        ),
        target,
        partName: `word/${target}`,
        docPrId: nextDefaultDocPrId++,
        data,
      };
    },
    allocateHyperlink(target) {
      if (hyperlinkIds.has(target)) return hyperlinkIds.get(target);
      const id = templateContext
        ? templateContext.allocateExternalHyperlink(target)
        : allocateDefaultRelationship(
          'http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink',
          target,
          'External',
        );
      hyperlinkIds.set(target, id);
      return id;
    },
    allocateOrderedListNumbering(baseNumId, start) {
      return templateContext?.allocateNumberingInstance(baseNumId, start) || baseNumId;
    },
  };
}

function prepareImageRelationship(image, index, context) {
  const allocated = context.allocateMedia(image, index);
  return {
    ...image,
    ...allocated,
    data: allocated.data || base64ToUint8Array(image.base64),
  };
}

async function prepareWordImagesForLayout(root, images, context) {
  const prepared = [];
  for (const image of images) {
    const slices = await splitTallDiagramImage(image, context);
    prepared.push(...slices);
    if (slices.length > 1) replaceWordImageWithSlices(root, image.name, slices);
  }
  return prepared;
}

async function splitTallDiagramImage(image, context) {
  if (image.kind !== 'diagram' || !image.base64 || !image.mimeType?.startsWith('image/')) return [image];
  const sourceWidth = Math.max(1, Number(image.displayWidth) || 1);
  const sourceHeight = Math.max(1, Number(image.displayHeight) || 1);
  const sliceAspectRatio = Math.max(0.5, context.usableHeightDxa * 0.78 / context.usableWidthDxa);
  if (sourceHeight / sourceWidth <= sliceAspectRatio * 1.4) return [image];

  try {
    const decoded = await loadWordImage(image);
    const pixelWidth = Math.max(1, decoded.naturalWidth || decoded.width || sourceWidth);
    const pixelHeight = Math.max(1, decoded.naturalHeight || decoded.height || sourceHeight);
    const sliceHeight = Math.max(1, Math.floor(pixelWidth * sliceAspectRatio));
    const overlap = Math.floor(sliceHeight * 0.12);
    const sliceStep = Math.max(1, sliceHeight - overlap);
    const partCount = pixelHeight <= sliceHeight
      ? 1
      : 1 + Math.ceil((pixelHeight - sliceHeight) / sliceStep);
    if (partCount < 2) return [image];
    const stem = String(image.name || 'diagram.png').replace(/\.[^.]+$/, '') || 'diagram';
    const slices = [];

    for (let index = 0; index < partCount; index += 1) {
      const sourceY = index * sliceStep;
      const currentHeight = Math.min(sliceHeight, pixelHeight - sourceY);
      const canvas = document.createElement('canvas');
      canvas.width = pixelWidth;
      canvas.height = currentHeight;
      const drawingContext = canvas.getContext('2d');
      if (!drawingContext) return [image];
      drawingContext.fillStyle = '#ffffff';
      drawingContext.fillRect(0, 0, canvas.width, canvas.height);
      drawingContext.drawImage(decoded, 0, sourceY, pixelWidth, currentHeight, 0, 0, pixelWidth, currentHeight);
      const dataUrl = canvas.toDataURL('image/png');
      slices.push({
        ...image,
        name: `${stem}-part-${index + 1}.png`,
        mimeType: 'image/png',
        base64: dataUrl.slice(dataUrl.indexOf(',') + 1),
        displayWidth: pixelWidth,
        displayHeight: currentHeight,
        sliceIndex: index,
        sliceCount: partCount,
      });
    }
    return slices;
  } catch {
    return [image];
  }
}

function loadWordImage(image) {
  return new Promise((resolve, reject) => {
    const decoded = new Image();
    decoded.decoding = 'async';
    decoded.onload = () => resolve(decoded);
    decoded.onerror = () => reject(new Error('The Word diagram image could not be decoded for pagination.'));
    decoded.src = `data:${image.mimeType};base64,${image.base64}`;
  });
}

function replaceWordImageWithSlices(root, originalName, slices) {
  [...root.querySelectorAll('[data-word-image-name]')]
    .filter((element) => element.dataset.wordImageName === originalName)
    .forEach((element) => {
      const replacements = slices.map((slice, index) => {
        const replacement = element.cloneNode(true);
        replacement.dataset.wordImageName = slice.name;
        replacement.dataset.wordImageSlice = `${index + 1}/${slices.length}`;
        if (index > 0) replacement.dataset.wordPageBreakBefore = 'true';
        replacement.setAttribute('width', String(slice.displayWidth));
        replacement.setAttribute('height', String(slice.displayHeight));
        replacement.alt = `${element.alt || 'Mermaid diagram'} (part ${index + 1} of ${slices.length})`;
        return replacement;
      });
      element.replaceWith(...replacements);
    });
}

function buildWordBodyXml(root, context) {
  const blocks = wordBlocksFromChildren(root, context);
  return blocks.length ? blocks.join('\n') : buildWordParagraph([buildWordTextRun('')]);
}

function wordBlocksFromChildren(node, context) {
  const blocks = [];
  node.childNodes.forEach((child) => {
    blocks.push(...wordBlocksFromNode(child, context));
  });
  return blocks;
}

function wordBlocksFromNode(node, context) {
  if (node.nodeType === Node.TEXT_NODE) {
    return node.textContent.trim()
      ? [buildWordParagraph([buildWordTextRun(node.textContent)])]
      : [];
  }
  if (node.nodeType !== Node.ELEMENT_NODE) return [];

  const tagName = node.tagName.toLowerCase();
  if (['script', 'style', 'svg'].includes(tagName)) return [];

  if (node.classList.contains('diagram-frame')) {
    const images = [...node.querySelectorAll('img.diagram-image')];
    if (images.length) return images.map((image) => buildWordImageParagraph(image, context));
    const error = node.querySelector('.diagram-error');
    if (error) {
      return [buildWordParagraph(
        [buildWordTextRun(error.textContent, context.isTemplate ? {} : { code: true, color: 'BE123C' })],
        paragraphStyleOptions('code', context, { code: true }),
      )];
    }
  }

  if (tagName === 'img' && node.dataset.wordImageName) {
    return [buildWordImageParagraph(node, context)];
  }

  if (node.classList.contains('code-block')) {
    return [buildWordCodeBlock(node, context)];
  }

  if (tagName === 'figure' && node.hasAttribute('data-progress-bar')) {
    return [buildWordProgressBar(node, context)];
  }

  if (/^h[1-6]$/.test(tagName)) {
    return [buildWordHeading(node, context)];
  }

  if (tagName === 'p') {
    if (node.querySelector('img[data-word-image-name]')) {
      return wordBlocksFromParagraphWithImages(node, context);
    }
    return [buildWordParagraph(
      collectWordInlineRuns(node, {}, context),
      paragraphStyleOptions('body', context, { after: 160 }),
    )];
  }

  if (tagName === 'pre') {
    const runOptions = context.hasTemplateStyle(context.styleMapping.code) ? {} : { code: true };
    return [buildWordParagraph(
      [buildWordTextRun(node.textContent, runOptions)],
      paragraphStyleOptions('code', context, { code: true, after: 180 }),
    )];
  }

  if (tagName === 'blockquote') {
    const inlineOptions = context.hasTemplateStyle(context.styleMapping.quote)
      ? { italic: true }
      : { italic: true, color: '475569' };
    return [buildWordParagraph(
      collectWordInlineRuns(node, inlineOptions, context),
      paragraphStyleOptions('quote', context, { indentLeft: 360, after: 160 }),
    )];
  }

  if (tagName === 'figcaption') {
    return [buildWordParagraph(
      collectWordInlineRuns(node, {}, context),
      paragraphStyleOptions('caption', context, { after: 120 }),
    )];
  }

  if (tagName === 'details') return wordBlocksFromChildren(node, context);

  if (tagName === 'summary') {
    return [buildWordParagraph(
      collectWordInlineRuns(node, context.isTemplate ? { bold: true } : { bold: true, color: '475569' }, context),
      { keepNext: true, keepLines: true, after: context.isTemplate ? 0 : 100 },
    )];
  }

  if (tagName === 'ul' || tagName === 'ol') {
    return wordListBlocks(node, context, 0);
  }

  if (tagName === 'table') return [buildWordTable(node, context)];

  if (tagName === 'hr') {
    return [buildWordParagraph([], { bottomBorder: true })];
  }

  if (['div', 'section', 'article', 'main', 'figure', 'thead', 'tbody'].includes(tagName)) {
    return wordBlocksFromChildren(node, context);
  }

  const runs = collectWordInlineRuns(node, {}, context);
  return runs.length
    ? [buildWordParagraph(runs, paragraphStyleOptions('body', context, { after: 160 }))]
    : [];
}

function buildWordHeading(node, context) {
  const level = Number(node.tagName.slice(1));
  const styleKey = level === 1 ? 'documentTitle' : `heading${Math.min(level - 1, 6)}`;
  const styleId = context.styleMapping[styleKey] || context.styleMapping.heading6 || '';
  const semanticDepth = Math.max(0, level - 1);
  const numbering = semanticDepth ? context.headingNumbering[styleKey] : null;
  const source = numbering?.automatic
    ? cloneHeadingWithoutLiteralCounter(node, semanticDepth)
    : node;
  const sizes = { 1: 48, 2: 36, 3: 28, 4: 24, 5: 22, 6: 20 };
  const templateStyleAvailable = context.hasTemplateStyle(styleId);
  const inlineOptions = templateStyleAvailable
    ? {}
    : { bold: true, size: sizes[level] ?? 24, color: '0F172A' };
  const paragraphOptions = {
    styleId,
    keepNext: true,
    keepLines: true,
  };
  if (!templateStyleAvailable) {
    paragraphOptions.before = level === 1 ? 160 : 240;
    paragraphOptions.after = 120;
  }
  return buildWordParagraph(collectWordInlineRuns(source, inlineOptions, context), paragraphOptions);
}

function cloneHeadingWithoutLiteralCounter(node, semanticDepth) {
  const prefixLength = findHeadingCounterPrefixLength(node.textContent || '', semanticDepth);
  if (!prefixLength) return node;
  const clone = node.cloneNode(true);
  let remaining = prefixLength;
  const walker = document.createTreeWalker(clone, NodeFilter.SHOW_TEXT);
  let textNode = walker.nextNode();
  while (textNode && remaining > 0) {
    const length = textNode.nodeValue?.length || 0;
    if (length <= remaining) {
      textNode.nodeValue = '';
      remaining -= length;
    } else {
      textNode.nodeValue = textNode.nodeValue.slice(remaining);
      remaining = 0;
    }
    textNode = walker.nextNode();
  }
  return clone;
}

function findHeadingCounterPrefixLength(value, semanticDepth) {
  if (!semanticDepth) return 0;
  const text = String(value || '');
  const pattern = semanticDepth === 1
    ? /^\s*[1-9]\d{0,2}[.)]\s+/
    : new RegExp(`^\\s*[1-9]\\d{0,2}(?:\\.[1-9]\\d{0,2}){${semanticDepth - 1}}(?:[.)])?\\s+`);
  return text.match(pattern)?.[0]?.length || 0;
}

function wordListBlocks(list, context, level, inheritedOrderedNumId = '') {
  const ordered = list.tagName.toLowerCase() === 'ol';
  const items = [...list.children].filter((child) => child.tagName?.toLowerCase() === 'li');
  const numbering = ordered ? context.listNumbering.ordered : context.listNumbering.bullet;
  const start = Number(list.getAttribute('start')) || 1;
  const resolvedNumId = ordered && numbering?.numId
    ? inheritedOrderedNumId || context.allocateOrderedListNumbering(numbering.numId, start)
    : numbering?.numId;
  const blocks = [];

  items.forEach((item, index) => {
    const inlineClone = item.cloneNode(true);
    inlineClone.querySelectorAll('ul, ol').forEach((nested) => nested.remove());
    const runs = collectWordInlineRuns(inlineClone, {}, context);
    const paragraphOptions = paragraphStyleOptions('listParagraph', context, { after: 80 });
    if (resolvedNumId) {
      paragraphOptions.numId = resolvedNumId;
      paragraphOptions.ilvl = Math.min(level, numbering.maxLevel ?? level);
    } else {
      runs.unshift(buildWordTextRun(ordered ? `${start + index}. ` : '- '));
      paragraphOptions.indentLeft = 360 + level * 360;
      paragraphOptions.hanging = 240;
    }
    blocks.push(buildWordParagraph(runs.length ? runs : [buildWordTextRun('')], paragraphOptions));

    [...item.children]
      .filter((child) => ['ul', 'ol'].includes(child.tagName?.toLowerCase()))
      .forEach((nested) => {
        const nestedOrdered = nested.tagName.toLowerCase() === 'ol';
        const inherited = ordered && nestedOrdered ? resolvedNumId : '';
        blocks.push(...wordListBlocks(nested, context, level + 1, inherited));
      });
  });
  return blocks;
}

function wordBlocksFromParagraphWithImages(paragraph, context) {
  const blocks = [];
  const textClone = paragraph.cloneNode(true);
  textClone.querySelectorAll('img[data-word-image-name]').forEach((image) => image.remove());
  if (textClone.textContent.trim()) {
    blocks.push(buildWordParagraph(
      collectWordInlineRuns(textClone, {}, context),
      paragraphStyleOptions('body', context, { after: 120 }),
    ));
  }
  paragraph.querySelectorAll('img[data-word-image-name]').forEach((image) => {
    blocks.push(buildWordImageParagraph(image, context));
  });
  return blocks;
}

function buildWordCodeBlock(block, context) {
  const code = block.querySelector('pre code');
  const templateCodeStyle = context.hasTemplateStyle(context.styleMapping.code);
  const runs = code
    ? collectWordCodeRuns(code, templateCodeStyle ? {} : { code: true }, context)
    : [];
  return buildWordParagraph(
    runs.length ? runs : [buildWordTextRun('', templateCodeStyle ? {} : { code: true })],
    paragraphStyleOptions('code', context, { code: true, after: 180 }),
  );
}

function buildWordProgressBar(figure, context) {
  const label = figure.querySelector('[data-progress-label]')?.textContent.trim() || 'Progress';
  const value = figure.querySelector('[data-progress-value]')?.textContent.trim()
    || `${figure.querySelector('progress')?.getAttribute('value') || '0'}%`;
  return buildWordParagraph([
    buildWordTextRun(`${label}: `, { bold: true }),
    buildWordTextRun(value, context.isTemplate
      ? { bold: true }
      : { bold: true, color: getProgressWordColour(figure.dataset.progressColour) }),
  ], paragraphStyleOptions('body', context, { after: 160 }));
}

function getProgressWordColour(colour) {
  const colours = {
    accent: 'D95F16',
    green: '16A34A',
    blue: '2563EB',
    amber: 'D97706',
    red: 'DC2626',
    neutral: '64748B',
  };
  return colours[colour] || colours.accent;
}

function collectWordCodeRuns(node, options, context) {
  if (node.nodeType === Node.TEXT_NODE) {
    return node.textContent ? [buildWordTextRun(node.textContent, options)] : [];
  }
  if (node.nodeType !== Node.ELEMENT_NODE) return [];
  const nextOptions = { ...options };
  if (!context.isTemplate) {
    const color = getHighlightWordColor(node);
    if (color) nextOptions.color = color;
  }
  if (node.classList.contains('hljs-emphasis')) nextOptions.italic = true;
  if (node.classList.contains('hljs-strong')) nextOptions.bold = true;
  return [...node.childNodes].flatMap((child) => collectWordCodeRuns(child, nextOptions, context));
}

function getHighlightWordColor(element) {
  const colorByClass = {
    'hljs-keyword': '1D4ED8',
    'hljs-selector-tag': '1D4ED8',
    'hljs-built_in': '1D4ED8',
    'hljs-name': '1D4ED8',
    'hljs-tag': '1D4ED8',
    'hljs-string': '15803D',
    'hljs-title': '15803D',
    'hljs-section': '15803D',
    'hljs-attribute': '15803D',
    'hljs-literal': '15803D',
    'hljs-template-tag': '15803D',
    'hljs-template-variable': '15803D',
    'hljs-type': '15803D',
    'hljs-addition': '15803D',
    'hljs-comment': '64748B',
    'hljs-quote': '64748B',
    'hljs-deletion': '64748B',
    'hljs-meta': '64748B',
    'hljs-number': 'B45309',
    'hljs-regexp': 'B45309',
    'hljs-link': 'B45309',
    'hljs-selector-id': 'B45309',
    'hljs-selector-class': 'B45309',
    'hljs-variable': 'A21CAF',
    'hljs-symbol': 'A21CAF',
    'hljs-bullet': 'A21CAF',
    'hljs-subst': 'A21CAF',
  };
  return [...element.classList].map((className) => colorByClass[className]).find(Boolean) || '';
}

function collectWordInlineRuns(node, options, context) {
  const runs = [];
  node.childNodes.forEach((child) => {
    if (child.nodeType === Node.TEXT_NODE) {
      if (child.textContent) runs.push(buildWordTextRun(child.textContent, options));
      return;
    }
    if (child.nodeType !== Node.ELEMENT_NODE) return;
    const tagName = child.tagName.toLowerCase();
    if (tagName === 'br') {
      runs.push('<w:r><w:br/></w:r>');
      return;
    }
    if (tagName === 'img' && (child.classList.contains('diagram-image') || child.dataset.wordImageName)) return;

    const nextOptions = { ...options };
    if (['strong', 'b'].includes(tagName)) nextOptions.bold = true;
    if (['em', 'i'].includes(tagName)) nextOptions.italic = true;
    if (tagName === 'code' || tagName === 'kbd') {
      if (context.hasTemplateStyle(context.styleMapping.inlineCode)) {
        nextOptions.characterStyleId = context.styleMapping.inlineCode;
      } else {
        nextOptions.code = true;
      }
    }
    if (tagName === 's' || tagName === 'del') nextOptions.strike = true;

    if (tagName === 'a') {
      const href = child.getAttribute('href') || '';
      const linkRuns = collectWordInlineRuns(child, context.isTemplate
        ? nextOptions
        : { ...nextOptions, color: '0369A1', underline: true }, context);
      if (isSafeExternalHyperlink(href)) {
        const relationshipId = context.allocateHyperlink(href);
        runs.push(`<w:hyperlink r:id="${escapeXml(relationshipId)}" w:history="1">${linkRuns.join('')}</w:hyperlink>`);
      } else if (href.startsWith('#')) {
        runs.push(`<w:hyperlink w:anchor="${escapeXml(href.slice(1))}" w:history="1">${linkRuns.join('')}</w:hyperlink>`);
      } else {
        runs.push(...linkRuns);
      }
      return;
    }

    runs.push(...collectWordInlineRuns(child, nextOptions, context));
  });
  return runs;
}

function isSafeExternalHyperlink(value) {
  return /^(https?:|mailto:)/i.test(String(value || '').trim());
}

function buildWordParagraph(runs, options = {}) {
  const pPr = buildWordParagraphProperties(options);
  return `    <w:p>${pPr}${runs.join('')}</w:p>`;
}

function buildWordParagraphProperties(options) {
  const parts = [];
  const spacing = [];
  if (options.baseParagraphPropertiesXml) parts.push(options.baseParagraphPropertiesXml);
  if (options.styleId && !containsWordElement(options.baseParagraphPropertiesXml, 'pStyle')) {
    parts.push(`<w:pStyle w:val="${escapeXml(options.styleId)}"/>`);
  }
  if (options.keepNext && !containsWordElement(options.baseParagraphPropertiesXml, 'keepNext')) parts.push('<w:keepNext/>');
  if (options.keepLines && !containsWordElement(options.baseParagraphPropertiesXml, 'keepLines')) parts.push('<w:keepLines/>');
  if (options.numId) {
    parts.push(`<w:numPr><w:ilvl w:val="${Number(options.ilvl) || 0}"/><w:numId w:val="${escapeXml(options.numId)}"/></w:numPr>`);
  }
  if ((options.before || options.after) && !containsWordElement(options.baseParagraphPropertiesXml, 'spacing')) {
    if (options.before) spacing.push(`w:before="${options.before}"`);
    if (options.after) spacing.push(`w:after="${options.after}"`);
    parts.push(`<w:spacing ${spacing.join(' ')}/>`);
  }
  if ((options.indentLeft || options.hanging) && !containsWordElement(options.baseParagraphPropertiesXml, 'ind')) {
    parts.push(`<w:ind w:left="${options.indentLeft ?? 0}"${options.hanging ? ` w:hanging="${options.hanging}"` : ''}/>`);
  }
  if (options.code && !options.styleId && !containsWordElement(options.baseParagraphPropertiesXml, 'shd')) {
    parts.push('<w:shd w:fill="F8FAFC"/>');
  }
  if (options.bottomBorder) {
    parts.push('<w:pBdr><w:bottom w:val="single" w:sz="8" w:space="1" w:color="CBD5E1"/></w:pBdr>');
  }
  return parts.length ? `<w:pPr>${parts.join('')}</w:pPr>` : '';
}

function paragraphStyleOptions(styleKey, context, fallbackOptions = {}) {
  const styleId = context.styleMapping[styleKey] || '';
  if (context.hasTemplateStyle(styleId)) return { styleId };
  return { styleId, ...fallbackOptions };
}

function buildWordTextRun(text, options = {}) {
  const rPr = buildWordRunProperties(options);
  const segments = String(text).replace(/\r\n/g, '\n').split('\n');
  const content = segments.map((segment, index) => {
    const textElement = `<w:t xml:space="preserve">${escapeXml(segment)}</w:t>`;
    return index === 0 ? textElement : `<w:br/>${textElement}`;
  }).join('');
  return `<w:r>${rPr}${content}</w:r>`;
}

function buildWordRunProperties(options) {
  const parts = [];
  if (options.baseRunPropertiesXml) parts.push(options.baseRunPropertiesXml);
  if (options.characterStyleId) parts.push(`<w:rStyle w:val="${escapeXml(options.characterStyleId)}"/>`);
  if (options.bold && !containsWordElement(options.baseRunPropertiesXml, 'b')) parts.push('<w:b/>');
  if (options.italic && !containsWordElement(options.baseRunPropertiesXml, 'i')) parts.push('<w:i/>');
  if (options.underline && !containsWordElement(options.baseRunPropertiesXml, 'u')) parts.push('<w:u w:val="single"/>');
  if (options.strike && !containsWordElement(options.baseRunPropertiesXml, 'strike')) parts.push('<w:strike/>');
  if (options.color && !containsWordElement(options.baseRunPropertiesXml, 'color')) parts.push(`<w:color w:val="${options.color}"/>`);
  if (options.size && !containsWordElement(options.baseRunPropertiesXml, 'sz')) parts.push(`<w:sz w:val="${options.size}"/>`);
  if (options.code && !options.characterStyleId) {
    if (!containsWordElement(options.baseRunPropertiesXml, 'rFonts')) {
      parts.push('<w:rFonts w:ascii="Consolas" w:hAnsi="Consolas" w:cs="Consolas"/>');
    }
    if (!containsWordElement(options.baseRunPropertiesXml, 'sz')) parts.push('<w:sz w:val="19"/>');
  }
  return parts.length ? `<w:rPr>${parts.join('')}</w:rPr>` : '';
}

function containsWordElement(xml, localName) {
  return new RegExp(`<w:${localName}(?:[\\s/>])`).test(xml || '');
}

function buildWordTable(table, context) {
  const rows = [...table.querySelectorAll('tr')];
  const columnCount = Math.max(...rows.map((row) => [...row.children].filter(isTableCell).length), 1);
  const gridWidths = calculateTableGridWidths(rows, columnCount, context);
  const prototype = context.tablePrototype;
  const tableProperties = buildTableProperties(prototype, context);
  const gridXml = `<w:tblGrid>${gridWidths.map((width) => `<w:gridCol w:w="${width}"/>`).join('')}</w:tblGrid>`;
  const rowXml = rows.map((row, rowIndex) => {
    const cells = [...row.children].filter(isTableCell);
    const isHeader = rowIndex === 0 && cells.some((cell) => cell.tagName.toLowerCase() === 'th');
    const rowPrototype = isHeader ? prototype?.headerRow : prototype?.bodyRow;
    let trPrXml = rowPrototype?.trPrXml || '';
    if (isHeader && !containsWordElement(trPrXml, 'tblHeader')) trPrXml += '<w:tblHeader/>';
    const cellsXml = cells.map((cell, cellIndex) => {
      const cellPrototype = rowPrototype?.cells?.[cellIndex]
        || rowPrototype?.cells?.[rowPrototype.cells.length - 1]
        || null;
      const runOptions = cellPrototype?.rPrXml ? { baseRunPropertiesXml: cellPrototype.rPrXml } : {};
      if (isHeader && !prototype) runOptions.bold = true;
      const runs = collectWordInlineRuns(cell, runOptions, context);
      const paragraphOptions = {
        baseParagraphPropertiesXml: cellPrototype?.pPrXml || '',
      };
      if (!paragraphOptions.baseParagraphPropertiesXml) paragraphOptions.styleId = context.styleMapping.body;
      const tcPr = `<w:tcPr><w:tcW w:w="${gridWidths[cellIndex] || gridWidths[gridWidths.length - 1]}" w:type="dxa"/>${cellPrototype?.tcPrXml || ''}</w:tcPr>`;
      return `<w:tc>${tcPr}${buildWordParagraph(runs.length ? runs : [buildWordTextRun('', runOptions)], paragraphOptions)}</w:tc>`;
    }).join('');
    return `<w:tr>${trPrXml ? `<w:trPr>${trPrXml}</w:trPr>` : ''}${cellsXml}</w:tr>`;
  }).join('');
  return `    <w:tbl>${tableProperties}${gridXml}${rowXml}</w:tbl>`;
}

function isTableCell(cell) {
  return ['td', 'th'].includes(cell.tagName?.toLowerCase());
}

function buildTableProperties(prototype, context) {
  if (prototype?.tblPrXml) return fitTablePropertiesToPage(prototype.tblPrXml, context.usableWidthDxa);
  const tableStyle = context.styleMapping.table
    ? `<w:tblStyle w:val="${escapeXml(context.styleMapping.table)}"/>`
    : '';
  return `<w:tblPr>${tableStyle}<w:tblW w:w="${context.usableWidthDxa}" w:type="dxa"/><w:tblBorders><w:top w:val="single" w:sz="4" w:color="D5DCE8"/><w:left w:val="single" w:sz="4" w:color="D5DCE8"/><w:bottom w:val="single" w:sz="4" w:color="D5DCE8"/><w:right w:val="single" w:sz="4" w:color="D5DCE8"/><w:insideH w:val="single" w:sz="4" w:color="D5DCE8"/><w:insideV w:val="single" w:sz="4" w:color="D5DCE8"/></w:tblBorders><w:tblCellMar><w:top w:w="80" w:type="dxa"/><w:left w:w="100" w:type="dxa"/><w:bottom w:w="80" w:type="dxa"/><w:right w:w="100" w:type="dxa"/></w:tblCellMar></w:tblPr>`;
}

function fitTablePropertiesToPage(tblPrXml, usableWidthDxa) {
  try {
    const xml = `<root xmlns:w="${WORD_MAIN_NS}">${tblPrXml}</root>`;
    const document = new DOMParser().parseFromString(xml, 'application/xml');
    if (document.getElementsByTagName('parsererror').length) return tblPrXml;
    const tblW = document.getElementsByTagNameNS(WORD_MAIN_NS, 'tblW')[0];
    const type = tblW?.getAttributeNS(WORD_MAIN_NS, 'type') || tblW?.getAttribute('w:type');
    const width = Number(tblW?.getAttributeNS(WORD_MAIN_NS, 'w') || tblW?.getAttribute('w:w'));
    if (tblW && type === 'dxa' && width > usableWidthDxa) {
      tblW.setAttributeNS(WORD_MAIN_NS, 'w:w', String(usableWidthDxa));
    }
    return new XMLSerializer().serializeToString(document.documentElement.firstElementChild);
  } catch {
    return tblPrXml;
  }
}

function calculateTableGridWidths(rows, columnCount, context) {
  const prototypeWidths = context.tablePrototype?.gridWidths || [];
  if (prototypeWidths.length === columnCount) {
    return scaleWidthsToMaximum(prototypeWidths, context.usableWidthDxa);
  }
  const weights = Array.from({ length: columnCount }, (_, columnIndex) => {
    const maximumLength = rows.reduce((maximum, row) => {
      const cells = [...row.children].filter(isTableCell);
      return Math.max(maximum, (cells[columnIndex]?.textContent || '').trim().length);
    }, 0);
    return Math.min(Math.max(maximumLength, 6), 48);
  });
  const total = weights.reduce((sum, value) => sum + value, 0) || columnCount;
  const widths = weights.map((weight) => Math.max(480, Math.floor(context.usableWidthDxa * weight / total)));
  const difference = context.usableWidthDxa - widths.reduce((sum, width) => sum + width, 0);
  widths[widths.length - 1] += difference;
  return widths;
}

function scaleWidthsToMaximum(widths, maximum) {
  const total = widths.reduce((sum, width) => sum + width, 0);
  if (!total || total <= maximum) return widths;
  const scaled = widths.map((width) => Math.max(1, Math.floor(width * maximum / total)));
  scaled[scaled.length - 1] += maximum - scaled.reduce((sum, width) => sum + width, 0);
  return scaled;
}

function buildWordImageParagraph(imageElement, context) {
  const imageName = imageElement.dataset.wordImageName || imageElement.getAttribute('src');
  const image = context.imageRelationships.find((item) => item.name === imageName);
  if (!image) return buildWordParagraph([buildWordTextRun(imageElement.alt || 'Mermaid diagram')]);

  const sourceWidth = Math.max(1, Number(image.displayWidth) || 640);
  const sourceHeight = Math.max(1, Number(image.displayHeight) || 360);
  const requestedCx = pxToEmu(sourceWidth);
  const requestedCy = pxToEmu(sourceHeight);
  const maxCx = context.usableWidthDxa * 635;
  const maxCy = Math.floor(context.usableHeightDxa * 635 * 0.9);
  const scale = Math.min(1, maxCx / requestedCx, maxCy / requestedCy);
  const cx = Math.max(1, Math.floor(requestedCx * scale));
  const cy = Math.max(1, Math.floor(requestedCy * scale));
  const name = escapeXml(imageElement.alt || `Diagram ${image.docPrId}`);
  const pageBreakBefore = imageElement.dataset.wordPageBreakBefore === 'true' ? '<w:pageBreakBefore/>' : '';

  return `    <w:p><w:pPr>${pageBreakBefore}<w:spacing w:before="160" w:after="160"/></w:pPr><w:r><w:drawing><wp:inline distT="0" distB="0" distL="0" distR="0"><wp:extent cx="${cx}" cy="${cy}"/><wp:docPr id="${image.docPrId}" name="${name}"/><wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect="1"/></wp:cNvGraphicFramePr><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic><pic:nvPicPr><pic:cNvPr id="${image.docPrId}" name="${escapeXml(image.name)}"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed="${image.relationshipId}"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="${cx}" cy="${cy}"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>`;
}

function pxToEmu(value) {
  return Math.max(1, Math.round(value * 9525));
}

async function createDefaultWordPackage({ bodyXml, title, context }) {
  const now = new Date().toISOString();
  const relationships = context.defaultRelationships;
  const contentTypes = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Default Extension="png" ContentType="image/png"/>
  <Default Extension="svg" ContentType="image/svg+xml"/>
  <Default Extension="jpg" ContentType="image/jpeg"/>
  <Default Extension="jpeg" ContentType="image/jpeg"/>
  <Default Extension="gif" ContentType="image/gif"/>
  <Default Extension="webp" ContentType="image/webp"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
  <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
  <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
</Types>`;
  const rootRels = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>`;
  const documentRels = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
${relationships.map((relationship) => `  <Relationship Id="${relationship.id}" Type="${relationship.type}" Target="${escapeXml(relationship.target)}"${relationship.targetMode ? ` TargetMode="${relationship.targetMode}"` : ''}/>`).join('\n')}
</Relationships>`;
  const documentXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">
  <w:body>
${bodyXml}
    <w:sectPr>
      <w:pgSz w:w="11906" w:h="16838"/>
      <w:pgMar w:top="840" w:right="840" w:bottom="960" w:left="840" w:header="720" w:footer="720" w:gutter="0"/>
    </w:sectPr>
  </w:body>
</w:document>`;
  const coreXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dc:title>${escapeXml(title)}</dc:title>
  <dc:creator>${APP_NAME}</dc:creator>
  <cp:lastModifiedBy>${APP_NAME}</cp:lastModifiedBy>
  <dcterms:created xsi:type="dcterms:W3CDTF">${now}</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">${now}</dcterms:modified>
</cp:coreProperties>`;
  const appXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
  <Application>${APP_NAME}</Application><DocSecurity>0</DocSecurity><ScaleCrop>false</ScaleCrop><Company></Company><LinksUpToDate>false</LinksUpToDate><SharedDoc>false</SharedDoc><HyperlinksChanged>false</HyperlinksChanged><AppVersion>1.0</AppVersion>
</Properties>`;

  return await createCompressedZipBlob([
    { name: '[Content_Types].xml', data: contentTypes },
    { name: '_rels/.rels', data: rootRels },
    { name: 'docProps/core.xml', data: coreXml },
    { name: 'docProps/app.xml', data: appXml },
    { name: 'word/document.xml', data: documentXml },
    { name: 'word/_rels/document.xml.rels', data: documentRels },
    ...context.imageRelationships.map((image) => ({ name: image.partName, data: image.data })),
  ], DOCX_MIME);
}

function sanitiseMediaName(value) {
  return String(value || 'image.png')
    .split(/[\\/]/)
    .pop()
    .replace(/[^a-z0-9._-]+/gi, '-')
    .replace(/^-+|-+$/g, '') || 'image.png';
}
