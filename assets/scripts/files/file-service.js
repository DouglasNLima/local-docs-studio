import { decodeZipText, readZipEntriesFromFile } from '../utils/zip.js';
import { convertDocumentFiles, isImportableDocumentFile } from './document-import-service.js';

const MARKDOWN_BUNDLE_MANIFEST_NAMES = new Set([
  'lens-docs-studio-bundle.json',
  'local-docs-studio-bundle.json',
  // Keep accepting bundles exported before the app was renamed.
  'md-mmd-renderer-bundle.json',
]);

export function createFileService({
  state,
  dom,
  callbacks,
  helpers,
}) {
  const { fileInput, folderInput, zipInput, documentInput, recentList, fileSearch, editor, preview } = dom;
  const {
    confirmDiscardUnsaved,
    clearFocusedModes,
    clearManagedAssets,
    closeOpenMenus,
    renderFileList,
    updateSaveButton,
    setExportTrust,
    resetEditorHistory,
    syncEditorReadOnly,
    updateEditorChrome,
    updateActiveFileLabel,
    updateDiagramControls,
    updatePreviewOutline,
    rememberScrollPosition,
    restoreScrollPosition,
    resetActiveScrollPosition,
    clearScrollPositions,
    renderPreview,
    setStatus,
    getMarkdownExportName,
    afterLibraryLoaded,
    afterActiveFileLoaded,
    beforeSaveActiveFile,
    afterSaveActiveFile,
  } = callbacks;
  const {
    compareRecords,
    downloadBlob,
    escapeHtml,
    getFolderNameFromFileList,
    isSupportedFile,
    normalisePath,
    uniqueByPath,
  } = helpers;

    async function openFile() {
      if (!confirmDiscardUnsaved('Open a file and discard unsaved edits?')) return;

      try {
        if ('showOpenFilePicker' in window) {
          const [handle] = await window.showOpenFilePicker({
            id: 'md-mmd-renderer-file',
            multiple: false,
            types: [{
              description: 'Markdown and Mermaid files',
              accept: {
                'text/markdown': ['.md', '.markdown'],
                'text/plain': ['.mmd', '.mermaid', '.txt'],
              },
            }],
          });

          if (!handle) return;
          const file = await handle.getFile();
          if (!isSupportedFile(file.name)) {
            setStatus('Choose a .md, .markdown, .mmd, or .mermaid file.', 'warning');
            return;
          }

          await setLibraryFromRecords([{ name: file.name, path: file.name, file, handle }], 'Single file');
          await addRecentEntry({ type: 'file', name: file.name, handle });
          return;
        }
      } catch (error) {
        if (error?.name === 'AbortError') return;
        setStatus('File picker failed. Using the browser fallback...', 'warning');
      }

      fileInput.click();
    }

    async function openFolder() {
      if (!confirmDiscardUnsaved('Open a folder and discard unsaved edits?')) return;

      try {
        if ('showDirectoryPicker' in window) {
          const directoryHandle = await window.showDirectoryPicker({
            id: 'md-mmd-renderer-folder',
            mode: 'readwrite',
          });
          setStatus('Reading folder...');
          const records = await collectDirectoryRecords(directoryHandle);
          await setLibraryFromRecords(records, directoryHandle.name || 'Selected folder');
          await addRecentEntry({ type: 'folder', name: directoryHandle.name || 'Selected folder', handle: directoryHandle });
          return;
        }
      } catch (error) {
        if (error?.name === 'AbortError') return;
        setStatus('Folder picker failed. Using the browser fallback...', 'warning');
      }

      folderInput.click();
    }

    function importZip() {
      if (!confirmDiscardUnsaved('Import this ZIP and discard unsaved edits?')) return;
      zipInput.click();
    }

    function importDocument() {
      documentInput.click();
    }

    async function newMarkdownDocument() {
      if (!confirmDiscardUnsaved('Start a blank Markdown document and discard unsaved edits?')) return;

      const name = 'untitled.md';
      await setLibraryFromRecords([{
        name,
        path: name,
        file: new File([''], name, { type: 'text/markdown' }),
      }], 'Blank document');
      editor.focus({ preventScroll: true });
      setStatus('Blank Markdown document ready.', 'ok');
    }

    async function collectDirectoryRecords(directoryHandle, prefix = '') {
      const records = [];

      for await (const [name, handle] of directoryHandle.entries()) {
        const path = normalisePath(prefix ? `${prefix}/${name}` : name);

        if (handle.kind === 'directory') {
          const nested = await collectDirectoryRecords(handle, path);
          records.push(...nested);
          continue;
        }

        if (handle.kind === 'file' && isSupportedFile(name)) {
          records.push({ name, path, handle });
        }
      }

      return records.sort(compareRecords);
    }

    async function initRecentHandles() {
      await refreshRecentEntries();
      renderRecentList();
    }

    function supportsRecentHandles() {
      return 'indexedDB' in window && 'showOpenFilePicker' in window;
    }

    async function refreshRecentEntries() {
      if (!supportsRecentHandles()) {
        state.recentEntries = [];
        return;
      }

      try {
        const db = await openRecentDb();
        const entries = await idbGetAll(db);
        state.recentEntries = entries
          .sort((left, right) => (right.lastOpened ?? 0) - (left.lastOpened ?? 0))
          .slice(0, 8);
        db.close();
      } catch (error) {
        state.recentEntries = [];
        console.warn('Recent handles unavailable.', error);
      }
    }

    async function addRecentEntry(entry) {
      if (!supportsRecentHandles() || !entry?.handle) {
        renderRecentList();
        return;
      }

      try {
        const db = await openRecentDb();
        const key = `${entry.type}:${entry.name}`;
        await idbPut(db, {
          key,
          type: entry.type,
          name: entry.name,
          handle: entry.handle,
          lastOpened: Date.now(),
        });
        db.close();
        await refreshRecentEntries();
      } catch (error) {
        console.warn('Could not persist recent handle.', error);
      }

      renderRecentList();
    }

    function renderRecentList() {
      recentList.innerHTML = '';

      if (!supportsRecentHandles()) {
        recentList.textContent = 'Recent reopen is unavailable in this browser; exported copies still work everywhere.';
        return;
      }

      if (!state.recentEntries.length) {
        recentList.textContent = 'Recent files appear here after you open them.';
        return;
      }

      state.recentEntries.forEach((entry) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.dataset.recentKey = entry.key;
        button.textContent = `${entry.type === 'folder' ? 'Folder' : 'File'}: ${entry.name}`;
        button.title = entry.name;
        recentList.appendChild(button);
      });
    }

    async function handleRecentClick(event) {
      const button = event.target.closest('[data-recent-key]');
      if (!button) return;

      const entry = state.recentEntries.find((item) => item.key === button.dataset.recentKey);
      if (!entry?.handle) return;
      if (!confirmDiscardUnsaved('Open this recent item and discard unsaved edits?')) return;

      try {
        if (!await ensureReadPermission(entry.handle)) {
          setStatus('Browser permission is needed to reopen this item.', 'warning');
          return;
        }

        if (entry.type === 'file') {
          const file = await entry.handle.getFile();
          await setLibraryFromRecords([{ name: file.name, path: file.name, file, handle: entry.handle }], 'Recent file');
          await addRecentEntry({ type: 'file', name: file.name, handle: entry.handle });
        } else {
          setStatus('Reading recent folder...');
          const records = await collectDirectoryRecords(entry.handle);
          await setLibraryFromRecords(records, entry.name);
          await addRecentEntry({ type: 'folder', name: entry.name, handle: entry.handle });
        }

        closeOpenMenus();
      } catch (error) {
        setStatus('Could not reopen that recent item.', 'danger');
        console.error(error);
      }
    }

    async function ensureReadPermission(handle) {
      if (!handle || typeof handle.queryPermission !== 'function' || typeof handle.requestPermission !== 'function') return true;

      const options = { mode: 'read' };
      if (await handle.queryPermission(options) === 'granted') return true;
      return await handle.requestPermission(options) === 'granted';
    }

    function openRecentDb() {
      return new Promise((resolve, reject) => {
        const request = indexedDB.open('md-mmd-renderer-recents', 1);
        request.onupgradeneeded = () => {
          const db = request.result;
          if (!db.objectStoreNames.contains('handles')) {
            db.createObjectStore('handles', { keyPath: 'key' });
          }
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
      });
    }

    function idbGetAll(db) {
      return new Promise((resolve, reject) => {
        const transaction = db.transaction('handles', 'readonly');
        const request = transaction.objectStore('handles').getAll();
        request.onsuccess = () => resolve(request.result || []);
        request.onerror = () => reject(request.error);
      });
    }

    function idbPut(db, value) {
      return new Promise((resolve, reject) => {
        const transaction = db.transaction('handles', 'readwrite');
        transaction.objectStore('handles').put(value);
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
      });
    }

    async function setLibraryFromRecords(records, folderName) {
      clearFocusedModes();
      clearManagedAssets?.();
      const uniqueRecords = uniqueByPath(records)
        .filter((record) => isSupportedFile(record.name))
        .sort(compareRecords);

      state.files = uniqueRecords;
      state.activePath = '';
      state.fileName = '';
      state.folderName = folderName;
      state.fileCache.clear();
      state.savedContentCache?.clear();
      state.dirtyPaths.clear();
      afterLibraryLoaded?.();
      clearScrollPositions?.();
      fileSearch.value = '';
      resetActiveScrollPosition?.('');
      renderFileList();
      syncEditorReadOnly?.();
      updateSaveButton();
      setExportTrust('', '');

      if (!uniqueRecords.length) {
        editor.value = '';
        editor.scrollTop = 0;
        preview.scrollTop = 0;
        resetEditorHistory();
        syncEditorReadOnly?.();
        updateEditorChrome?.();
        preview.innerHTML = '<div class="empty-state">No Markdown or Mermaid files were found.</div>';
        updateActiveFileLabel();
        updateDiagramControls(0);
        updatePreviewOutline();
        updateSaveButton();
        setStatus('No supported files found.', 'warning');
        return;
      }

      setStatus(`${uniqueRecords.length} file${uniqueRecords.length === 1 ? '' : 's'} loaded.`);
      await selectFile(uniqueRecords[0].path);
    }

    async function importZipFile(file) {
      if (!file) return;

      try {
        setStatus(`Reading ${file.name}...`);
        const entries = await readZipEntriesFromFile(file);
        const imported = buildZipImport(file, entries);

        if (!imported.records.length) {
          setStatus(imported.docsSiteZip
            ? 'No editable Markdown or Mermaid source files were found in this Docs Site ZIP.'
            : 'No Markdown or Mermaid files were found in this ZIP.', 'warning');
          return;
        }

        clearFocusedModes();
        clearManagedAssets?.();
        state.files = imported.records;
        state.activePath = '';
        state.fileName = '';
        state.folderName = imported.folderName;
        state.fileCache.clear();
        state.savedContentCache?.clear();
        imported.records.forEach((record) => {
          const text = imported.textByPath.get(record.path) || '';
          state.fileCache.set(record.path, text);
          state.savedContentCache?.set(record.path, text);
        });
        state.dirtyPaths.clear();
        afterLibraryLoaded?.();
        clearScrollPositions?.();
        fileSearch.value = '';
        resetActiveScrollPosition?.('');
        imported.assets.forEach((asset) => {
          state.managedAssets.set(asset.path, asset);
        });

        renderFileList();
        syncEditorReadOnly?.();
        updateSaveButton();
        setExportTrust('', '');
        await selectFile(imported.records[0].path);

        const assetText = imported.assets.length
          ? ` and ${imported.assets.length} image asset${imported.assets.length === 1 ? '' : 's'}`
          : '';
        const skippedSvgText = imported.skippedSvgAssets
          ? ' SVG images are not imported for security; use PNG, JPEG, GIF, or WebP.'
          : '';
        const sourceText = imported.bundle ? 'Markdown bundle' : 'ZIP';
        const docsSiteText = imported.docsSiteZip && !imported.bundle
          ? ' Rendered Docs Site HTML was not converted back to Markdown.'
          : '';
        setStatus(`Imported ${imported.records.length} document${imported.records.length === 1 ? '' : 's'}${assetText} from ${sourceText}.${docsSiteText}${skippedSvgText}`, imported.docsSiteZip && !imported.bundle || imported.skippedSvgAssets ? 'warning' : 'ok');
      } catch (error) {
        setStatus('ZIP import failed.', 'danger');
        console.error(error);
      } finally {
        if (zipInput) zipInput.value = '';
      }
    }

    async function importDocumentFiles(files) {
      const sourceFiles = [...(files || [])];
      if (!sourceFiles.length) return;

      const documentFiles = sourceFiles.filter(isImportableDocumentFile);

      if (!documentFiles.length) {
        setStatus('Choose a .docx, .html, .htm, or .pdf file to import as Markdown.', 'warning');
        return;
      }

      const confirmMessage = documentFiles.length === 1
        ? 'Import this document as Markdown and discard unsaved edits?'
        : 'Import these documents as Markdown and discard unsaved edits?';
      if (!confirmDiscardUnsaved(confirmMessage)) return;

      try {
        setStatus(`Converting ${documentFiles.length} document${documentFiles.length === 1 ? '' : 's'} to Markdown...`);
        const imported = await convertDocumentFiles(documentFiles);

        if (!imported.records.length) {
          setStatus(imported.warnings[0] || 'No documents could be converted to Markdown.', 'warning');
          return;
        }

        await setConvertedDocumentLibrary(imported);
      } catch (error) {
        setStatus('Document import failed.', 'danger');
        console.error(error);
      } finally {
        if (documentInput) documentInput.value = '';
      }
    }

    async function setConvertedDocumentLibrary(imported) {
      clearFocusedModes();
      clearManagedAssets?.();
      state.files = imported.records.sort(compareRecords);
      state.activePath = '';
      state.fileName = '';
      state.folderName = imported.records.length === 1 ? 'Imported document' : 'Imported documents';
      state.fileCache.clear();
      state.savedContentCache?.clear();
      state.dirtyPaths.clear();
      afterLibraryLoaded?.();
      clearScrollPositions?.();
      fileSearch.value = '';
      resetActiveScrollPosition?.('');
      imported.records.forEach((record) => {
        const text = imported.textByPath.get(record.path) || '';
        state.fileCache.set(record.path, text);
        state.savedContentCache?.set(record.path, text);
      });
      imported.assets.forEach((asset) => {
        state.managedAssets.set(asset.path, asset);
      });

      renderFileList();
      syncEditorReadOnly?.();
      updateSaveButton();
      setExportTrust('', '');
      await selectFile(state.files[0].path);

      const warningText = imported.warnings.length
        ? ` ${imported.warnings.slice(0, 2).join(' ')}`
        : '';
      const assetText = imported.assets.length
        ? ` and ${imported.assets.length} image asset${imported.assets.length === 1 ? '' : 's'}`
        : '';
      const statusType = imported.warnings.length ? 'warning' : 'ok';
      const persistenceText = 'Use Save changes or Export Markdown Bundle to persist the converted Markdown.';

      setExportTrust(`${imported.records.length} converted document${imported.records.length === 1 ? '' : 's'} loaded${assetText}. ${persistenceText}${warningText}`, statusType);
      setStatus(`Imported ${imported.records.length} converted document${imported.records.length === 1 ? '' : 's'}${assetText}. ${persistenceText}${warningText}`, statusType);
    }

    function buildZipImport(file, entries) {
      const records = [];
      const textByPath = new Map();
      const assets = [];
      let bundle = null;
      let docsSiteZip = false;
      let skippedSvgAssets = 0;

      for (const [rawName, bytes] of entries) {
        const path = sanitiseZipEntryPath(rawName);
        if (!path) continue;

        if (path === 'site-manifest.json') {
          docsSiteZip = true;
        }

        if (MARKDOWN_BUNDLE_MANIFEST_NAMES.has(path)) {
          bundle = parseBundleManifest(bytes);
          continue;
        }

        if (isSupportedFile(path)) {
          const text = decodeZipText(bytes);
          const name = path.split('/').pop() || path;
          records.push({
            name,
            path,
            file: new File([text], name, { type: getDocumentMimeType(path) }),
          });
          textByPath.set(path, text);
          continue;
        }

        if (isBlockedSvgImagePath(path)) {
          skippedSvgAssets += 1;
          continue;
        }

        if (isImportableImagePath(path)) {
          assets.push(createManagedAssetFromZip(path, bytes));
        }
      }

      const uniqueRecords = uniqueByPath(records).sort(compareRecords);
      return {
        bundle,
        docsSiteZip,
        records: uniqueRecords,
        textByPath,
        assets: dedupeAssetsByPath(assets),
        skippedSvgAssets,
        folderName: bundle?.title || file.name.replace(/\.zip$/i, '') || 'Imported ZIP',
      };
    }

    function parseBundleManifest(bytes) {
      try {
        const manifest = JSON.parse(decodeZipText(bytes));
        return manifest?.formatVersion === 'markdown-bundle-1.0' ? manifest : null;
      } catch {
        return null;
      }
    }

    function sanitiseZipEntryPath(rawName) {
      const raw = String(rawName || '').replace(/\\/g, '/');
      if (!raw || raw.endsWith('/') || /^[a-z]:/i.test(raw) || raw.startsWith('/')) return '';
      const parts = raw.split('/').filter(Boolean);
      if (!parts.length || parts[0] === '__MACOSX') return '';
      if (parts.some((part) => part === '.' || part === '..')) return '';
      return normalisePath(parts.join('/'));
    }

    function isImportableImagePath(path) {
      return /\.(png|jpe?g|gif|webp)$/i.test(path);
    }

    function isBlockedSvgImagePath(path) {
      return /\.svg$/i.test(path);
    }

    function createManagedAssetFromZip(path, bytes) {
      const mimeType = getImageMimeType(path);
      const blob = new Blob([bytes], { type: mimeType });
      const base64 = uint8ArrayToBase64(bytes);
      const name = path.split('/').pop() || 'image';
      return {
        path,
        name,
        alt: name.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ').trim() || 'Image',
        mimeType,
        base64,
        dataUrl: `data:${mimeType};base64,${base64}`,
        objectUrl: URL.createObjectURL(blob),
        size: bytes.byteLength,
      };
    }

    function dedupeAssetsByPath(assets) {
      return [...new Map(assets.map((asset) => [asset.path, asset])).values()];
    }

    function uint8ArrayToBase64(bytes) {
      let binary = '';
      const chunkSize = 0x8000;
      for (let index = 0; index < bytes.length; index += chunkSize) {
        binary += String.fromCharCode(...bytes.subarray(index, index + chunkSize));
      }
      return btoa(binary);
    }

    function getImageMimeType(path) {
      if (/\.webp$/i.test(path)) return 'image/webp';
      if (/\.gif$/i.test(path)) return 'image/gif';
      if (/\.jpe?g$/i.test(path)) return 'image/jpeg';
      return 'image/png';
    }

    function getDocumentMimeType(path) {
      return /\.(mmd|mermaid)$/i.test(path) ? 'text/plain' : 'text/markdown';
    }

    async function selectFile(path) {
      const record = state.files.find((item) => item.path === path);
      if (!record) return;
      rememberScrollPosition?.();
      if (path !== state.activePath && state.activePath && state.dirtyPaths.has(state.activePath)) {
        const current = state.files.find((item) => item.path === state.activePath);
        const name = current?.name ?? 'the current file';
        if (!window.confirm(`${name} has unsaved edits. Switch files and discard those edits?`)) return;
        state.dirtyPaths.delete(state.activePath);
      }

      try {
        state.activePath = record.path;
        state.fileName = record.name;

        if (!state.fileCache.has(record.path)) {
          const file = record.file ?? await record.handle.getFile();
          record.file = file;
          state.fileCache.set(record.path, await file.text());
        }

        editor.value = state.fileCache.get(record.path) ?? '';
        editor.scrollTop = 0;
        preview.scrollTop = 0;
        resetEditorHistory();
        syncEditorReadOnly?.();
        updateEditorChrome?.();
        const recovered = await afterActiveFileLoaded?.(record, editor.value);
        if (recovered?.content !== undefined && recovered.content !== editor.value) {
          editor.value = recovered.content;
          state.fileCache.set(record.path, recovered.content);
          if (recovered.dirty) state.dirtyPaths.add(record.path);
          resetEditorHistory();
          updateEditorChrome?.();
        }
        renderFileList();
        updateActiveFileLabel();
        updateSaveButton();
        await renderPreview();
        restoreScrollPosition?.(record.path);
      } catch (error) {
        setStatus(`Could not open ${record.name}.`, 'danger');
        preview.innerHTML = `<pre class="error">${escapeHtml(error?.message ?? String(error))}</pre>`;
        updateSaveButton();
      }
    }

    async function saveActiveFile() {
      const record = state.files.find((item) => item.path === state.activePath);
      if (!record) {
        setStatus('No file selected.', 'warning');
        return;
      }

      if (record.readOnly) {
        setStatus('This guide is read-only. Open or create a Markdown file to save changes.', 'warning');
        updateSaveButton();
        return;
      }

      const content = editor.value;
      if (beforeSaveActiveFile && !await beforeSaveActiveFile(record, content)) {
        return;
      }

      try {
        if (record.handle && await ensureWritePermission(record.handle)) {
          setStatus(`Saving ${record.name}...`);
          const writable = await record.handle.createWritable();
          await writable.write(content);
          await writable.close();
          record.file = await record.handle.getFile();
          record.converted = false;
          state.fileCache.set(record.path, content);
          state.dirtyPaths.delete(record.path);
          renderFileList();
          updateActiveFileLabel();
          updateSaveButton();
          setStatus(buildSaveStatus(record.name), state.managedAssets?.size ? 'warning' : 'ok');
          await afterSaveActiveFile?.(record, content);
          await addRecentEntry({ type: 'file', name: record.name, handle: record.handle });
          return;
        }

        if ('showSaveFilePicker' in window) {
          const oldPath = record.path;
          const handle = await window.showSaveFilePicker({
            suggestedName: record.name || getMarkdownExportName(),
            types: [{
              description: 'Markdown and Mermaid files',
              accept: {
                'text/markdown': ['.md', '.markdown'],
                'text/plain': ['.mmd', '.mermaid', '.txt'],
              },
            }],
          });

          const writable = await handle.createWritable();
          await writable.write(content);
          await writable.close();
          record.handle = handle;
          record.name = handle.name || record.name;
          record.path = normalisePath(handle.name || record.path || record.name);
          record.file = await handle.getFile();
          record.converted = false;
          state.fileName = record.name;
          state.activePath = record.path;
          state.fileCache.delete(oldPath);
          state.fileCache.set(record.path, content);
          state.dirtyPaths.delete(oldPath);
          state.dirtyPaths.delete(record.path);
          renderFileList();
          updateActiveFileLabel();
          updateSaveButton();
          setStatus(buildSaveStatus(record.name), state.managedAssets?.size ? 'warning' : 'ok');
          await afterSaveActiveFile?.(record, content, oldPath);
          await addRecentEntry({ type: 'file', name: record.name, handle });
          return;
        }

        downloadBlob(new Blob([content], { type: 'text/markdown;charset=utf-8' }), record.name || getMarkdownExportName());
        record.converted = false;
        renderFileList();
        updateActiveFileLabel();
        updateSaveButton();
        setStatus(state.managedAssets?.size
          ? 'Downloaded Markdown copy. Image binaries are included in exports, not this Markdown file.'
          : 'Browser cannot write to this file directly; downloaded a copy.', 'warning');
      } catch (error) {
        if (error?.name === 'AbortError') return;
        setStatus('Save failed.', 'danger');
        console.error(error);
      }
    }

    function buildSaveStatus(name) {
      return state.managedAssets?.size
        ? `${name} saved with image links. Image binaries are included in exports.`
        : `${name} saved.`;
    }

    async function ensureWritePermission(handle) {
      if (!handle || typeof handle.createWritable !== 'function') return false;
      if (typeof handle.queryPermission !== 'function' || typeof handle.requestPermission !== 'function') return true;

      const options = { mode: 'readwrite' };
      if (await handle.queryPermission(options) === 'granted') return true;
      return await handle.requestPermission(options) === 'granted';
    }

    return {
      newMarkdownDocument,
      openFile,
      openFolder,
      importZip,
      importZipFile,
      importDocument,
      importDocumentFiles,
      collectDirectoryRecords,
      initRecentHandles,
      supportsRecentHandles,
      refreshRecentEntries,
      addRecentEntry,
      renderRecentList,
      handleRecentClick,
      ensureReadPermission,
      setLibraryFromRecords,
      selectFile,
      saveActiveFile,
      ensureWritePermission,
    };
}
