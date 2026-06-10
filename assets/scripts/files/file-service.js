import { decodeZipText, readZipEntriesFromFile } from '../utils/zip.js';
import { convertDocumentFiles, isImportableDocumentFile } from './document-import-service.js';
import {
  LENS_ARTIFACT_BUNDLE_MANIFEST_NAME,
  parseLensArtifactBundleManifest,
} from './lens-artifact-bundle-service.js';
import { nativeBridgeMessageTypes } from '../native/native-bridge-client.js';

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
  nativeBridgeClient = null,
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
    promptForText,
    confirmAction,
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

    if (nativeBridgeClient?.on) {
      nativeBridgeClient.on(nativeBridgeMessageTypes.workspaceChanged, handleNativeWorkspaceChanged);
    }

    async function openFile() {
      if (!await confirmDiscardUnsaved('Open a file and discard unsaved edits?')) return;

      if (await hasNativeFileCapability('file.open')) {
        const opened = await openNativeFile();
        if (opened) return;
      }

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

          await setLibraryFromRecords([{ name: file.name, path: file.name, file, handle }], 'Single file', { workspaceKind: 'file' });
          await addRecentEntry({ type: 'file', name: file.name, handle });
          return;
        }
      } catch (error) {
        if (error?.name === 'AbortError') return;
        setStatus('File picker failed. Using the browser fallback...', 'warning');
      }

      fileInput.dataset.mode = 'open';
      fileInput.multiple = false;
      fileInput.click();
    }

    async function openNativeFile() {
      try {
        setStatus('Opening file from Windows...', 'busy');
        const result = await nativeBridgeClient.openFile();
        if (!result.ok) {
          setStatus(result.message || 'Windows open file failed safely. Using the browser fallback...', 'warning');
          return false;
        }

        const payload = result.response?.payload || {};
        if (payload.cancelled) {
          setStatus('Open file cancelled.', 'info');
          return true;
        }

        if (!isValidNativeFilePayload(payload)) {
          setStatus('Windows open file returned an unsupported file response. Using the browser fallback...', 'warning');
          return false;
        }

        const name = payload.displayName || payload.name;
        const content = String(payload.content ?? '');
        const file = new File([content], name, { type: getMimeTypeForPath(name) });
        await setLibraryFromRecords([{
          name,
          path: name,
          file,
          nativeHandleId: payload.nativeHandleId,
        }], 'Windows file', { workspaceKind: 'file' });
        setStatus(`${name} opened from Windows.`, 'ok');
        return true;
      } catch (error) {
        setStatus('Windows open file failed safely. Using the browser fallback...', 'warning');
        console.error(error);
        return false;
      }
    }

    async function openFolder() {
      if (!await confirmDiscardUnsaved('Open a folder and discard unsaved edits?')) return;

      if (await hasNativeFileCapability('workspace.openFolder')) {
        const opened = await openNativeFolder();
        if (opened) return;
      }

      try {
        if ('showDirectoryPicker' in window) {
          const directoryHandle = await window.showDirectoryPicker({
            id: 'md-mmd-renderer-folder',
            mode: 'readwrite',
          });
          setStatus('Reading folder...');
          const records = await collectDirectoryRecords(directoryHandle);
          await setLibraryFromRecords(records, directoryHandle.name || 'Selected folder', {
            directoryHandle,
            workspaceKind: 'folder',
          });
          await addRecentEntry({ type: 'folder', name: directoryHandle.name || 'Selected folder', handle: directoryHandle });
          return;
        }
      } catch (error) {
        if (error?.name === 'AbortError') return;
        setStatus('Folder picker failed. Using the browser fallback...', 'warning');
      }

      folderInput.click();
    }

    async function openNativeFolder() {
      try {
        setStatus('Opening folder from Windows...', 'busy');
        const result = await nativeBridgeClient.openFolder();
        if (!result.ok) {
          setStatus(result.message || 'Windows open folder failed safely. Using the browser fallback...', 'warning');
          return false;
        }

        const payload = result.response?.payload || {};
        if (payload.cancelled) {
          setStatus('Open folder cancelled.', 'info');
          return true;
        }

        if (!isValidNativeWorkspacePayload(payload)) {
          setStatus('Windows open folder returned an unsupported workspace response. Using the browser fallback...', 'warning');
          return false;
        }

        const records = payload.files.map((file) => {
          const path = normalisePath(file.path || file.displayPath || file.name);
          const name = file.name || path.split('/').pop() || path;
          const content = String(file.content ?? '');
          return {
            name,
            path,
            file: new File([content], name, { type: getMimeTypeForPath(path) }),
            nativeHandleId: file.nativeHandleId,
          };
        });
        await setLibraryFromRecords(records, payload.workspaceName || 'Windows workspace', {
          workspaceKind: 'native-folder',
          nativeWorkspaceId: payload.nativeWorkspaceId,
        });

        const skipped = Array.isArray(payload.skipped) ? payload.skipped : [];
        if (skipped.length) {
          setStatus(`${records.length} file${records.length === 1 ? '' : 's'} loaded from Windows. ${skipped.length} file${skipped.length === 1 ? '' : 's'} skipped by workspace limits.`, 'warning');
        } else if (records.length) {
          setStatus(`${records.length} file${records.length === 1 ? '' : 's'} loaded from Windows.`, 'ok');
        }
        return true;
      } catch (error) {
        setStatus('Windows open folder failed safely. Using the browser fallback...', 'warning');
        console.error(error);
        return false;
      }
    }

    async function importZip() {
      zipInput.click();
    }

    function importDocument() {
      documentInput.click();
    }

    async function newMarkdownDocument() {
      if (!state.files.length && !await confirmDiscardUnsaved('Start a blank Markdown document and discard unsaved edits?')) return;

      const suggestedPath = getAvailableUntitledPath(getNewFileDirectory());
      const rawPath = await promptForText({
        title: 'New Markdown file',
        message: 'Create a Markdown or Mermaid file in the current workspace.',
        kicker: 'New file',
        label: 'File path',
        value: suggestedPath,
        hint: 'Use a relative .md, .markdown, .mmd, or .mermaid path.',
        confirmLabel: 'Create file',
        validate: (value) => {
          const path = sanitiseWorkspaceFilePath(value);
          if (!path) return 'Use a relative .md, .markdown, .mmd, or .mermaid path inside the workspace.';
          const existing = state.files.find((record) => record.path.toLowerCase() === path.toLowerCase());
          return existing ? `${existing.path} is already in the workspace.` : '';
        },
      });
      if (!rawPath) return;

      const name = sanitiseWorkspaceFilePath(rawPath);
      if (!name) {
        setStatus('Use a relative .md, .markdown, .mmd, or .mermaid path inside the workspace.', 'warning');
        return;
      }

      const existing = state.files.find((record) => record.path.toLowerCase() === name.toLowerCase());
      if (existing) {
        await selectFile(existing.path);
        setStatus(`${existing.path} is already in the workspace.`, 'warning');
        return;
      }

      if (state.workspaceDirectoryHandle) {
        await createFileInWorkspace(name);
        return;
      }

      if (state.nativeWorkspaceId && await hasNativeFileCapability('workspace.createFile')) {
        await createNativeWorkspaceFile(name);
        return;
      }

      const fileName = name.split('/').pop() || name;
      const record = {
        name: fileName,
        path: name,
        file: new File([''], fileName, { type: getMimeTypeForPath(name) }),
        needsSave: true,
      };

      await addRecordsToWorkspace([record], {
        folderName: state.folderName || (state.files.length ? 'Workspace' : 'Blank document'),
        selectPath: name,
      });
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
      if (!await confirmDiscardUnsaved('Open this recent item and discard unsaved edits?')) return;

      try {
        if (!await ensureReadPermission(entry.handle)) {
          setStatus('Browser permission is needed to reopen this item.', 'warning');
          return;
        }

        if (entry.type === 'file') {
          const file = await entry.handle.getFile();
          await setLibraryFromRecords([{ name: file.name, path: file.name, file, handle: entry.handle }], 'Recent file', { workspaceKind: 'file' });
          await addRecentEntry({ type: 'file', name: file.name, handle: entry.handle });
        } else {
          setStatus('Reading recent folder...');
          const records = await collectDirectoryRecords(entry.handle);
          await setLibraryFromRecords(records, entry.name, {
            directoryHandle: entry.handle,
            workspaceKind: 'folder',
          });
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

    async function setLibraryFromRecords(records, folderName, options = {}) {
      clearFocusedModes();
      clearManagedAssets?.();
      const uniqueRecords = uniqueByPath(records)
        .filter((record) => isSupportedFile(record.name))
        .map(prepareRecord)
        .sort(compareRecords);

      state.files = uniqueRecords;
      state.activePath = '';
      state.fileName = '';
      state.folderName = folderName;
      state.workspaceDirectoryHandle = options.directoryHandle || null;
      state.nativeWorkspaceId = options.nativeWorkspaceId || '';
      state.workspaceKind = options.workspaceKind || (options.directoryHandle ? 'folder' : '');
      state.selectedTreeFolderPath = '';
      state.artifactBundle = null;
      state.fileCache.clear();
      state.savedContentCache?.clear();
      state.dirtyPaths.clear();
      state.externalChangePaths?.clear();
      state.externalChangeDetails?.clear();
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
        if (!await confirmDiscardUnsaved('Import this ZIP and discard unsaved edits?')) return;

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
        state.files = imported.records.map(prepareRecord);
        state.activePath = '';
        state.fileName = '';
        state.folderName = imported.folderName;
        state.workspaceDirectoryHandle = null;
        state.nativeWorkspaceId = '';
        state.workspaceKind = imported.artifactBundle ? 'artefact-bundle' : imported.bundle ? 'bundle' : 'zip';
        state.artifactBundle = imported.artifactBundle;
        state.fileCache.clear();
        state.savedContentCache?.clear();
        imported.records.forEach((record) => {
          const text = imported.textByPath.get(record.path) || '';
          state.fileCache.set(record.path, text);
          state.savedContentCache?.set(record.path, text);
        });
        state.dirtyPaths.clear();
        state.externalChangePaths?.clear();
        state.externalChangeDetails?.clear();
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
        const selectedPath = imported.artifactBundle?.entryDocument || imported.records[0].path;
        await selectFile(selectedPath);

        const assetText = imported.assets.length
          ? ` and ${imported.assets.length} image asset${imported.assets.length === 1 ? '' : 's'}`
          : '';
        const skippedSvgText = imported.skippedSvgAssets
          ? ' SVG images are not imported for security; use PNG, JPEG, GIF, or WebP.'
          : '';
        const sourceText = imported.artifactBundle ? 'artefact bundle' : imported.bundle ? 'Markdown bundle' : 'ZIP';
        const docsSiteText = imported.docsSiteZip && !imported.bundle
          ? ' Rendered Docs Site HTML was not converted back to Markdown.'
          : '';
        const artifactEntryText = imported.artifactBundle?.entryDocument
          ? ` Opened ${imported.artifactBundle.entryDocument}.`
          : '';
        const artifactWarningText = imported.artifactWarnings.length
          ? ` ${imported.artifactWarnings.slice(0, 2).join(' ')}`
          : '';
        const statusTone = imported.docsSiteZip && !imported.bundle || imported.skippedSvgAssets || imported.artifactWarnings.length ? 'warning' : 'ok';
        setStatus(`Imported ${imported.records.length} document${imported.records.length === 1 ? '' : 's'}${assetText} from ${sourceText}.${artifactEntryText}${docsSiteText}${skippedSvgText}${artifactWarningText}`, statusTone);
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
      if (!await confirmDiscardUnsaved(confirmMessage)) return;

      try {
        setStatus(`Converting ${documentFiles.length} document${documentFiles.length === 1 ? '' : 's'} to Markdown...`, 'busy');
        await waitForNextFrame();
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
      state.files = imported.records.map(prepareRecord).sort(compareRecords);
      state.activePath = '';
      state.fileName = '';
      state.folderName = imported.records.length === 1 ? 'Imported document' : 'Imported documents';
      state.workspaceDirectoryHandle = null;
      state.nativeWorkspaceId = '';
      state.workspaceKind = 'converted';
      state.artifactBundle = null;
      state.fileCache.clear();
      state.savedContentCache?.clear();
      state.dirtyPaths.clear();
      state.externalChangePaths?.clear();
      state.externalChangeDetails?.clear();
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

    function waitForNextFrame() {
      return new Promise((resolve) => {
        if (typeof requestAnimationFrame === 'function') {
          requestAnimationFrame(() => resolve());
          return;
        }
        setTimeout(resolve, 0);
      });
    }

    function buildZipImport(file, entries) {
      const records = [];
      const textByPath = new Map();
      const assets = [];
      let bundle = null;
      let artifactManifestBytes = null;
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

        if (path === LENS_ARTIFACT_BUNDLE_MANIFEST_NAME) {
          artifactManifestBytes = bytes;
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
      const artifactResult = artifactManifestBytes
        ? parseLensArtifactBundleManifest(artifactManifestBytes, uniqueRecords)
        : { bundle: null, warnings: [] };
      const sortedRecords = artifactResult.bundle
        ? sortRecordsByArtifactBundle(uniqueRecords, artifactResult.bundle)
        : uniqueRecords;
      return {
        bundle,
        artifactBundle: artifactResult.bundle,
        artifactWarnings: artifactResult.warnings,
        docsSiteZip,
        records: sortedRecords,
        textByPath,
        assets: dedupeAssetsByPath(assets),
        skippedSvgAssets,
        folderName: artifactResult.bundle?.title || bundle?.title || file.name.replace(/\.zip$/i, '') || 'Imported ZIP',
      };
    }

    function sortRecordsByArtifactBundle(records, artifactBundle) {
      return [...records].sort((left, right) => {
        const leftMeta = artifactBundle.recordMetadataByPath.get(left.path);
        const rightMeta = artifactBundle.recordMetadataByPath.get(right.path);
        const leftRank = getArtifactSortRank(leftMeta);
        const rightRank = getArtifactSortRank(rightMeta);
        if (leftRank !== rightRank) return leftRank - rightRank;
        if (leftRank === 0 && leftMeta.order !== rightMeta.order) {
          return leftMeta.order - rightMeta.order;
        }
        return compareRecords(left, right);
      });
    }

    function getArtifactSortRank(metadata) {
      if (!metadata) return 2;
      return Number.isFinite(metadata.order) ? 0 : 1;
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

      try {
        if (record.handle) {
          const externalDecision = await handleExternalChange(record, { reason: 'select' });
          if (externalDecision === 'cancel') return;
        }

        state.activePath = record.path;
        state.fileName = record.name;

        if (!state.fileCache.has(record.path)) {
          const { file, text } = await readRecordSource(record);
          record.file = file;
          updateRecordFingerprint(record, file);
          state.fileCache.set(record.path, text);
        }

        editor.value = state.fileCache.get(record.path) ?? '';
        editor.scrollTop = 0;
        preview.scrollTop = 0;
        resetEditorHistory();
        syncEditorReadOnly?.();
        updateEditorChrome?.();
        renderFileList();
        updateActiveFileLabel();
        updateSaveButton();
        const recovered = await afterActiveFileLoaded?.(record, editor.value);
        if (recovered?.content !== undefined && recovered.content !== editor.value) {
          editor.value = recovered.content;
          state.fileCache.set(record.path, recovered.content);
          if (recovered.dirty) state.dirtyPaths.add(record.path);
          resetEditorHistory();
          updateEditorChrome?.();
          renderFileList();
          updateActiveFileLabel();
          updateSaveButton();
        }
        await renderPreview();
        restoreScrollPosition?.(record.path);
        showNativeWorkspaceChangeStatus(record);
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

      if (record.handle) {
        const externalDecision = await handleExternalChange(record, { reason: 'save' });
        if (externalDecision === 'reloaded') return;
      }

      const content = editor.value;
      if (beforeSaveActiveFile && !await beforeSaveActiveFile(record, content)) {
        return;
      }

      try {
        await flushPendingRenderBeforeSave();
        if (record.nativeHandleId && await hasNativeFileCapability('file.save')) {
          if (state.workspaceKind === 'native-folder' && await hasNativeFileCapability('workspace.saveFile')) {
            await saveRecordToNativeWorkspaceHandle(record, content);
            return;
          }
          await saveRecordToNativeHandle(record, content);
          return;
        }

        if (record.handle) {
          if (!await ensureWritePermission(record.handle)) {
            setStatus('Browser permission is needed to save back to the opened file.', 'warning');
            updateSaveButton();
            return;
          }
          await saveRecordToHandle(record, record.handle, content);
          await addRecentEntry({ type: 'file', name: record.name, handle: record.handle });
          return;
        }

        await saveActiveFileAs();
      } catch (error) {
        if (error?.name === 'AbortError') return;
        setStatus('Save failed.', 'danger');
        console.error(error);
      }
    }

    async function flushPendingRenderBeforeSave() {
      if (!state.debounceId) return;
      window.clearTimeout(state.debounceId);
      state.debounceId = 0;
      await renderPreview();
    }

    async function saveActiveFileAs() {
      const record = state.files.find((item) => item.path === state.activePath);
      if (!record) {
        setStatus('No file selected.', 'warning');
        return false;
      }

      if (record.readOnly) {
        setStatus('This guide is read-only. Open or create a Markdown file to save changes.', 'warning');
        updateSaveButton();
        return false;
      }

      const content = editor.value;
      try {
        await flushPendingRenderBeforeSave();
        if (await hasNativeFileCapability('file.saveAs')) {
          const saved = await saveRecordToNativeHandleAs(record, content);
          if (saved) return true;
        }

        if ('showSaveFilePicker' in window) {
          const oldPath = record.path;
          const handle = await window.showSaveFilePicker({
            suggestedName: record.name || getMarkdownExportName(),
            types: getMarkdownPickerTypes(),
          });

          await saveRecordToHandle(record, handle, content, { oldPath, replaceHandle: true });
          await addRecentEntry({ type: 'file', name: record.name, handle });
          return true;
        }

        downloadBlob(new Blob([content], { type: 'text/markdown;charset=utf-8' }), record.name || getMarkdownExportName());
        record.converted = false;
        record.needsSave = false;
        renderFileList();
        updateActiveFileLabel();
        updateSaveButton();
        setStatus(state.managedAssets?.size
          ? 'Downloaded Markdown copy. Image binaries are included in exports, not this Markdown file.'
          : 'Browser cannot write to files directly; downloaded a Markdown copy.', 'warning');
        return true;
      } catch (error) {
        if (error?.name === 'AbortError') return false;
        setStatus('Save as failed.', 'danger');
        console.error(error);
        return false;
      }
    }

    async function hasNativeFileCapability(capability) {
      if (!nativeBridgeClient?.isAvailable?.()) return false;
      try {
        return await nativeBridgeClient.hasCapability(capability);
      } catch {
        return false;
      }
    }

    async function saveRecordToNativeHandle(record, content) {
      setStatus(`Saving ${record.name} through Windows...`, 'busy');
      const result = await nativeBridgeClient.saveFile({
        nativeHandleId: record.nativeHandleId,
        content,
      });

      if (!result.ok) {
        setStatus(result.message || 'Windows save failed safely.', 'danger');
        updateSaveButton();
        return;
      }

      const payload = result.response?.payload || {};
      if (!payload.saved) {
        setStatus('Windows save did not complete.', 'warning');
        updateSaveButton();
        return;
      }

      updateRecordAfterNativeSave(record, payload, content);
      setStatus(buildSaveStatus(record.name), state.managedAssets?.size ? 'warning' : 'ok');
      await afterSaveActiveFile?.(record, content);
    }

    async function saveRecordToNativeWorkspaceHandle(record, content) {
      setStatus(`Saving ${record.name} through Windows workspace...`, 'busy');
      const result = await nativeBridgeClient.saveWorkspaceFile({
        nativeHandleId: record.nativeHandleId,
        content,
      });

      if (!result.ok) {
        setStatus(result.message || 'Windows workspace save failed safely.', 'danger');
        updateSaveButton();
        return;
      }

      const payload = result.response?.payload || {};
      if (!payload.saved) {
        setStatus('Windows workspace save did not complete.', 'warning');
        updateSaveButton();
        return;
      }

      updateRecordAfterNativeSave(record, payload, content);
      setStatus(buildSaveStatus(record.name), state.managedAssets?.size ? 'warning' : 'ok');
      await afterSaveActiveFile?.(record, content);
    }

    async function saveRecordToNativeHandleAs(record, content) {
      setStatus(`Saving ${record.name} through Windows...`, 'busy');
      const oldPath = record.path;
      const result = await nativeBridgeClient.saveFileAs({
        suggestedName: record.name || getMarkdownExportName(),
        content,
      });

      if (!result.ok) {
        setStatus(result.message || 'Windows save as failed safely. Using the browser fallback...', 'warning');
        return false;
      }

      const payload = result.response?.payload || {};
      if (payload.cancelled) {
        setStatus('Save as cancelled.', 'info');
        return true;
      }
      if (!payload.saved || !isValidNativeSaveAsPayload(payload)) {
        setStatus('Windows save as returned an unsupported response. Using the browser fallback...', 'warning');
        return false;
      }

      updateRecordAfterNativeSave(record, payload, content, { oldPath, replaceHandle: true });
      setStatus(buildSaveStatus(record.name), state.managedAssets?.size ? 'warning' : 'ok');
      await afterSaveActiveFile?.(record, content, oldPath);
      return true;
    }

    function updateRecordAfterNativeSave(record, payload, content, options = {}) {
      const oldPath = options.oldPath || record.path;
      const nextName = payload.displayName || payload.name || record.name;
      if (options.replaceHandle) {
        record.name = nextName;
        record.path = getUniqueRecordPath(normalisePath(nextName || record.path || record.name), record);
        record.nativeHandleId = payload.nativeHandleId;
        record.handle = null;
        state.files.sort(compareRecords);
      }

      record.file = new File([content], record.name, { type: getMimeTypeForPath(record.name) });
      record.converted = false;
      record.needsSave = false;
      updateRecordFingerprint(record, record.file);
      state.fileName = record.name;
      state.activePath = record.path;
      if (oldPath && oldPath !== record.path) {
        state.fileCache.delete(oldPath);
        state.dirtyPaths.delete(oldPath);
        state.externalChangePaths?.delete(oldPath);
        state.externalChangeDetails?.delete(oldPath);
      }
      state.fileCache.set(record.path, content);
      state.dirtyPaths.delete(record.path);
      state.externalChangePaths?.delete(record.path);
      state.externalChangeDetails?.delete(record.path);
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
    }

    function isValidNativeFilePayload(payload) {
      return payload
        && !payload.cancelled
        && typeof payload.name === 'string'
        && isSupportedFile(payload.name)
        && typeof payload.content === 'string'
        && typeof payload.nativeHandleId === 'string'
        && payload.nativeHandleId.trim();
    }

    function isValidNativeSaveAsPayload(payload) {
      return payload
        && typeof payload.name === 'string'
        && isSupportedFile(payload.name)
        && typeof payload.nativeHandleId === 'string'
        && payload.nativeHandleId.trim();
    }

    function isValidNativeWorkspacePayload(payload) {
      return payload
        && !payload.cancelled
        && typeof payload.workspaceName === 'string'
        && typeof payload.nativeWorkspaceId === 'string'
        && payload.nativeWorkspaceId.trim()
        && Array.isArray(payload.files)
        && payload.files.every(isValidNativeWorkspaceFilePayload);
    }

    function isValidNativeWorkspaceFilePayload(file) {
      const path = normalisePath(file?.path || file?.displayPath || file?.name || '');
      return file
        && typeof file.name === 'string'
        && isSupportedFile(file.name)
        && path
        && !path.startsWith('/')
        && !/^[a-z]:/i.test(path)
        && !path.split('/').some((part) => part === '.' || part === '..')
        && isSupportedFile(path)
        && typeof file.content === 'string'
        && typeof file.nativeHandleId === 'string'
        && file.nativeHandleId.trim();
    }

    async function saveRecordToHandle(record, handle, content, options = {}) {
      const oldPath = options.oldPath || record.path;
      setStatus(`Saving ${record.name}...`);
      const writable = await handle.createWritable();
      await writable.write(content);
      await writable.close();

      const file = await handle.getFile();
      if (options.replaceHandle) {
        record.handle = handle;
        record.name = handle.name || record.name;
        record.path = getUniqueRecordPath(normalisePath(handle.name || record.path || record.name), record);
        state.files.sort(compareRecords);
      }

      record.file = file;
      record.converted = false;
      record.needsSave = false;
      updateRecordFingerprint(record, file);
      state.fileName = record.name;
      state.activePath = record.path;
      if (oldPath && oldPath !== record.path) {
        state.fileCache.delete(oldPath);
        state.dirtyPaths.delete(oldPath);
        state.externalChangePaths?.delete(oldPath);
        state.externalChangeDetails?.delete(oldPath);
      }
      state.fileCache.set(record.path, content);
      state.dirtyPaths.delete(record.path);
      state.externalChangePaths?.delete(record.path);
      state.externalChangeDetails?.delete(record.path);
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
      setStatus(buildSaveStatus(record.name), state.managedAssets?.size ? 'warning' : 'ok');
      await afterSaveActiveFile?.(record, content, oldPath);
    }

    async function addFilesToWorkspace() {
      try {
        if ('showOpenFilePicker' in window) {
          const handles = await window.showOpenFilePicker({
            id: 'md-mmd-renderer-add-files',
            multiple: true,
            types: getMarkdownPickerTypes(),
          });
          const records = [];
          for (const handle of handles || []) {
            const file = await handle.getFile();
            if (!isSupportedFile(file.name)) continue;
            records.push({ name: file.name, path: file.name, file, handle });
          }
          await addRecordsToWorkspace(records, { folderName: state.folderName || 'Workspace' });
          return;
        }
      } catch (error) {
        if (error?.name === 'AbortError') return;
        setStatus('File picker failed. Using the browser fallback...', 'warning');
      }

      fileInput.dataset.mode = 'add';
      fileInput.multiple = true;
      fileInput.click();
    }

    async function addFilesFromInput(files) {
      const records = [...(files || [])]
        .filter((file) => isSupportedFile(file.name))
        .map((file) => ({
          name: file.name,
          path: normalisePath(file.webkitRelativePath || file.name),
          file,
        }));
      await addRecordsToWorkspace(records, { folderName: state.folderName || 'Added files' });
    }

    async function addRecordsToWorkspace(records, options = {}) {
      const prepared = records
        .filter((record) => isSupportedFile(record.name))
        .map(prepareRecord);

      if (!prepared.length) {
        setStatus('No supported Markdown or Mermaid files were selected.', 'warning');
        return;
      }

      const added = [];
      prepared.forEach((record) => {
        record.path = getUniqueRecordPath(record.path, record);
        record.name = record.name || record.path.split('/').pop() || record.path;
        state.files.push(record);
        added.push(record);
      });

      state.files = uniqueByPath(state.files).sort(compareRecords);
      state.folderName = options.folderName || state.folderName || 'Workspace';
      state.workspaceKind = state.workspaceKind || 'virtual';
      state.artifactBundle = null;
      fileSearch.value = '';
      afterLibraryLoaded?.();
      renderFileList();
      syncEditorReadOnly?.();
      updateSaveButton();
      setExportTrust('', '');
      await selectFile(options.selectPath || added[added.length - 1].path);
      setStatus(`${added.length} file${added.length === 1 ? '' : 's'} added to the workspace.`, 'ok');
    }

    async function createFileInWorkspace(path) {
      if (!path) {
        setStatus('Use a relative .md, .markdown, .mmd, or .mermaid path inside the workspace.', 'warning');
        return;
      }

      const existing = state.files.find((record) => record.path.toLowerCase() === path.toLowerCase());
      if (existing) {
        await selectFile(existing.path);
        setStatus(`${existing.path} is already in the workspace.`, 'warning');
        return;
      }

      try {
        if (!await ensureDirectoryWritePermission(state.workspaceDirectoryHandle)) {
          setStatus('Browser permission is needed to create files in this workspace.', 'warning');
          return;
        }

        const handle = await getWorkspaceFileHandle(path, { create: true });
        const file = await handle.getFile();
        const text = await file.text();
        const name = path.split('/').pop() || path;
        const record = prepareRecord({ name, path, handle, file });
        state.fileCache.set(path, text);
        state.savedContentCache?.set(path, text);
        await addRecordsToWorkspace([record], {
          folderName: state.folderName || state.workspaceDirectoryHandle.name || 'Workspace',
          selectPath: path,
        });
        setStatus(`${path} added to the workspace.`, 'ok');
      } catch (error) {
        if (error?.name === 'AbortError') return;
        setStatus('Could not create the file in this workspace.', 'danger');
        console.error(error);
      }
    }

    async function createNativeWorkspaceFile(path) {
      try {
        setStatus(`Creating ${path} through Windows workspace...`, 'busy');
        const result = await nativeBridgeClient.createWorkspaceFile({
          nativeWorkspaceId: state.nativeWorkspaceId,
          path,
          content: '',
        });

        if (!result.ok) {
          setStatus(result.message || 'Windows workspace file creation failed safely.', 'danger');
          return;
        }

        const payload = result.response?.payload || {};
        if (!payload.created || !isValidNativeWorkspaceFilePayload(payload)) {
          setStatus('Windows workspace returned an unsupported file response.', 'warning');
          return;
        }

        const recordPath = normalisePath(payload.path || payload.displayPath || path);
        const recordName = payload.name || recordPath.split('/').pop() || recordPath;
        const content = String(payload.content ?? '');
        const record = prepareRecord({
          name: recordName,
          path: recordPath,
          file: new File([content], recordName, { type: getMimeTypeForPath(recordPath) }),
          nativeHandleId: payload.nativeHandleId,
        });
        state.fileCache.set(recordPath, content);
        state.savedContentCache?.set(recordPath, content);
        await addRecordsToWorkspace([record], {
          folderName: state.folderName || 'Windows workspace',
          selectPath: recordPath,
        });
        state.workspaceKind = 'native-folder';
        setStatus(`${recordPath} added to the Windows workspace.`, 'ok');
      } catch (error) {
        setStatus('Could not create the file in this Windows workspace.', 'danger');
        console.error(error);
      }
    }

    function handleNativeWorkspaceChanged(message) {
      const payload = message?.payload || {};
      if (!state.nativeWorkspaceId || payload.nativeWorkspaceId !== state.nativeWorkspaceId) return;
      if (!Array.isArray(payload.changes)) return;

      let marked = 0;
      payload.changes.forEach((change) => {
        const safeChange = normaliseNativeWorkspaceChange(change);
        if (!safeChange) return;
        marked += applyNativeWorkspaceChange(safeChange) ? 1 : 0;
      });

      if (!marked) return;
      state.files.sort(compareRecords);
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
      const activeDetail = state.externalChangeDetails?.get(state.activePath);
      if (activeDetail) {
        clearTimeout(state.debounceId);
        setStatus(getNativeWorkspaceChangeStatus(activeDetail, state.dirtyPaths.has(state.activePath)), 'warning');
        return;
      }
      setStatus(`${marked} workspace file${marked === 1 ? '' : 's'} changed outside the app. Open marked files to review them.`, 'warning');
    }

    function normaliseNativeWorkspaceChange(change) {
      if (!change || typeof change !== 'object') return null;
      const kind = String(change.kind || '').trim();
      if (!['changed', 'created', 'deleted', 'renamed'].includes(kind)) return null;
      const path = sanitiseWorkspaceFilePath(change.path);
      if (!path) return null;
      const oldPath = kind === 'renamed' ? sanitiseWorkspaceFilePath(change.oldPath) : '';
      if (kind === 'renamed' && !oldPath) return null;
      const nativeHandleId = typeof change.nativeHandleId === 'string' && change.nativeHandleId.trim()
        ? change.nativeHandleId.trim()
        : '';
      return { kind, path, oldPath, nativeHandleId };
    }

    function applyNativeWorkspaceChange(change) {
      if (change.kind === 'created') {
        const existing = findRecordByPath(change.path);
        if (existing) {
          markExternalChange(existing.path, { ...change, kind: 'changed' });
          return true;
        }

        const name = change.path.split('/').pop() || change.path;
        const record = prepareRecord({
          name,
          path: change.path,
          file: new File([''], name, { type: getMimeTypeForPath(change.path) }),
          nativeHandleId: change.nativeHandleId,
        });
        record.externalPlaceholder = true;
        state.files.push(record);
        markExternalChange(record.path, change);
        return true;
      }

      if (change.kind === 'renamed') {
        const record = findRecordByPath(change.oldPath);
        if (!record) {
          return applyNativeWorkspaceChange({ ...change, kind: 'created' });
        }

        if (state.dirtyPaths.has(record.path)) {
          markExternalChange(record.path, change);
          return true;
        }

        const oldPath = record.path;
        record.path = change.path;
        record.name = change.path.split('/').pop() || record.name;
        if (change.nativeHandleId) record.nativeHandleId = change.nativeHandleId;
        moveCachedRecordPath(oldPath, record.path);
        markExternalChange(record.path, change);
        state.externalChangePaths?.delete(oldPath);
        state.externalChangeDetails?.delete(oldPath);
        if (state.activePath === oldPath) {
          state.activePath = record.path;
          state.fileName = record.name;
        }
        return true;
      }

      const record = findRecordByPath(change.path);
      if (!record) return false;
      if (change.nativeHandleId) record.nativeHandleId = change.nativeHandleId;
      markExternalChange(record.path, change);
      return true;
    }

    function findRecordByPath(path) {
      const lower = String(path || '').toLowerCase();
      return state.files.find((record) => record.path.toLowerCase() === lower);
    }

    function markExternalChange(path, detail) {
      state.externalChangePaths?.add(path);
      state.externalChangeDetails?.set(path, detail);
    }

    function clearExternalChange(path) {
      state.externalChangePaths?.delete(path);
      state.externalChangeDetails?.delete(path);
    }

    function moveCachedRecordPath(oldPath, nextPath) {
      for (const cache of [state.fileCache, state.savedContentCache]) {
        if (!cache?.has(oldPath)) continue;
        cache.set(nextPath, cache.get(oldPath));
        cache.delete(oldPath);
      }
      if (state.dirtyPaths.has(oldPath)) {
        state.dirtyPaths.delete(oldPath);
        state.dirtyPaths.add(nextPath);
      }
      clearExternalChange(oldPath);
    }

    function getNativeWorkspaceChangeStatus(detail, dirty) {
      if (detail.kind === 'deleted') {
        return 'File was deleted outside Lens Docs Studio. Local content is preserved in memory.';
      }
      if (detail.kind === 'renamed') {
        return dirty
          ? 'File was renamed outside Lens Docs Studio while local edits exist. Review before saving.'
          : 'File was renamed outside Lens Docs Studio. Review before saving.';
      }
      if (detail.kind === 'created') {
        return `External file created: ${detail.path}. Use Refresh active file to load it.`;
      }
      return dirty
        ? 'External change detected while local edits exist. Save or refresh explicitly.'
        : 'External change detected. Use Refresh active file to reload.';
    }

    function showNativeWorkspaceChangeStatus(record) {
      const detail = state.externalChangeDetails?.get(record?.path);
      if (!detail) return false;
      setStatus(getNativeWorkspaceChangeStatus(detail, state.dirtyPaths.has(record.path)), 'warning');
      return true;
    }

    async function refreshActiveFile() {
      const record = state.files.find((item) => item.path === state.activePath);
      if (!record) {
        setStatus('No file selected.', 'warning');
        return;
      }

      if (record.nativeHandleId && state.workspaceKind === 'native-folder' && await hasNativeFileCapability('workspace.refreshFile')) {
        await refreshNativeWorkspaceFile(record);
        return;
      }

      if (!record.handle) {
        setStatus('This document has no linked local file to refresh.', 'warning');
        return;
      }
      if (state.dirtyPaths.has(record.path) && !await confirmAction(`${record.name} has in-memory edits. Reload the local file and discard those edits?`, {
        title: 'Reload local file?',
        kicker: 'Refresh file',
        confirmLabel: 'Reload file',
        danger: true,
      })) {
        return;
      }

      await reloadRecordFromHandle(record, { status: `${record.name} refreshed from the local file.` });
    }

    async function refreshNativeWorkspaceFile(record) {
      const detail = state.externalChangeDetails?.get(record.path);
      if (detail?.kind === 'deleted') {
        setStatus('File was deleted outside Lens Docs Studio. Local content is preserved in memory.', 'warning');
        return;
      }

      if (state.dirtyPaths.has(record.path) && !await confirmAction(`${record.name} has in-memory edits. Reload the Windows workspace file and discard those edits?`, {
        title: 'Reload Windows file?',
        kicker: 'Refresh file',
        confirmLabel: 'Reload file',
        danger: true,
      })) {
        showNativeWorkspaceChangeStatus(record);
        return;
      }

      try {
        setStatus(`Refreshing ${record.name} from Windows workspace...`, 'busy');
        const result = await nativeBridgeClient.refreshWorkspaceFile({
          nativeWorkspaceId: state.nativeWorkspaceId,
          nativeHandleId: record.nativeHandleId,
          path: record.path,
        });

        if (!result.ok) {
          setStatus(result.message || 'Windows workspace refresh failed safely.', 'danger');
          return;
        }

        const payload = result.response?.payload || {};
        if (!payload.refreshed || !isValidNativeWorkspaceFilePayload(payload)) {
          setStatus('Windows workspace returned an unsupported refresh response.', 'warning');
          return;
        }

        await reloadRecordFromNativePayload(record, payload, { status: `${record.name} refreshed from the Windows workspace.` });
      } catch (error) {
        setStatus('Could not refresh the Windows workspace file.', 'danger');
        console.error(error);
      }
    }

    async function checkForExternalUpdates() {
      if (!state.files.some((record) => record.handle)) return;
      const active = state.files.find((record) => record.path === state.activePath);
      if (active?.handle) {
        await handleExternalChange(active, { reason: 'focus' });
      }

      let changed = 0;
      for (const record of state.files) {
        if (!record.handle || record.path === state.activePath) continue;
        try {
          const file = await record.handle.getFile();
          if (hasFingerprintChanged(record, file)) {
            state.externalChangePaths?.add(record.path);
            state.externalChangeDetails?.set(record.path, { kind: 'changed', path: record.path });
            changed += 1;
          }
        } catch {
          // A missing or permission-blocked file is reported when the user opens or saves it.
        }
      }
      if (changed) {
        renderFileList();
        updateActiveFileLabel();
        setStatus(`${changed} workspace file${changed === 1 ? '' : 's'} changed outside the app. Open a marked file to review it.`, 'warning');
      }
    }

    async function handleExternalChange(record, { reason } = {}) {
      if (!record?.handle) return 'none';
      let file;
      try {
        file = await record.handle.getFile();
      } catch {
        state.externalChangePaths?.add(record.path);
        state.externalChangeDetails?.set(record.path, { kind: 'changed', path: record.path });
        renderFileList();
        updateActiveFileLabel();
        setStatus(`Could not check ${record.name}. Browser permission may be needed.`, 'warning');
        return 'unavailable';
      }

      if (!record.fingerprint) {
        record.file = file;
        updateRecordFingerprint(record, file);
        return 'none';
      }

      if (!hasFingerprintChanged(record, file)) {
        state.externalChangePaths?.delete(record.path);
        state.externalChangeDetails?.delete(record.path);
        return 'none';
      }

      const alreadyMarked = state.externalChangePaths?.has(record.path);
      state.externalChangePaths?.add(record.path);
      state.externalChangeDetails?.set(record.path, { kind: 'changed', path: record.path });
      if (reason === 'focus' && alreadyMarked) return 'marked';

      const dirty = state.dirtyPaths.has(record.path);
      const reload = await confirmAction(dirty
        ? `${record.name} changed outside the app. Reload the local file and discard your in-memory edits? Choose Cancel to keep your local edits.`
        : `${record.name} changed outside the app. Reload the latest version?`, {
        title: 'External change detected',
        kicker: 'Local file changed',
        confirmLabel: dirty ? 'Reload and discard edits' : 'Reload latest',
        danger: dirty,
      });

      if (reload) {
        await reloadRecordFromFile(record, file, { status: `${record.name} reloaded from the local file.` });
        return 'reloaded';
      }

      renderFileList();
      updateActiveFileLabel();
      setStatus(`${record.name} changed outside the app. Keeping the in-memory version for now.`, 'warning');
      return 'kept';
    }

    async function reloadRecordFromHandle(record, options = {}) {
      const file = await record.handle.getFile();
      await reloadRecordFromFile(record, file, options);
    }

    async function reloadRecordFromFile(record, file, options = {}) {
      const text = await file.text();
      record.file = file;
      record.converted = false;
      record.needsSave = false;
      updateRecordFingerprint(record, file);
      state.fileCache.set(record.path, text);
      state.savedContentCache?.set(record.path, text);
      state.dirtyPaths.delete(record.path);
      state.externalChangePaths?.delete(record.path);
      state.externalChangeDetails?.delete(record.path);
      if (record.path === state.activePath) {
        editor.value = text;
        editor.scrollTop = 0;
        preview.scrollTop = 0;
        resetEditorHistory();
        syncEditorReadOnly?.();
        updateEditorChrome?.();
        await afterSaveActiveFile?.(record, text);
        await renderPreview();
        restoreScrollPosition?.(record.path);
      }
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
      setStatus(options.status || `${record.name} refreshed.`, 'ok');
    }

    async function reloadRecordFromNativePayload(record, payload, options = {}) {
      const nextPath = normalisePath(payload.path || payload.displayPath || record.path);
      const nextName = payload.name || nextPath.split('/').pop() || record.name;
      const oldPath = record.path;
      const text = String(payload.content ?? '');
      record.name = nextName;
      record.path = nextPath;
      record.file = new File([text], nextName, { type: getMimeTypeForPath(nextPath) });
      record.nativeHandleId = payload.nativeHandleId || record.nativeHandleId;
      record.converted = false;
      record.needsSave = false;
      record.externalPlaceholder = false;
      updateRecordFingerprint(record, record.file);
      if (oldPath !== record.path) {
        moveCachedRecordPath(oldPath, record.path);
      }
      state.fileCache.set(record.path, text);
      state.savedContentCache?.set(record.path, text);
      state.dirtyPaths.delete(record.path);
      clearExternalChange(record.path);
      state.activePath = record.path;
      state.fileName = record.name;
      editor.value = text;
      editor.scrollTop = 0;
      preview.scrollTop = 0;
      resetEditorHistory();
      syncEditorReadOnly?.();
      updateEditorChrome?.();
      await afterSaveActiveFile?.(record, text, oldPath);
      await renderPreview();
      restoreScrollPosition?.(record.path);
      state.files.sort(compareRecords);
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
      setStatus(options.status || `${record.name} refreshed.`, 'ok');
    }

    async function readRecordSource(record) {
      const file = record.handle ? await record.handle.getFile() : record.file;
      if (!file) return { file: null, text: '' };
      return { file, text: await file.text() };
    }

    function prepareRecord(record) {
      const path = normalisePath(record.path || record.name || 'untitled.md');
      const name = record.name || path.split('/').pop() || path;
      const prepared = {
        ...record,
        name,
        path,
        needsSave: Boolean(record.needsSave),
      };
      if (prepared.file) updateRecordFingerprint(prepared, prepared.file);
      return prepared;
    }

    function updateRecordFingerprint(record, file) {
      record.fingerprint = getFileFingerprint(file);
      record.lastModified = file?.lastModified || 0;
      record.size = file?.size || 0;
    }

    function getFileFingerprint(file) {
      if (!file) return null;
      return {
        lastModified: file.lastModified || 0,
        size: file.size || 0,
      };
    }

    function hasFingerprintChanged(record, file) {
      const current = getFileFingerprint(file);
      const previous = record.fingerprint;
      return Boolean(previous && current && (previous.lastModified !== current.lastModified || previous.size !== current.size));
    }

    function getMarkdownPickerTypes() {
      return [{
        description: 'Markdown and Mermaid files',
        accept: {
          'text/markdown': ['.md', '.markdown'],
          'text/plain': ['.mmd', '.mermaid', '.txt'],
        },
      }];
    }

    function getAvailableUntitledPath(directory = '') {
      const prefix = sanitiseWorkspaceFolderPath(directory);
      const existing = new Set(state.files.map((record) => record.path.toLowerCase()));
      const candidates = [`${prefix ? `${prefix}/` : ''}untitled.md`];
      for (let index = 2; index < 1000; index += 1) {
        candidates.push(`${prefix ? `${prefix}/` : ''}untitled-${index}.md`);
      }
      return candidates.find((path) => !existing.has(path.toLowerCase())) || `${prefix ? `${prefix}/` : ''}untitled-${Date.now()}.md`;
    }

    function getNewFileDirectory() {
      const selected = sanitiseWorkspaceFolderPath(state.selectedTreeFolderPath);
      if (selected && getWorkspaceFolderPaths().includes(selected)) return selected;
      const activeDirectory = sanitiseWorkspaceFolderPath(state.activePath.split('/').slice(0, -1).join('/'));
      if (activeDirectory) return activeDirectory;
      return '';
    }

    function getWorkspaceFolderPaths() {
      const paths = new Set();
      state.files.forEach((record) => {
        const parts = String(record.path || '').split('/').filter(Boolean);
        parts.pop();
        parts.forEach((_, index) => {
          paths.add(parts.slice(0, index + 1).join('/'));
        });
      });
      return [...paths];
    }

    function getUniqueRecordPath(path, currentRecord) {
      const cleanPath = normalisePath(path || currentRecord?.name || 'untitled.md');
      const existing = new Set(state.files
        .filter((record) => record !== currentRecord)
        .map((record) => record.path.toLowerCase()));
      if (!existing.has(cleanPath.toLowerCase())) return cleanPath;

      const extensionMatch = cleanPath.match(/(\.[^./]+)$/);
      const extension = extensionMatch?.[1] || '';
      const stem = extension ? cleanPath.slice(0, -extension.length) : cleanPath;
      for (let index = 2; index < 1000; index += 1) {
        const candidate = `${stem}-${index}${extension}`;
        if (!existing.has(candidate.toLowerCase())) return candidate;
      }
      return `${stem}-${Date.now()}${extension}`;
    }

    function sanitiseWorkspaceFilePath(value) {
      const path = normalisePath(String(value || '').trim());
      if (!path || path.startsWith('/') || /^[a-z]:/i.test(path)) return '';
      const parts = path.split('/').filter(Boolean);
      if (!parts.length || parts.some((part) => part === '.' || part === '..')) return '';
      const cleanPath = parts.join('/');
      return isSupportedFile(cleanPath) ? cleanPath : '';
    }

    function sanitiseWorkspaceFolderPath(value) {
      const path = normalisePath(String(value || '').trim());
      if (!path || path.startsWith('/') || /^[a-z]:/i.test(path)) return '';
      const parts = path.split('/').filter(Boolean);
      if (parts.some((part) => part === '.' || part === '..')) return '';
      return parts.join('/');
    }

    function getMimeTypeForPath(path) {
      return /\.(mmd|mermaid)$/i.test(path) ? 'text/plain' : 'text/markdown';
    }

    async function getWorkspaceFileHandle(path, options = {}) {
      const parts = path.split('/').filter(Boolean);
      const fileName = parts.pop();
      let directory = state.workspaceDirectoryHandle;
      for (const part of parts) {
        directory = await directory.getDirectoryHandle(part, { create: Boolean(options.create) });
      }
      return await directory.getFileHandle(fileName, { create: Boolean(options.create) });
    }

    async function ensureDirectoryWritePermission(handle) {
      if (!handle || typeof handle.queryPermission !== 'function' || typeof handle.requestPermission !== 'function') return true;
      const options = { mode: 'readwrite' };
      if (await handle.queryPermission(options) === 'granted') return true;
      return await handle.requestPermission(options) === 'granted';
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
      addFilesToWorkspace,
      addFilesFromInput,
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
      saveActiveFileAs,
      refreshActiveFile,
      checkForExternalUpdates,
      ensureWritePermission,
    };
}
