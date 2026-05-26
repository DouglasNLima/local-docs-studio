export const supportedFilePattern = /\.(md|markdown|mmd|mermaid)$/i;
export const APP_VERSION = '0.1.0';
export const APP_BUILD = '53';
export const APP_BROWSER_TITLE = `Lens Docs Studio v${APP_VERSION} (build ${APP_BUILD})`;

export const storageKeys = {
  sidebarWidth: 'md-mmd-renderer.sidebarWidth',
  editorWidth: 'md-mmd-renderer.editorWidth',
  diagramZoom: 'md-mmd-renderer.diagramZoom',
  theme: 'md-mmd-renderer.theme',
  outline: 'md-mmd-renderer.outline',
  scrollSync: 'md-mmd-renderer.scrollSync',
  documentReviewOpen: 'md-mmd-renderer.documentReviewOpen',
  docsPreview: 'md-mmd-renderer.docsPreview',
  editorLayout: 'md-mmd-renderer.editorLayout',
  studioMode: 'md-mmd-renderer.studioMode',
  generatorMode: 'md-mmd-renderer.generatorMode',
  devopsMarkdownExport: 'md-mmd-renderer.devopsMarkdownExport',
  focusMode: 'md-mmd-renderer.focusMode',
  sidebarCollapsed: 'md-mmd-renderer.sidebarCollapsed',
  fileBrowserView: 'md-mmd-renderer.fileBrowserView',
  typewriterMode: 'md-mmd-renderer.typewriterMode',
  workspaceSearchOptions: 'md-mmd-renderer.workspaceSearchOptions',
  draftStoreVersion: 'md-mmd-renderer.draftStoreVersion',
};

export const mermaidStarters = [
  'graph ', 'graph\n', 'flowchart ', 'flowchart\n',
  'sequenceDiagram', 'classDiagram', 'classDiagram-v2', 'stateDiagram', 'stateDiagram-v2',
  'erDiagram', 'journey', 'gantt', 'pie', 'gitGraph', 'mindmap', 'zenuml',
  'timeline', 'quadrantChart', 'requirementDiagram', 'C4Context',
  'C4Container', 'C4Component', 'C4Dynamic', 'C4Deployment',
  'sankey-beta', 'xychart-beta', 'block-beta', 'packet-beta', 'info',
  'kanban', 'architecture-beta', 'radar-beta', 'treemap-beta',
];

export function createInitialState({ readStoredNumber }) {
  return {
    files: [],
    activePath: '',
    fileName: '',
    folderName: '',
    workspaceDirectoryHandle: null,
    workspaceKind: '',
    renderId: 0,
    debounceId: 0,
    fileCache: new Map(),
    savedContentCache: new Map(),
    dirtyPaths: new Set(),
    externalChangePaths: new Set(),
    deletionOverridePaths: new Set(),
    draftWorkspaceKey: '',
    diagramZoom: readStoredNumber(storageKeys.diagramZoom, 1),
    diagramTotal: 0,
    lastRenderResult: { ok: false, diagramErrors: 0, diagramTotal: 0 },
    outlineOpen: localStorage.getItem(storageKeys.outline) === 'true',
    scrollSyncEnabled: localStorage.getItem(storageKeys.scrollSync) !== 'false',
    documentReviewOpen: localStorage.getItem(storageKeys.documentReviewOpen) === 'true',
    docsPreview: localStorage.getItem(storageKeys.docsPreview) === 'true',
    editorLayout: resolveStoredEditorLayout(),
    studioMode: localStorage.getItem(storageKeys.studioMode) === 'true',
    generatorMode: localStorage.getItem(storageKeys.generatorMode) || '',
    devopsMarkdownExport: localStorage.getItem(storageKeys.devopsMarkdownExport) === 'true',
    focusMode: localStorage.getItem(storageKeys.focusMode) === 'true',
    sidebarCollapsed: localStorage.getItem(storageKeys.sidebarCollapsed) === 'true',
    fileBrowserView: localStorage.getItem(storageKeys.fileBrowserView) === 'tree' ? 'tree' : 'list',
    collapsedTreeFolders: new Set(),
    typewriterMode: false,
    managedAssets: new Map(),
    managedAssetCounter: 0,
    artifactBundle: null,
    activeBuiltInExportProfile: '',
    sessionDevopsMarkdownExport: null,
    exportProfileDefaults: null,
    recentEntries: [],
    scrollPositions: new Map(),
    mermaidRenderChain: Promise.resolve(),
    editorHistory: {
      undo: [],
      redo: [],
      last: null,
      suppress: false,
      max: 100,
      typing: false,
      lastTypedAt: 0,
    },
  };
}

function resolveStoredEditorLayout() {
  const stored = localStorage.getItem(storageKeys.editorLayout);
  return ['editor', 'split', 'preview'].includes(stored) ? stored : 'split';
}
