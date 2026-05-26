import { storageKeys } from '../state/config.js';
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
    fileCount,
    folderBadge,
    artifactBundleSummary,
    activeFileLabel,
    saveButton,
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

    function renderFileList() {
      const term = fileSearch.value.trim().toLowerCase();
      const visibleFiles = state.files.filter((file) => file.path.toLowerCase().includes(term));

      fileCount.textContent = String(state.files.length);
      if (railFileCount) railFileCount.textContent = state.files.length > 0 ? String(state.files.length) : '';
      folderBadge.textContent = state.folderName || 'No folder';
      artifactBundleReader.render();
      fileList.innerHTML = '';

      if (!state.files.length) {
        fileList.appendChild(createFileListEmptyState());
        return;
      }

      if (!visibleFiles.length) {
        fileList.appendChild(createEmptyState('No files match this filter.'));
        return;
      }

      visibleFiles.forEach((file) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = `file-item${file.path === state.activePath ? ' active' : ''}`;
        button.dataset.path = file.path;
        button.title = file.path;

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
        path.textContent = file.path;

        textWrap.append(name, path);
        button.append(icon, textWrap);

        if (state.dirtyPaths.has(file.path)) {
          const dirty = document.createElement('span');
          dirty.className = 'dirty-dot';
          dirty.title = 'Edited in memory';
          button.appendChild(dirty);
        } else if (file.readOnly) {
          const readOnly = document.createElement('span');
          readOnly.className = 'read-only-dot';
          readOnly.title = 'Read-only document';
          readOnly.textContent = 'RO';
          button.appendChild(readOnly);
        } else {
          const spacer = document.createElement('span');
          spacer.setAttribute('aria-hidden', 'true');
          button.appendChild(spacer);
        }

        fileList.appendChild(button);
      });
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
          <button type="button" data-sidebar-action="create">Create document</button>
        </div>`;
      return element;
    }

    function updateActiveFileLabel() {
      const record = state.files.find((item) => item.path === state.activePath);
      const dirty = state.activePath && state.dirtyPaths.has(state.activePath) ? ' · edited in memory' : '';
      const readOnly = record?.readOnly ? ' · read-only' : '';
      activeFileLabel.textContent = state.activePath ? `${state.activePath}${dirty}${readOnly}` : 'No file selected';
    }

    function updateSaveButton() {
      const record = state.files.find((item) => item.path === state.activePath);
      const isDirty = state.activePath && state.dirtyPaths.has(state.activePath);
      const canSaveConvertedCopy = Boolean(record?.converted && !record?.handle);
      saveButton.disabled = !record || (!isDirty && !canSaveConvertedCopy) || Boolean(record?.readOnly);
      if (record?.readOnly) {
        saveButton.title = 'Read-only guide documents cannot be saved.';
        return;
      }
      if (canSaveConvertedCopy) {
        saveButton.title = 'Save or download the converted Markdown copy';
        return;
      }
      saveButton.title = record?.handle
        ? 'Save changes back to the opened file'
        : 'Save changes using your browser file picker';
    }

    function hasUnsavedChanges() {
      return state.dirtyPaths.size > 0 || state.files.some((file) => file.converted && !file.handle);
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
      document.querySelector('meta[name="theme-color"]')?.setAttribute('content', isLight ? '#f3f4f6' : '#111318');
      themeToggleButton.textContent = isLight ? 'Switch to dark mode' : 'Switch to light mode';
      themeToggleButton.setAttribute('aria-pressed', String(isLight));
      themeToggleButton.setAttribute('aria-label', isLight ? 'Switch to dark mode' : 'Switch to light mode');
      themeToggleButton.title = isLight ? 'Switch to dark mode' : 'Switch to light mode';
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
