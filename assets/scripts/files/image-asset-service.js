import { base64ToUint8Array, uint8ArrayToBase64 } from '../utils/binary.js';
import { slugFromText } from '../utils/format.js';
import { collectImageTokens, IMAGE_EXTENSIONS, IMAGE_SESSION_MAX_BYTES, inspectImageBytes, relativeImageReference, resolveImageReference, rewriteImageReferences } from '../utils/image-references.js';

export const imageStorageLabel = (asset) => asset?.storage === 'workspace' ? 'Saved in workspace'
  : asset?.storage === 'workspace-readonly' ? 'Available from selected folder — read-only'
  : asset?.storage === 'failed' ? 'Save failed — bytes available in session'
  : 'Available in session — not saved to folder';

export function createImageAssetService({ state, nativeBridgeClient, callbacks = {} }) {
  let writeChain = Promise.resolve();
  const pendingReads = new Map();
  const recoveryAssets = new Set();
  const { readRecordText, replaceDocumentContent, setStatus = () => {} } = callbacks;
  const context = () => ({ map: state.managedAssets, directory: state.workspaceDirectoryHandle,
    nativeId: state.nativeWorkspaceId, version: state.imageWorkspaceVersion || 0, root: state.workspaceRootPath || '' });
  const isCurrent = (origin) => origin.version === (state.imageWorkspaceVersion || 0)
    && origin.directory === state.workspaceDirectoryHandle && origin.nativeId === state.nativeWorkspaceId;
  const resolverOptions = () => ({ root: state.workspaceRootPath || '' });

  function resolve(reference, documentPath = state.activePath, options = {}) {
    const result = resolveImageReference(reference, documentPath, { ...resolverOptions(), ...options });
    if (result.kind !== 'local') return result;
    if (!state.managedAssets.has(result.path)) {
      // Only a specifically tagged legacy session asset may retain old root
      // semantics. Disk paths never receive a silent root-relative fallback.
      const legacy = [...state.managedAssets.values()].find((asset) => asset.legacyRootReference
        && asset.path === reference);
      if (legacy) return { ...result, path: legacy.path, legacy: true };
    }
    return result;
  }

  async function refreshInventory() {
    const origin = context();
    if (origin.nativeId) {
      const payload = await host('listWorkspaceImages', { nativeWorkspaceId: origin.nativeId });
      if (!isCurrent(origin)) return;
      for (const item of payload.images || []) registerExisting(item.path, item, origin);
      return;
    }
    if (!origin.directory) return;
    let count = 0;
    async function walk(directory, prefix = '', depth = 0) {
      if (depth > 12 || count >= 500 || !isCurrent(origin)) return;
      for await (const [name, handle] of directory.entries()) {
        const path = prefix ? `${prefix}/${name}` : name;
        if (handle.kind === 'directory') await walk(handle, path, depth + 1);
        else if (IMAGE_EXTENSIONS.test(name) && count++ < 500) registerExisting(path, { handle }, origin);
      }
    }
    await walk(origin.directory);
  }

  function registerExisting(path, metadata, origin) {
    const previous = origin.map.get(path);
    if (previous && previous.storage !== 'workspace') return;
    origin.map.set(path, { ...previous, ...metadata, path, name: path.split('/').pop(), storage: 'workspace' });
  }

  async function ensureAsset(path, origin = context()) {
    const existing = origin.map.get(path);
    if (existing?.base64) return existing;
    const key = `${origin.version}:${origin.nativeId}:${path}`;
    if (pendingReads.has(key)) return pendingReads.get(key);
    const reading = (async () => {
      let bytes;
      if (origin.nativeId) {
        const result = await host('readWorkspaceImage', { nativeWorkspaceId: origin.nativeId, path });
        bytes = base64ToUint8Array(result.base64);
      } else if (origin.directory) {
        const handle = await fileHandle(origin.directory, path);
        const file = await handle.getFile();
        bytes = new Uint8Array(await file.arrayBuffer());
      } else throw new Error(`Missing file: ${path}. Locate/import the image or select its workspace folder.`);
      enforceSessionLimit(bytes.length, origin.map);
      const asset = await assetFromBytes(bytes, path);
      if (!isCurrent(origin)) { URL.revokeObjectURL(asset.objectUrl); throw new Error('The workspace changed while reading the image.'); }
      Object.assign(asset, { storage: 'workspace', ...existing, base64: asset.base64, dataUrl: asset.dataUrl, objectUrl: asset.objectUrl });
      origin.map.set(path, asset);
      return asset;
    })();
    pendingReads.set(key, reading);
    try { return await reading; } finally { pendingReads.delete(key); }
  }

  async function acquireFile(file, { handle = null } = {}) {
    const origin = context();
    if (handle && origin.directory?.resolve) {
      const parts = await origin.directory.resolve(handle);
      if (parts?.length) return await ensureAsset(parts.join('/'), origin);
    }
    const bytes = new Uint8Array(await file.arrayBuffer());
    const info = inspectImageBytes(bytes, { name: file.name, mimeType: file.type });
    enforceSessionLimit(bytes.length, origin.map);
    const stem = slugFromText(file.name.replace(/\.[^.]+$/, '')) || 'image';
    const asset = await assetFromBytes(bytes, `${origin.root ? `${origin.root}/` : ''}assets/images/${stem}.${info.extension}`);
    if (!isCurrent(origin)) { recoveryAssets.add(asset); throw new Error('The workspace changed. Image bytes remain available in Manage assets for recovery.'); }
    asset.path = availableSessionPath(asset.path, origin.map);
    asset.name = asset.path.split('/').pop();
    asset.storage = 'session';
    origin.map.set(asset.path, asset);
    await persistAsset(asset, origin);
    return asset;
  }

  async function resolveInput(value, documentPath = state.activePath) {
    const origin = context();
    let result;
    if (/^(?:[a-z]:[\\/]|\\\\|\/)/i.test(value) && !/^https?:/i.test(value)) {
      if (!state.nativeWorkspaceId) throw new Error('Select/import the file to confirm that it belongs to the workspace.');
      const payload = await host('readWorkspaceImage', { nativeWorkspaceId: state.nativeWorkspaceId, path: value, absoluteInput: true });
      const asset = await assetFromBytes(base64ToUint8Array(payload.base64), payload.path);
      if (!isCurrent(origin)) { URL.revokeObjectURL(asset.objectUrl); throw new Error('The workspace changed while resolving the image.'); }
      asset.storage = 'workspace';
      state.managedAssets.set(asset.path, asset);
      result = { kind: 'local', path: payload.path, reference: relativeImageReference(documentPath, payload.path), asset };
    } else {
      result = resolve(value, documentPath, { literal: !/^(?:https?:|data:)/i.test(value) });
      if (result.kind === 'local') {
        try { result.asset = await ensureAsset(result.path, origin); }
        catch (literalError) {
          const uri = resolve(value, documentPath);
          if (uri.kind !== 'local' || uri.path === result.path) throw literalError;
          result = { ...uri, asset: await ensureAsset(uri.path, origin) };
        }
      }
    }
    if (result.kind === 'invalid') throw new Error(result.message);
    return result;
  }

  async function pickNativeImage() {
    const origin = context();
    const payload = await host('pickWorkspaceImage', { nativeWorkspaceId: origin.nativeId });
    if (payload.cancelled) return null;
    const asset = await assetFromBytes(base64ToUint8Array(payload.base64), payload.path);
    asset.storage = 'workspace';
    if (!isCurrent(origin)) {
      recoveryAssets.add(asset);
      throw new Error(`Image available in the previous workspace: ${asset.path}. No reference inserted.`);
    }
    const previous = origin.map.get(asset.path);
    if (previous?.base64) { URL.revokeObjectURL(asset.objectUrl); return previous; }
    origin.map.set(asset.path, asset);
    return asset;
  }

  async function persistAsset(asset, origin = context()) {
    if (asset.storage === 'workspace') return asset;
    if (!origin.nativeId && !origin.directory) return asset;
    const write = async () => {
      if (!isCurrent(origin)) return asset;
      try {
        const bytes = base64ToUint8Array(asset.base64 || '');
        inspectImageBytes(bytes, { name: asset.path, mimeType: asset.mimeType });
        let path;
        if (origin.nativeId) {
          const result = await host('createWorkspaceImage', { nativeWorkspaceId: origin.nativeId, name: asset.name, base64: asset.base64, mimeType: asset.mimeType });
          if (!result.created || result.base64 !== asset.base64) throw new Error('Windows did not confirm the saved image bytes.');
          path = result.path;
        } else {
          const permission = origin.directory.queryPermission ? await origin.directory.queryPermission({ mode: 'readwrite' }) : 'granted';
          if (permission !== 'granted') throw new Error('Folder write permission is needed. Use Save pending images to workspace to retry.');
          path = await writeBrowserImage(origin.directory, asset.name, bytes);
        }
        const previousPath = asset.path;
        asset.path = path; asset.name = path.split('/').pop(); asset.storage = 'workspace'; asset.error = '';
        if (origin.map.get(previousPath) === asset) origin.map.delete(previousPath);
        if (!isCurrent(origin)) {
          recoveryAssets.add(asset);
          setStatus(`Image saved in the previous workspace: ${path}. No reference was inserted.`, 'warning');
          return asset;
        }
        origin.map.set(path, asset);
      } catch (error) {
        asset.storage = 'failed'; asset.error = error.message;
        if (!isCurrent(origin)) recoveryAssets.add(asset);
      }
      return asset;
    };
    const operation = writeChain.then(write, write);
    writeChain = operation.catch(() => {});
    return operation;
  }

  async function hydrateImages(root, documentPath, { diagnostics = true, source = '' } = {}) {
    const origin = context();
    await Promise.all([...root.querySelectorAll('img[src]')].map(async (image) => {
      const source = image.getAttribute('src');
      const result = resolve(source, documentPath);
      if (result.kind === 'external' || result.kind === 'embedded') return;
      try {
        if (result.kind !== 'local') throw new Error(result.message);
        const asset = await ensureAsset(result.path, origin);
        if (!isCurrent(origin)) return;
        image.dataset.managedAssetPath = asset.path;
        image.src = asset.objectUrl || asset.dataUrl;
        image.title = `${imageStorageLabel(asset)}: ${asset.path}`;
        await image.decode?.().catch(() => {});
      } catch (error) {
        image.removeAttribute('src');
        if (!isCurrent(origin)) return;
        image.dataset.imageMissing = result.path || source;
        if (diagnostics) {
          const notice = document.createElement('span');
          notice.className = 'image-reference-error';
          notice.textContent = `${error.message} (${result.path || source}). Use Insert image to locate/import it.`;
          notice.setAttribute('role', 'status');
          image.after(notice);
        }
      }
    }));
    if (diagnostics && isCurrent(origin)) {
      for (const token of collectImageTokens(source)) {
        const result = resolve(token.href, documentPath);
        if (result.kind !== 'invalid' || [...root.querySelectorAll('[data-image-missing]')].some((element) => element.dataset.imageMissing === token.href)) continue;
        const notice = document.createElement('p'); notice.className = 'image-reference-error';
        notice.textContent = `${token.href}: ${result.message} Use Insert image to locate/import it.`;
        notice.setAttribute('role', 'status'); root.append(notice);
      }
    }
  }

  function storageSummary(source, path) {
    const results = collectImageTokens(source).map((token) => resolve(token.href, path));
    const pending = results.filter((result) => result.kind === 'local' && state.managedAssets.get(result.path)?.base64
      && !['workspace', 'workspace-readonly'].includes(state.managedAssets.get(result.path)?.storage));
    const failed = pending.some((result) => state.managedAssets.get(result.path)?.storage === 'failed');
    const external = results.filter((result) => result.kind === 'external').length;
    return { message: [pending.length ? `${pending.length} image${pending.length === 1 ? '' : 's'} ${failed ? 'could not be saved' : 'available in session'} — not saved to folder. Use Manage assets or export a Markdown Bundle ZIP.` : '', external ? `${external} external image${external === 1 ? '' : 's'} require network access.` : ''].filter(Boolean).join(' '), pending: pending.length, external };
  }

  async function ensureDocumentImages(source, documentPath) {
    const missing = [];
    for (const token of collectImageTokens(source)) {
      const result = resolve(token.href, documentPath);
      if (result.kind === 'local') {
        try { await ensureAsset(result.path); } catch (error) { missing.push(error.message); }
      } else if (result.kind === 'invalid') missing.push(`${token.href}: ${result.message}`);
    }
    return missing;
  }

  async function savePendingImages() {
    const origin = context();
    if (origin.directory?.requestPermission && await origin.directory.requestPermission({ mode: 'readwrite' }) !== 'granted') throw new Error('Folder write permission was denied. Export a Markdown Bundle ZIP to keep the image bytes.');
    if (!origin.directory && !origin.nativeId) throw new Error('Open an authorised workspace folder first, or export a Markdown Bundle ZIP.');
    const pending = [...origin.map.values()].filter((asset) => !['workspace', 'workspace-readonly'].includes(asset.storage));
    for (const asset of pending) {
      if (!asset.base64) { asset.error = 'Image bytes are missing. Locate/import the file.'; continue; }
      const oldPath = asset.path;
      const snapshots = [];
      let readOnlyReference = '';
      for (const record of state.files) {
        const source = await readRecordText(record);
        const references = new Map(collectImageTokens(source).map((token) => [token.href, resolve(token.href, record.path)]));
        if (record.readOnly && [...references.values()].some((reference) => reference.path === oldPath)) readOnlyReference = record.path;
        else if (!record.readOnly) snapshots.push({ record, source, references });
      }
      if (readOnlyReference) {
        asset.error = `Cannot update image references in read-only document ${readOnlyReference}. Export a Markdown Bundle or make an editable copy first.`;
        continue;
      }
      await persistAsset(asset, origin);
      if (!isCurrent(origin)) break;
      if (asset.storage !== 'workspace') continue;
      for (const { record, source, references } of snapshots) {
        if (!state.files.includes(record) || await readRecordText(record) !== source) {
          setStatus(`Image saved at ${asset.path}; a changed document needs its reference updated manually.`, 'warning'); continue;
        }
        const next = rewriteImageReferences(source, record.path, (path) => path === oldPath ? asset.path : '', { ...resolverOptions(), resolveReference: (href) => references.get(href) });
        if (next !== source) replaceDocumentContent(record, next);
      }
    }
    return { saved: pending.filter((asset) => asset.storage === 'workspace').length, failed: pending.filter((asset) => asset.storage !== 'workspace').length };
  }

  async function getUsage() {
    const usage = new Map();
    for (const record of state.files) {
      const source = await readRecordText(record);
      for (const token of collectImageTokens(source)) {
        const result = resolve(token.href, record.path);
        const key = result.path || token.href;
        const entry = usage.get(key) || { result, records: [] };
        entry.records.push(record.path); usage.set(key, entry);
      }
    }
    return usage;
  }

  function recoverableAssets() { return [...recoveryAssets]; }
  async function registerSelectedImage(file, path) {
    const origin = context();
    const bytes = new Uint8Array(await file.arrayBuffer());
    enforceSessionLimit(bytes.length, origin.map);
    const asset = await assetFromBytes(bytes, path);
    if (!isCurrent(origin)) { URL.revokeObjectURL(asset.objectUrl); return; }
    asset.storage = 'workspace-readonly';
    origin.map.set(path, asset);
  }
  function canonicaliseLegacyReferences(source, path) {
    return rewriteImageReferences(source, path, (identity) => identity, { ...resolverOptions(), resolveReference: resolve });
  }
  function recoverAsset(asset) {
    recoveryAssets.delete(asset);
    if (asset.objectUrl) URL.revokeObjectURL(asset.objectUrl);
    asset.objectUrl = URL.createObjectURL(new Blob([base64ToUint8Array(asset.base64)], { type: asset.mimeType }));
    asset.storage = 'session';
    asset.path = availableSessionPath(`assets/images/${asset.name}`, state.managedAssets);
    state.managedAssets.set(asset.path, asset);
  }

  async function host(method, payload) {
    const result = await nativeBridgeClient[method](payload);
    if (!result.ok) throw new Error(result.message || 'Windows image operation failed.');
    return result.response?.payload || {};
  }

  return { resolve, resolveInput, refreshInventory, ensureAsset, acquireFile, persistAsset, hydrateImages, ensureDocumentImages,
    savePendingImages, getUsage, recoverableAssets, recoverAsset, isCurrent, context, canonicaliseLegacyReferences, registerSelectedImage, storageSummary, pickNativeImage };
}

async function assetFromBytes(bytes, path) {
  const info = inspectImageBytes(bytes, { name: path });
  const blob = new Blob([bytes], { type: info.mimeType });
  // Decode locally to reject truncated payloads as well as spoofed signatures.
  const bitmap = await createImageBitmap(blob);
  if (bitmap.width * bitmap.height > 40_000_000) { bitmap.close(); throw new Error('Choose an image up to 40 million pixels.'); }
  bitmap.close();
  const base64 = uint8ArrayToBase64(bytes);
  return { path, name: path.split('/').pop(), alt: path.split('/').pop().replace(/\.[^.]+$/, '').replace(/[-_]/g, ' '),
    mimeType: info.mimeType, size: bytes.length, base64, dataUrl: `data:${info.mimeType};base64,${base64}`, objectUrl: URL.createObjectURL(blob) };
}

function enforceSessionLimit(size, assets) {
  const total = [...assets.values()].reduce((sum, asset) => sum + (asset.base64 ? asset.size || 0 : 0), 0);
  if (total + size > IMAGE_SESSION_MAX_BYTES) throw new Error('Image session limit (100 MB) reached. Export the workspace before importing more images.');
}

function availableSessionPath(path, assets) {
  const dot = path.lastIndexOf('.');
  let candidate = path;
  for (let number = 2; assets.has(candidate); number++) candidate = `${path.slice(0, dot)}-${number}${path.slice(dot)}`;
  return candidate;
}

async function fileHandle(root, path, create = false) {
  const parts = path.split('/');
  const name = parts.pop();
  let directory = root;
  for (const part of parts) directory = await directory.getDirectoryHandle(part, { create });
  return directory.getFileHandle(name, { create });
}

async function writeBrowserImage(root, name, bytes) {
  const imageDirectory = await (await root.getDirectoryHandle('assets', { create: true })).getDirectoryHandle('images', { create: true });
  const dot = name.lastIndexOf('.');
  for (let index = 1; index <= 10000; index++) {
    const candidate = index === 1 ? name : `${name.slice(0, dot)}-${index}${name.slice(dot)}`;
    try { await imageDirectory.getFileHandle(candidate); continue; }
    catch (error) { if (error.name !== 'NotFoundError') throw error; }
    const handle = await imageDirectory.getFileHandle(candidate, { create: true });
    const writable = await handle.createWritable({ mode: 'exclusive' });
    try {
      // Check again with the writer lock held; never overwrite a non-empty file.
      if ((await handle.getFile()).size) { await writable.abort(); continue; }
      await writable.write(bytes); await writable.close();
    } catch (error) { await writable.abort().catch(() => {}); throw error; }
    const saved = new Uint8Array(await (await handle.getFile()).arrayBuffer());
    if (saved.length !== bytes.length || saved.some((value, index) => value !== bytes[index])) throw new Error('Saved image bytes could not be verified.');
    return `assets/images/${candidate}`;
  }
  throw new Error('Could not choose a free image filename.');
}
