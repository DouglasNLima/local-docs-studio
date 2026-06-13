import { createContentRegistries } from './registries/content.js';
import { getDomElements } from './dom.js';
import { APP_BUILD, APP_VERSION, createInitialState, storageKeys } from './state/config.js';
import { createEditorService } from './editor/editor-service.js';
import { createDraftService } from './editor/draft-service.js';
import { createFindReplaceService } from './editor/find-replace-service.js';
import { createInsertHelperService } from './editor/insert-helper-service.js';
import { getClipboardPayloadFromEvent, pasteModes, readClipboardPayload, resolvePasteReplacement } from './editor/paste-service.js';
import { createProgressBarEditorService } from './editor/progress-bar-editor-service.js';
import { createTableEditorService } from './editor/table-editor-service.js';
import { createWorkspaceSearchService } from './editor/workspace-search-service.js';
import { isImportableDocumentFile } from './files/document-import-service.js';
import { createFileService } from './files/file-service.js';
import { createExportService } from './exports/export-service.js';
import { createExportProfileService } from './exports/export-profile-service.js';
import { createNativeBridgeClient } from './native/native-bridge-client.js';
import { createRenderingService } from './rendering/render-service.js';
import { createContextMenuService } from './ui/context-menu-service.js';
import { createDialogService } from './ui/dialog-service.js';
import { createSupportBundle, redactSupportBundleText } from './ui/support-bundle-service.js';
import { createUiService } from './ui/ui-service.js';
import { createWindowsSetupService } from './ui/windows-setup-service.js';
import { createDocumentUxService } from './document/document-ux-service.js';
import { analyseMarkdownGovernance } from './document/markdown-governance-service.js';
import { createScrollSyncService } from './document/scroll-sync-service.js';
import { createSelectionSyncService } from './document/selection-sync-service.js';
import { downloadBlob, registerServiceWorker } from './utils/browser.js';
import { compareRecords, getFolderNameFromFileList, isSupportedFile, normalisePath, uniqueByPath } from './utils/files.js';
import { escapeHtml, readStoredNumber, sanitiseFileName, slugFromText, todayIso } from './utils/format.js';
import { collectMarkdownRelativeTargets, collectWikilinkTargets, findWikilinkBacklinks, resolveWikilinkTarget } from './utils/wikilinks.js';
import { getLineInfoAtIndex } from './utils/search.js';
import { idbRequest, idbTransactionDone, openObjectStoreDb } from './utils/idb.js';

const SNAPSHOT_DB_NAME = 'local-docs-studio-snapshots';
const SNAPSHOT_DB_VERSION = 1;
const SNAPSHOT_STORE = 'snapshots';
const LOCAL_LIBRARY_KEY = 'md-mmd-renderer.localLibrary';
const WINDOWS_HOST = 'LensDocsStudio.Windows';
const WINDOWS_DIAGNOSTIC_CAPABILITIES = [
  'diagnostics.ping',
  'file.open',
  'file.save',
  'file.saveAs',
  'workspace.openFolder',
  'workspace.saveFile',
  'workspace.createFile',
  'workspace.watch',
  'workspace.refreshFile',
];

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
      saveAsButton,
      refreshFileButton,
      appVersionBadge,
      sampleButton,
      downloadButton,
      exportWordButton,
      exportPdfButton,
      exportMarkdownBundleButton,
      exportArtifactReviewPackButton,
      devopsMarkdownExportToggle,
      exportDocsButton,
      builtInExportProfiles,
      activeExportProfileLabel,
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
      fileViewListButton,
      fileViewTreeButton,
      treeExpandButton,
      treeCollapseButton,
      treeRevealButton,
      fileCount,
      folderBadge,
      artifactBundleSummary,
      activeFileLabel,
      diagramCount,
      mermaidThemeSelect,
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
      editorFindToggleButton,
      editorFindPanel,
      editorFindInput,
      editorFindCount,
      editorFindPrevButton,
      editorFindNextButton,
      editorFindClearButton,
      editorFindLayer,
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
      appDialog,
      appDialogForm,
      appDialogKicker,
      appDialogTitle,
      appDialogMessage,
      appDialogFields,
      appDialogCloseButton,
      appDialogCancelButton,
      appDialogConfirmButton,
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
      progressBarEditorDialog,
      progressBarEditorForm,
      progressBarEditorSummary,
      progressBarLabelInput,
      progressBarPercentInput,
      progressBarPercentRange,
      progressBarPercentOutput,
      progressBarColourOptions,
      progressBarApplyButton,
      progressBarCancelButton,
      insertHelperDialog,
      insertHelperForm,
      insertHelperKicker,
      insertHelperTitle,
      insertHelperSummary,
      insertHelperFields,
      insertHelperApplyButton,
      insertHelperCancelButton,
      windowsSetupDialog,
      windowsSetupTitle,
      windowsSetupSummary,
      windowsSetupStepList,
      windowsSetupBody,
      windowsSetupBackButton,
      windowsSetupNextButton,
      windowsSetupSkipButton,
      windowsSetupCloseButton,
      windowsShellDiagnosticsDialog,
      windowsShellDiagnosticsBody,
      windowsShellDiagnosticsCloseButton,
      windowsShellDiagnosticsRetryButton,
      windowsShellDiagnosticsCreateBundleButton,
      windowsShellDiagnosticsCopyBundleButton,
      windowsShellDiagnosticsExportBundleButton,
      windowsShellDiagnosticsDoneButton,
    } = getDomElements();
    let templateDialogResolve = null;
    let pendingSpecialPasteMode = '';
    let pasteStatusRestoreTimeout = 0;
    let snapshotDbPromise = null;

    const state = createInitialState({ readStoredNumber });
    const nativeBridgeClient = createNativeBridgeClient();
    let openTableEditor = () => {};
    let openProgressBarEditor = () => {};
    let openInsertHelper = () => {};
    let openArtifactReaderPath = async () => {};
    let getEffectiveDevopsMarkdownExport = () => Boolean(state.devopsMarkdownExport);
    let latestSupportBundle = null;
    const dialogTools = createDialogService({
      dom: {
        appDialog,
        appDialogForm,
        appDialogKicker,
        appDialogTitle,
        appDialogMessage,
        appDialogFields,
        appDialogCloseButton,
        appDialogCancelButton,
        appDialogConfirmButton,
      },
    });
    const { confirm: confirmDialog, prompt: promptDialog } = dialogTools;
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
          progressBar: () => openProgressBarEditor(),
          insertHelper: (type) => openInsertHelper(type),
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
      syncMobileMenuLayout,
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
      setFileBrowserView,
      toggleTreeFolder,
      expandTreeFolders,
      collapseTreeFolders,
      revealActiveFileInTree,
    } = createUiService({
      state,
      dom: {
        shell,
        app,
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
      },
      callbacks: {
        openArtifactPath: (path) => openArtifactReaderPath(path),
        confirm: confirmDialog,
      },
    });

    function syncOpenMobileMenuLayout() {
      syncMobileMenuLayout(document.querySelector('details.menu[open]'));
    }

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
        confirmAction: confirmDialog,
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
        editorFindToggleButton,
        editorFindPanel,
        editorFindInput,
        editorFindCount,
        editorFindPrevButton,
        editorFindNextButton,
        editorFindClearButton,
        editorFindLayer,
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
    const progressBarEditorTools = createProgressBarEditorService({
      editor,
      dom: {
        progressBarEditorDialog,
        progressBarEditorForm,
        progressBarEditorSummary,
        progressBarLabelInput,
        progressBarPercentInput,
        progressBarPercentRange,
        progressBarPercentOutput,
        progressBarColourOptions,
        progressBarApplyButton,
        progressBarCancelButton,
      },
      callbacks: {
        replaceEditorRange,
        setStatus,
      },
    });
    openProgressBarEditor = progressBarEditorTools.openProgressBarEditor;
    const insertHelperTools = createInsertHelperService({
      editor,
      dom: {
        insertHelperDialog,
        insertHelperForm,
        insertHelperKicker,
        insertHelperTitle,
        insertHelperSummary,
        insertHelperFields,
        insertHelperApplyButton,
        insertHelperCancelButton,
      },
      callbacks: {
        replaceEditorRange,
        setStatus,
      },
    });
    openInsertHelper = insertHelperTools.openInsertHelper;
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
        getGovernanceAudit,
        openBacklink,
        openGovernanceIssue,
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
        editorFindPanel,
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
        editorFindPanel,
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
      restoreMermaidThemePreference,
      setMermaidTheme,
      shouldRerenderForAppThemeChange,
      resolveMode,
      resolveModeFor,
    } = createRenderingService({
      state,
      dom: {
        editor,
        preview,
        diagramCount,
        mermaidThemeSelect,
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
        getEffectiveDevopsMarkdownExport: () => getEffectiveDevopsMarkdownExport(),
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
      copyDiagramFramePng,
      exportPreviewHtml,
      exportPreviewWord,
      exportPreviewPdf,
      exportMarkdownBundle,
      exportArtifactReviewPack,
      copyRenderedHtml,
      copyMarkdownWithImages,
      copyRenderedText,
      copyCurrentMermaidSource,
      exportCurrentDiagramSvg,
      exportCurrentDiagramPng,
      exportDocsSite,
      setExportTrust,
    } = exportTools;
    const exportProfileTools = createExportProfileService({
      state,
      dom: {
        builtInExportProfileList: builtInExportProfiles,
        activeExportProfileLabel,
        devopsMarkdownExportToggle,
        exportArtifactReviewPackButton,
      },
      callbacks: {
        getExportTitle,
        setStatus,
      },
    });
    getEffectiveDevopsMarkdownExport = exportProfileTools.getEffectiveDevopsMarkdownExport;
    const {
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
      getOpenFolderDiagnostics,
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
        afterLibraryLoaded: () => {
          draftTools.updateWorkspaceDraftKey();
          exportProfileTools.resetSessionProfile();
        },
        afterActiveFileLoaded: draftTools.afterActiveFileLoaded,
        beforeSaveActiveFile: draftTools.beforeSaveActiveFile,
        afterSaveActiveFile: draftTools.afterSaveActiveFile,
        promptForText: promptDialog,
        confirmAction: confirmDialog,
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
      nativeBridgeClient,
    });
    openArtifactReaderPath = selectFile;
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
        copyMarkdownWithImages,
        copyRenderedText,
        copyTableBlock,
        downloadTableCsv,
        copyToClipboard,
        copyDiagramFramePng,
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
    const windowsSetupTools = createWindowsSetupService({
      dom: {
        windowsSetupDialog,
        windowsSetupTitle,
        windowsSetupSummary,
        windowsSetupStepList,
        windowsSetupBody,
        windowsSetupBackButton,
        windowsSetupNextButton,
        windowsSetupSkipButton,
        windowsSetupCloseButton,
      },
      callbacks: {
        openWorkspace: openFolder,
        openSample: initialiseSample,
        openFeatureGuide: openToolGuide,
        startBlankDocument: startBlankSetupDocument,
        closeOpenMenus,
        setStatus,
      },
      nativeBridgeClient,
      storageKeys,
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
    restoreMermaidThemePreference();
    restoreLayoutPreferences();
    restoreEditorLayout();
    restoreFocusMode();
    restoreSidebarCollapsed();
    restoreDevOpsMarkdownExport();
    updateDocsPreviewButton();
    updateStudioMode();
    dialogTools.installDialogHandlers();
    installResizers();
    draftTools.initDraftStore();
    draftTools.installDraftHandlers();
    installEditorEnhancements();
    exportProfileTools.installBuiltInProfileHandlers();
    exportProfileTools.renderBuiltInProfiles();
    findReplaceTools.installFindReplaceHandlers();
    tableEditorTools.installTableEditorHandlers();
    progressBarEditorTools.installProgressBarEditorHandlers();
    insertHelperTools.installInsertHelperHandlers();
    workspaceSearchTools.installWorkspaceSearchHandlers();
    installDocumentUxHandlers();
    installScrollSyncHandlers();
    installSelectionSyncHandlers();
    installContextMenuHandlers();
    windowsSetupTools.installWindowsSetupHandlers();
    installEventHandlers();
    renderLocalLibrary();
    initialiseWelcome();
    localStorage.removeItem(storageKeys.typewriterMode);
    initRecentHandles();
    registerServiceWorker();
    document.documentElement.dataset.appReady = 'true';
    nativeBridgeClient.notifyAppReady?.();
    void windowsSetupTools.maybeOpenFirstRun();

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
      exportArtifactReviewPackButton?.addEventListener('click', async () => {
        if (await exportArtifactReviewPack()) {
          await draftTools.clearWorkspaceDrafts();
        }
      });
      devopsMarkdownExportToggle?.addEventListener('change', () => {
        state.devopsMarkdownExport = devopsMarkdownExportToggle.checked;
        localStorage.setItem(storageKeys.devopsMarkdownExport, String(state.devopsMarkdownExport));
        exportProfileTools.handlePersistedDevopsToggleChange();
      });
      exportDocsButton.addEventListener('click', exportDocsSite);
      exportSvgButton.addEventListener('click', exportCurrentDiagramSvg);
      exportPngButton.addEventListener('click', exportCurrentDiagramPng);
      copyMermaidButton.addEventListener('click', copyCurrentMermaidSource);
      copyHtmlButton.addEventListener('click', copyRenderedHtml);
      copyTextButton.addEventListener('click', copyRenderedText);
      themeToggleButton.addEventListener('click', handleThemeToggle);
      docsPreviewButton.addEventListener('click', toggleDocsPreview);
      studioToggleButton.addEventListener('click', toggleStudioMode);
      saveButton.addEventListener('click', saveActiveFile);
      saveAsButton?.addEventListener('click', saveActiveFileAs);
      refreshFileButton?.addEventListener('click', refreshActiveFile);
      fileViewListButton?.addEventListener('click', () => setFileBrowserView('list'));
      fileViewTreeButton?.addEventListener('click', () => setFileBrowserView('tree'));
      treeExpandButton?.addEventListener('click', expandTreeFolders);
      treeCollapseButton?.addEventListener('click', collapseTreeFolders);
      treeRevealButton?.addEventListener('click', revealActiveFileInTree);
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

      document.querySelectorAll('[data-local-library-action]').forEach((button) => {
        button.addEventListener('click', async () => {
          if (button.dataset.localLibraryAction === 'saveTemplate') await saveCurrentDocumentAsLocalTemplate();
          if (button.dataset.localLibraryAction === 'saveSnippet') await saveSelectionAsLocalSnippet();
          if (button.dataset.localLibraryAction === 'exportLibrary') exportLocalLibrary();
          if (button.dataset.localLibraryAction === 'importLibrary') await importLocalLibrary();
          closeOpenMenus();
        });
      });

      createMenu.addEventListener('click', async (event) => {
        const templateButton = event.target.closest('[data-local-template-id]');
        if (templateButton) {
          await loadLocalTemplate(templateButton.dataset.localTemplateId);
          closeOpenMenus();
        }
        const snippetButton = event.target.closest('[data-local-snippet-id]');
        if (snippetButton) {
          insertLocalSnippet(snippetButton.dataset.localSnippetId);
          closeOpenMenus();
        }
      });

      document.querySelectorAll('[data-studio-template]').forEach((button) => {
        button.addEventListener('click', async () => loadStudioTemplate(button.dataset.studioTemplate));
      });

      document.querySelectorAll('[data-studio-snippet]').forEach((button) => {
        button.addEventListener('click', () => insertStudioSnippet(button.dataset.studioSnippet));
      });

      document.querySelectorAll('[data-menu-action]').forEach((button) => {
        button.addEventListener('click', async () => {
          if (button.dataset.menuAction === 'newMarkdown') await newMarkdownDocument();
          if (button.dataset.menuAction === 'openFile') await openFile();
          if (button.dataset.menuAction === 'openFolder') await openFolder();
          if (button.dataset.menuAction === 'addFile') await addFilesToWorkspace();
          if (button.dataset.menuAction === 'importZip') await importZip();
          if (button.dataset.menuAction === 'importDocument') importDocument();
          if (button.dataset.menuAction === 'save') await saveActiveFile();
          if (button.dataset.menuAction === 'saveAs') await saveActiveFileAs();
          if (button.dataset.menuAction === 'refreshFile') await refreshActiveFile();
          if (button.dataset.menuAction === 'createSnapshot') await createActiveSnapshot();
          if (button.dataset.menuAction === 'manageSnapshots') await openSnapshotManager();
          if (button.dataset.menuAction === 'openToolGuide') await openToolGuide();
          if (button.dataset.menuAction === 'checkNativeBridge') await checkNativeBridge();
          if (button.dataset.menuAction === 'openWindowsShellDiagnostics') await openWindowsShellDiagnostics();
          if (button.dataset.menuAction === 'openWindowsSetup') await windowsSetupTools.openWindowsSetupWizard();
          closeOpenMenus();
        });
      });

      windowsShellDiagnosticsCloseButton?.addEventListener('click', closeWindowsShellDiagnostics);
      windowsShellDiagnosticsRetryButton?.addEventListener('click', openWindowsShellDiagnostics);
      windowsShellDiagnosticsCreateBundleButton?.addEventListener('click', createDiagnosticsSupportBundle);
      windowsShellDiagnosticsCopyBundleButton?.addEventListener('click', copyDiagnosticsSupportBundle);
      windowsShellDiagnosticsExportBundleButton?.addEventListener('click', exportDiagnosticsSupportBundle);
      windowsShellDiagnosticsDoneButton?.addEventListener('click', closeWindowsShellDiagnostics);
      windowsShellDiagnosticsDialog?.addEventListener('cancel', closeWindowsShellDiagnostics);
      windowsShellDiagnosticsDialog?.addEventListener('click', (event) => {
        if (event.target === windowsShellDiagnosticsDialog) closeWindowsShellDiagnostics();
      });

      document.querySelectorAll('[data-edit-action]').forEach((button) => {
        button.addEventListener('click', async () => {
          if (button.dataset.editAction === 'quickSwitch') workspaceSearchTools.openWorkspaceSearch('files');
          if (button.dataset.editAction === 'workspaceSearch') workspaceSearchTools.openWorkspaceSearch('content');
          if (button.dataset.editAction === 'findEditor') findReplaceTools.openEditorFind();
          if (button.dataset.editAction === 'replaceEditor') findReplaceTools.openFindReplace({ replace: true });
          if (button.dataset.editAction === 'copyMarkdownWithImages') await copyMarkdownWithImages();
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
          if (button.dataset.viewAction === 'renderPreview') {
            clearPasteStatusRestore();
            await renderPreview();
          }
          if (button.dataset.viewAction === 'maximizePreview') togglePreviewMaximized();
          if (button.dataset.viewAction === 'toggleOutline') toggleOutline();
          if (button.dataset.viewAction === 'openDocsMap') await openDocsMap();
          if (button.dataset.viewAction === 'manageAssets') await openAssetLibrary();
          closeOpenMenus();
        });
      });

      document.querySelectorAll('[data-export-profile-action]').forEach((button) => {
        button.addEventListener('click', async () => {
          if (button.dataset.exportProfileAction === 'save') await saveExportProfile();
          if (button.dataset.exportProfileAction === 'apply') applyExportProfile();
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
          if (!menu.open) {
            syncMobileMenuLayout();
            return;
          }
          syncMobileMenuLayout(menu);
          document.querySelectorAll('details.menu[open]').forEach((other) => {
            if (other !== menu) other.removeAttribute('open');
          });
        });
      });

      document.addEventListener('click', (event) => {
        if (event.target.closest('details.menu')) return;
        closeOpenMenus();
      });
      window.addEventListener('resize', syncOpenMobileMenuLayout);
      window.visualViewport?.addEventListener('resize', syncOpenMobileMenuLayout);

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
          findReplaceTools.openEditorFind();
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
          clearPasteStatusRestore();
          renderPreview();
          return;
        }

        if (!['b', 'i', 'k'].includes(key)) return;

        event.preventDefault();
        executeMarkdownCommand(key === 'b' ? 'bold' : key === 'i' ? 'italic' : 'link');
      });

      fileInput.addEventListener('change', async () => {
        const mode = fileInput.dataset.mode || 'open';
        const files = [...(fileInput.files ?? [])];
        if (!files.length) return;
        if (mode === 'add') {
          await addFilesFromInput(files);
        } else {
          const file = files[0];
          await setLibraryFromRecords([{ name: file.name, path: file.name, file }], 'Single file', { workspaceKind: 'file' });
        }
        fileInput.dataset.mode = 'open';
        fileInput.multiple = false;
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

        await setLibraryFromRecords(records, folderName, { workspaceKind: 'folder-fallback' });
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
        const treeFolder = event.target.closest('[data-tree-folder]');
        if (treeFolder) {
          event.stopPropagation();
          toggleTreeFolder(treeFolder.dataset.treeFolder);
          return;
        }

        const actionButton = event.target.closest('[data-sidebar-action]');
        if (actionButton) {
          event.stopPropagation();
          const action = actionButton.dataset.sidebarAction;
          if (action === 'openFile') await openFile();
          if (action === 'openFolder') await openFolder();
          if (action === 'diagnostics') await openWindowsShellDiagnostics();
          if (action === 'newFile') await newMarkdownDocument();
          if (action === 'addFile') await addFilesToWorkspace();
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
      mermaidThemeSelect?.addEventListener('change', () => {
        void setMermaidTheme(mermaidThemeSelect.value);
      });
      inputMaximizeButton?.addEventListener('click', () => toggleInputMaximized());
      previewMaximizeButton.addEventListener('click', togglePreviewMaximized);
      preview.addEventListener('click', handlePreviewClick);
      recentList.addEventListener('click', handleRecentClick);
      window.addEventListener('focus', () => {
        void checkForExternalUpdates();
      });
      document.addEventListener('visibilitychange', () => {
        if (!document.hidden) void checkForExternalUpdates();
      });

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
          findReplaceTools.openEditorFind();
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
          await importZipFile(zipFiles[0]);
          return;
        }

        const documentFiles = files.filter(isImportableDocumentFile);
        if (documentFiles.length) {
          await importDocumentFiles(documentFiles);
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

        if (!await confirmDiscardUnsaved('Open dropped files and discard unsaved edits?')) return;

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
      clearPasteStatusRestore();

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
        const message = `Clipboard access blocked. Press Ctrl/Cmd+V to ${getPasteInstruction(mode)}.`;
        setStatus(message, 'warning');
        schedulePasteStatusRestore(message, 'warning');
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
      schedulePasteStatusRestore(replacement.status, replacement.statusType);
    }

    function schedulePasteStatusRestore(text, statusType, retries = 6) {
      clearPasteStatusRestore();
      pasteStatusRestoreTimeout = window.setTimeout(() => {
        pasteStatusRestoreTimeout = 0;
        if (status.textContent === 'Rendered') {
          setStatus(text, statusType);
          return;
        }
        if (status.textContent === 'Rendering...' && retries > 0) {
          schedulePasteStatusRestore(text, statusType, retries - 1);
        }
      }, 350);
    }

    function clearPasteStatusRestore() {
      if (!pasteStatusRestoreTimeout) return;
      window.clearTimeout(pasteStatusRestoreTimeout);
      pasteStatusRestoreTimeout = 0;
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

    async function createActiveSnapshot() {
      const record = getActiveRecord();
      if (!record || !state.activePath) {
        setStatus('Open a Markdown document before creating a snapshot.', 'warning');
        return;
      }
      if (record.readOnly) {
        setStatus('Read-only documents cannot create snapshots.', 'warning');
        return;
      }

      const snapshot = {
        id: `${getSnapshotWorkspaceKey()}::${state.activePath}::${Date.now()}`,
        workspaceKey: getSnapshotWorkspaceKey(),
        path: state.activePath,
        name: record.name,
        text: editor.value,
        createdAt: Date.now(),
      };
      const db = await getSnapshotDb();
      if (!db) return;
      const transaction = db.transaction(SNAPSHOT_STORE, 'readwrite');
      transaction.objectStore(SNAPSHOT_STORE).put(snapshot);
      await idbTransactionDone(transaction);
      setStatus(`Snapshot created for ${record.name}.`, 'ok');
    }

    async function openSnapshotManager() {
      const record = getActiveRecord();
      if (!record || !state.activePath) {
        setStatus('Open a document before managing snapshots.', 'warning');
        return;
      }

      const dialog = document.createElement('dialog');
      dialog.className = 'utility-dialog snapshot-dialog';
      dialog.setAttribute('aria-labelledby', 'snapshotDialogTitle');
      document.body.appendChild(dialog);

      const close = () => {
        dialog.close();
        dialog.remove();
      };
      dialog.addEventListener('cancel', (event) => {
        event.preventDefault();
        close();
      });
      dialog.addEventListener('click', (event) => {
        if (event.target === dialog || event.target.closest('[data-snapshot-close]')) {
          close();
        }
      });
      dialog.addEventListener('click', async (event) => {
        const button = event.target.closest('[data-snapshot-action]');
        if (!button) return;
        const id = button.dataset.snapshotId || '';
        if (button.dataset.snapshotAction === 'restore') {
          const snapshot = await getSnapshot(id);
          if (snapshot) {
            await restoreSnapshot(snapshot);
            close();
          }
        }
        if (button.dataset.snapshotAction === 'delete') {
          await deleteSnapshot(id);
          await renderSnapshotManager(dialog);
        }
      });

      await renderSnapshotManager(dialog);
      if (typeof dialog.showModal === 'function') dialog.showModal();
      else dialog.setAttribute('open', '');
    }

    async function renderSnapshotManager(dialog) {
      const snapshots = await listActiveSnapshots();
      const rows = snapshots.map((snapshot) => {
        const created = new Date(snapshot.createdAt).toLocaleString();
        return `<section class="snapshot-item">
          <div class="snapshot-item-head">
            <strong>${escapeHtml(snapshot.name || snapshot.path)}</strong>
            <span>${escapeHtml(created)}</span>
          </div>
          <details>
            <summary>Compare with current document</summary>
            <div class="snapshot-diff">${renderSnapshotDiff(snapshot.text, editor.value)}</div>
          </details>
          <div class="asset-library-actions">
            <button type="button" data-snapshot-action="delete" data-snapshot-id="${escapeHtml(snapshot.id)}">Delete</button>
            <button class="primary" type="button" data-snapshot-action="restore" data-snapshot-id="${escapeHtml(snapshot.id)}">Restore</button>
          </div>
        </section>`;
      }).join('');

      dialog.innerHTML = `<form class="utility-dialog-card snapshot-card" method="dialog">
        <div class="utility-dialog-top">
          <span class="template-dialog-kicker">Local snapshots</span>
          <button class="template-dialog-close" type="button" data-snapshot-close aria-label="Close snapshots">X</button>
        </div>
        <h2 id="snapshotDialogTitle">Snapshots for ${escapeHtml(state.activePath)}</h2>
        <p>Snapshots are explicit browser-local versions. They are separate from automatic draft recovery.</p>
        <div class="snapshot-list">${rows || '<div class="workspace-search-empty">No snapshots for this document yet.</div>'}</div>
        <div class="template-dialog-actions">
          <button class="primary" type="button" data-snapshot-close>Done</button>
        </div>
      </form>`;
    }

    async function restoreSnapshot(snapshot) {
      if (isActiveReadOnly()) {
        setStatus('Read-only documents cannot restore snapshots.', 'warning');
        return;
      }
      editor.value = snapshot.text || '';
      state.fileCache.set(state.activePath, editor.value);
      state.dirtyPaths.add(state.activePath);
      resetEditorHistory();
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
      await renderPreview();
      setStatus(`Restored snapshot from ${new Date(snapshot.createdAt).toLocaleString()}.`, 'warning');
    }

    async function listActiveSnapshots() {
      const db = await getSnapshotDb();
      if (!db || !state.activePath) return [];
      const transaction = db.transaction(SNAPSHOT_STORE, 'readonly');
      const store = transaction.objectStore(SNAPSHOT_STORE);
      const snapshots = await idbRequest(store.getAll());
      return snapshots
        .filter((snapshot) => snapshot.workspaceKey === getSnapshotWorkspaceKey() && snapshot.path === state.activePath)
        .sort((left, right) => right.createdAt - left.createdAt);
    }

    async function getSnapshot(id) {
      const db = await getSnapshotDb();
      if (!db) return null;
      const transaction = db.transaction(SNAPSHOT_STORE, 'readonly');
      return await idbRequest(transaction.objectStore(SNAPSHOT_STORE).get(id));
    }

    async function deleteSnapshot(id) {
      const db = await getSnapshotDb();
      if (!db) return;
      const transaction = db.transaction(SNAPSHOT_STORE, 'readwrite');
      transaction.objectStore(SNAPSHOT_STORE).delete(id);
      await idbTransactionDone(transaction);
      setStatus('Snapshot deleted.', 'ok');
    }

    async function getSnapshotDb() {
      if (!snapshotDbPromise) {
        snapshotDbPromise = openObjectStoreDb(SNAPSHOT_DB_NAME, SNAPSHOT_DB_VERSION, [{
          name: SNAPSHOT_STORE,
          keyPath: 'id',
          indexes: [
            { name: 'workspaceKey', keyPath: 'workspaceKey' },
            { name: 'path', keyPath: 'path' },
          ],
        }]).catch((error) => {
          console.warn('Snapshot store unavailable.', error);
          setStatus('Local snapshots are unavailable in this browser.', 'warning');
          return null;
        });
      }
      return await snapshotDbPromise;
    }

    function getSnapshotWorkspaceKey() {
      return state.draftWorkspaceKey || `${state.folderName || 'workspace'}::${state.files.map((file) => file.path).sort().join('|')}`;
    }

    function renderSnapshotDiff(snapshotText, currentText) {
      const snapshotLines = String(snapshotText || '').split('\n');
      const currentLines = String(currentText || '').split('\n');
      const total = Math.max(snapshotLines.length, currentLines.length);
      const rows = [];
      for (let index = 0; index < Math.min(total, 80); index += 1) {
        const before = snapshotLines[index] ?? '';
        const after = currentLines[index] ?? '';
        const changed = before !== after;
        rows.push(`<div class="diff-line ${changed ? 'diff-saved' : ''}"><span>${index + 1}</span><code>${escapeHtml(before || ' ')}</code></div>`);
      }
      if (total > 80) rows.push('<div class="diff-line"><span>...</span><code>Diff preview truncated.</code></div>');
      return rows.join('');
    }

    function markWorkspaceCleanContent(path, content) {
      state.savedContentCache.set(path, content);
      draftTools.updateWorkspaceDraftKey();
    }

    function clearWorkspaceContentCaches() {
      state.fileCache.clear();
      state.savedContentCache.clear();
      state.dirtyPaths.clear();
      state.externalChangePaths?.clear();
      state.externalChangeDetails?.clear();
      state.workspaceDirectoryHandle = null;
      state.nativeWorkspaceId = '';
      state.workspaceKind = '';
      state.selectedTreeFolderPath = '';
      state.artifactBundle = null;
      exportProfileTools.resetSessionProfile();
    }

    function focusEditorAtLine(line, column = 1, length = 1) {
      const lines = editor.value.split('\n');
      const targetLine = Math.max(1, Math.min(Number(line) || 1, lines.length));
      const start = lines.slice(0, targetLine - 1).join('\n').length + (targetLine > 1 ? 1 : 0) + Math.max(0, (Number(column) || 1) - 1);
      const end = Math.min(editor.value.length, start + Math.max(1, Number(length) || 1));
      editor.focus();
      editor.setSelectionRange(start, end);
      scrollEditorToIndex(start, 0.32);
    }

    function scrollEditorToIndex(index, viewportRatio = 0.25) {
      const target = Math.max(0, Math.min(Number(index) || 0, editor.value.length));
      const style = getComputedStyle(editor);
      const mirror = document.createElement('div');
      const marker = document.createElement('span');

      mirror.style.position = 'absolute';
      mirror.style.visibility = 'hidden';
      mirror.style.pointerEvents = 'none';
      mirror.style.left = '-9999px';
      mirror.style.top = '0';
      mirror.style.boxSizing = style.boxSizing;
      mirror.style.width = `${editor.clientWidth}px`;
      mirror.style.minHeight = '0';
      mirror.style.padding = style.padding;
      mirror.style.border = '0';
      mirror.style.font = style.font;
      mirror.style.letterSpacing = style.letterSpacing;
      mirror.style.lineHeight = style.lineHeight;
      mirror.style.tabSize = style.tabSize;
      mirror.style.whiteSpace = style.whiteSpace;
      mirror.style.overflowWrap = style.overflowWrap;
      mirror.style.wordBreak = style.wordBreak;
      mirror.style.overflow = 'hidden';

      marker.textContent = '\u200b';
      mirror.append(document.createTextNode(editor.value.slice(0, target)), marker);
      document.body.appendChild(mirror);
      const markerTop = marker.offsetTop;
      mirror.remove();

      editor.scrollTop = Math.max(0, markerTop - editor.clientHeight * viewportRatio);
      editor.dispatchEvent(new Event('scroll'));
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

    async function getGovernanceAudit() {
      const records = [];
      for (const record of getDocumentationRecords()) {
        records.push({
          name: record.name,
          path: record.path,
          text: await readRecordText(record),
        });
      }
      return analyseMarkdownGovernance({ records, activePath: state.activePath });
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

    async function openAssetLibrary() {
      const dialog = document.createElement('dialog');
      dialog.className = 'utility-dialog asset-library-dialog';
      dialog.setAttribute('aria-labelledby', 'assetLibraryTitle');
      document.body.appendChild(dialog);

      const close = () => {
        dialog.close();
        dialog.remove();
      };
      dialog.addEventListener('cancel', (event) => {
        event.preventDefault();
        close();
      });
      dialog.addEventListener('click', (event) => {
        if (event.target === dialog || event.target.closest('[data-asset-close]')) {
          close();
        }
      });
      dialog.addEventListener('click', async (event) => {
        const button = event.target.closest('[data-asset-action]');
        if (!button) return;
        const path = button.dataset.assetPath || '';
        if (button.dataset.assetAction === 'rename') {
          const input = button.closest('.asset-library-item')?.querySelector('[data-asset-input]');
          await renameManagedAsset(path, input?.value || '');
          await renderAssetLibrary(dialog);
        }
        if (button.dataset.assetAction === 'remove') {
          await removeManagedAsset(path);
          await renderAssetLibrary(dialog);
        }
      });

      await renderAssetLibrary(dialog);
      if (typeof dialog.showModal === 'function') dialog.showModal();
      else dialog.setAttribute('open', '');
    }

    async function renderAssetLibrary(dialog) {
      const usage = await getManagedAssetUsage();
      const assets = [...state.managedAssets.values()].sort((left, right) => left.path.localeCompare(right.path));
      const rows = assets.map((asset) => {
        const count = usage.get(asset.path)?.length || 0;
        const size = formatBytes(asset.size ?? 0);
        const src = asset.objectUrl || asset.dataUrl || '';
        return `<div class="asset-library-item">
          <img src="${escapeHtml(src)}" alt="${escapeHtml(asset.alt || asset.name || 'Asset preview')}">
          <div class="asset-library-main">
            <strong>${escapeHtml(asset.name || asset.path)}</strong>
            <span>${escapeHtml(asset.mimeType || 'image')} · ${escapeHtml(size)} · ${count} use${count === 1 ? '' : 's'}</span>
            <input value="${escapeHtml(asset.path)}" data-asset-input="${escapeHtml(asset.path)}" aria-label="Asset path for ${escapeHtml(asset.name || asset.path)}">
          </div>
          <div class="asset-library-actions">
            <button type="button" data-asset-action="rename" data-asset-path="${escapeHtml(asset.path)}">Rename</button>
            <button type="button" data-asset-action="remove" data-asset-path="${escapeHtml(asset.path)}"${count ? ' disabled' : ''}>Remove</button>
          </div>
        </div>`;
      }).join('');

      dialog.innerHTML = `<form class="utility-dialog-card asset-library-card" method="dialog">
        <div class="utility-dialog-top">
          <span class="template-dialog-kicker">Session assets</span>
          <button class="template-dialog-close" type="button" data-asset-close aria-label="Close asset library">X</button>
        </div>
        <h2 id="assetLibraryTitle">Managed assets</h2>
        <p>Review image assets imported into this browser session. Rename updates loaded Markdown references; remove is available for unused assets.</p>
        <div class="asset-library-list">${rows || '<div class="workspace-search-empty">No managed assets in this session.</div>'}</div>
        <div class="template-dialog-actions">
          <button class="primary" type="button" data-asset-close>Done</button>
        </div>
      </form>`;
    }

    async function getManagedAssetUsage() {
      const usage = new Map([...state.managedAssets.keys()].map((path) => [path, []]));
      if (!usage.size) return usage;
      for (const record of getDocumentationRecords()) {
        const source = await readRecordText(record);
        collectImageReferences(source, record.path).forEach((path) => {
          if (usage.has(path)) usage.get(path).push(record.path);
        });
      }
      return usage;
    }

    async function renameManagedAsset(oldPath, requestedPath) {
      const asset = state.managedAssets.get(oldPath);
      const newPath = normaliseManagedAssetPath(requestedPath);
      if (!asset || !newPath || newPath === oldPath) return;
      if (state.managedAssets.has(newPath)) {
        setStatus('Another managed asset already uses that path.', 'warning');
        return;
      }
      if (!/\.(png|jpe?g|gif|webp)$/i.test(newPath)) {
        setStatus('Managed image assets must keep a PNG, JPEG, GIF, or WebP extension.', 'warning');
        return;
      }

      state.managedAssets.delete(oldPath);
      asset.path = newPath;
      asset.name = newPath.split('/').pop() || asset.name;
      state.managedAssets.set(newPath, asset);
      await replaceAssetReferences(oldPath, newPath);
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
      await renderPreview();
      setStatus(`Renamed asset to ${newPath}.`, 'ok');
    }

    async function removeManagedAsset(path) {
      const usage = await getManagedAssetUsage();
      if ((usage.get(path)?.length || 0) > 0) {
        setStatus('Only unused managed assets can be removed.', 'warning');
        return;
      }
      const asset = state.managedAssets.get(path);
      if (asset?.objectUrl) URL.revokeObjectURL(asset.objectUrl);
      state.managedAssets.delete(path);
      await renderPreview();
      setStatus(`Removed unused asset ${path}.`, 'ok');
    }

    async function replaceAssetReferences(oldPath, newPath) {
      for (const record of getDocumentationRecords()) {
        if (record.readOnly) continue;
        const source = await readRecordText(record);
        if (!source.includes(oldPath)) continue;
        const next = source.split(oldPath).join(newPath);
        state.fileCache.set(record.path, next);
        state.dirtyPaths.add(record.path);
        if (record.path === state.activePath) {
          editor.value = next;
        }
      }
    }

    function normaliseManagedAssetPath(path) {
      const normalised = normalisePath(String(path || '').trim())
        .replace(/^\/+/, '')
        .replace(/^\.\/+/, '');
      return normalised || '';
    }

    function formatBytes(size) {
      const value = Number(size) || 0;
      if (value >= 1024 * 1024) return `${(value / (1024 * 1024)).toFixed(1)} MB`;
      if (value >= 1024) return `${Math.round(value / 1024)} KB`;
      return `${value} B`;
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

    async function openGovernanceIssue(path, line = 1, column = 1, length = 1) {
      await selectFile(path);
      focusEditorAtLine(line, column, length);
      setStatus(`Opened governance issue at ${path}:${line}.`, 'info');
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
          <p class="welcome-kicker">Local Markdown, Mermaid, and documentation studio</p>
          <h2>Start with a documentation folder.</h2>
          <p>Open a folder to browse and watch a local workspace, open one file for a quick edit, or create a starter document. Your files stay local unless you choose to save, copy, export, or import content.</p>
          <div class="welcome-choice-grid">
            <div class="welcome-choice">
              <strong>Open local docs</strong>
              <span>Use Open folder for workspace-style browsing and file watching. Use Open file for a single Markdown, Mermaid, or text document.</span>
              <div class="welcome-actions">
                <button class="primary" type="button" data-welcome-action="openFolder">Open folder</button>
                <button type="button" data-welcome-action="openFile">Open file</button>
                <button type="button" data-welcome-action="diagnostics">Diagnostics</button>
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
          <div class="privacy-note">The Windows desktop shell runs the same local app from packaged files. If Open folder does not work there, use Diagnostics from this screen or Help.</div>
        </section>`;
      updateDocumentUx();
      setStatus('Ready');
    }

    async function loadExample(key) {
      if (!await confirmDiscardUnsaved('Load this example and discard unsaved edits?')) return;

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
      if (!await confirmDiscardUnsaved('Open the feature guide and discard unsaved edits?')) return;

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

    async function checkNativeBridge() {
      setStatus('Checking Windows bridge...', 'busy');
      const result = await nativeBridgeClient.ping();
      if (!result.available) {
        setStatus('Windows bridge unavailable in this browser mode.', 'info');
        return;
      }

      if (!result.ok) {
        setStatus(result.message || 'Windows bridge diagnostic failed safely.', 'warning');
        return;
      }

      const payload = result.response?.payload || {};
      const host = typeof payload.host === 'string' && payload.host.trim()
        ? payload.host.trim()
        : 'Windows host';
      const capabilities = Array.isArray(payload.capabilities)
        ? payload.capabilities.filter((capability) => typeof capability === 'string' && capability.trim())
        : [];
      const capabilitySummary = capabilities.length ? capabilities.join(', ') : 'no capabilities reported';
      setStatus(`Windows bridge available: ${host} (${capabilitySummary}).`, 'ok');
    }

    async function openWindowsShellDiagnostics() {
      if (!windowsShellDiagnosticsDialog || !windowsShellDiagnosticsBody) {
        setStatus('Windows shell diagnostics are unavailable in this browser.', 'warning');
        return;
      }

      setStatus('Checking Windows shell diagnostics...', 'busy');
      setWindowsShellDiagnosticsPending();
      const routeState = typeof nativeBridgeClient.getCapabilityState === 'function'
        ? await nativeBridgeClient.getCapabilityState('workspace.openFolder')
        : await getFallbackCapabilityState('workspace.openFolder');
      const diagnostics = typeof nativeBridgeClient.getDiagnostics === 'function'
        ? nativeBridgeClient.getDiagnostics()
        : {};
      const result = {
        ok: routeState.pingPassed,
        available: routeState.bridgeAvailable,
        reason: routeState.reason,
        message: routeState.message,
        response: {
          protocolVersion: routeState.pingPassed ? 1 : undefined,
          payload: {
            host: routeState.host,
            capabilities: routeState.capabilities,
          },
        },
      };
      const payload = result.response?.payload || {};
      const capabilities = Array.isArray(payload.capabilities)
        ? payload.capabilities.filter((capability) => typeof capability === 'string' && capability.trim())
        : diagnostics.lastCapabilities || [];
      const capabilitySet = new Set(capabilities);
      const host = typeof payload.host === 'string' && payload.host.trim()
        ? payload.host.trim()
        : '';
      const isWindowsShell = result.ok && host === WINDOWS_HOST;
      const webViewBridgePresent = Boolean(routeState.bridgeAvailable);
      const pingPassed = Boolean(result.ok);
      const openFolderNative = Boolean(routeState.hasCapability);
      const openFolderCapabilityState = getOpenFolderCapabilityDiagnosticState(routeState);
      const supportsDirectoryPicker = 'showDirectoryPicker' in window;
      const supportsFolderInput = Boolean(folderInput);
      const openFolderRoute = getOpenFolderDiagnosticRoute({
        bridgeAvailable: webViewBridgePresent,
        pingPassed,
        openFolderNative,
        supportsDirectoryPicker,
        supportsFolderInput,
      });
      const browserFallbackActive = openFolderRoute.browserFallbackActive;
      const pwaMode = isStandaloneDisplayMode();
      const lastError = getSafeDiagnosticError(result, diagnostics);
      const openFolderAttempt = getOpenFolderAttemptDiagnostic(getOpenFolderDiagnostics?.());
      const nextStep = getWindowsShellDiagnosticNextStep({
        pingPassed,
        isWindowsShell: isWindowsShell || webViewBridgePresent,
        openFolderNative,
        supportsDirectoryPicker,
        supportsFolderInput,
        bridgeAvailable: webViewBridgePresent,
      });
      latestSupportBundle = null;

      windowsShellDiagnosticsBody.innerHTML = `
        ${renderDiagnosticSection('Mode', [
          ['Running in browser/PWA', isWindowsShell ? 'No' : (pwaMode ? 'Yes (PWA display mode)' : 'Yes')],
          ['WebView2 shell detected', getWebViewShellDiagnosticValue({ isWindowsShell, bridgeAvailable: webViewBridgePresent, host })],
          ['Bridge message handler registered', yesNo(Boolean(diagnostics.messageHandlerRegistered))],
        ])}
        ${renderDiagnosticSection('Native bridge', [
          ['Ping result', pingPassed ? 'Pass' : 'Fail'],
          ['Protocol version', result.response?.protocolVersion ? String(result.response.protocolVersion) : 'Not reported'],
          ['Host', host || 'Not reported'],
          ['Last native request', diagnostics.lastRequestType || 'None'],
          ['Last native response', diagnostics.lastResponseType || 'None'],
          ['Last native error', lastError],
        ])}
        <section class="windows-shell-diagnostics-section">
          <h3>Capabilities</h3>
          <div class="windows-setup-capabilities" aria-label="Windows bridge capabilities">
            ${WINDOWS_DIAGNOSTIC_CAPABILITIES.map((capability) => `<span data-ready="${capabilitySet.has(capability)}">${escapeHtml(capability)}</span>`).join('')}
          </div>
        </section>
        ${renderDiagnosticSection('Workspace action routing', [
          ['Open folder route', openFolderRoute.label],
          ['workspace.openFolder capability', openFolderCapabilityState],
          ['Open folder will use native bridge', yesNo(openFolderNative)],
          ['Browser fallback state', getBrowserFallbackState({ bridgeAvailable: webViewBridgePresent, browserFallbackActive })],
          ['Browser fallback route', browserFallbackActive ? openFolderRoute.fallbackLabel : 'Not active'],
          ['Directory picker API available', yesNo(supportsDirectoryPicker)],
          ['Folder input fallback available', yesNo(supportsFolderInput)],
        ])}
        ${renderDiagnosticSection('Last Open folder attempt', [
          ['Attempt state', openFolderAttempt.label],
          ['Attempt detail', openFolderAttempt.detail],
        ])}
        <section class="windows-shell-diagnostics-section">
          <h3>Troubleshooting</h3>
          <p>${escapeHtml(nextStep)}</p>
        </section>
        ${renderSupportBundleIntro()}
      `;
      updateSupportBundleButtons(false);

      if (windowsShellDiagnosticsDialog.open) {
        // Refreshing an already open diagnostic dialog only updates its contents.
      } else if (typeof windowsShellDiagnosticsDialog.showModal === 'function') {
        windowsShellDiagnosticsDialog.showModal();
      } else {
        windowsShellDiagnosticsDialog.setAttribute('open', '');
      }

      setStatus(pingPassed
        ? 'Windows shell diagnostics completed.'
        : 'Windows shell diagnostics found the bridge unavailable or failing.', pingPassed ? 'ok' : 'warning');
    }

    function setWindowsShellDiagnosticsPending() {
      latestSupportBundle = null;
      windowsShellDiagnosticsBody.innerHTML = `
        ${renderDiagnosticSection('Native bridge', [
          ['Ping result', 'Pending'],
          ['workspace.openFolder capability', 'Pending'],
          ['Open folder route', 'Pending'],
          ['Browser fallback state', 'Pending'],
        ])}
        <section class="windows-shell-diagnostics-section">
          <h3>Troubleshooting</h3>
          <p>Refreshing bridge and folder-picker diagnostics...</p>
        </section>
        ${renderSupportBundleIntro({ pending: true })}
      `;
      updateSupportBundleButtons(false);
      if (!windowsShellDiagnosticsDialog.open && typeof windowsShellDiagnosticsDialog.showModal === 'function') {
        windowsShellDiagnosticsDialog.showModal();
      } else {
        windowsShellDiagnosticsDialog.setAttribute('open', '');
      }
    }

    async function createDiagnosticsSupportBundle() {
      if (!windowsShellDiagnosticsBody) return;
      try {
        latestSupportBundle = createSupportBundle(buildSupportBundleInput());
        const section = windowsShellDiagnosticsBody.querySelector('[data-support-bundle-panel]');
        if (section) {
          section.innerHTML = renderSupportBundlePreview(latestSupportBundle);
        }
        updateSupportBundleButtons(true);
        setSupportBundleActionMessage('Support bundle preview created locally. Copy and export stay local until you choose to share them.', 'ok');
        setStatus('Support bundle preview created locally. Review it before copying or exporting.', 'ok');
      } catch (error) {
        latestSupportBundle = null;
        updateSupportBundleButtons(false);
        const safeMessage = redactSupportBundleText(error?.message || 'Support bundle creation failed safely.', 120) || 'Support bundle creation failed safely.';
        const section = windowsShellDiagnosticsBody.querySelector('[data-support-bundle-panel]');
        if (section) {
          section.innerHTML = `
            <h3>Support bundle</h3>
            <p>Could not create the local support bundle preview: ${escapeHtml(safeMessage)}</p>
            <p class="support-bundle-action-message" data-support-bundle-message data-status-kind="warning">Could not create the local support bundle preview. No data was uploaded.</p>
          `;
        }
        setStatus('Could not create the local support bundle preview.', 'danger');
      }
    }

    async function copyDiagnosticsSupportBundle() {
      if (!latestSupportBundle) {
        setSupportBundleActionMessage('Create a support bundle preview before copying a diagnostics summary.', 'warning');
        setStatus('Create a support bundle preview before copying it.', 'warning');
        return;
      }
      try {
        await navigator.clipboard.writeText(latestSupportBundle.summaryText);
        setSupportBundleActionMessage('Diagnostics summary copied locally. No upload was started.', 'ok');
        setStatus('Support bundle summary copied. You choose whether to share it.', 'ok');
      } catch {
        setSupportBundleActionMessage('Could not copy the diagnostics summary. No data was uploaded.', 'danger');
        setStatus('Could not copy the support bundle summary.', 'danger');
      }
    }

    function exportDiagnosticsSupportBundle() {
      if (!latestSupportBundle) {
        setSupportBundleActionMessage('Create a support bundle preview before exporting local JSON.', 'warning');
        setStatus('Create a support bundle preview before exporting it.', 'warning');
        return;
      }
      downloadBlob(
        new Blob([latestSupportBundle.json], { type: 'application/json;charset=utf-8' }),
        latestSupportBundle.filename
      );
      setSupportBundleActionMessage(`Support bundle JSON exported locally as ${latestSupportBundle.filename}. No upload was started.`, 'ok');
      setStatus('Support bundle JSON exported locally. No upload was started.', 'ok');
    }

    function closeWindowsShellDiagnostics() {
      if (windowsShellDiagnosticsDialog?.open && typeof windowsShellDiagnosticsDialog.close === 'function') {
        windowsShellDiagnosticsDialog.close();
      } else {
        windowsShellDiagnosticsDialog?.removeAttribute('open');
      }
    }

    function updateSupportBundleButtons(created) {
      if (windowsShellDiagnosticsCopyBundleButton) windowsShellDiagnosticsCopyBundleButton.disabled = !created;
      if (windowsShellDiagnosticsExportBundleButton) windowsShellDiagnosticsExportBundleButton.disabled = !created;
    }

    function renderSupportBundleIntro({ pending = false } = {}) {
      const copy = pending
        ? 'Create support bundle will be available after diagnostics finish.'
        : 'Create a local support bundle preview with safe operational metadata from this diagnostics session. Copy summary creates concise local diagnostics text. Export local JSON saves the preview as a local file with a neutral filename. Private documents, full paths, secrets, tokens, connection strings, emails, raw stack traces, screenshots, browser storage, and WebView2 user data are not included. Nothing is uploaded automatically; you choose whether to share the copied summary or exported JSON.';
      return `
        <section class="windows-shell-diagnostics-section support-bundle-panel" data-support-bundle-panel>
          <h3>Support bundle</h3>
          <p>${escapeHtml(copy)}</p>
          <p class="support-bundle-action-message" data-support-bundle-message hidden></p>
        </section>
      `;
    }

    function renderSupportBundlePreview(bundleResult) {
      const bundle = bundleResult.bundle;
      return `
        <h3>Support bundle preview</h3>
        <p>This bundle is created locally. You choose whether to copy or export it, and no automatic upload occurs.</p>
        <div class="windows-setup-status-list">
          ${[
            ['Schema version', String(bundle.schemaVersion)],
            ['Generated UTC', bundle.generatedAtUtc],
            ['Runtime mode', bundle.environment.runtimeMode],
            ['WebView2 shell detected', bundle.environment.webView2ShellDetected],
            ['Bridge ping state', bundle.diagnostics.bridgePingState],
            ['workspace.openFolder capability', bundle.openFolder.workspaceOpenFolderCapability],
            ['Open folder route', bundle.openFolder.routeDecision],
            ['Last Open folder attempt', bundle.openFolder.lastAttemptState],
            ['Selected workspace present', bundle.openFolder.selectedWorkspacePresent],
            ['Workspace path included', bundle.openFolder.workspacePathIncluded],
            ['Watcher last event', bundle.watcher.lastEventCategory],
            ['Privacy boundary', 'no document content, full private paths, secrets, tokens, connection strings, raw stack traces, screenshots, browser storage, or WebView2 user data'],
          ].map(([label, value]) => `
            <div class="windows-setup-status-row" data-status-kind="${getDiagnosticStatusKind(value)}">
              <span>${escapeHtml(label)}</span>
              <strong>${escapeHtml(value)}</strong>
            </div>
          `).join('')}
        </div>
        <pre class="support-bundle-preview" aria-label="Support bundle JSON preview">${escapeHtml(bundleResult.json)}</pre>
        <p class="support-bundle-action-message" data-support-bundle-message hidden></p>
      `;
    }

    function setSupportBundleActionMessage(message, kind = 'info') {
      const messageElement = windowsShellDiagnosticsBody?.querySelector('[data-support-bundle-message]');
      if (!messageElement) return;
      messageElement.textContent = redactSupportBundleText(message, 180) || 'Diagnostics action completed.';
      messageElement.dataset.statusKind = ['ok', 'warning', 'danger', 'info'].includes(kind) ? kind : 'info';
      messageElement.hidden = false;
    }

    function renderDiagnosticSection(title, rows) {
      return `
        <section class="windows-shell-diagnostics-section">
          <h3>${escapeHtml(title)}</h3>
          <div class="windows-setup-status-list">
            ${rows.map(([label, value]) => `
              <div class="windows-setup-status-row" data-status-kind="${getDiagnosticStatusKind(value)}">
                <span>${escapeHtml(label)}</span>
                <strong>${escapeHtml(value)}</strong>
              </div>
            `).join('')}
          </div>
        </section>
      `;
    }

    function getDiagnosticStatusKind(value) {
      if (/^(yes|pass)/i.test(value)) return 'ok';
      if (/^(no|fail|timeout|unavailable|not reported|missing|failing|blocked|native error)/i.test(value)) return 'warning';
      return 'info';
    }

    async function getFallbackCapabilityState(capability) {
      const result = await nativeBridgeClient.ping();
      const payload = result.response?.payload || {};
      const capabilities = Array.isArray(payload.capabilities)
        ? payload.capabilities.filter((item) => typeof item === 'string' && item.trim())
        : [];
      const host = typeof payload.host === 'string' && payload.host.trim()
        ? payload.host.trim()
        : '';
      return {
        bridgeAvailable: Boolean(result.available),
        pingPassed: Boolean(result.ok),
        reason: result.reason || '',
        message: result.message || '',
        host,
        capabilities,
        hasCapability: Boolean(result.ok && capabilities.includes(capability)),
      };
    }

    function getOpenFolderDiagnosticRoute({
      bridgeAvailable,
      pingPassed,
      openFolderNative,
      supportsDirectoryPicker,
      supportsFolderInput,
    }) {
      if (openFolderNative) {
        return {
          label: 'native bridge',
          browserFallbackActive: false,
          fallbackLabel: 'Not active',
        };
      }
      if (bridgeAvailable) {
        return {
          label: pingPassed
            ? 'blocked because native bridge is present but capability failed'
            : 'blocked because native bridge is present but capability check failed',
          browserFallbackActive: false,
          fallbackLabel: 'Blocked while native bridge is present',
        };
      }
      if (supportsDirectoryPicker) {
        return {
          label: 'browser directory picker',
          browserFallbackActive: true,
          fallbackLabel: 'browser directory picker',
        };
      }
      if (supportsFolderInput) {
        return {
          label: 'browser file input fallback',
          browserFallbackActive: true,
          fallbackLabel: 'browser file input fallback',
        };
      }
      return {
        label: 'unavailable',
        browserFallbackActive: false,
        fallbackLabel: 'No supported folder picker',
      };
    }

    function getWebViewShellDiagnosticValue({ isWindowsShell, bridgeAvailable, host }) {
      if (isWindowsShell) return 'Yes';
      if (bridgeAvailable) return host ? 'Unknown host' : 'Unknown';
      return 'No';
    }

    function getOpenFolderCapabilityDiagnosticState(routeState) {
      if (routeState.hasCapability) return 'available';
      if (!routeState.bridgeAvailable) return 'pending';
      if (!routeState.pingPassed) return 'failing';
      return 'missing';
    }

    function getBrowserFallbackState({ bridgeAvailable, browserFallbackActive }) {
      if (bridgeAvailable) return 'inactive in packaged WebView2';
      return browserFallbackActive ? 'available in browser/PWA context' : 'unavailable in this browser';
    }

    function getOpenFolderAttemptDiagnostic(openFolderDiagnostics = {}) {
      const detail = typeof openFolderDiagnostics.lastMessage === 'string' && openFolderDiagnostics.lastMessage.trim()
        ? openFolderDiagnostics.lastMessage.trim()
        : 'No Open folder result has been recorded in this session.';
      const labels = {
        'not-attempted': 'not attempted',
        'native-picker-opened': 'native picker opened',
        'folder-selected': 'folder selected',
        cancelled: 'user cancelled',
        'native-error': 'native error',
        pending: 'pending',
        timeout: 'timeout',
        'bridge-unavailable': 'bridge unavailable',
        'capability-missing': 'workspace.openFolder missing',
        'browser-picker-opened': 'browser picker opened',
        'browser-picker-error': 'browser picker error',
      };
      return {
        label: labels[openFolderDiagnostics.lastAttempt] || 'not attempted',
        detail,
      };
    }

    function getSafeDiagnosticError(result, diagnostics = {}) {
      if (result.ok) return diagnostics.lastErrorReason || 'None';
      if (result.reason === 'timeout' || diagnostics.requestTimedOut) return 'timeout';
      if (typeof result.message === 'string' && result.message.trim()) return result.message.trim();
      if (typeof diagnostics.lastErrorReason === 'string' && diagnostics.lastErrorReason.trim()) {
        return diagnostics.lastErrorReason.trim();
      }
      return result.available ? 'Bridge request failed safely.' : 'unavailable';
    }

    function getWindowsShellDiagnosticNextStep({
      pingPassed,
      isWindowsShell,
      openFolderNative,
      supportsDirectoryPicker,
      supportsFolderInput,
      bridgeAvailable,
    }) {
      if (openFolderNative) {
        return 'Use File > Open folder from the Windows shell, confirm a real folder picker appears, then continue the watcher/conflict manual evidence pass.';
      }
      if (bridgeAvailable) {
        return 'Use Retry bridge check. If workspace.openFolder is still missing or failing, keep browser fallback inactive, record this dialog, and do not start watcher evidence.';
      }
      if (!pingPassed || !isWindowsShell) {
        return 'In browser or PWA mode, use the browser picker if available. For packaged evidence, launch the Windows executable and retry diagnostics.';
      }
      if (!supportsDirectoryPicker && supportsFolderInput) {
        return 'Browser file input fallback is available only outside the packaged WebView2 shell and is not valid Windows folder-picker evidence.';
      }
      return 'Record the missing workspace.openFolder capability before retrying manual watcher evidence.';
    }

    function buildSupportBundleInput() {
      const diagnostics = typeof nativeBridgeClient.getDiagnostics === 'function'
        ? nativeBridgeClient.getDiagnostics()
        : {};
      const capabilitySet = new Set(Array.isArray(diagnostics.lastCapabilities) ? diagnostics.lastCapabilities : []);
      const openFolderDiagnostics = getOpenFolderDiagnostics?.() || {};
      const bridgeAvailable = Boolean(diagnostics.available);
      const host = diagnostics.lastCapabilities?.length ? WINDOWS_HOST : '';
      const webView2ShellDetected = bridgeAvailable ? (host === WINDOWS_HOST ? 'yes' : 'unknown') : 'no';
      const supportsDirectoryPicker = 'showDirectoryPicker' in window;
      const supportsFolderInput = Boolean(folderInput);
      const openFolderNative = capabilitySet.has('workspace.openFolder');
      const route = getOpenFolderDiagnosticRoute({
        bridgeAvailable,
        pingPassed: bridgeAvailable && !diagnostics.requestTimedOut,
        openFolderNative,
        supportsDirectoryPicker,
        supportsFolderInput,
      });
      const activeFileState = getSupportBundleActiveFileState();
      const watcherState = getSupportBundleWatcherState();
      return {
        generatedAtUtc: new Date().toISOString(),
        app: {
          appVersion: APP_VERSION,
          appBuild: APP_BUILD,
          sourceCommit: '',
        },
        environment: {
          runtimeMode: getSupportBundleRuntimeMode({ bridgeAvailable, webView2ShellDetected }),
          packagedNativeMode: bridgeAvailable ? 'yes' : 'no',
          webView2ShellDetected,
          appOriginCategory: getSupportBundleOriginCategory(),
          platformCategory: getSupportBundlePlatformCategory(),
          browserEngine: getSupportBundleBrowserEngine(),
        },
        diagnostics: {
          bridgeMessageHandlerRegistered: Boolean(diagnostics.messageHandlerRegistered),
          bridgePingState: diagnostics.requestTimedOut ? 'timeout' : (bridgeAvailable ? 'pass' : 'unavailable'),
          protocolVersion: bridgeAvailable ? '1' : 'not reported',
          host: bridgeAvailable ? WINDOWS_HOST : '',
          capabilities: [...capabilitySet],
          lastNativeRequestType: diagnostics.lastRequestType || 'none',
          lastNativeResponseType: diagnostics.lastResponseType || 'none',
          nativeErrorCategory: diagnostics.lastErrorReason || 'none',
          nativeErrorMessage: diagnostics.lastErrorReason || 'none',
        },
        openFolder: {
          routeDecision: getSupportBundleRouteDecision(route.label),
          workspaceOpenFolderCapability: openFolderNative ? 'available' : (bridgeAvailable ? 'missing' : 'pending'),
          browserFallbackState: getSupportBundleBrowserFallbackState(getBrowserFallbackState({
            bridgeAvailable,
            browserFallbackActive: route.browserFallbackActive,
          })),
          lastAttemptState: getSupportBundleAttemptState(openFolderDiagnostics.lastAttempt),
          lastAttemptDetail: openFolderDiagnostics.lastMessage || '',
          selectedWorkspacePresent: state.files.length ? 'yes' : 'no',
          workspaceKind: getSupportBundleWorkspaceKind(),
          supportedFileCount: state.files.length,
          skippedFileCount: state.lastSkippedFileCount,
          activeFileState,
        },
        watcher: watcherState,
      };
    }

    function getSupportBundleRuntimeMode({ bridgeAvailable }) {
      if (bridgeAvailable && getSupportBundleOriginCategory() === 'packaged-virtual-host') return 'windows-shell-packaged';
      if (bridgeAvailable) return 'windows-shell-development';
      const category = getSupportBundleOriginCategory();
      if (category === 'github-pages') return 'browser-github-pages';
      if (category === 'local-static-server') return 'browser-local-static-server';
      if (category === 'file-url') return 'browser-file-url';
      return 'unknown';
    }

    function getSupportBundleOriginCategory() {
      const { protocol, hostname, origin } = window.location;
      if (/^https:\/\/lens-docs-studio\.local\/?$/i.test(origin || '')) return 'packaged-virtual-host';
      if (protocol === 'file:') return 'file-url';
      if (/github\.io$/i.test(hostname || '')) return 'github-pages';
      if (/^(localhost|127\.0\.0\.1|\[::1\])$/i.test(hostname || '')) return 'local-static-server';
      return 'unknown';
    }

    function getSupportBundlePlatformCategory() {
      const source = `${navigator.userAgentData?.platform || ''} ${navigator.platform || ''} ${navigator.userAgent || ''}`.toLowerCase();
      if (source.includes('windows') || source.includes('win32')) return 'windows';
      if (source.includes('mac')) return 'macos';
      if (source.includes('android')) return 'android';
      if (/iphone|ipad|ios/.test(source)) return 'ios';
      if (source.includes('linux')) return 'linux';
      return 'unknown';
    }

    function getSupportBundleBrowserEngine() {
      const source = navigator.userAgent || '';
      if (/Edg\//.test(source)) return 'edge';
      if (/Firefox\//.test(source)) return 'firefox';
      if (/Chrome\/|Chromium\//.test(source)) return 'chromium';
      if (/Safari\//.test(source)) return 'safari';
      return 'unknown';
    }

    function getSupportBundleRouteDecision(label) {
      if (label === 'native bridge') return 'native-bridge';
      if (/blocked because native bridge/i.test(label)) return 'blocked-native-bridge-present';
      if (label === 'browser directory picker') return 'browser-directory-picker';
      if (label === 'browser file input fallback') return 'browser-file-input-fallback';
      if (label === 'unavailable') return 'unavailable';
      return 'unknown';
    }

    function getSupportBundleBrowserFallbackState(label) {
      if (label === 'inactive in packaged WebView2') return 'inactive-in-packaged-webview2';
      if (label === 'available in browser/PWA context') return 'available-in-browser-pwa-context';
      if (label === 'unavailable in this browser') return 'unavailable-in-this-browser';
      return 'unknown';
    }

    function getSupportBundleAttemptState(value) {
      const states = {
        'not-attempted': 'not-attempted',
        'native-picker-opened': 'native-picker-opened',
        'folder-selected': 'folder-selected',
        cancelled: 'user-cancelled',
        'native-error': 'native-error',
        pending: 'pending',
        timeout: 'timeout',
        'bridge-unavailable': 'bridge-unavailable',
        'capability-missing': 'capability-missing',
        'browser-picker-opened': 'browser-picker-opened',
        'browser-picker-error': 'browser-picker-error',
      };
      return states[value] || 'not-attempted';
    }

    function getSupportBundleWorkspaceKind() {
      const kinds = {
        folder: 'browser-folder',
        'folder-fallback': 'browser-folder',
        'native-folder': 'native-folder',
        file: 'single-file',
        zip: 'imported-zip',
        bundle: 'imported-zip',
        'artefact-bundle': 'imported-artefact-bundle',
        converted: 'converted-document',
        virtual: 'virtual-document',
      };
      return kinds[state.workspaceKind] || (state.files.length ? 'virtual-document' : 'unknown');
    }

    function getSupportBundleActiveFileState() {
      if (!state.activePath) return 'none';
      const detail = state.externalChangeDetails?.get(state.activePath);
      if (state.dirtyPaths.has(state.activePath) && detail) return 'dirty-external-conflict';
      if (state.dirtyPaths.has(state.activePath)) return 'edited-in-app';
      if (detail?.kind === 'deleted') return 'external-deleted';
      if (detail?.kind === 'renamed') return 'external-renamed';
      if (detail) return 'external-change';
      return 'clean';
    }

    function getSupportBundleWatcherState() {
      const details = [...(state.externalChangeDetails?.values() || [])];
      const latest = details
        .filter((detail) => detail && typeof detail === 'object')
        .sort((a, b) => String(b.receivedAtUtc || '').localeCompare(String(a.receivedAtUtc || '')))[0];
      const dirtyConflict = state.activePath && state.externalChangeDetails?.has(state.activePath) && state.dirtyPaths.has(state.activePath);
      return {
        lastEventCategory: dirtyConflict ? 'dirty-conflict' : (latest?.kind || 'none'),
        lastEventAtUtc: latest?.receivedAtUtc || '',
        relativePathOnly: latest ? 'yes' : 'unknown',
        dirtyConflictState: dirtyConflict ? 'present' : 'none',
      };
    }

    function yesNo(value) {
      return value ? 'Yes' : 'No';
    }

    function isStandaloneDisplayMode() {
      return Boolean(
        window.matchMedia?.('(display-mode: standalone)')?.matches
        || window.navigator?.standalone
      );
    }

    async function loadStudioTemplate(key) {
      const template = studioTemplates[key];
      if (!template) return;
      if (!await confirmDiscardUnsaved(`Load the ${template.label} template and discard unsaved edits?`)) return;
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
      if (!await confirmDiscardUnsaved(`Create ${template.label} and discard unsaved edits?`)) return;

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

    async function saveCurrentDocumentAsLocalTemplate() {
      if (!state.activePath || !editor.value.trim()) {
        setStatus('Open or write a document before saving a local template.', 'warning');
        return;
      }
      const label = await promptDialog({
        title: 'Save document as template',
        message: 'Name this browser-local template.',
        kicker: 'Local template',
        label: 'Template name',
        value: getExportTitle() || state.fileName.replace(/\.[^.]+$/, '') || 'Local template',
        confirmLabel: 'Save template',
        validate: (value) => value.trim() ? '' : 'Enter a template name.',
      });
      if (!label) return;
      const library = readLocalLibrary();
      library.templates.unshift({
        id: createLocalLibraryId('template'),
        label: label.trim(),
        name: `${slugFromText(label) || 'local-template'}.md`,
        content: editor.value,
        createdAt: Date.now(),
      });
      writeLocalLibrary(library);
      renderLocalLibrary();
      setStatus(`Saved local template "${label.trim()}".`, 'ok');
    }

    async function saveSelectionAsLocalSnippet() {
      const selection = getEditorSelection();
      const text = editor.value.slice(selection.start, selection.end).trim();
      if (!text) {
        setStatus('Select Markdown before saving a local snippet.', 'warning');
        return;
      }
      const label = await promptDialog({
        title: 'Save selection as snippet',
        message: 'Name this browser-local snippet.',
        kicker: 'Local snippet',
        label: 'Snippet name',
        value: text.split(/\r?\n/)[0].slice(0, 40) || 'Local snippet',
        confirmLabel: 'Save snippet',
        validate: (value) => value.trim() ? '' : 'Enter a snippet name.',
      });
      if (!label) return;
      const library = readLocalLibrary();
      library.snippets.unshift({
        id: createLocalLibraryId('snippet'),
        label: label.trim(),
        content: text,
        createdAt: Date.now(),
      });
      writeLocalLibrary(library);
      renderLocalLibrary();
      setStatus(`Saved local snippet "${label.trim()}".`, 'ok');
    }

    async function loadLocalTemplate(id) {
      const template = readLocalLibrary().templates.find((item) => item.id === id);
      if (!template) {
        setStatus('Local template not found.', 'warning');
        return;
      }
      if (!await confirmDiscardUnsaved(`Load ${template.label} and discard unsaved edits?`)) return;
      clearGeneratorMode();
      clearManagedAssets();
      clearScrollPositions();
      clearWorkspaceContentCaches();
      const name = template.name || `${slugFromText(template.label) || 'local-template'}.md`;
      state.files = [{ name, path: name, file: new File([template.content], name, { type: 'text/markdown' }) }];
      state.folderName = 'Local Library';
      state.activePath = name;
      state.fileName = name;
      state.fileCache.set(name, template.content);
      markWorkspaceCleanContent(name, template.content);
      editor.value = template.content;
      resetScrollForCurrentDocument();
      resetEditorHistory();
      syncEditorReadOnly();
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
      await renderPreview();
      setStatus(`Loaded local template "${template.label}".`, 'ok');
    }

    function insertLocalSnippet(id) {
      const snippet = readLocalLibrary().snippets.find((item) => item.id === id);
      if (!snippet) {
        setStatus('Local snippet not found.', 'warning');
        return;
      }
      if (isActiveReadOnly()) {
        setStatus('This document is read-only. Open or create a Markdown file before inserting snippets.', 'warning');
        return;
      }
      if (!state.activePath) {
        setGeneratedDocument({
          label: 'Local Notes',
          mode: 'Local Library',
          name: () => 'local-notes.md',
          content: () => '# Local Notes\n',
        }, { label: 'Local Library', folderName: 'Local Library' }, {});
        editor.setSelectionRange(editor.value.length, editor.value.length);
      }
      const selection = getEditorSelection();
      const prefix = selection.start > 0 && editor.value[selection.start - 1] !== '\n' ? '\n\n' : '';
      const suffix = selection.end < editor.value.length && editor.value[selection.end] !== '\n' ? '\n\n' : '';
      const replacement = `${prefix}${snippet.content}${suffix}`;
      replaceEditorRange(selection.start, selection.end, replacement, selection.start + prefix.length, selection.start + prefix.length + snippet.content.length);
      setStatus(`Inserted local snippet "${snippet.label}".`, 'ok');
    }

    async function saveExportProfile() {
      const label = await promptDialog({
        title: 'Save export profile',
        message: 'Name this browser-local export profile.',
        kicker: 'Export profile',
        label: 'Profile name',
        value: state.folderName || 'Local export profile',
        confirmLabel: 'Save profile',
        validate: (value) => value.trim() ? '' : 'Enter an export profile name.',
      });
      if (!label) return;
      const library = readLocalLibrary();
      library.profiles.unshift({
        id: createLocalLibraryId('profile'),
        label: label.trim(),
        devopsMarkdownExport: Boolean(getEffectiveDevopsMarkdownExport()),
        docsSite: {
          title: state.folderName || getExportTitle() || 'Docs site',
          description: `Static documentation bundle with ${Math.max(state.files.length, 1)} page${state.files.length === 1 ? '' : 's'}.`,
          theme: 'system',
        },
        createdAt: Date.now(),
      });
      writeLocalLibrary(library);
      setStatus(`Saved export profile "${label.trim()}".`, 'ok');
    }

    function applyExportProfile() {
      const profile = readLocalLibrary().profiles[0];
      if (!profile) {
        setStatus('No local export profiles saved yet.', 'warning');
        return;
      }
      state.devopsMarkdownExport = Boolean(profile.devopsMarkdownExport);
      localStorage.setItem(storageKeys.devopsMarkdownExport, String(state.devopsMarkdownExport));
      exportProfileTools.applySavedLocalProfile(profile);
      setStatus(`Applied export profile "${profile.label}".`, 'ok');
    }

    function exportLocalLibrary() {
      const library = readLocalLibrary();
      const payload = {
        formatVersion: 'lens-docs-studio-library-1.0',
        exportedAt: new Date().toISOString(),
        ...library,
      };
      downloadBlob(new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json;charset=utf-8' }), 'lens-docs-studio-library.json');
      setStatus('Local library exported as JSON.', 'ok');
    }

    function importLocalLibrary() {
      return new Promise((resolve) => {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = '.json,application/json';
        input.addEventListener('change', async () => {
          const file = input.files?.[0];
          if (!file) {
            resolve();
            return;
          }
          try {
            const imported = JSON.parse(await file.text());
            const library = normaliseLocalLibrary(imported);
            writeLocalLibrary(library);
            renderLocalLibrary();
            setStatus('Local library imported from JSON.', 'ok');
          } catch (error) {
            setStatus('Could not import local library JSON.', 'danger');
            console.error(error);
          }
          resolve();
        });
        input.click();
      });
    }

    function renderLocalLibrary() {
      const library = readLocalLibrary();
      const templateList = document.getElementById('localTemplateList');
      const snippetList = document.getElementById('localSnippetList');
      if (templateList) {
        templateList.innerHTML = library.templates.length
          ? library.templates.slice(0, 12).map((item) => `<button type="button" data-local-template-id="${escapeHtml(item.id)}">${escapeHtml(item.label)}</button>`).join('')
          : '<span class="menu-note">No local templates yet.</span>';
      }
      if (snippetList) {
        snippetList.innerHTML = library.snippets.length
          ? library.snippets.slice(0, 12).map((item) => `<button type="button" data-local-snippet-id="${escapeHtml(item.id)}">${escapeHtml(item.label)}</button>`).join('')
          : '<span class="menu-note">No local snippets yet.</span>';
      }
    }

    function readLocalLibrary() {
      try {
        return normaliseLocalLibrary(JSON.parse(localStorage.getItem(LOCAL_LIBRARY_KEY) || '{}'));
      } catch {
        return normaliseLocalLibrary({});
      }
    }

    function writeLocalLibrary(library) {
      localStorage.setItem(LOCAL_LIBRARY_KEY, JSON.stringify(normaliseLocalLibrary(library)));
    }

    function normaliseLocalLibrary(value) {
      const source = value && typeof value === 'object' ? value : {};
      return {
        templates: Array.isArray(source.templates) ? source.templates.filter((item) => item?.content && item?.label) : [],
        snippets: Array.isArray(source.snippets) ? source.snippets.filter((item) => item?.content && item?.label) : [],
        profiles: Array.isArray(source.profiles) ? source.profiles.filter((item) => item?.label) : [],
      };
    }

    function createLocalLibraryId(prefix) {
      return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
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

    async function startBlankSetupDocument() {
      if (!await confirmDiscardUnsaved('Start a blank Markdown document and discard unsaved edits?')) return;
      const name = 'untitled.md';
      clearFocusedModes();
      clearManagedAssets();
      clearScrollPositions();
      clearWorkspaceContentCaches();
      state.files = [{
        name,
        path: name,
        file: new File([''], name, { type: 'text/markdown' }),
        needsSave: true,
      }];
      state.folderName = 'Blank document';
      state.activePath = name;
      state.fileName = name;
      state.fileCache.set(name, '');
      markWorkspaceCleanContent(name, '');
      state.dirtyPaths.add(name);
      editor.value = '';
      editor.placeholder = 'Start writing Markdown here.';
      resetScrollForCurrentDocument();
      resetEditorHistory();
      syncEditorReadOnly();
      renderFileList();
      updateActiveFileLabel();
      updateSaveButton();
      await renderPreview();
      setStatus('Blank Markdown document ready.', 'ok');
      editor.focus({ preventScroll: true });
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
      const label = shouldMaximize ? 'Restore split editor' : 'Maximise editor';
      const text = inputMaximizeButton.querySelector('.visually-hidden');
      if (text) text.textContent = shouldMaximize ? 'Restore' : 'Maximise';
      inputMaximizeButton.setAttribute('aria-pressed', String(shouldMaximize));
      inputMaximizeButton.setAttribute('aria-label', label);
      inputMaximizeButton.title = label;
      updateEditorChrome();
    }

    function toggleDocsPreview() {
      state.docsPreview = !state.docsPreview;
      localStorage.setItem(storageKeys.docsPreview, String(state.docsPreview));
      updateDocsPreviewButton();
      renderPreview();
    }

    function handleThemeToggle() {
      toggleTheme();
      if (shouldRerenderForAppThemeChange()) {
        renderPreview();
      }
    }

    function updateDocsPreviewButton() {
      docsPreviewButton.setAttribute('aria-pressed', String(state.docsPreview));
      docsPreviewButton.textContent = state.docsPreview ? 'Show single-page preview' : 'Preview docs site';
      docsPreviewButton.title = state.docsPreview ? 'Return to single-page preview' : 'Preview this folder as a docs site';
    }

    function restoreDevOpsMarkdownExport() {
      exportProfileTools.restoreEffectiveDevopsToggle();
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
        if (action === 'diagnostics') await openWindowsShellDiagnostics();
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

      if (action === 'copyPng') {
        await copyDiagramFramePng(getDiagramFrameFromAction(diagramAction), diagramAction);
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
      scrollEditorToIndex(index, 0.25);
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




