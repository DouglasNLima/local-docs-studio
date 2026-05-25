import { htmlToMarkdown } from '../utils/html-markdown.js';
import { normalisePath } from '../utils/files.js';
import { sanitiseFileName, slugFromText } from '../utils/format.js';

const MAMMOTH_MODULE_PATH = '../../vendor/mammoth-1.12.0.browser.min.js';
const PDFJS_MODULE_PATH = '../../vendor/pdfjs-5.7.284.js';
const PDFJS_WORKER_PATH = '../../vendor/pdfjs-5.7.284.worker.js';
const DOCX_MIME = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
const MARKDOWN_MIME = 'text/markdown;charset=utf-8';

let mammothPromise = null;
let pdfjsPromise = null;

export async function convertDocumentFiles(files) {
  const selectedFiles = [...(files || [])].filter(isImportableDocumentFile);
  const usedDocumentPaths = new Set();
  const usedAssetPaths = new Set();
  const records = [];
  const textByPath = new Map();
  const assets = [];
  const warnings = [];

  for (const file of selectedFiles) {
    try {
      const converted = await convertDocumentFile(file, {
        assets,
        usedAssetPaths,
        warnings,
      });
      const path = makeUniqueMarkdownPath(file.name, usedDocumentPaths);
      const name = path.split('/').pop() || path;
      const markdown = converted.markdown || fallbackMarkdown(file.name);

      records.push({
        name,
        path,
        converted: true,
        sourceFormat: getSourceFormat(file),
        file: new File([markdown], name, { type: MARKDOWN_MIME }),
      });
      textByPath.set(path, markdown);
    } catch (error) {
      warnings.push(`${file.name}: ${error?.message || 'could not be converted.'}`);
    }
  }

  return {
    records,
    textByPath,
    assets,
    warnings,
  };
}

export function isImportableDocumentFile(file) {
  return isDocxFile(file) || isHtmlFile(file) || isPdfFile(file);
}

export function isPdfFile(file) {
  return /\.pdf$/i.test(file?.name || '') || /application\/pdf/i.test(file?.type || '');
}

function isDocxFile(file) {
  return /\.docx$/i.test(file?.name || '') || file?.type === DOCX_MIME;
}

function isHtmlFile(file) {
  return /\.html?$/i.test(file?.name || '') || /text\/html/i.test(file?.type || '');
}

async function convertDocumentFile(file, context) {
  if (isDocxFile(file)) return await convertDocxFile(file, context);
  if (isHtmlFile(file)) return await convertHtmlFile(file, context);
  if (isPdfFile(file)) return await convertPdfFile(file, context);
  throw new Error('Unsupported document format.');
}

async function convertDocxFile(file, context) {
  const mammoth = await loadMammoth();
  const result = await mammoth.convertToHtml({
    arrayBuffer: await file.arrayBuffer(),
  });

  collectMammothMessages(file, result?.messages || [], context.warnings);
  return convertHtmlSource(file, result?.value || '', context);
}

async function convertHtmlFile(file, context) {
  return convertHtmlSource(file, await file.text(), context);
}

async function convertPdfFile(file, context) {
  const pdfjs = await loadPdfJs();
  const loadingTask = pdfjs.getDocument({
    data: new Uint8Array(await file.arrayBuffer()),
    disableFontFace: true,
    useSystemFonts: true,
  });
  let pdf = null;

  try {
    pdf = await loadingTask.promise;
    const pages = [];
    let extractedCharacters = 0;

    for (let pageNumber = 1; pageNumber <= pdf.numPages; pageNumber += 1) {
      const page = await pdf.getPage(pageNumber);
      const content = await page.getTextContent();
      const pageText = extractPdfPageText(content);
      extractedCharacters += pageText.length;
      pages.push(`## Page ${pageNumber}\n\n${pageText || '_No extractable text on this page._'}`);
    }

    if (!extractedCharacters) {
      context.warnings.push(`${file.name}: no extractable text found in the PDF.`);
    }

    const markdown = [
      `# ${sanitiseFileName(getFileStem(file.name)) || 'Imported PDF'}`,
      '',
      '> Imported from PDF as text-only Markdown. Layout, images, and OCR are not preserved.',
      '',
      pages.join('\n\n'),
    ].join('\n').replace(/\n{3,}/g, '\n\n').trimEnd() + '\n';

    return { markdown };
  } finally {
    if (pdf?.destroy) await pdf.destroy();
    else await loadingTask.destroy?.();
  }
}

function convertHtmlSource(file, html, context) {
  const resolver = createImportedImageResolver(file, context);
  const markdown = htmlToMarkdown(html, {
    resolveImageSource: resolver,
  });
  return { markdown };
}

async function loadMammoth() {
  if (!mammothPromise) {
    mammothPromise = import(MAMMOTH_MODULE_PATH).then(() => {
      if (!window.mammoth?.convertToHtml) {
        throw new Error('Word converter failed to load.');
      }
      return window.mammoth;
    });
  }
  return await mammothPromise;
}

async function loadPdfJs() {
  if (!pdfjsPromise) {
    pdfjsPromise = import(PDFJS_MODULE_PATH).then((pdfjs) => {
      if (!pdfjs?.getDocument) throw new Error('PDF converter failed to load.');
      pdfjs.GlobalWorkerOptions.workerSrc = new URL(PDFJS_WORKER_PATH, import.meta.url).href;
      return pdfjs;
    });
  }
  return await pdfjsPromise;
}

function collectMammothMessages(file, messages, warnings) {
  messages
    .filter((message) => message?.message)
    .slice(0, 5)
    .forEach((message) => {
      warnings.push(`${file.name}: ${message.message}`);
    });

  if (messages.length > 5) {
    warnings.push(`${file.name}: ${messages.length - 5} additional Word conversion warnings were hidden.`);
  }
}

function createImportedImageResolver(file, { assets, usedAssetPaths, warnings }) {
  let imageIndex = 0;
  const sourceStem = slugFromText(getFileStem(file.name));

  return ({ src, alt }) => {
    if (!/^data:/i.test(src)) return src;

    const parsed = parseDataImage(src);
    if (!parsed) {
      warnings.push(`${file.name}: one embedded image was skipped because only PNG, JPEG, GIF, and WebP images are supported.`);
      return null;
    }

    imageIndex += 1;
    const asset = createManagedAssetFromDataImage({
      ...parsed,
      alt,
      sourceStem,
      imageIndex,
      usedAssetPaths,
    });
    assets.push(asset);
    return asset.path;
  };
}

function parseDataImage(src) {
  const match = String(src || '').trim().match(/^data:(image\/(?:png|jpe?g|gif|webp));base64,([\s\S]+)$/i);
  if (!match) return null;

  const mimeType = match[1].toLowerCase().replace('image/jpg', 'image/jpeg');
  const base64 = match[2].replace(/\s+/g, '');
  if (!/^[a-z0-9+/]+={0,2}$/i.test(base64)) return null;

  return {
    mimeType,
    extension: getImageExtension(mimeType),
    base64,
    dataUrl: `data:${mimeType};base64,${base64}`,
  };
}

function createManagedAssetFromDataImage({
  alt,
  sourceStem,
  imageIndex,
  extension,
  mimeType,
  base64,
  dataUrl,
  usedAssetPaths,
}) {
  const labelStem = alt ? slugFromText(alt) : `image-${imageIndex}`;
  const path = makeUniqueAssetPath(`assets/images/${sourceStem}-${labelStem}.${extension}`, usedAssetPaths);
  const bytes = base64ToUint8Array(base64);
  const blob = new Blob([bytes], { type: mimeType });
  const name = path.split('/').pop() || `image-${imageIndex}.${extension}`;

  return {
    path,
    name,
    alt: String(alt || '').trim() || 'Image',
    mimeType,
    base64,
    dataUrl,
    objectUrl: URL.createObjectURL(blob),
    size: bytes.byteLength,
  };
}

function makeUniqueMarkdownPath(fileName, usedPaths) {
  const stem = sanitiseFileName(getFileStem(fileName)) || 'imported-document';
  return makeUniquePath(`${stem}.md`, usedPaths);
}

function makeUniqueAssetPath(path, usedPaths) {
  return makeUniquePath(path, usedPaths);
}

function makeUniquePath(path, usedPaths) {
  const normalised = normalisePath(path);
  const extensionMatch = normalised.match(/(\.[^./]+)$/);
  const extension = extensionMatch?.[1] || '';
  const base = extension ? normalised.slice(0, -extension.length) : normalised;
  let candidate = normalised;
  let suffix = 2;

  while (usedPaths.has(candidate.toLowerCase())) {
    candidate = `${base}-${suffix}${extension}`;
    suffix += 1;
  }

  usedPaths.add(candidate.toLowerCase());
  return candidate;
}

function extractPdfPageText(content) {
  const lines = [];

  for (const item of content?.items || []) {
    const text = normaliseInlinePdfText(item?.str);
    if (!text) continue;

    const y = Number(item?.transform?.[5]);
    const x = Number(item?.transform?.[4]);
    const existing = Number.isFinite(y)
      ? lines.find((line) => Math.abs(line.y - y) <= 2)
      : null;
    const line = existing || {
      y: Number.isFinite(y) ? y : -lines.length,
      parts: [],
    };
    line.parts.push({
      x: Number.isFinite(x) ? x : line.parts.length,
      text,
    });
    if (!existing) lines.push(line);
  }

  return lines
    .sort((a, b) => b.y - a.y)
    .map((line) => line.parts
      .sort((a, b) => a.x - b.x)
      .map((part) => part.text)
      .join(' ')
      .replace(/\s+/g, ' ')
      .trim())
    .filter(Boolean)
    .join('\n\n');
}

function normaliseInlinePdfText(value) {
  return String(value || '')
    .replace(/\u0000/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}

function getFileStem(fileName) {
  return String(fileName || 'imported-document')
    .split(/[\\/]/)
    .pop()
    .replace(/\.(docx|html?|pdf)$/i, '') || 'imported-document';
}

function getSourceFormat(file) {
  if (isDocxFile(file)) return 'DOCX';
  if (isHtmlFile(file)) return 'HTML';
  if (isPdfFile(file)) return 'PDF';
  return 'Document';
}

function getImageExtension(mimeType) {
  if (/webp$/i.test(mimeType)) return 'webp';
  if (/gif$/i.test(mimeType)) return 'gif';
  if (/jpe?g$/i.test(mimeType)) return 'jpg';
  return 'png';
}

function base64ToUint8Array(base64) {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index);
  }
  return bytes;
}

function fallbackMarkdown(fileName) {
  return `# ${sanitiseFileName(getFileStem(fileName)) || 'Imported Document'}\n`;
}
