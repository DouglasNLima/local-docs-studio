const MAMMOTH_MODULE_PATH = '../../vendor/mammoth-1.12.0.browser.min.js';

let mammothPromise = null;

self.addEventListener('message', (event) => {
  void handleMessage(event.data || {});
});

async function handleMessage(message) {
  const { id, fileName, arrayBuffer } = message;

  try {
    const mammoth = await loadMammoth();
    const warnings = [];
    const assets = [];
    const usedAssetPaths = new Set((message.usedAssetPaths || []).map((path) => String(path).toLowerCase()));
    const sourceStem = slugFromText(getFileStem(fileName));
    let imageIndex = 0;

    const result = await mammoth.convertToHtml({ arrayBuffer }, {
      convertImage: mammoth.images.imgElement(async (image) => {
        const mimeType = normaliseImageMimeType(image.contentType);
        if (!mimeType) {
          warnings.push(`${fileName}: one embedded image was skipped because only PNG, JPEG, GIF, and WebP images are supported.`);
          return {};
        }

        const base64 = String(await image.readAsBase64String()).replace(/\s+/g, '');
        if (!/^[a-z0-9+/]+={0,2}$/i.test(base64)) {
          warnings.push(`${fileName}: one embedded image was skipped because its data could not be read.`);
          return {};
        }

        imageIndex += 1;
        const alt = String(image.altText || '').trim();
        const extension = getImageExtension(mimeType);
        const labelStem = alt ? slugFromText(alt) : `image-${imageIndex}`;
        const path = makeUniqueAssetPath(`assets/images/${sourceStem}-${labelStem}.${extension}`, usedAssetPaths);
        const name = path.split('/').pop() || `image-${imageIndex}.${extension}`;

        assets.push({
          path,
          name,
          alt: alt || 'Image',
          mimeType,
          base64,
          size: base64ByteLength(base64),
        });

        return { src: path };
      }),
    });

    self.postMessage({
      id,
      ok: true,
      html: result?.value || '',
      messages: serialiseMammothMessages(result?.messages || []),
      warnings,
      assets,
    });
  } catch (error) {
    self.postMessage({
      id,
      ok: false,
      error: error?.message || 'Word converter failed.',
    });
  }
}

async function loadMammoth() {
  if (!mammothPromise) {
    mammothPromise = import(MAMMOTH_MODULE_PATH).then(() => {
      if (!self.mammoth?.convertToHtml || !self.mammoth?.images?.imgElement) {
        throw new Error('Word converter failed to load.');
      }
      return self.mammoth;
    });
  }
  return await mammothPromise;
}

function serialiseMammothMessages(messages) {
  return messages.map((message) => ({
    type: message?.type || '',
    message: message?.message || '',
  }));
}

function normaliseImageMimeType(mimeType) {
  const value = String(mimeType || '').toLowerCase().replace('image/jpg', 'image/jpeg');
  return /^image\/(?:png|jpeg|gif|webp)$/.test(value) ? value : '';
}

function getImageExtension(mimeType) {
  if (/webp$/i.test(mimeType)) return 'webp';
  if (/gif$/i.test(mimeType)) return 'gif';
  if (/jpe?g$/i.test(mimeType)) return 'jpg';
  return 'png';
}

function makeUniqueAssetPath(path, usedPaths) {
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

function normalisePath(path) {
  return String(path || '').replace(/\\/g, '/').replace(/^\/+/, '');
}

function slugFromText(value) {
  return slugify(value) || 'document';
}

function slugify(value) {
  return String(value)
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

function getFileStem(fileName) {
  return String(fileName || 'imported-document')
    .split(/[\\/]/)
    .pop()
    .replace(/\.(docx|html?|pdf)$/i, '') || 'imported-document';
}

function base64ByteLength(base64) {
  const value = String(base64 || '');
  const padding = value.endsWith('==') ? 2 : value.endsWith('=') ? 1 : 0;
  return Math.max(0, Math.floor(value.length * 3 / 4) - padding);
}
