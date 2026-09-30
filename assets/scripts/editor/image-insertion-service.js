import { escapeHtml } from '../utils/format.js';
import { relativeImageReference, serialiseImage } from '../utils/image-references.js';
import { imageStorageLabel } from '../files/image-asset-service.js';

export function createImageInsertionService({ state, editor, assets, callbacks }) {
  const { captureInsertionContext, isInsertionContextCurrent, restoreInsertionContext, replaceEditorRange,
    setStatus, renderPreview, chooseImageFolder, downloadRecoveryAsset } = callbacks;
  let dialog = null;
  let run = 0;
  let origin = null;
  let selected = null;
  let applying = false;
  let sourceRevision = 0;

  function openImageDialog() {
    if (editor.readOnly) return;
    close(false);
    origin = captureInsertionContext();
    if (!origin.record) { setStatus('Open or create an editable Markdown document before inserting an image.', 'warning'); return; }
    const currentRun = ++run;
    selected = null; applying = false; sourceRevision = 0;
    dialog = document.createElement('dialog');
    dialog.className = 'utility-dialog image-insertion-dialog';
    dialog.setAttribute('aria-labelledby', 'imageInsertionTitle');
    dialog.innerHTML = `<form class="utility-dialog-card image-insertion-card">
      <div class="utility-dialog-top"><span class="template-dialog-kicker">Images</span><button type="button" data-image-cancel aria-label="Close Insert image">X</button></div>
      <h2 id="imageInsertionTitle">Insert image</h2>
      <label for="imageSourceInput">Source</label><select id="imageSourceInput"><option value="workspace">Workspace</option><option value="import">Import file</option><option value="url">URL or local reference</option></select>
      <div data-image-source="workspace"><label for="imageSearchInput">Search workspace images</label><input id="imageSearchInput" type="search" autocomplete="off"><div class="image-workspace-list" role="group" aria-label="Workspace images"></div></div>
      <div data-image-source="import" hidden><label for="imageImportInput">Image file</label><input id="imageImportInput" type="file" accept=".png,.jpg,.jpeg,.gif,.webp"><button type="button" data-image-pick>Choose image file</button><p>Existing files are reused when their workspace identity is confirmed. Other files are copied to assets/images/.</p></div>
      <div data-image-source="url" hidden><label for="imageReferenceInput">HTTPS URL or document-relative image path</label><input id="imageReferenceInput" type="text" autocomplete="off"><button type="button" data-image-resolve>Preview reference</button><p>External images remain external and require network access.</p></div>
      <div class="image-common-fields"><label for="imageAltInput">Alt text</label><input id="imageAltInput" type="text" autocomplete="off"><label for="imageCaptionInput">Caption (optional)</label><input id="imageCaptionInput" type="text" placeholder="Optional caption" autocomplete="off"></div>
      <img class="image-insertion-preview" alt="Selected image preview" hidden>
      <p class="image-storage-status" role="status" aria-live="polite"></p><label for="imageFinalReference">Final reference</label><input id="imageFinalReference" readonly>
      <p class="image-insertion-error" role="alert"></p>
      <div class="template-dialog-actions"><button type="button" data-image-folder>Select image destination folder</button><button type="button" data-image-cancel>Cancel</button><button class="primary" type="submit" data-image-insert disabled>Insert image</button></div>
    </form>`;
    document.body.append(dialog);
    query('#imageAltInput').value = origin.value.slice(origin.start, origin.end).trim();
    query('[data-image-folder]').hidden = Boolean(state.workspaceDirectoryHandle || state.nativeWorkspaceId);
    dialog.addEventListener('cancel', (event) => { event.preventDefault(); close(); });
    dialog.addEventListener('click', async (event) => {
      if (event.target.closest('[data-image-cancel]')) { close(); return; }
      const imageButton = event.target.closest('[data-image-path]');
      if (imageButton) await process(async () => ({ kind: 'local', asset: await assets.ensureAsset(imageButton.dataset.imagePath) }), currentRun);
      if (event.target.closest('[data-image-resolve]')) await process(() => assets.resolveInput(query('#imageReferenceInput').value, origin.path), currentRun);
      if (event.target.closest('[data-image-pick]')) await pickFile(currentRun);
      if (event.target.closest('[data-image-folder]')) await process(async () => {
        await chooseImageFolder();
        // An explicit destination selection changes asset capabilities but not
        // the document text/selection. Refresh the operation's context.
        if (origin.record === state.files.find((record) => record.path === state.activePath) && origin.value === editor.value) origin = { ...origin, ...captureInsertionContext(), start: origin.start, end: origin.end, top: origin.top, left: origin.left };
        query('[data-image-folder]').hidden = Boolean(state.workspaceDirectoryHandle || state.nativeWorkspaceId);
        await renderWorkspace(currentRun);
        if (selected?.asset) await assets.persistAsset(selected.asset);
        return selected;
      }, currentRun);
    });
    query('form').addEventListener('submit', apply);
    query('#imageSourceInput').addEventListener('change', () => {
      sourceRevision++;
      const source = query('#imageSourceInput').value;
      for (const panel of dialog.querySelectorAll('[data-image-source]')) panel.hidden = panel.dataset.imageSource !== source;
      selected = null; updateSelected();
      query(`[data-image-source="${source}"] input`)?.focus({ preventScroll: true });
    });
    query('#imageSearchInput').addEventListener('input', () => filterWorkspace());
    query('#imageImportInput').addEventListener('change', async (event) => {
      const file = event.target.files?.[0];
      if (file) await process(async () => ({ kind: 'local', asset: await assets.acquireFile(file) }), currentRun);
    });
    query('#imageReferenceInput').addEventListener('input', () => { sourceRevision++; selected = null; updateSelected(); });
    dialog.showModal(); query('#imageSearchInput').focus({ preventScroll: true });
    void renderWorkspace(currentRun);
  }

  async function pickFile(currentRun) {
    if (state.nativeWorkspaceId) {
      await process(async () => { const asset = await assets.pickNativeImage(); return asset ? { kind: 'local', asset } : selected; }, currentRun);
      return;
    }
    if (!window.showOpenFilePicker) { query('#imageImportInput').click(); return; }
    await process(async () => {
      const [handle] = await window.showOpenFilePicker({ types: [{ description: 'Raster images', accept: { 'image/png': ['.png'], 'image/jpeg': ['.jpg', '.jpeg'], 'image/gif': ['.gif'], 'image/webp': ['.webp'] } }] });
      return { kind: 'local', asset: await assets.acquireFile(await handle.getFile(), { handle }) };
    }, currentRun);
  }

  async function process(action, currentRun) {
    if (applying || !dialog || run !== currentRun) return;
    applying = true; query('[data-image-insert]').disabled = true;
    const revision = sourceRevision;
    query('.image-insertion-error').textContent = ''; query('.image-storage-status').textContent = 'Processing image…';
    try {
      const result = await action();
      if (run !== currentRun || !dialog) {
        if (result?.asset) setStatus(`${imageStorageLabel(result.asset)}: ${result.asset.path}. No reference was inserted after cancellation.`, 'warning');
        return;
      }
      if (revision !== sourceRevision) {
        updateSelected();
        if (result?.asset) setStatus(`${imageStorageLabel(result.asset)}: ${result.asset.path}. The source changed; select the image again to insert it.`, 'warning');
        return;
      }
      selected = result;
      if (selected?.asset && !query('#imageAltInput').value) query('#imageAltInput').value = selected.asset.alt || '';
      updateSelected();
    } catch (error) {
      if (run === currentRun && dialog && error.name !== 'AbortError') query('.image-insertion-error').textContent = error.message;
    } finally {
      if (run === currentRun && dialog) { applying = false; query('[data-image-insert]').disabled = !selected; }
    }
  }

  function updateSelected() {
    if (!dialog) return;
    const asset = selected?.asset;
    const reference = asset ? relativeImageReference(origin.path, asset.path) : selected?.reference || '';
    query('#imageFinalReference').value = reference;
    const preview = query('.image-insertion-preview');
    preview.hidden = !selected;
    if (selected) preview.src = asset?.objectUrl || asset?.dataUrl || reference;
    else preview.removeAttribute('src');
    const temporary = 'Available in session — not saved to folder. Export a Markdown Bundle ZIP or select an image destination folder.';
    query('.image-storage-status').textContent = asset
      ? `${imageStorageLabel(asset)}. ${asset.storage === 'workspace' ? `Workspace path: ${asset.path}.` : temporary} Markdown remains unsaved.${asset.error ? ` ${asset.error}` : ''}`
      : selected?.kind === 'external' ? 'External image — requires network access.' : selected?.kind === 'embedded' ? 'Embedded image.' : 'Select an image. New imports use assets/images/ in an authorised workspace; otherwise they remain temporary.';
    query('[data-image-insert]').disabled = !selected || applying;
  }

  async function renderWorkspace(currentRun) {
    try {
      await assets.refreshInventory();
      if (!dialog || run !== currentRun) return;
      const list = query('.image-workspace-list'); list.replaceChildren();
      for (const asset of [...state.managedAssets.values()].sort((a, b) => a.path.localeCompare(b.path))) {
        const button = document.createElement('button'); button.type = 'button'; button.dataset.imagePath = asset.path;
        button.className = 'image-workspace-option';
        button.innerHTML = `<img alt="" loading="lazy"><span><strong>${escapeHtml(asset.name)}</strong><span>${escapeHtml(asset.path)}</span><span>${escapeHtml(imageStorageLabel(asset))}</span></span>`;
        list.append(button);
        // Read only visible inventory thumbnails; full resolution remains lazy.
        if (list.children.length <= 40) void assets.ensureAsset(asset.path).then((loaded) => {
          if (dialog && run === currentRun) button.querySelector('img').src = loaded.objectUrl || loaded.dataUrl;
        }).catch((error) => { button.title = error.message; });
      }
      if (!list.children.length) list.textContent = 'No images found. Import a file, enter a URL, or select a destination folder.';
      filterWorkspace();
    } catch (error) { if (dialog && run === currentRun) query('.image-insertion-error').textContent = error.message; }
  }

  function filterWorkspace() {
    const needle = query('#imageSearchInput').value.toLocaleLowerCase();
    for (const button of dialog.querySelectorAll('[data-image-path]')) button.hidden = !button.textContent.toLocaleLowerCase().includes(needle);
  }

  function apply(event) {
    event.preventDefault();
    if (applying || !selected) return;
    if (!isInsertionContextCurrent(origin)) { query('.image-insertion-error').textContent = 'The document changed. Close this dialogue and reopen Insert image at the current cursor. The asset remains available.'; return; }
    const reference = query('#imageFinalReference').value;
    const text = serialiseImage({ reference, alt: query('#imageAltInput').value, caption: query('#imageCaptionInput').value });
    const snapshot = origin;
    close(false);
    restoreInsertionContext(snapshot);
    insertAtContext(snapshot, text);
    setStatus(`${selected?.asset ? imageStorageLabel(selected.asset) : 'Image inserted'}. Markdown remains unsaved.`, selected?.asset?.storage === 'failed' ? 'warning' : 'ok');
  }

  function insertAtContext(snapshot, text) {
    const prefix = snapshot.start && snapshot.value[snapshot.start - 1] !== '\n' ? '\n\n' : '';
    const suffix = snapshot.end < snapshot.value.length && snapshot.value[snapshot.end] !== '\n' ? '\n\n' : '';
    const cursor = snapshot.start + prefix.length + text.length;
    replaceEditorRange(snapshot.start, snapshot.end, `${prefix}${text}${suffix}`, cursor, cursor);
  }

  async function insertFiles(files) {
    if (editor.readOnly || !files.length) return;
    const snapshot = captureInsertionContext();
    const snippets = [];
    let failed = false;
    for (const file of files) {
      try {
        const asset = await assets.acquireFile(file);
        failed ||= asset.storage === 'failed';
        snippets.push(serialiseImage({ reference: relativeImageReference(snapshot.path, asset.path), alt: asset.alt }));
      } catch (error) { setStatus(error.message, 'warning'); }
    }
    if (!isInsertionContextCurrent(snapshot)) { setStatus('The document changed during image import. No reference inserted; available bytes can be recovered in Manage assets.', 'warning'); return; }
    if (!snippets.length) return;
    restoreInsertionContext(snapshot); insertAtContext(snapshot, snippets.join('\n\n'));
    const pending = [...state.managedAssets.values()].some((asset) => asset.storage !== 'workspace');
    setStatus(failed ? 'Image save failed. Bytes and references remain available in session; retry in Manage assets or export a Markdown Bundle ZIP.'
      : pending ? 'Images inserted as temporary session assets — not saved to folder. Export a Markdown Bundle ZIP or save pending images.'
      : 'Images saved in workspace. Markdown remains unsaved.', failed || pending ? 'warning' : 'ok');
  }

  function close(restore = true) {
    run++;
    if (dialog) { dialog.close(); dialog.remove(); dialog = null; }
    if (restore) restoreInsertionContext(origin);
  }
  function query(selector) { return dialog?.querySelector(selector); }

  async function openAssetLibrary() {
    const manager = document.createElement('dialog'); manager.className = 'utility-dialog asset-library-dialog';
    manager.setAttribute('aria-labelledby', 'assetLibraryTitle'); document.body.append(manager);
    let managerContext = assets.context();
    const returnContext = captureInsertionContext();
    const closeManager = () => { manager.close(); manager.remove(); restoreInsertionContext(returnContext); };
    manager.addEventListener('cancel', (event) => { event.preventDefault(); closeManager(); });
    manager.addEventListener('click', async (event) => {
      if (event.target.closest('[data-asset-close]')) { closeManager(); return; }
      try {
        if (event.target.closest('[data-image-folder]')) { await chooseImageFolder(); managerContext = assets.context(); }
        else if (!assets.isCurrent(managerContext)) throw new Error('The workspace changed. Reopen Manage assets.');
        if (event.target.closest('[data-asset-save]')) {
          const result = await assets.savePendingImages();
          setStatus(`${result.saved} images saved in workspace; ${result.failed} still pending. Markdown changes need saving.`, result.failed ? 'warning' : 'ok');
          await renderPreview();
        }
        const recovery = event.target.closest('[data-asset-recover]');
        if (recovery) assets.recoverAsset(assets.recoverableAssets()[Number(recovery.dataset.assetRecover)]);
        const download = event.target.closest('[data-asset-download]');
        if (download) downloadRecoveryAsset(assets.recoverableAssets()[Number(download.dataset.assetDownload)]);
        await renderManager();
      } catch (error) { manager.querySelector('[role="alert"]').textContent = error.message; }
    });
    async function renderManager() {
      try { await assets.refreshInventory(); } catch (error) { setStatus(error.message, 'warning'); }
      const usage = await assets.getUsage();
      const entries = [...state.managedAssets.values()];
      const rows = entries.map((asset) => `<div class="asset-library-item"><img src="${escapeHtml(asset.objectUrl || asset.dataUrl || '')}" alt="${escapeHtml(asset.name)}"><div class="asset-library-main"><strong>${escapeHtml(asset.name)}</strong><span>${escapeHtml(asset.path)}</span><span>${escapeHtml(imageStorageLabel(asset))} · ${usage.get(asset.path)?.records.length || 0} uses</span>${asset.error ? `<span>${escapeHtml(asset.error)}</span>` : ''}</div><span title="Physical rename/remove is unsupported. Files and references are preserved.">Rename/remove unavailable</span></div>`).join('');
      const dependencies = [...usage.entries()].filter(([key]) => !state.managedAssets.has(key)).map(([key, entry]) => `<p>${escapeHtml(entry.result.kind === 'external' ? 'External image' : entry.result.kind === 'embedded' ? 'Embedded image' : 'Missing file')} — ${escapeHtml(key)}</p>`).join('');
      const recovery = assets.recoverableAssets().map((asset, index) => `<p>Previous operation: ${escapeHtml(asset.path)} (${escapeHtml(imageStorageLabel(asset))}) <button type="button" data-asset-recover="${index}">Recover in this session</button><button type="button" data-asset-download="${index}">Download image bytes</button></p>`).join('');
      manager.innerHTML = `<div class="utility-dialog-card asset-library-card"><h2 id="assetLibraryTitle">Managed assets</h2><p>Workspace files and temporary image bytes. Saving an image does not save Markdown. Physical rename and removal are unavailable; existing files are preserved.</p><p>Pending destination: assets/images/ in the selected folder. Without folder access, export a Markdown Bundle ZIP.</p><div class="asset-library-list">${rows || 'No managed images.'}${dependencies}${recovery}</div><p role="alert"></p><div class="template-dialog-actions"><button type="button" data-image-folder${state.nativeWorkspaceId || state.workspaceDirectoryHandle ? ' hidden' : ''}>Select image destination folder</button><button type="button" data-asset-save${editor.readOnly ? ' disabled' : ''}>Save pending images to workspace</button><button class="primary" type="button" data-asset-close>Done</button></div></div>`;
    }
    await renderManager(); manager.showModal();
  }

  return { openImageDialog, insertFiles, openAssetLibrary };
}
