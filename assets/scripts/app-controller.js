import { createContentRegistries } from './registries/content.js';
import { getDomElements } from './dom.js';
import { createInitialState, storageKeys } from './state/config.js';
import { createEditorService } from './editor/editor-service.js';
import { createDraftService } from './editor/draft-service.js';
import { createFindReplaceService } from './editor/find-replace-service.js';
import { getClipboardPayloadFromEvent, pasteModes, readClipboardPayload, resolvePasteReplacement } from './editor/paste-service.js';
import { createTableEditorService } from './editor/table-editor-service.js';
import { createWorkspaceSearchService } from './editor/workspace-search-service.js';
import { isImportableDocumentFile, isPdfFile } from './files/document-import-service.js';
import { createFileService } from './files/file-service.js';
import { createExportService } from './exports/export-service.js';
import { createRenderingService } from './rendering/render-service.js';
import { createContextMenuService } from './ui/context-menu-service.js';
import { createUiService } from './ui/ui-service.js';
import { createDocumentUxService } from './document/document-ux-service.js';
import { createScrollSyncService } from './document/scroll-sync-service.js';
import { createSelectionSyncService } from './document/selection-sync-service.js';
import { downloadBlob, registerServiceWorker } from './utils/browser.js';
import { compareRecords, getFolderNameFromFileList, isSupportedFile, normalisePath, uniqueByPath } from './utils/files.js';
import { escapeHtml, readStoredNumber, sanitiseFileName, slugFromText, todayIso } from './utils/format.js';
import { collectMarkdownRelativeTargets, collectWikilinkTargets, findWikilinkBacklinks, resolveWikilinkTarget } from './utils/wikilinks.js';
import { getLineInfoAtIndex } from './utils/search.js';

export function createAppController() {
    const {
      app,
      shell,
      workspace,
      editorShell,
      editorSyntaxLayer,
      editor,
      editorLineNumbers,
      mermaidAutocomplete,
      editorToolbar,
      preview,
      status,
      fileInput,
      folderInput,
      zipInput,
      documentInput,
      saveButton,
      sampleButton,
      downloadButton,
      exportWordButton,
      exportPdfButton,
      exportMarkdownBundleButton,
      devopsMarkdownExportToggle,
      exportDocsButton,
      exportSvgButton,
      exportPngButton,
      copyMermaidButton,
      copyHtmlButton,
      copyTextButton,
      themeToggleButton,
      docsPreviewButton,
      layoutModeControl,
      studioToggleButton,
      createMenu,
      sidebarSplitter,
      workspaceSplitter,
      fileList,
      recentList,
      fileSearch,
      fileCount,
      folderBadge,
      activeFileLabel,
      diagramCount,
      zoomOutButton,
      zoomInButton,
      fitZoomButton,
      resetZoomButton,
      outlineToggleButton,
      scrollSyncToggle,
      previewFindToggleButton,
      previewFindPanel,
      previewFindInput,
      previewFindCount,
      previewFindPrevButton,
      previewFindNextButton,
      previewFindClearButton,
      documentReviewToggleButton,
      documentReviewPanel,
      documentReviewSummary,
      documentReviewMetrics,
      documentReviewAlerts,
      inputMaximizeButton,
      previewMaximizeButton,
      exportTrust,
      outlinePanel,
      outlineLinks,
      zoomValue,
      templateDialog,
      templateDialogForm,
      templateDialogMode,
      templateDialogTitle,
      templateDialogDescription,
      templateDialogFields,
      templateDialogSubmit,
      focusModeButton,
      focusModeExitButton,
      sidebarCollapseBtn,
      sidebarExpandBtn,
      railFileCount,
      recoveryDialog,
      recoveryDiffSaved,
      recoveryDiffDraft,
      recoverySummary,
      recoveryKeepSavedButton,
      recoveryRestoreDraftButton,
      recoveryDeleteDraftButton,
      lossProtectionDialog,
      lossProtectionSummary,
      lossProtectionUndoButton,
      lossProtectionSaveAnywayButton,
      lossProtectionCancelButton,
      workspaceSearchDialog,
      workspaceSearchInput,
      workspaceSearchModeFiles,
      workspaceSearchModeContent,
      workspaceSearchRegexToggle,
      workspaceSearchCaseToggle,
      workspaceSearchResults,
      workspaceSearchCount,
      workspaceSearchCloseButton,
      findReplaceDialog,
      findReplaceTitle,
      findReplaceFindInput,
      findReplaceReplaceInput,
      findReplaceRegexToggle,
      findReplaceCaseToggle,
      findReplaceCount,
      findReplacePrevButton,
      findReplaceNextButton,
      findReplaceCurrentButton,
      findReplaceAllButton,
      findReplaceCloseButton,
      tableEditorDialog,
      tableEditorGrid,
      tableEditorSummary,
      tableEditorApplyButton,
      tableEditorCancelButton,
      tableEditorAddRowButton,
      tableEditorAddColumnButton,
      tableEditorRemoveRowButton,
      tableEditorRemoveColumnButton,
      tableEditorMoveRowUpButton,
      tableEditorMoveRowDownButton,
      tableEditorMoveColumnLeftButton,
      tableEditorMoveColumnRightButton,
    } = getDomElements();
    let templateDialogResolve = null;
    let pendingSpecialPasteMode = '';

    const state = createInitialState({ readStoredNumber });
    let openTableEditor = () => {};
    const {
      recordEditorHistoryInput,
      resetEditorHistory,
      undoEditorChange,
      redoEditorChange,
      installEditorEnhancements,
      updateEditorChrome,
      handleMermaidAutocompleteKeydown,
      updateMermaidAutocompleteFromInput,
      hideMermaidAutocomplete,
      handleEditorToolbarClick,
      executeMarkdownCommand,
      getEditorSelection,
      replaceEditorRange,
    } = createEditorService({
      editor,
      state,
      dom: {
        editorShell,
        editorSyntaxLayer,
        editorLineNumbers,
        mermaidAutocomplete,
      },
      callbacks: {
        commandHandlers: {
          table: () => openTableEditor(),
        },
        getAutocompleteFiles: () => state.files,
      },
    });
    const {
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
    } = createUiService({
      state,
      dom: {
        shell,
        app,
        workspace,
        fileList,
        fileSearch,
        fileCount,
        folderBadge,
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
      },
    });
    const draftTools = createDraftService({
      state,
      dom: {
        editor,
        recoveryDialog,
        recoveryDiffSaved,
        recoveryDiffDraft,
        recoverySummary,
        recoveryKeepSavedButton,
        recoveryRestoreDraftButton,
        recoveryDeleteDraftButton,
        lossProtectionDialog,
        lossProtectionSummary,
        lossProtectionUndoButton,
        lossProtectionSaveAnywayButton,
        lossProtectionCancelButton,
      },
      callbacks: {
        setStatus,
        renderFileList,
        updateActiveFileLabel,
        updateSaveButton,
        resetEditorHistory,
        renderPreview: () => renderPreview(),
      },
    });
    const findReplaceTools = createFindReplaceService({
      editor,
      dom: {
        findReplaceDialog,
        findReplaceTitle,
        findReplaceFindInput,
        findReplaceReplaceInput,
        findReplaceRegexToggle,
        findReplaceCaseToggle,
        findReplaceCount,
        findReplacePrevButton,
        findReplaceNextButton,
        findReplaceCurrentButton,
        findReplaceAllButton,
        findReplaceCloseButton,
      },
      callbacks: {
        closeOpenMenus,
        replaceEditorRange,
        setStatus,
      },
    });
    const tableEditorTools = createTableEditorService({
      editor,
      dom: {
        tableEditorDialog,
        tableEditorGrid,
        tableEditorSummary,
        tableEditorApplyButton,
        tableEditorCancelButton,
        tableEditorAddRowButton,
        tableEditorAddColumnButton,
        tableEditorRemoveRowButton,
        tableEditorRemoveColumnButton,
        tableEditorMoveRowUpButton,
        tableEditorMoveRowDownButton,
        tableEditorMoveColumnLeftButton,
        tableEditorMoveColumnRightButton,
      },
      callbacks: {
        replaceEditorRange,
        setStatus,
      },
    });
    openTableEditor = tableEditorTools.openTableEditor;
    const {
      installDocumentUxHandlers,
      toggleOutline,
      updateDocumentUx,
    } = createDocumentUxService({
      state,
      dom: {
        preview,
        outlineToggleButton,
        outlinePanel,
        outlineLinks,
        previewFindToggleButton,
        previewFindPanel,
        previewFindInput,
        previewFindCount,
        previewFindPrevButton,
        previewFindNextButton,
        previewFindClearButton,
        documentReviewToggleButton,
        documentReviewPanel,
        documentReviewSummary,
        documentReviewMetrics,
        documentReviewAlerts,
      },
      callbacks: {
        getBacklinks,
        getWorkspaceAudit,
        openBacklink,
      },
    });
    const {
      installScrollSyncHandlers,
      beforePreviewRender,
      afterPreviewRender,
      rememberScrollPosition,
      restoreScrollPosition,
      resetActiveScrollPosition,
      clearScrollPositions,
    } = createScrollSyncService({
      state,
      dom: {
        app,
        editor,
        preview,
        scrollSyncToggle,
      },
    });
    const {
      installSelectionSyncHandlers,
      clearSelectionSync,
    } = createSelectionSyncService({
      state,
      dom: {
        app,
        editor,
        preview,
        scrollSyncToggle,
      },
    });
    let exportTools;
    const {
      renderPreview,
      buildMarkdownHtml,
      buildMermaidOnlyHtml,
      renderMermaidDiagrams,
      prepareDiagramFramesIn,
      prepareTableBlocksIn,
      getSvgBaseSize,
      setDiagramZoom,
      applyDiagramZoom,
      fitDiagramsToWidth,
      updateDiagramControls,
      resolveMode,
      resolveModeFor,
    } = createRenderingService({
      state,
      dom: {
        editor,
        preview,
        diagramCount,
        zoomOutButton,
        zoomInButton,
        fitZoomButton,
        resetZoomButton,
        zoomValue,
      },
      callbacks: {
        setStatus,
        setExportTrust: (...args) => exportTools?.setExportTrust(...args),
        updatePreviewOutline: updateDocumentUx,
        beforePreviewRender: () => {
          clearSelectionSync();
          beforePreviewRender();
        },
        afterPreviewRender,
        getExportFileStem,
        getDocTitleFromPath,
      },
    });
    exportTools = createExportService({
      state,
      dom: { preview, editor, exportTrust },
      callbacks: {
        renderPreview,
        renderMermaidDiagrams,
        buildMarkdownHtml,
        buildMermaidOnlyHtml,
        resolveModeFor,
        prepareDiagramFramesIn,
        prepareTableBlocksIn,
        getSvgBaseSize,
        getExportFileStem,
        getExportTitle,
        getExportName,
        getWordExportName,
        getDocTitleFromPath,
        closeOpenMenus,
        setStatus,
      },
    });
    const {
      copyToClipboard,
      copyCodeBlock,
      copyTableBlock,
      downloadTableCsv,
      getDiagramFrameFromAction,
      copyDiagramSource,
      exportDiagramFrameSvg,
      exportDiagramFramePng,
      exportPreviewHtml,
      exportPreviewWord,
      exportPreviewPdf,
      exportMarkdownBundle,
      copyRenderedHtml,
      copyRenderedText,
      copyCurrentMermaidSource,
      exportCurrentDiagramSvg,
      exportCurrentDiagramPng,
      exportDocsSite,
      setExportTrust,
    } = exportTools;
    const {
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
    } = createFileService({
      state,
      dom: { fileInput, folderInput, zipInput, documentInput, recentList, fileSearch, editor, preview },
      callbacks: {
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
        updatePreviewOutline: updateDocumentUx,
        rememberScrollPosition,
        restoreScrollPosition,
        resetActiveScrollPosition,
        clearScrollPositions,
        renderPreview,
        setStatus,
        getMarkdownExportName,
        afterLibraryLoaded: draftTools.updateWorkspaceDraftKey,
        afterActiveFileLoaded: draftTools.afterActiveFileLoaded,
        beforeSaveActiveFile: draftTools.beforeSaveActiveFile,
        afterSaveActiveFile: draftTools.afterSaveActiveFile,
      },
      helpers: {
        compareRecords,
        downloadBlob,
        escapeHtml,
        getFolderNameFromFileList,
        isSupportedFile,
        normalisePath,
        uniqueByPath,
      },
    });
    const workspaceSearchTools = createWorkspaceSearchService({
      state,
      dom: {
        workspaceSearchDialog,
        workspaceSearchInput,
        workspaceSearchModeFiles,
        workspaceSearchModeContent,
        workspaceSearchRegexToggle,
        workspaceSearchCaseToggle,
        workspaceSearchResults,
        workspaceSearchCount,
        workspaceSearchCloseButton,
      },
      callbacks: {
        closeOpenMenus,
        selectFile,
        readRecordText,
        focusEditorAtLine,
        setStatus,
      },
    });
    const {
      installContextMenuHandlers,
      closeContextMenu,
    } = createContextMenuService({
      state,
      dom: {
        editor,
        preview,
      },
      callbacks: {
        closeOpenMenus,
        copyCodeBlock,
        copyDiagramSource,
        copyRenderedHtml,
        copyRenderedText,
        copyTableBlock,
        downloadTableCsv,
        copyToClipboard,
        executeMarkdownCommand,
        exportDiagramFramePng,
        exportDiagramFrameSvg,
        focusEditorAtSource,
        getEditorSelection,
        hideMermaidAutocomplete,
        isActiveReadOnly,
        redoEditorChange,
        replaceEditorRange,
        setStatus,
        undoEditorChange,
      },
    });

    const {
      sample,
      examples,
      studioTemplates,
      studioSnippets,
      projectDocTemplates,
      releaseNoteTemplates,
      requirementsTemplates,
      projectDocSnippets,
      releaseNoteSnippets,
      requirementsSnippets,
      generatorGroups,
    } = createContentRegistries({ slugFromText, todayIso });

    restoreThemePreference();
    restoreLayoutPreferences();
    restoreEditorLayout();
    restoreFocusMode();
    restoreSidebarCollapsed();
    restoreDevOpsMarkdownExport();
    updateDocsPreviewButton();
    updateStudioMode();
    installResizers();
    draftTools.initDraftStore();
    draftTools.installDraftHandlers();
    installEditorEnhancements();
    findReplaceTools.installFindReplaceHandlers();
    tableEditorTools.installTableEditorHandlers();
    workspaceSearchTools.installWorkspaceSearchHandlers();
    installDocumentUxHandlers();
    installScrollSyncHandlers();
    installSelectionSyncHandlers();
    installContextMenuHandlers();
    installEventHandlers();
    initialiseWelcome();
    localStorage.removeItem(storageKeys.typewriterMode);
    initRecentHandles();
    registerServiceWorker();

    function installEventHandlers() {
      sampleButton.addEventListener('click', () => loadExample('sample'));
      downloadButton.addEventListener('click', exportPreviewHtml);
      exportWordButton.addEventListener('click', exportPreviewWord);
      exportPdfButton.addEventListener('click', exportPreviewPdf);
      exportMarkdownBundleButton.addEventListener('click', async () => {
        if (await exportMarkdownBundle()) {
          await draftTools.clearWorkspaceDrafts();
        }
      });
      devopsMarkdownExportToggle?.addEventListener('change', () => {
        state.devopsMarkdownExport = devopsMarkdownExportToggle.checked;
        localStorage.setItem(storageKeys.devopsMarkdownExport, String(state.devopsMarkdownExport));
      });
      exportDocsButton.addEventListener('click', exportDocsSite);
      exportSvgButton.addEventListener('click', exportCurrentDiagramSvg);
      exportPngButton.addEventListener('click', exportCurrentDiagramPng);
      copyMermaidButton.addEventListener('click', copyCurrentMermaidSource);
      copyHtmlButton.addEventListener('click', copyRenderedHtml);
      copyTextButton.addEventListener('click', copyRenderedText);
      themeToggleButton.addEventListener('click', toggleTheme);
      docsPreviewButton.addEventListener('click', toggleDocsPreview);
      studioToggleButton.addEventListener('click', toggleStudioMode);
      saveButton.addEventListener('click', saveActiveFile);
      focusModeButton?.addEventListener('click', () => toggleFocusMode());
      focusModeExitButton?.addEventListener('click', () => {
        toggleFocusMode(false);
        focusModeButton?.focus({ preventScroll: true });
      });
      sidebarCollapseBtn?.addEventListener('click', () => toggleSidebarCollapsed(true));
      sidebarExpandBtn?.addEventListener('click', () => toggleSidebarCollapsed(false));
      editorToolbar.addEventListener('click', handleEditorToolbarClick);
      templateDialogForm.addEventListener('submit', handleTemplateDialogSubmit);
      templateDialog.addEventListener('cancel', (event) => {
        event.preventDefault();
        closeTemplateDialog(null);
      });
      templateDialog.addEventListener('click', (event) => {
        if (event.target === templateDialog || event.target.closest('[data-template-dialog-cancel]')) {
          closeTemplateDialog(null);
        }
      });

      document.querySelectorAll('[data-example]').forEach((button) => {
        button.addEventListener('click', () => loadExample(button.dataset.example));
      });

      document.querySelectorAll('[data-create-template]').forEach((button) => {
        button.addEventListener('click', () => loadGeneratedTemplate(button.dataset.createTemplate));
      });

      document.querySelectorAll('[data-create-snippet]').forEach((button) => {
        button.addEventListener('click', () => insertGeneratorSnippet(button.dataset.createSnippet));
      });

      document.querySelectorAll('[data-studio-template]').forEach((button) => {
        button.addEventListener('click', () => loadStudioTemplate(button.dataset.studioTemplate));
      });

      document.querySelectorAll('[data-studio-snippet]').forEach((button) => {
        button.addEventListener('click', () => insertStudioSnippet(button.dataset.studioSnippet));
      });

      document.querySelectorAll('[data-menu-action]').forEach((button) => {
        button.addEventListener('click', async () => {
          if (button.dataset.menuAction === 'newMarkdown') await newMarkdownDocument();
          if (button.dataset.menuAction === 'openFile') await openFile();
          if (button.dataset.menuAction === 'openFolder') await openFolder();
          if (button.dataset.menuAction === 'importZip') importZip();
          if (button.dataset.menuAction === 'importDocument') importDocument();
          if (button.dataset.menuAction === 'save') await saveActiveFile();
          if (button.dataset.menuAction === 'openToolGuide') await openToolGuide();
          closeOpenMenus();
        });
      });

      document.querySelectorAll('[data-edit-action]').forEach((button) => {
        button.addEventListener('click', async () => {
          if (button.dataset.editAction === 'quickSwitch') workspaceSearchTools.openWorkspaceSearch('files');
          if (button.dataset.editAction === 'workspaceSearch') workspaceSearchTools.openWorkspaceSearch('content');
          if (button.dataset.editAction === 'findEditor') findReplaceTools.openFindReplace({ replace: false });
          if (button.dataset.editAction === 'replaceEditor') findReplaceTools.openFindReplace({ replace: true });
          if (button.dataset.editAction === 'pasteTable') await handlePasteSpecialAction(pasteModes.table);
          if (button.dataset.editAction === 'pasteText') await handlePasteSpecialAction(pasteModes.text);
          if (button.dataset.editAction === 'pasteCode') await handlePasteSpecialAction(pasteModes.code);
          if (button.dataset.editAction === 'pasteQuote') await handlePasteSpecialAction(pasteModes.quote);
          if (button.dataset.editAction === 'pasteHtmlMarkdown') await handlePasteSpecialAction(pasteModes.htmlMarkdown);
          if (button.dataset.editAction === 'pasteList') await handlePasteSpecialAction(pasteModes.list);
          if (button.dataset.editAction === 'pasteChecklist') await handlePasteSpecialAction(pasteModes.checklist);
          if (button.dataset.editAction === 'pasteNumberedList') await handlePasteSpecialAction(pasteModes.numberedList);
          if (button.dataset.editAction === 'pasteMermaid') await handlePasteSpecialAction(pasteModes.mermaid);
        });
      });

      document.querySelectorAll('[data-view-action]').forEach((button) => {
        button.addEventListener('click', async () => {
          if (button.dataset.viewAction === 'renderPreview') await renderPreview();
          if (button.dataset.viewAction === 'maximizePreview') togglePreviewMaximized();
          if (button.dataset.viewAction === 'toggleOutline') toggleOutline();
          if (button.dataset.viewAction === 'openDocsMap') await openDocsMap();
          closeOpenMenus();
        });
      });

      layoutModeControl.querySelectorAll('[data-layout-mode]').forEach((button) => {
        button.addEventListener('click', () => {
          togglePreviewMaximized(false);
          toggleInputMaximized(false);
          setEditorLayout(button.dataset.layoutMode);
          updateEditorChrome();
          closeOpenMenus();
        });
      });

      document.querySelectorAll('details.menu').forEach((menu) => {
        menu.querySelector('summary')?.addEventListener('click', () => {
          document.querySelectorAll('details.menu[open]').forEach((other) => {
            if (other !== menu) other.removeAttribute('open');
          });
        });

        menu.addEventListener('toggle', () => {
          if (!menu.open) return;
          document.querySelectorAll('details.menu[open]').forEach((other) => {
            if (other !== menu) other.removeAttribute('open');
          });
        });
      });

      document.addEventListener('click', (event) => {
        if (event.target.closest('details.menu')) return;
        closeOpenMenus();
      });

      editor.addEventListener('paste', handleEditorPaste);

      editor.addEventListener('input', (event) => {
        if (isActiveReadOnly()) {
          const content = state.fileCache.get(state.activePath) ?? '';
          if (editor.value !== content) {
            editor.value = content;
          }
          resetEditorHistory();
          updateEditorChrome?.();
          setStatus('This guide is read-only. Open or create a Markdown file to edit.', 'warning');
          return;
        }

        if (!state.editorHistory.suppress) {
          recordEditorHistoryInput(event);
        }

        updateMermaidAutocompleteFromInput();

        if (state.activePath) {
          state.fileCache.set(state.activePath, editor.value);
          state.dirtyPaths.add(state.activePath);
          draftTools.scheduleDraftSave();
          renderFileList();
          updateActiveFileLabel();
          updateSaveButton();
        }

        clearTimeout(state.debounceId);
        state.debounceId = window.setTimeout(renderPreview, 250);
      });

      editor.addEventListener('keydown', (event) => {
        if (handleMermaidAutocompleteKeydown(event)) return;

        const isShortcut = event.ctrlKey || event.metaKey;
        if (!isShortcut) return;

        const key = event.key.toLowerCase();
        if (key === 'f' && !event.shiftKey) {
          event.preventDefault();
          findReplaceTools.openFindReplace({ replace: false });
          return;
        }

        if (key === 'h') {
          event.preventDefault();
          findReplaceTools.openFindReplace({ replace: true });
          return;
        }

        if (isActiveReadOnly() && ['z', 'y', 'b', 'i', 'k'].includes(key)) {
          event.preventDefault();
          setStatus('This guide is read-only. Open or create a Markdown file to edit.', 'warning');
          return;
        }

        if (key === 'z') {
          event.preventDefault();
          if (event.shiftKey) {
            redoEditorChange();
          } else {
            undoEditorChange();
          }
          return;
        }

        if (key === 'y') {
          event.preventDefault();
          redoEditorChange();
          return;
        }

        if (key === 's') {
          event.preventDefault();
          saveActiveFile();
          return;
        }

        if (key === 'enter') {
          event.preventDefault();
          renderPreview();
          return;
        }

        if (!['b', 'i', 'k'].includes(key)) return;

        event.preventDefault();
        executeMarkdownCommand(key === 'b' ? 'bold' : key === 'i' ? 'italic' : 'link');
      });

      fileInput.addEventListener('change', async () => {
        const file = fileInput.files?.[0];
        if (!file) return;
        await setLibraryFromRecords([{ name: file.name, path: file.name, file }], 'Single file');
        fileInput.value = '';
      });

      folderInput.addEventListener('change', async () => {
        const files = [...(folderInput.files ?? [])];
        const folderName = getFolderNameFromFileList(files) || 'Selected folder';
        const records = files
          .filter((file) => isSupportedFile(file.name))
          .map((file) => ({
            name: file.name,
            path: normalisePath(file.webkitRelativePath || file.name),
            file,
          }));

        await setLibraryFromRecords(records, folderName);
        folderInput.value = '';
      });

      zipInput.addEventListener('change', async () => {
        const file = zipInput.files?.[0];
        if (!file) return;
        await importZipFile(file);
      });

      documentInput.addEventListener('change', async () => {
        const files = [...(documentInput.files ?? [])];
        await importDocumentFiles(files);
      });

      fileSearch.addEventListener('input', renderFileList);

      fileList.addEventListener('click', async (event) => {
        const actionButton = event.target.closest('[data-sidebar-action]');
        if (actionButton) {
          event.stopPropagation();
          const action = actionButton.dataset.sidebarAction;
          if (action === 'openFile') await openFile();
          if (action === 'openFolder') await openFolder();
          if (action === 'create') openCreateMenu();
          return;
        }

        const item = event.target.closest('[data-path]');
        if (!item) return;
        await selectFile(item.dataset.path);
      });

      zoomOutButton.addEventListener('click', () => setDiagramZoom(state.diagramZoom - .1));
      zoomInButton.addEventListener('click', () => setDiagramZoom(state.diagramZoom + .1));
      resetZoomButton.addEventListener('click', () => setDiagramZoom(1));
      fitZoomButton.addEventListener('click', fitDiagramsToWidth);
      inputMaximizeButton?.addEventListener('click', () => toggleInputMaximized());
      previewMaximizeButton.addEventListener('click', togglePreviewMaximized);
      preview.addEventListener('click', handlePreviewClick);
      recentList.addEventListener('click', handleRecentClick);

      window.addEventListener('keydown', (event) => {
        const isShortcut = event.ctrlKey || event.metaKey;
        const key = event.key.toLowerCase();
        if (isShortcut && key === 'p') {
          event.preventDefault();
          workspaceSearchTools.openWorkspaceSearch('files');
          return;
        }
        if (isShortcut && event.shiftKey && key === 'f') {
          event.preventDefault();
          workspaceSearchTools.openWorkspaceSearch('content');
          return;
        }
        if (isShortcut && key === 'f' && document.activeElement !== editor) {
          event.preventDefault();
          findReplaceTools.openFindReplace({ replace: false });
          return;
        }
        if (isShortcut && key === 'h' && document.activeElement !== editor) {
          event.preventDefault();
          findReplaceTools.openFindReplace({ replace: true });
          return;
        }
        if (event.key === 'F11' && event.ctrlKey) {
          event.preventDefault();
          toggleFocusMode();
          return;
        }
        if (event.key === 'Escape' && app.classList.contains('focus-mode')) {
          toggleFocusMode(false);
          return;
        }
        if (event.key === 'Escape' && app.classList.contains('preview-maximized')) {
          togglePreviewMaximized(false);
          return;
        }
        if (event.key === 'Escape' && app.classList.contains('input-maximized')) {
          toggleInputMaximized(false);
          return;
        }
        if (event.key === 'Escape') {
          hideMermaidAutocomplete();
          closeOpenMenus();
          closeContextMenu();
        }
      });

      window.addEventListener('beforeunload', (event) => {
        if (!hasUnsavedChanges()) return;
        event.preventDefault();
        event.returnValue = '';
      });

      window.addEventListener('dragover', (event) => {
        event.preventDefault();
        app.classList.add('drop-active');
      });

      window.addEventListener('dragleave', (event) => {
        if (event.relatedTarget) return;
        app.classList.remove('drop-active');
      });

      window.addEventListener('drop', async (event) => {
        event.preventDefault();
        app.classList.remove('drop-active');

        const files = [...(event.dataTransfer?.files ?? [])];
        const zipFiles = files.filter(isZipFile);
        if (zipFiles.length) {
          if (!confirmDiscardUnsaved('Import this ZIP and discard unsaved edits?')) return;
          await importZipFile(zipFiles[0]);
          return;
        }

        const documentFiles = files.filter(isImportableDocumentFile);
        const pdfFiles = files.filter(isPdfFile);
        if (documentFiles.length) {
          await importDocumentFiles([...documentFiles, ...pdfFiles]);
          return;
        }

        if (pdfFiles.length) {
          setStatus('PDF import is planned for a future text-only converter. Import DOCX or HTML for now.', 'warning');
          return;
        }

        const blockedSvgImages = files.filter(isBlockedSvgImageFile);
        const imageFiles = files.filter(isImageFile);
        const droppedOnEditor = Boolean(event.target.closest?.('.editor-shell')) || document.activeElement === editor;
        if (imageFiles.length && (droppedOnEditor || !files.some((file) => isSupportedFile(file.name)))) {
          await insertDroppedImages(imageFiles, blockedSvgImages.length);
          return;
        }

        if (blockedSvgImages.length && (droppedOnEditor || !files.some((file) => isSupportedFile(file.name)))) {
          setStatus('SVG images are not imported for security; use PNG, JPEG, GIF, or WebP.', 'warning');
          return;
        }

        const dropped = files
          .filter((file) => isSupportedFile(file.name))
          .map((file) => ({
            name: file.name,
            path: normalisePath(file.webkitRelativePath || file.name),
            file,
          }));

        if (!dropped.length) {
          setStatus('No supported Markdown or Mermaid files were dropped.', 'warning');
          return;
        }

        if (!confirmDiscardUnsaved('Open dropped files and discard unsaved edits?')) return;

        await setLibraryFromRecords(dropped, dropped.length === 1 ? 'Dropped file' : 'Dropped files');
      });
    }

    function handleEditorPaste(event) {
      if (isActiveReadOnly()) {
        event.preventDefault();
        setStatus('This guide is read-only. Open or create a Markdown file to edit.', 'warning');
        return;
      }

      const clipboardImages = getClipboardImageFiles(event.clipboardData);
      if (clipboardImages.files.length || clipboardImages.blockedSvgCount) {
        event.preventDefault();
        pendingSpecialPasteMode = '';
        void insertDroppedImages(clipboardImages.files, clipboardImages.blockedSvgCount, 'clipboard');
        return;
      }

      const mode = pendingSpecialPasteMode || pasteModes.auto;
      const payload = getClipboardPayloadFromEvent(event);
      const replacement = resolvePasteReplacement(payload, mode);

      if (!replacement) {
        if (pendingSpecialPasteMode) {
          event.preventDefault();
          setStatus(getPasteFailureMessage(pendingSpecialPasteMode), 'warning');
          pendingSpecialPasteMode = '';
        }
        return;
      }

      event.preventDefault();
      pendingSpecialPasteMode = '';
      insertPasteReplacement(replacement);
    }

    async function handlePasteSpecialAction(mode) {
      closeOpenMenus();

      if (isActiveReadOnly()) {
        setStatus('This guide is read-only. Open or create a Markdown file to edit.', 'warning');
        editor.focus();
        return;
      }

      try {
        const payload = await readClipboardPayload();
        if (!insertSpecialPastePayload(mode, payload)) {
          setStatus(getPasteFailureMessage(mode), 'warning');
        }
      } catch (error) {
        pendingSpecialPasteMode = mode;
        editor.focus();
        setStatus(`Clipboard access blocked. Press Ctrl/Cmd+V to ${getPasteInstruction(mode)}.`, 'warning');
      }
    }

    function insertSpecialPastePayload(mode, payload) {
      const replacement = resolvePasteReplacement(payload, mode);
      if (!replacement) return false;
      insertPasteReplacement(replacement);
      return true;
    }

    function insertPasteReplacement(replacement) {
      const selection = getEditorSelection();
      const prefix = replacement.block && selection.start > 0 && editor.value[selection.start - 1] !== '\n' ? '\n\n' : '';
      const suffix = replacement.block && selection.end < editor.value.length && editor.value[selection.end] !== '\n' ? '\n\n' : '';
      const markdown = `${prefix}${replacement.text}${suffix}`;
      const cursor = selection.start + prefix.length + replacement.text.length;

      replaceEditorRange(selection.start, selection.end, markdown, cursor, cursor);
      setStatus(replacement.status, replacement.statusType);
      window.setTimeout(() => {
        if (status.textContent === 'Rendered') {
          setStatus(replacement.status, replacement.statusType);
        }
      }, 350);
    }

    function getPasteFailureMessage(mode) {
      if (mode === pasteModes.table) return 'No table data found on clipboard.';
      if (mode === pasteModes.htmlMarkdown) return 'No HTML found on clipboard.';
      if ([pasteModes.list, pasteModes.checklist, pasteModes.numberedList].includes(mode)) return 'No list text found on clipboard.';
      return 'No readable text found on clipboard.';
    }

    function getPasteInstruction(mode) {
      if (mode === pasteModes.table) return 'paste as table';
      if (mode === pasteModes.code) return 'paste as code block';
      if (mode === pasteModes.quote) return 'paste as quote';
      if (mode === pasteModes.htmlMarkdown) return 'paste HTML as Markdown';
      if (mode === pasteModes.list) return 'paste as list';
      if (mode === pasteModes.checklist) return 'paste as checklist';
      if (mode === pasteModes.numberedList) return 'paste as numbered list';
      if (mode === pasteModes.mermaid) return 'paste as Mermaid block';
      return 'paste as text';
    }

    function getActiveRecord() {
      return state.files.find((item) => item.path === state.activePath);
    }

    async function readRecordText(record) {
      if (!record) return '';
      if (record.path === state.activePath) {
        state.fileCache.set(record.path, editor.value);
        return editor.value;
      }
      if (state.fileCache.has(record.path)) {
        return state.fileCache.get(record.path) ?? '';
      }
      const file = record.file ?? await record.handle?.getFile();
      if (!file) return '';
      record.file = file;
      const text = await file.text();
      state.fileCache.set(record.path, text);
      if (!state.dirtyPaths.has(record.path)) {
        state.savedContentCache.set(record.path, text);
      }
      return text;
    }

    function markWorkspaceCleanContent(path, content) {
      state.savedContentCache.set(path, content);
      draftTools.updateWorkspaceDraftKey();
    }

    function clearWorkspaceContentCaches() {
      state.fileCache.clear();
      state.savedContentCache.clear();
      state.dirtyPaths.clear();
    }

    function focusEditorAtLine(line, column = 1, length = 1) {
      const lines = editor.value.split('\n');
      const targetLine = Math.max(1, Math.min(Number(line) || 1, lines.length));
      const start = lines.slice(0, targetLine - 1).join('\n').length + (targetLine > 1 ? 1 : 0) + Math.max(0, (Number(column) || 1) - 1);
      const end = Math.min(editor.value.length, start + Math.max(1, Number(length) || 1));
      editor.focus();
      editor.setSelectionRange(start, end);
      const lineHeight = parseFloat(getComputedStyle(editor).lineHeight) || 22;
      editor.scrollTop = Math.max(0, (targetLine - 1) * lineHeight - editor.clientHeight * 0.32);
    }

    async function getBacklinks() {
      if (!state.activePath) return [];
      const backlinks = [];
      for (const record of state.files) {
        if (record.path === state.activePath) continue;
        const source = await readRecordText(record);
        const matches = findWikilinkBacklinks({
          source,
          fromPath: record.path,
          activePath: state.activePath,
          files: state.files,
        });
        matches.forEach((link) => {
          const line = getLineInfoAtIndex(source, link.index);
          backlinks.push({
            path: record.path,
            line: line.line,
            column: line.column,
            label: link.label,
            target: link.target,
          });
        });
      }
      return backlinks;
    }

    async function getWorkspaceAudit() {
      const records = getDocumentationRecords();
      const incoming = new Map(records.map((record) => [record.path, 0]));
      const referencedAssets = new Set();
      let brokenLinkCount = 0;

      for (const record of records) {
        const source = await readRecordText(record);
        collectImageReferences(source, record.path).forEach((path) => referencedAssets.add(path));
        collectDocumentationLinks(source).forEach((link) => {
          if (isAssetTarget(link.target)) return;
          const target = resolveWikilinkTarget(link.target, records, record.path);
          if (target?.path && incoming.has(target.path)) {
            incoming.set(target.path, incoming.get(target.path) + 1);
          } else {
            brokenLinkCount += 1;
          }
        });
      }

      const orphanAssetCount = [...state.managedAssets.keys()].filter((path) => !referencedAssets.has(path)).length;
      const unlinkedPageCount = records
        .filter((record) => !/(^|\/)(readme|index)\.(md|markdown)$/i.test(record.path))
        .filter((record) => (incoming.get(record.path) || 0) === 0)
        .length;

      return { brokenLinkCount, orphanAssetCount, unlinkedPageCount };
    }

    async function openDocsMap() {
      const records = getDocumentationRecords();
      if (!records.length) {
        setStatus('Open Markdown or Mermaid files before building a docs map.', 'warning');
        return;
      }

      if (state.activePath) {
        state.fileCache.set(state.activePath, editor.value);
      }

      const content = await buildDocsMapMarkdown(records);
      const name = 'docs-map.md';
      const existing = state.files.find((record) => record.path === name);
      const record = existing || {
        name,
        path: name,
        readOnly: true,
        generatedMap: true,
      };
      record.file = new File([content], name, { type: 'text/markdown' });
      record.readOnly = true;
      record.generatedMap = true;
      if (!existing) state.files.push(record);

      state.folderName = state.folderName || 'Documentation';
      state.activePath = name;
      state.fileName = name;
      state.fileCache.set(name, content);
      state.savedContentCache.set(name, content);
      state.dirtyPaths.delete(name);
      resetScrollForCurrentDocument();
      resetEditorHistory();
      editor.value = content;
      syncEditorReadOnly();
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
      await renderPreview();
      setStatus('Docs map opened as a read-only virtual document.', 'ok');
    }

    async function buildDocsMapMarkdown(records) {
      const nodeByPath = new Map(records.map((record, index) => [record.path, `doc${index + 1}`]));
      const links = [];
      const unresolved = [];

      for (const record of records) {
        const source = await readRecordText(record);
        collectDocumentationLinks(source).forEach((link) => {
          if (isAssetTarget(link.target)) return;
          const target = resolveWikilinkTarget(link.target, records, record.path);
          if (target?.path) {
            links.push({ from: record.path, to: target.path, label: link.kind });
          } else {
            unresolved.push({ from: record.path, target: link.target, label: link.label || link.target });
          }
        });
      }

      const diagramLines = [
        'flowchart LR',
        ...records.map((record) => `  ${nodeByPath.get(record.path)}["${escapeMermaidLabel(record.path)}"]`),
        ...links.map((link) => `  ${nodeByPath.get(link.from)} --> ${nodeByPath.get(link.to)}`),
      ];
      const linkRows = links.length
        ? links.map((link) => `| ${escapeTableCell(link.from)} | ${escapeTableCell(link.to)} | ${escapeTableCell(link.label)} |`).join('\n')
        : '| No links found |  |  |';
      const unresolvedRows = unresolved.length
        ? unresolved.map((link) => `| ${escapeTableCell(link.from)} | ${escapeTableCell(link.target)} | ${escapeTableCell(link.label)} |`).join('\n')
        : '| None |  |  |';

      return `# Documentation Map

Generated from ${records.length} loaded document${records.length === 1 ? '' : 's'}.

\`\`\`mermaid
${diagramLines.join('\n')}
\`\`\`

## Links

| From | To | Type |
|---|---|---|
${linkRows}

## Unresolved Links

| From | Target | Label |
|---|---|---|
${unresolvedRows}
`;
    }

    function getDocumentationRecords() {
      return state.files.filter((record) => isSupportedFile(record.name) && !record.generatedMap);
    }

    function collectDocumentationLinks(source) {
      return [
        ...collectWikilinkTargets(source),
        ...collectMarkdownRelativeTargets(source).filter((link) => String(source || '')[Math.max(0, link.index - 1)] !== '!'),
      ];
    }

    function collectImageReferences(source, fromPath) {
      const paths = [];
      String(source || '').replace(/!\[[^\]\n]*\]\(([^)\n]+)\)/g, (raw, href) => {
        const target = String(href || '').split(/[?#]/)[0].trim();
        if (target && !/^(https?:|data:|blob:)/i.test(target)) {
          paths.push(normaliseRelativePath(target, fromPath));
        }
        return raw;
      });
      return paths;
    }

    function normaliseRelativePath(target, fromPath) {
      const activeDir = fromPath.includes('/') ? fromPath.slice(0, fromPath.lastIndexOf('/') + 1) : '';
      const raw = target.startsWith('/') ? target.slice(1) : `${activeDir}${target}`;
      const parts = raw.replace(/\\/g, '/').split('/');
      const stack = [];
      parts.forEach((part) => {
        if (!part || part === '.') return;
        if (part === '..') stack.pop();
        else stack.push(part);
      });
      return stack.join('/');
    }

    function isAssetTarget(target) {
      return /\.(png|jpe?g|gif|webp|svg|pdf|zip)$/i.test(String(target || '').split(/[?#]/)[0]);
    }

    function escapeMermaidLabel(value) {
      return String(value || '').replaceAll('"', '\\"');
    }

    function escapeTableCell(value) {
      return String(value || '').replaceAll('|', '\\|').replace(/\r?\n/g, ' ');
    }

    async function openBacklink(path, line = 1) {
      await selectFile(path);
      focusEditorAtLine(line, 1, 1);
    }

    function isActiveReadOnly() {
      return Boolean(getActiveRecord()?.readOnly);
    }

    function syncEditorReadOnly() {
      const readOnly = isActiveReadOnly();
      editor.readOnly = readOnly;
      editor.setAttribute('aria-readonly', String(readOnly));
      editor.classList.toggle('is-read-only', readOnly);
      editorToolbar.querySelectorAll('[data-command]').forEach((button) => {
        button.disabled = readOnly;
      });
    }

    function resetScrollForCurrentDocument() {
      resetActiveScrollPosition(state.activePath);
      clearSelectionSync();
    }

    function clearManagedAssets() {
      state.managedAssets.forEach((asset) => {
        if (asset.objectUrl) URL.revokeObjectURL(asset.objectUrl);
      });
      state.managedAssets.clear();
      state.managedAssetCounter = 0;
    }

    function isImageFile(file) {
      return /^image\/(png|jpe?g|gif|webp)$/i.test(file.type)
        || /\.(png|jpe?g|gif|webp)$/i.test(file.name);
    }

    function isZipFile(file) {
      return /\.zip$/i.test(file.name) || /zip/i.test(file.type || '');
    }

    function isBlockedSvgImageFile(file) {
      return /^image\/svg\+xml$/i.test(file.type) || /\.svg$/i.test(file.name);
    }

    function getClipboardImageFiles(clipboardData) {
      if (!clipboardData) return { files: [], blockedSvgCount: 0 };

      const items = [...(clipboardData.items ?? [])];
      const itemFiles = items
        .filter((item) => item.kind === 'file')
        .map((item) => item.getAsFile?.())
        .filter(Boolean);
      const files = itemFiles.length ? itemFiles : [...(clipboardData.files ?? [])];
      const imageFiles = files
        .filter((file) => isImageFile(file))
        .map((file) => withClipboardImageName(file));
      const blockedSvgCount = files.filter((file) => isBlockedSvgImageFile(file)).length;

      return { files: imageFiles, blockedSvgCount };
    }

    function withClipboardImageName(file) {
      if (file.name && file.name !== 'image.png') return file;
      const extension = resolveImageExtension(file);
      const name = `pasted-image.${extension}`;
      return new File([file], name, { type: file.type || getImageMimeType(name), lastModified: file.lastModified || Date.now() });
    }

    async function insertDroppedImages(files, skippedSvgCount = 0, source = 'drop') {
      if (!files.length && skippedSvgCount) {
        setStatus('SVG images are not imported for security; use PNG, JPEG, GIF, or WebP.', 'warning');
        return;
      }
      if (!files.length) return;
      if (isActiveReadOnly()) {
        setStatus('This guide is read-only. Open or create a Markdown file before adding images.', 'warning');
        return;
      }

      ensureImageDropDocument();

      const snippets = [];
      for (const file of files) {
        const asset = await createManagedImageAsset(file);
        state.managedAssets.set(asset.path, asset);
        snippets.push(`![${asset.alt}](${asset.path})`);
      }

      const selection = getEditorSelection();
      const prefix = selection.start > 0 && editor.value[selection.start - 1] !== '\n' ? '\n\n' : '';
      const suffix = selection.end < editor.value.length && editor.value[selection.end] !== '\n' ? '\n\n' : '';
      const markdown = `${prefix}${snippets.join('\n\n')}${suffix}`;
      const start = selection.start + prefix.length;
      replaceEditorRange(selection.start, selection.end, markdown, start, start + snippets.join('\n\n').length);
      setExportTrust('Images added as session assets. Save writes Markdown links; exports include the image files.', 'warning');
      const verb = source === 'clipboard' ? 'pasted' : 'inserted';
      if (skippedSvgCount) {
        setStatus(`${files.length} image${files.length === 1 ? '' : 's'} ${verb}. SVG images are not imported for security; use PNG, JPEG, GIF, or WebP.`, 'warning');
        return;
      }
      setStatus(`${files.length} image${files.length === 1 ? '' : 's'} ${verb} as exportable assets.`, 'ok');
    }

    function ensureImageDropDocument() {
      if (state.activePath) return;
      clearFocusedModes();
      const name = 'image-notes.md';
      state.files = [{
        name,
        path: name,
        file: new File(['# Image Notes\n'], name, { type: 'text/markdown' }),
      }];
      state.folderName = 'Image Notes';
      state.activePath = name;
      state.fileName = name;
      clearWorkspaceContentCaches();
      state.fileCache.set(name, '# Image Notes\n');
      markWorkspaceCleanContent(name, '# Image Notes\n');
      editor.value = '# Image Notes\n';
      clearScrollPositions();
      resetScrollForCurrentDocument();
      resetEditorHistory();
      syncEditorReadOnly();
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
    }

    async function createManagedImageAsset(file) {
      const dataUrl = await readFileAsDataUrl(file);
      const base64 = dataUrl.split(',')[1] || '';
      const path = makeManagedAssetPath(file);
      const objectUrl = URL.createObjectURL(file);
      const alt = file.name.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ').trim() || 'Image';
      return {
        path,
        name: path.split('/').pop(),
        alt,
        mimeType: file.type || getImageMimeType(path),
        base64,
        dataUrl,
        objectUrl,
        size: file.size,
      };
    }

    function makeManagedAssetPath(file) {
      const extension = resolveImageExtension(file);
      const stem = slugFromText(file.name.replace(/\.[^.]+$/, '')) || 'image';
      let suffix = '';
      let path = '';
      do {
        state.managedAssetCounter += 1;
        suffix = state.managedAssetCounter === 1 ? '' : `-${state.managedAssetCounter}`;
        path = `assets/images/${stem}${suffix}.${extension}`;
      } while (state.managedAssets.has(path));
      return path;
    }

    function resolveImageExtension(file) {
      const match = file.name.match(/\.([a-z0-9]+)$/i);
      if (match) {
        const extension = match[1].toLowerCase();
        return extension === 'jpeg' ? 'jpg' : extension;
      }
      if (/webp/i.test(file.type)) return 'webp';
      if (/gif/i.test(file.type)) return 'gif';
      if (/jpe?g/i.test(file.type)) return 'jpg';
      return 'png';
    }

    function getImageMimeType(path) {
      if (/\.webp$/i.test(path)) return 'image/webp';
      if (/\.gif$/i.test(path)) return 'image/gif';
      if (/\.jpe?g$/i.test(path)) return 'image/jpeg';
      return 'image/png';
    }

    function readFileAsDataUrl(file) {
      return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result || ''));
        reader.onerror = () => reject(reader.error);
        reader.readAsDataURL(file);
      });
    }

    function initialiseWelcome() {
      clearFocusedModes();
      clearManagedAssets();
      clearScrollPositions();
      clearWorkspaceContentCaches();
      state.files = [];
      state.folderName = '';
      state.activePath = '';
      state.fileName = '';
      draftTools.updateWorkspaceDraftKey();
      editor.value = '';
      editor.placeholder = 'Open a Markdown or Mermaid file, choose an example, or start typing here.';
      resetScrollForCurrentDocument();
      resetEditorHistory();
      syncEditorReadOnly();
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
      updateDiagramControls(0);
      setExportTrust('', '');
      preview.innerHTML = `
        <section class="welcome-state" aria-label="Welcome">
          <p class="welcome-kicker">Browser-only Markdown and Mermaid</p>
          <h2>Start with a file, a folder, or a ready-made document.</h2>
          <p>Preview Markdown and Mermaid side by side, then export with render checks when the document is ready.</p>
          <div class="welcome-choice-grid">
            <div class="welcome-choice">
              <strong>Open local work</strong>
              <span>Use an existing Markdown or Mermaid file, or browse a folder of docs.</span>
              <div class="welcome-actions">
                <button class="primary" type="button" data-welcome-action="openFile">Open file</button>
                <button type="button" data-welcome-action="openFolder">Open folder</button>
              </div>
            </div>
            <div class="welcome-choice">
              <strong>Create from a template</strong>
              <span>Generate a README, release note, PRD, user story set, or Mermaid diagram.</span>
              <button class="primary" type="button" data-welcome-action="create">Create document</button>
            </div>
            <div class="welcome-choice">
              <strong>Explore quickly</strong>
              <span>Load the sample to see Markdown, tables, code blocks, and Mermaid rendering.</span>
              <button type="button" data-welcome-action="sample">Try sample</button>
            </div>
          </div>
          <div class="privacy-note">Files stay in this browser unless you save, copy, or export them.</div>
        </section>`;
      updateDocumentUx();
      setStatus('Ready');
    }

    async function loadExample(key) {
      if (!confirmDiscardUnsaved('Load this example and discard unsaved edits?')) return;

      if (key === 'sample') {
        initialiseSample();
        closeOpenMenus();
        return;
      }

      const example = examples[key];
      if (!example) return;
      setExampleContent(example);
      closeOpenMenus();
    }

    async function openToolGuide() {
      if (!confirmDiscardUnsaved('Open the feature guide and discard unsaved edits?')) return;

      try {
        setStatus('Opening feature guide...');
        const response = await fetch('./docs/tool-guide.md');
        if (!response.ok) {
          throw new Error(`Guide request failed with ${response.status}`);
        }

        const content = await response.text();
        const name = 'tool-feature-guide.md';
        clearFocusedModes();
        clearManagedAssets();
        clearScrollPositions();
        clearWorkspaceContentCaches();
        state.files = [{
          name,
          path: name,
          file: new File([content], name, { type: 'text/markdown' }),
          readOnly: true,
        }];
        state.folderName = 'Help';
        state.activePath = name;
        state.fileName = name;
        state.fileCache.set(name, content);
        markWorkspaceCleanContent(name, content);
        editor.value = content;
        resetScrollForCurrentDocument();
        resetEditorHistory();
        syncEditorReadOnly();
        renderFileList();
        updateActiveFileLabel();
        updateSaveButton();
        setExportTrust('Feature guide opened read-only. Export and copy actions still work.', 'ok');
        await renderPreview();
        setStatus('Feature guide opened read-only.', 'ok');
      } catch (error) {
        setStatus('Could not open the feature guide.', 'danger');
        console.error(error);
      }
    }

    function loadStudioTemplate(key) {
      const template = studioTemplates[key];
      if (!template) return;
      if (!confirmDiscardUnsaved(`Load the ${template.label} template and discard unsaved edits?`)) return;
      state.studioMode = true;
      localStorage.setItem(storageKeys.studioMode, 'true');
      clearGeneratorMode();
      updateStudioMode();
      setStudioDocument(template, true);
      closeOpenMenus();
    }

    function setStudioDocument(template, markClean) {
      clearGeneratorMode();
      clearManagedAssets();
      clearScrollPositions();
      clearWorkspaceContentCaches();
      state.files = [{
        name: template.name,
        path: template.name,
        file: new File([template.content], template.name, { type: 'text/plain' }),
      }];
      state.folderName = 'Mermaid Studio';
      state.activePath = template.name;
      state.fileName = template.name;
      state.fileCache.set(template.name, template.content);
      markWorkspaceCleanContent(template.name, template.content);
      editor.value = template.content;
      resetScrollForCurrentDocument();
      resetEditorHistory();
      syncEditorReadOnly();
      if (!markClean) {
        state.dirtyPaths.add(template.name);
      }
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
      renderPreview();
    }

    function insertStudioSnippet(key) {
      const snippet = studioSnippets[key];
      if (!snippet) return;
      if (isActiveReadOnly()) {
        setStatus('This guide is read-only. Open or create a Markdown file before inserting snippets.', 'warning');
        closeOpenMenus();
        return;
      }
      if (!state.studioMode) {
        state.studioMode = true;
        localStorage.setItem(storageKeys.studioMode, 'true');
        updateStudioMode();
      }
      const selection = getEditorSelection();
      const prefix = selection.start > 0 && editor.value[selection.start - 1] !== '\n' ? '\n' : '';
      const suffix = selection.end < editor.value.length && editor.value[selection.end] !== '\n' ? '\n' : '';
      const replacement = `${prefix}${snippet}${suffix}`;
      const start = selection.start + prefix.length;
      replaceEditorRange(selection.start, selection.end, replacement, start, start + snippet.length);
      closeOpenMenus();
    }

    async function loadGeneratedTemplate(key) {
      const resolved = resolveGeneratorItem(key, 'templates');
      if (!resolved) {
        setStatus('Template not found.', 'warning');
        return;
      }

      const { group, value: template } = resolved;
      if (!confirmDiscardUnsaved(`Create ${template.label} and discard unsaved edits?`)) return;

      closeOpenMenus();
      const metadata = await promptTemplateMetadata(template, group);
      if (!metadata) {
        return;
      }

      setGeneratedDocument(template, group, metadata);
    }

    function promptTemplateMetadata(template, group) {
      const fields = template.fields ?? [];
      if (!fields.length) return Promise.resolve({});

      if (templateDialogResolve) {
        closeTemplateDialog(null);
      }

      templateDialogMode.textContent = group.label;
      templateDialogTitle.textContent = `Create ${template.label}`;
      templateDialogDescription.textContent = 'Set the starting details. You can edit everything after the document is created.';
      templateDialogSubmit.textContent = `Create ${template.label}`;
      templateDialogFields.innerHTML = '';

      fields.forEach((field, index) => {
        const fallback = resolveTemplateFallback(field.fallback);
        const fieldId = `template-field-${field.key}-${index}`;
        const wrapper = document.createElement('div');
        wrapper.className = 'template-field';

        const label = document.createElement('label');
        label.htmlFor = fieldId;
        label.textContent = field.label;

        const input = document.createElement('input');
        input.id = fieldId;
        input.name = field.key;
        input.type = 'text';
        input.value = fallback;
        input.placeholder = fallback;
        input.dataset.templateFieldKey = field.key;
        input.dataset.fallback = fallback;
        input.autocomplete = 'off';

        const hint = document.createElement('small');
        hint.textContent = index === 0 ? 'Used for the document title and generated file name where relevant.' : 'Leave blank to keep the suggested value.';

        wrapper.append(label, input, hint);
        templateDialogFields.appendChild(wrapper);
      });

      return new Promise((resolve) => {
        templateDialogResolve = resolve;
        if (typeof templateDialog.showModal === 'function') {
          templateDialog.showModal();
        } else {
          templateDialog.setAttribute('open', '');
        }

        requestAnimationFrame(() => {
          const firstInput = templateDialogFields.querySelector('input');
          firstInput?.focus();
          firstInput?.select();
        });
      });
    }

    function resolveTemplateFallback(fallback) {
      return typeof fallback === 'function' ? fallback() : String(fallback ?? '');
    }

    function handleTemplateDialogSubmit(event) {
      event.preventDefault();
      const metadata = {};
      templateDialogFields.querySelectorAll('[data-template-field-key]').forEach((input) => {
        const fallback = input.dataset.fallback ?? '';
        metadata[input.dataset.templateFieldKey] = input.value.trim() || fallback;
      });
      closeTemplateDialog(metadata);
    }

    function closeTemplateDialog(value) {
      const resolve = templateDialogResolve;
      templateDialogResolve = null;

      if (templateDialog.open) {
        templateDialog.close();
      } else {
        templateDialog.removeAttribute('open');
      }

      resolve?.(value);
    }

    function setGeneratedDocument(template, group, metadata) {
      state.studioMode = false;
      localStorage.setItem(storageKeys.studioMode, 'false');
      updateStudioMode();
      setGeneratorMode(template.mode || group.label);
      clearManagedAssets();
      clearScrollPositions();

      const content = template.content(metadata);
      const name = template.name(metadata);
      clearWorkspaceContentCaches();
      state.files = [{
        name,
        path: name,
        file: new File([content], name, { type: 'text/markdown' }),
      }];
      state.folderName = group.folderName;
      state.activePath = name;
      state.fileName = name;
      state.fileCache.set(name, content);
      markWorkspaceCleanContent(name, content);
      editor.value = content;
      resetScrollForCurrentDocument();
      resetEditorHistory();
      syncEditorReadOnly();
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
      renderPreview();
      setStatus(`${template.label} template created.`, 'ok');
    }

    function insertGeneratorSnippet(key) {
      const resolved = resolveGeneratorItem(key, 'snippets');
      if (!resolved) {
        setStatus('Snippet not found.', 'warning');
        return;
      }

      const { group, value: snippet } = resolved;
      if (isActiveReadOnly()) {
        setStatus('This guide is read-only. Open or create a Markdown file before inserting snippets.', 'warning');
        closeOpenMenus();
        return;
      }

      state.studioMode = false;
      localStorage.setItem(storageKeys.studioMode, 'false');
      updateStudioMode();
      setGeneratorMode(group.label);

      if (!state.activePath) {
        setGeneratedDocument({
          label: `${group.label} Notes`,
          mode: group.label,
          name: () => `${slugFromText(group.label)}-notes.md`,
          content: () => `# ${group.label}\n`,
        }, group, {});
        editor.setSelectionRange(editor.value.length, editor.value.length);
      }

      const selection = getEditorSelection();
      const prefix = selection.start > 0 && editor.value[selection.start - 1] !== '\n' ? '\n\n' : '';
      const suffix = selection.end < editor.value.length && editor.value[selection.end] !== '\n' ? '\n\n' : '';
      const replacement = `${prefix}${snippet}${suffix}`;
      const start = selection.start + prefix.length;
      replaceEditorRange(selection.start, selection.end, replacement, start, start + snippet.length);
      closeOpenMenus();
    }

    function resolveGeneratorItem(key, collectionName) {
      const [groupKey, itemKey] = String(key || '').split('.');
      const group = generatorGroups[groupKey];
      const value = group?.[collectionName]?.[itemKey];
      return group && value ? { group, value } : null;
    }

    function setGeneratorMode(mode) {
      state.generatorMode = mode;
      if (mode) {
        localStorage.setItem(storageKeys.generatorMode, mode);
      } else {
        localStorage.removeItem(storageKeys.generatorMode);
      }
    }

    function clearGeneratorMode() {
      setGeneratorMode('');
    }

    function clearFocusedModes() {
      clearGeneratorMode();
      if (!state.studioMode) return;
      state.studioMode = false;
      localStorage.setItem(storageKeys.studioMode, 'false');
      updateStudioMode();
    }

    function setExampleContent(example) {
      clearFocusedModes();
      clearManagedAssets();
      clearScrollPositions();
      clearWorkspaceContentCaches();
      state.files = [{
        name: example.name,
        path: example.name,
        file: new File([example.content], example.name, { type: 'text/markdown' }),
      }];
      state.folderName = example.folderName;
      state.activePath = example.name;
      state.fileName = example.name;
      state.fileCache.set(example.name, example.content);
      markWorkspaceCleanContent(example.name, example.content);
      editor.value = example.content;
      resetScrollForCurrentDocument();
      resetEditorHistory();
      syncEditorReadOnly();
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
      renderPreview();
    }

    function initialiseSample() {
      clearFocusedModes();
      clearManagedAssets();
      clearScrollPositions();
      clearWorkspaceContentCaches();
      state.files = [{
        name: 'sample.md',
        path: 'sample.md',
        file: new File([sample], 'sample.md', { type: 'text/markdown' }),
      }];
      state.folderName = 'Sample';
      state.activePath = 'sample.md';
      state.fileName = 'sample.md';
      state.fileCache.set('sample.md', sample);
      markWorkspaceCleanContent('sample.md', sample);
      editor.value = sample;
      resetScrollForCurrentDocument();
      resetEditorHistory();
      syncEditorReadOnly();
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
      renderPreview();
    }

    function togglePreviewMaximized(force) {
      const shouldMaximize = typeof force === 'boolean'
        ? force
        : !app.classList.contains('preview-maximized');

      if (shouldMaximize) toggleInputMaximized(false);
      app.classList.toggle('preview-maximized', shouldMaximize);
      const label = shouldMaximize ? 'Restore split preview' : 'Maximise preview';
      const text = previewMaximizeButton.querySelector('.visually-hidden');
      if (text) text.textContent = shouldMaximize ? 'Restore' : 'Maximise';
      previewMaximizeButton.setAttribute('aria-pressed', String(shouldMaximize));
      previewMaximizeButton.setAttribute('aria-label', label);
      previewMaximizeButton.title = label;
      applyDiagramZoom();
    }

    function toggleInputMaximized(force) {
      if (!inputMaximizeButton) return;
      const shouldMaximize = typeof force === 'boolean'
        ? force
        : !app.classList.contains('input-maximized');

      if (shouldMaximize) togglePreviewMaximized(false);
      app.classList.toggle('input-maximized', shouldMaximize);
      inputMaximizeButton.textContent = shouldMaximize ? 'Restore' : 'Maximise';
      inputMaximizeButton.setAttribute('aria-pressed', String(shouldMaximize));
      inputMaximizeButton.setAttribute('aria-label', shouldMaximize ? 'Restore split input' : 'Maximise input');
      updateEditorChrome();
    }

    function toggleDocsPreview() {
      state.docsPreview = !state.docsPreview;
      localStorage.setItem(storageKeys.docsPreview, String(state.docsPreview));
      updateDocsPreviewButton();
      renderPreview();
    }

    function updateDocsPreviewButton() {
      docsPreviewButton.setAttribute('aria-pressed', String(state.docsPreview));
      docsPreviewButton.textContent = state.docsPreview ? 'Show single-page preview' : 'Preview docs site';
      docsPreviewButton.title = state.docsPreview ? 'Return to single-page preview' : 'Preview this folder as a docs site';
    }

    function restoreDevOpsMarkdownExport() {
      if (devopsMarkdownExportToggle) {
        devopsMarkdownExportToggle.checked = state.devopsMarkdownExport;
      }
    }

    function toggleStudioMode(force) {
      state.studioMode = typeof force === 'boolean' ? force : !state.studioMode;
      localStorage.setItem(storageKeys.studioMode, String(state.studioMode));
      if (state.studioMode) {
        clearGeneratorMode();
      }
      updateStudioMode();

      if (state.studioMode && !editor.value.trim()) {
        setStudioDocument(studioTemplates.flowchart, false);
      } else {
        renderPreview();
      }
    }

    function updateStudioMode() {
      app.classList.toggle('studio-active', state.studioMode);
      studioToggleButton.setAttribute('aria-pressed', String(state.studioMode));
      studioToggleButton.textContent = state.studioMode ? 'Leave Mermaid Studio' : 'Open Mermaid Studio';
      studioToggleButton.title = state.studioMode ? 'Leave Mermaid Diagram Studio mode' : 'Open Mermaid Diagram Studio mode';
    }

    async function handlePreviewClick(event) {
      const welcomeAction = event.target.closest('[data-welcome-action]');
      if (welcomeAction) {
        event.stopPropagation();
        const action = welcomeAction.dataset.welcomeAction;
        if (action === 'openFile') await openFile();
        if (action === 'openFolder') await openFolder();
        if (action === 'sample') await loadExample('sample');
        if (action === 'create') openCreateMenu();
        return;
      }

      const docLink = event.target.closest('[data-doc-path]');
      if (docLink) {
        event.preventDefault();
        await selectFile(docLink.dataset.docPath);
        return;
      }

      const wikilink = event.target.closest('[data-wikilink-target]');
      if (wikilink) {
        event.preventDefault();
        const target = resolveWikilinkTarget(wikilink.dataset.wikilinkTarget, state.files, state.activePath);
        if (!target) {
          wikilink.classList.add('wikilink-unresolved');
          setStatus(`No loaded file matches [[${wikilink.dataset.wikilinkTarget}]].`, 'warning');
          return;
        }
        await selectFile(target.path);
        setStatus(`Opened ${target.path}.`, 'ok');
        return;
      }

      const codeAction = event.target.closest('[data-code-action="copy"]');
      if (codeAction) {
        await copyCodeBlock(codeAction);
        return;
      }

      const tableAction = event.target.closest('[data-table-action]');
      if (tableAction) {
        if (tableAction.dataset.tableAction === 'copy') {
          await copyTableBlock(tableAction);
        }
        if (tableAction.dataset.tableAction === 'downloadCsv') {
          downloadTableCsv(tableAction);
        }
        return;
      }

      const diagramAction = event.target.closest('[data-diagram-action]');
      if (!diagramAction) return;

      const action = diagramAction.dataset.diagramAction;
      if (action === 'copySource') {
        await copyDiagramSource(getDiagramFrameFromAction(diagramAction), diagramAction);
        return;
      }

      if (action === 'exportSvg') {
        exportDiagramFrameSvg(getDiagramFrameFromAction(diagramAction));
        return;
      }

      if (action === 'exportPng') {
        await exportDiagramFramePng(getDiagramFrameFromAction(diagramAction));
        return;
      }

      const diagram = diagramAction.closest('.mermaid') || diagramAction.closest('.diagram-frame')?.querySelector('.mermaid');
      const error = diagram?.querySelector('.diagram-error pre')?.textContent ?? '';
      if (action === 'copyError') {
        await copyToClipboard(error, 'Mermaid error copied.', 'Could not copy Mermaid error.');
        return;
      }

      if (action === 'jumpSource') {
        focusEditorAtSource(diagram?.dataset.source ?? '');
      }
    }

    function focusEditorAtSource(source) {
      if (!source) {
        editor.focus();
        return;
      }

      const index = editor.value.indexOf(source);
      editor.focus();
      if (index === -1) return;
      editor.setSelectionRange(index, index + source.length);
      const line = editor.value.slice(0, index).split('\n').length - 1;
      const lineHeight = parseFloat(getComputedStyle(editor).lineHeight) || 22;
      editor.scrollTop = Math.max(0, line * lineHeight - editor.clientHeight * .25);
    }

    function getExportTitle() {
      return state.fileName ? state.fileName.replace(/\.[^.]+$/, '') : 'rendered-document';
    }

    function getDocTitleFromPath(path) {
      const name = String(path).split('/').pop() || 'Untitled';
      const stem = name.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ').trim();
      return stem ? stem.replace(/\b\w/g, (letter) => letter.toUpperCase()) : name;
    }

    function getExportName() {
      return `${getExportFileStem()}.html`;
    }

    function getWordExportName() {
      return `${getExportFileStem()}.docx`;
    }

    function getMarkdownExportName() {
      const extension = resolveMode(editor.value) === 'mermaid' ? 'mmd' : 'md';
      return `${getExportFileStem()}.${extension}`;
    }

    function getExportFileStem() {
      return sanitiseFileName(getExportTitle()) || 'rendered-document';
    }

}




