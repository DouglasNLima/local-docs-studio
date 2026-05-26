import { APP_BROWSER_TITLE, APP_BUILD, APP_VERSION, storageKeys } from '../state/config.js';
import { createArtifactBundleReader } from './artifact-bundle-reader.js';
import { getFileExtensionLabel } from '../utils/files.js';
import { clamp, readStoredNumber } from '../utils/format.js';

export function createUiService({
  state,
  dom,
  callbacks = {},
}) {
  const {
    app,
    shell,
    workspace,
    fileList,
    fileSearch,
    fileViewListButton,
    fileViewTreeButton,
    treeExpandButton,
    treeCollapseButton,
    treeRevealButton,
    fileCount,
    folderBadge,
    artifactBundleSummary,
    activeFileLabel,
    saveButton,
    saveAsButton,
    refreshFileButton,
    appVersionBadge,
    createMenu,
    layoutModeControl,
    sidebarSplitter,
    workspaceSplitter,
    themeToggleButton,
    zoomValue,
    status,
    focusModeButton,
    focusModeExitButton,
    sidebarCollapseBtn,
    sidebarExpandBtn,
    railFileCount,
  } = dom;
  const {
    openArtifactPath,
  } = callbacks;

  const artifactBundleReader = createArtifactBundleReader({
    container: artifactBundleSummary,
    state,
    onOpenPath: openArtifactPath,
  });

    document.title = APP_BROWSER_TITLE;
    if (appVersionBadge) appVersionBadge.textContent = `v${APP_VERSION} (build ${APP_BUILD})`;

    function renderFileList() {
      const term = fileSearch.value.trim().toLowerCase();
      const visibleFiles = state.files.filter((file) => file.path.toLowerCase().includes(term));

      fileCount.textContent = String(state.files.length);
      if (railFileCount) railFileCount.textContent = state.files.length > 0 ? String(state.files.length) : '';
      folderBadge.textContent = state.folderName || 'No folder';
      artifactBundleReader.render();
      updateFileBrowserControls();
      fileList.innerHTML = '';

      if (!state.files.length) {
        fileList.appendChild(createFileListEmptyState());
        return;
      }

      if (!visibleFiles.length) {
        fileList.appendChild(createEmptyState('No files match this filter.'));
        return;
      }

      if (state.fileBrowserView === 'tree') {
        renderFileTree(visibleFiles, term);
        return;
      }

      visibleFiles.forEach((file) => {
        fileList.appendChild(createFileItem(file));
      });
    }

    function renderFileTree(records, term) {
      const tree = buildFileTree(records);
      const container = document.createElement('div');
      container.className = 'file-tree';
      container.setAttribute('role', 'tree');
      container.setAttribute('aria-label', 'Workspace folder tree');

      tree.files.forEach((file) => {
        container.appendChild(createFileItem(file, { treeDepth: 0, pathLabel: 'Workspace root' }));
      });
      renderTreeFolders(container, [...tree.folders.values()], 0, Boolean(term));

      fileList.appendChild(container);
    }

    function renderTreeFolders(container, folders, depth, forceExpanded) {
      folders
        .sort((left, right) => left.path.localeCompare(right.path, undefined, { sensitivity: 'base' }))
        .forEach((folder) => {
          const expanded = forceExpanded || !state.collapsedTreeFolders.has(folder.path);
          const button = document.createElement('button');
          button.type = 'button';
          button.className = 'tree-folder';
          button.dataset.treeFolder = folder.path;
          button.style.setProperty('--tree-depth', String(depth));
          button.setAttribute('aria-expanded', String(expanded));
          button.title = folder.path;

          const twisty = document.createElement('span');
          twisty.className = 'tree-folder-twisty';
          twisty.setAttribute('aria-hidden', 'true');
          twisty.innerHTML = expanded
            ? '<svg class="toolbar-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg>'
            : '<svg class="toolbar-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="m9 18 6-6-6-6"/></svg>';

          const icon = document.createElement('span');
          icon.className = 'tree-folder-icon';
          icon.setAttribute('aria-hidden', 'true');
          icon.innerHTML = '<svg class="toolbar-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="M3 5h6l2 2h10v12H3z"/></svg>';

          const textWrap = document.createElement('span');
          textWrap.className = 'file-text';

          const name = document.createElement('span');
          name.className = 'file-name';
          name.textContent = folder.name;

          const meta = document.createElement('span');
          meta.className = 'file-path';
          const fileCountText = countTreeFiles(folder);
          meta.textContent = `${folder.path} · ${fileCountText} file${fileCountText === 1 ? '' : 's'}`;

          textWrap.append(name, meta);
          button.append(twisty, icon, textWrap);
          container.appendChild(button);

          if (!expanded) return;
          folder.files
            .sort(compareFileRecords)
            .forEach((file) => {
              const parentPath = file.path.split('/').slice(0, -1).join('/') || 'Workspace root';
              container.appendChild(createFileItem(file, { treeDepth: depth + 1, pathLabel: parentPath }));
            });
          renderTreeFolders(container, [...folder.folders.values()], depth + 1, forceExpanded);
        });
    }

    function createFileItem(file, options = {}) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = `file-item${file.path === state.activePath ? ' active' : ''}${options.treeDepth !== undefined ? ' tree-file-item' : ''}`;
      button.dataset.path = file.path;
      button.title = file.path;
      if (options.treeDepth !== undefined) {
        button.style.setProperty('--tree-depth', String(options.treeDepth));
      }

      const icon = document.createElement('span');
      icon.className = 'file-icon';
      icon.textContent = getFileExtensionLabel(file.name);

      const textWrap = document.createElement('span');
      textWrap.className = 'file-text';

      const name = document.createElement('span');
      name.className = 'file-name';
      name.textContent = file.name;

      const path = document.createElement('span');
      path.className = 'file-path';
      path.textContent = options.pathLabel || file.path;

      textWrap.append(name, path);
      button.append(icon, textWrap);
      appendFileStateMarker(button, file);
      return button;
    }

    function appendFileStateMarker(button, file) {
      if (state.externalChangePaths.has(file.path)) {
        const external = document.createElement('span');
        external.className = 'external-change-dot';
        external.title = state.dirtyPaths.has(file.path)
          ? 'Edited in memory and changed outside the app'
          : 'Changed outside the app';
        button.appendChild(external);
        return;
      }
      if (state.dirtyPaths.has(file.path)) {
        const dirty = document.createElement('span');
        dirty.className = 'dirty-dot';
        dirty.title = 'Edited in memory';
        button.appendChild(dirty);
        return;
      }
      if (file.readOnly) {
        const readOnly = document.createElement('span');
        readOnly.className = 'read-only-dot';
        readOnly.title = 'Read-only document';
        readOnly.textContent = 'RO';
        button.appendChild(readOnly);
        return;
      }
      const spacer = document.createElement('span');
      spacer.setAttribute('aria-hidden', 'true');
      button.appendChild(spacer);
    }

    function buildFileTree(records) {
      const root = createTreeNode('', '');
      records.forEach((record) => {
        const parts = String(record.path || record.name).split('/').filter(Boolean);
        const fileName = parts.pop() || record.name;
        let node = root;
        parts.forEach((part) => {
          const folderPath = node.path ? `${node.path}/${part}` : part;
          if (!node.folders.has(part)) {
            node.folders.set(part, createTreeNode(part, folderPath));
          }
          node = node.folders.get(part);
        });
        node.files.push({ ...record, name: record.name || fileName });
      });
      root.files.sort(compareFileRecords);
      return root;
    }

    function createTreeNode(name, path) {
      return {
        name,
        path,
        folders: new Map(),
        files: [],
      };
    }

    function countTreeFiles(folder) {
      let total = folder.files.length;
      folder.folders.forEach((child) => {
        total += countTreeFiles(child);
      });
      return total;
    }

    function compareFileRecords(left, right) {
      return left.path.localeCompare(right.path, undefined, { sensitivity: 'base' });
    }

    function createEmptyState(message) {
      const element = document.createElement('div');
      element.className = 'empty-state';
      element.textContent = message;
      return element;
    }

    function createFileListEmptyState() {
      const element = document.createElement('div');
      element.className = 'empty-state';
      element.innerHTML = `
        <strong>No documents loaded</strong>
        <p>Open a file or folder, or create a new Markdown document from a local template.</p>
        <div class="empty-actions">
          <button type="button" data-sidebar-action="openFile">Open file</button>
          <button type="button" data-sidebar-action="openFolder">Open folder</button>
          <button type="button" data-sidebar-action="newFile">New file</button>
          <button type="button" data-sidebar-action="addFile">Add file</button>
        </div>`;
      return element;
    }

    function updateActiveFileLabel() {
      const record = state.files.find((item) => item.path === state.activePath);
      const dirty = state.activePath && state.dirtyPaths.has(state.activePath) ? ' · edited in memory' : '';
      const external = state.activePath && state.externalChangePaths.has(state.activePath) ? ' · changed outside the app' : '';
      const readOnly = record?.readOnly ? ' · read-only' : '';
      activeFileLabel.textContent = state.activePath ? `${state.activePath}${dirty}${external}${readOnly}` : 'No file selected';
    }

    function updateSaveButton() {
      const record = state.files.find((item) => item.path === state.activePath);
      const canSaveConvertedCopy = Boolean(record?.converted && !record?.handle);
      const disabled = !record || Boolean(record?.readOnly);
      saveButton.disabled = disabled;
      if (saveAsButton) saveAsButton.disabled = disabled;
      if (refreshFileButton) refreshFileButton.disabled = !record || !record.handle;
      if (record?.readOnly) {
        saveButton.title = 'Read-only guide documents cannot be saved.';
        if (saveAsButton) saveAsButton.title = 'Read-only guide documents cannot be saved.';
        if (refreshFileButton) refreshFileButton.title = record.handle ? 'Refresh active file' : 'No linked local file to refresh';
        return;
      }
      if (canSaveConvertedCopy) {
        saveButton.title = 'Save or download the converted Markdown copy';
        if (saveAsButton) saveAsButton.title = 'Save the converted Markdown copy as another file';
        if (refreshFileButton) refreshFileButton.title = 'Converted documents have no linked local source to refresh';
        return;
      }
      saveButton.title = record?.handle
        ? 'Save changes back to the opened file'
        : 'Save changes using your browser file picker';
      if (saveAsButton) saveAsButton.title = 'Save a copy using your browser file picker';
      if (refreshFileButton) refreshFileButton.title = record?.handle
        ? 'Read the linked local file again'
        : 'No linked local file to refresh';
    }

    function hasUnsavedChanges() {
      return state.dirtyPaths.size > 0 || state.files.some((file) => (file.converted || file.needsSave) && !file.handle);
    }

    function confirmDiscardUnsaved(message) {
      if (!hasUnsavedChanges()) return true;
      return window.confirm(message);
    }

    function closeOpenMenus() {
      document.querySelectorAll('details.menu[open]').forEach((menu) => {
        menu.removeAttribute('open');
      });
    }

    function openCreateMenu() {
      closeOpenMenus();
      createMenu.open = true;
      createMenu.querySelector('summary')?.focus();
    }

    function setFileBrowserView(view) {
      const next = view === 'tree' ? 'tree' : 'list';
      state.fileBrowserView = next;
      localStorage.setItem(storageKeys.fileBrowserView, next);
      renderFileList();
    }

    function toggleTreeFolder(path) {
      if (!path) return;
      if (state.collapsedTreeFolders.has(path)) {
        state.collapsedTreeFolders.delete(path);
      } else {
        state.collapsedTreeFolders.add(path);
      }
      renderFileList();
    }

    function expandTreeFolders() {
      state.collapsedTreeFolders.clear();
      setFileBrowserView('tree');
    }

    function collapseTreeFolders() {
      state.collapsedTreeFolders = new Set(getAllTreeFolderPaths());
      setFileBrowserView('tree');
    }

    function revealActiveFileInTree() {
      if (!state.activePath) return;
      state.fileBrowserView = 'tree';
      localStorage.setItem(storageKeys.fileBrowserView, 'tree');
      const term = fileSearch.value.trim().toLowerCase();
      if (term && !state.activePath.toLowerCase().includes(term)) {
        fileSearch.value = '';
      }
      getAncestorFolderPaths(state.activePath).forEach((path) => {
        state.collapsedTreeFolders.delete(path);
      });
      renderFileList();
      window.requestAnimationFrame(() => {
        const activeButton = findFileButton(state.activePath);
        activeButton?.focus({ preventScroll: true });
        activeButton?.scrollIntoView({ block: 'nearest' });
      });
    }

    function updateFileBrowserControls() {
      const isTree = state.fileBrowserView === 'tree';
      fileViewListButton?.classList.toggle('active', !isTree);
      fileViewTreeButton?.classList.toggle('active', isTree);
      fileViewListButton?.setAttribute('aria-pressed', String(!isTree));
      fileViewTreeButton?.setAttribute('aria-pressed', String(isTree));
      [treeExpandButton, treeCollapseButton, treeRevealButton].forEach((button) => {
        if (!button) return;
        button.disabled = !isTree || !state.files.length;
      });
      if (treeRevealButton) treeRevealButton.disabled = !isTree || !state.activePath;
    }

    function getAllTreeFolderPaths() {
      const paths = new Set();
      state.files.forEach((file) => {
        getAncestorFolderPaths(file.path).forEach((path) => paths.add(path));
      });
      return [...paths];
    }

    function getAncestorFolderPaths(path) {
      const parts = String(path || '').split('/').filter(Boolean);
      parts.pop();
      return parts.map((_, index) => parts.slice(0, index + 1).join('/'));
    }

    function findFileButton(path) {
      return [...fileList.querySelectorAll('[data-path]')].find((button) => button.dataset.path === path);
    }

    function installResizers() {
      installColumnResizer({
        handle: sidebarSplitter,
        container: shell,
        variableName: '--sidebar-width',
        storageKey: storageKeys.sidebarWidth,
        min: 220,
        max: () => Math.min(560, Math.max(220, shell.clientWidth - 560)),
        getValueFromPointer: (event) => event.clientX - shell.getBoundingClientRect().left,
      });

      installColumnResizer({
        handle: workspaceSplitter,
        container: workspace,
        variableName: '--editor-width',
        storageKey: storageKeys.editorWidth,
        min: 280,
        max: () => Math.max(280, workspace.clientWidth - 320),
        getValueFromPointer: (event) => event.clientX - workspace.getBoundingClientRect().left,
      });
    }

    function installColumnResizer({ handle, container, variableName, storageKey, min, max, getValueFromPointer }) {
      let dragging = false;

      const setValue = (rawValue) => {
        const maxValue = typeof max === 'function' ? max() : max;
        const value = clamp(rawValue, min, maxValue);
        document.documentElement.style.setProperty(variableName, `${value}px`);
        localStorage.setItem(storageKey, String(value));
      };

      handle.addEventListener('pointerdown', (event) => {
        dragging = true;
        handle.classList.add('is-dragging');
        handle.setPointerCapture(event.pointerId);
        document.body.style.userSelect = 'none';
        setValue(getValueFromPointer(event));
      });

      handle.addEventListener('pointermove', (event) => {
        if (!dragging) return;
        setValue(getValueFromPointer(event));
      });

      const stopDragging = () => {
        dragging = false;
        handle.classList.remove('is-dragging');
        document.body.style.userSelect = '';
      };

      handle.addEventListener('pointerup', stopDragging);
      handle.addEventListener('pointercancel', stopDragging);

      handle.addEventListener('keydown', (event) => {
        if (!['ArrowLeft', 'ArrowRight'].includes(event.key)) return;
        event.preventDefault();
        const current = parseFloat(getComputedStyle(document.documentElement).getPropertyValue(variableName)) || container.clientWidth / 2;
        setValue(current + (event.key === 'ArrowRight' ? 24 : -24));
      });
    }

    function restoreThemePreference() {
      const stored = localStorage.getItem(storageKeys.theme);
      const preferred = window.matchMedia?.('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
      applyTheme(stored === 'light' || stored === 'dark' ? stored : preferred);
    }

    function toggleTheme() {
      const current = document.documentElement.dataset.theme === 'light' ? 'light' : 'dark';
      const next = current === 'light' ? 'dark' : 'light';
      applyTheme(next);
      localStorage.setItem(storageKeys.theme, next);
    }

    function applyTheme(theme) {
      document.documentElement.dataset.theme = theme;
      const isLight = theme === 'light';
      const label = isLight ? 'Switch to dark mode' : 'Switch to light mode';
      document.querySelector('meta[name="theme-color"]')?.setAttribute('content', isLight ? '#f3f4f6' : '#111318');
      const hiddenLabel = themeToggleButton.querySelector('.visually-hidden');
      if (hiddenLabel) hiddenLabel.textContent = label;
      themeToggleButton.setAttribute('aria-pressed', String(isLight));
      themeToggleButton.setAttribute('aria-label', label);
      themeToggleButton.title = label;
    }

    function restoreLayoutPreferences() {
      const sidebarWidth = readStoredNumber(storageKeys.sidebarWidth, NaN);
      const editorWidth = readStoredNumber(storageKeys.editorWidth, NaN);

      if (Number.isFinite(sidebarWidth)) {
        document.documentElement.style.setProperty('--sidebar-width', `${clamp(sidebarWidth, 220, 560)}px`);
      }

      if (Number.isFinite(editorWidth)) {
        document.documentElement.style.setProperty('--editor-width', `${clamp(editorWidth, 280, 1600)}px`);
      }

      state.diagramZoom = clamp(state.diagramZoom, .25, 3);
      zoomValue.textContent = `${Math.round(state.diagramZoom * 100)}%`;
    }

    function restoreEditorLayout() {
      applyEditorLayout(state.editorLayout || 'split');
    }

    function setEditorLayout(mode) {
      const next = ['editor', 'split', 'preview'].includes(mode) ? mode : 'split';
      state.editorLayout = next;
      localStorage.setItem(storageKeys.editorLayout, next);
      applyEditorLayout(next);
    }

    function applyEditorLayout(mode) {
      app.classList.remove('layout-editor', 'layout-split', 'layout-preview');
      app.classList.add(`layout-${mode}`);
      layoutModeControl?.querySelectorAll('[data-layout-mode]').forEach((button) => {
        const active = button.dataset.layoutMode === mode;
        button.classList.toggle('active', active);
        button.setAttribute('aria-pressed', String(active));
      });
    }

    function toggleFocusMode(force) {
      const next = typeof force === 'boolean' ? force : !state.focusMode;
      state.focusMode = next;
      localStorage.setItem(storageKeys.focusMode, String(next));
      applyFocusMode(next);
    }

    function applyFocusMode(on) {
      app.classList.toggle('focus-mode', on);
      if (focusModeButton) {
        focusModeButton.setAttribute('aria-pressed', String(on));
        focusModeButton.setAttribute('aria-label', on ? 'Exit Focus Mode' : 'Enter Focus Mode');
        focusModeButton.title = on ? 'Exit Focus Mode (Esc / Ctrl+F11)' : 'Focus Mode (Ctrl+F11)';
        if (on) {
          focusModeButton.setAttribute('aria-hidden', 'true');
          focusModeButton.tabIndex = -1;
        } else {
          focusModeButton.removeAttribute('aria-hidden');
          focusModeButton.removeAttribute('tabindex');
        }
      }
      if (focusModeExitButton) {
        focusModeExitButton.hidden = !on;
      }
    }

    function restoreFocusMode() {
      applyFocusMode(state.focusMode);
    }

    function toggleSidebarCollapsed(force) {
      const next = typeof force === 'boolean' ? force : !state.sidebarCollapsed;
      state.sidebarCollapsed = next;
      localStorage.setItem(storageKeys.sidebarCollapsed, String(next));
      applySidebarCollapsed(next);
    }

    function applySidebarCollapsed(collapsed) {
      shell.classList.toggle('sidebar-collapsed', collapsed);
      if (sidebarCollapseBtn) {
        sidebarCollapseBtn.setAttribute('aria-pressed', String(collapsed));
        sidebarCollapseBtn.title = collapsed ? 'Expand sidebar' : 'Collapse sidebar';
        sidebarCollapseBtn.setAttribute('aria-label', collapsed ? 'Expand sidebar' : 'Collapse sidebar');
      }
      if (sidebarExpandBtn) {
        sidebarExpandBtn.setAttribute('aria-expanded', String(!collapsed));
      }
    }

    function restoreSidebarCollapsed() {
      applySidebarCollapsed(state.sidebarCollapsed);
    }

    function setStatus(message, tone = '') {
      status.className = `status${tone ? ` ${tone}` : ''}`;
      status.textContent = message;
    }

    return {
      renderFileList,
      updateActiveFileLabel,
      updateSaveButton,
      hasUnsavedChanges,
      confirmDiscardUnsaved,
      closeOpenMenus,
      openCreateMenu,
      setFileBrowserView,
      toggleTreeFolder,
      expandTreeFolders,
      collapseTreeFolders,
      revealActiveFileInTree,
      installResizers,
      restoreThemePreference,
      toggleTheme,
      restoreEditorLayout,
      setEditorLayout,
      restoreLayoutPreferences,
      setStatus,
      toggleFocusMode,
      restoreFocusMode,
      toggleSidebarCollapsed,
      restoreSidebarCollapsed,
    };
}
