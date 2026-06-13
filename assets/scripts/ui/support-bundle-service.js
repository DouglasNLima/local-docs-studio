const SUPPORT_BUNDLE_SCHEMA_VERSION = 1;
const TEXT_LIMIT = 160;

const SAFE_ERROR_CATEGORIES = new Set([
  'none',
  'timeout',
  'unavailable',
  'post-failed',
  'unsupported-protocol',
  'unsupported-message',
  'invalid-source',
  'invalid-timestamp',
  'host-error',
  'picker-cancelled',
  'picker-failed',
  'bridge-unavailable',
  'capability-missing',
  'browser-picker-error',
  'pending',
  'unknown',
]);

export function createSupportBundle(input = {}) {
  const generatedAtUtc = normaliseIso(input.generatedAtUtc) || new Date().toISOString();
  const diagnostics = input.diagnostics || {};
  const openFolder = input.openFolder || {};
  const watcher = input.watcher || {};
  const environment = input.environment || {};
  const app = input.app || {};

  const bundle = {
    schemaVersion: SUPPORT_BUNDLE_SCHEMA_VERSION,
    generatedAtUtc,
    app: {
      name: 'Lens Docs Studio',
      tagline: 'Local Markdown, Mermaid, and documentation studio',
      appVersion: safeLabel(app.appVersion) || 'unknown',
      appBuild: safeLabel(app.appBuild) || 'unknown',
      sourceCommit: safeCommit(app.sourceCommit),
    },
    environment: {
      runtimeMode: safeEnum(environment.runtimeMode, [
        'browser-github-pages',
        'browser-local-static-server',
        'browser-file-url',
        'windows-shell-packaged',
        'windows-shell-development',
        'unknown',
      ], 'unknown'),
      packagedNativeMode: yesNoUnknown(environment.packagedNativeMode),
      webView2ShellDetected: yesNoUnknown(environment.webView2ShellDetected),
      appOriginCategory: safeEnum(environment.appOriginCategory, [
        'packaged-virtual-host',
        'github-pages',
        'local-static-server',
        'file-url',
        'unknown',
      ], 'unknown'),
      platformCategory: safeEnum(environment.platformCategory, [
        'windows',
        'macos',
        'linux',
        'android',
        'ios',
        'unknown',
      ], 'unknown'),
      browserEngine: safeEnum(environment.browserEngine, [
        'chromium',
        'firefox',
        'safari',
        'edge',
        'unknown',
      ], 'unknown'),
    },
    diagnostics: {
      bridgeMessageHandlerRegistered: yesNoUnknown(diagnostics.bridgeMessageHandlerRegistered),
      bridgePingState: safeEnum(diagnostics.bridgePingState, ['pass', 'fail', 'unavailable', 'timeout', 'unknown'], 'unknown'),
      protocolVersion: boundedText(diagnostics.protocolVersion || 'not reported'),
      host: diagnostics.host === 'LensDocsStudio.Windows' ? 'LensDocsStudio.Windows' : 'unknown',
      capabilities: normaliseCapabilities(diagnostics.capabilities),
      lastNativeRequestType: boundedText(diagnostics.lastNativeRequestType || 'none'),
      lastNativeResponseType: boundedText(diagnostics.lastNativeResponseType || 'none'),
      nativeErrorCategory: normaliseErrorCategory(diagnostics.nativeErrorCategory),
      nativeErrorMessage: boundedText(diagnostics.nativeErrorMessage || 'none'),
    },
    openFolder: {
      routeDecision: safeEnum(openFolder.routeDecision, [
        'native-bridge',
        'blocked-native-bridge-present',
        'browser-directory-picker',
        'browser-file-input-fallback',
        'unavailable',
        'unknown',
      ], 'unknown'),
      workspaceOpenFolderCapability: safeEnum(openFolder.workspaceOpenFolderCapability, [
        'available',
        'missing',
        'failing',
        'pending',
        'unknown',
      ], 'unknown'),
      browserFallbackState: safeEnum(openFolder.browserFallbackState, [
        'inactive-in-packaged-webview2',
        'available-in-browser-pwa-context',
        'unavailable-in-this-browser',
        'unknown',
      ], 'unknown'),
      lastAttemptState: safeEnum(openFolder.lastAttemptState, [
        'not-attempted',
        'native-picker-opened',
        'folder-selected',
        'user-cancelled',
        'native-error',
        'pending',
        'timeout',
        'bridge-unavailable',
        'capability-missing',
        'browser-picker-opened',
        'browser-picker-error',
        'unknown',
      ], 'unknown'),
      lastAttemptDetail: boundedOperationalText(openFolder.lastAttemptDetail || 'none'),
      selectedWorkspacePresent: yesNoUnknown(openFolder.selectedWorkspacePresent),
      workspacePathIncluded: 'no',
      workspaceKind: safeEnum(openFolder.workspaceKind, [
        'browser-folder',
        'native-folder',
        'single-file',
        'imported-zip',
        'imported-artefact-bundle',
        'converted-document',
        'virtual-document',
        'unknown',
      ], 'unknown'),
      supportedFileCountBucket: countBucket(openFolder.supportedFileCount),
      skippedFileCountBucket: countBucket(openFolder.skippedFileCount),
      activeFileState: safeEnum(openFolder.activeFileState, [
        'none',
        'clean',
        'edited-in-app',
        'external-change',
        'dirty-external-conflict',
        'external-deleted',
        'external-renamed',
        'unknown',
      ], 'unknown'),
    },
    watcher: {
      lastEventCategory: safeEnum(watcher.lastEventCategory, [
        'changed',
        'created',
        'deleted',
        'renamed',
        'dirty-conflict',
        'refresh-confirmed',
        'refresh-cancelled',
        'none',
        'unknown',
      ], 'unknown'),
      lastEventAtUtc: normaliseIso(watcher.lastEventAtUtc) || 'not recorded',
      relativePathOnly: yesNoUnknown(watcher.relativePathOnly),
      dirtyConflictState: safeEnum(watcher.dirtyConflictState, ['none', 'present', 'unknown'], 'unknown'),
    },
    privacy: {
      localOnly: 'yes',
      userChoosesWhetherToShare: 'yes',
      automaticUpload: 'no',
      telemetry: 'no',
      documentContentIncluded: 'no',
      importedZipContentIncluded: 'no',
      screenshotsIncluded: 'no',
      browserStorageIncluded: 'no',
      webView2UserDataIncluded: 'no',
      fullPrivatePathsIncluded: 'no',
      secretsTokensConnectionStringsIncluded: 'no',
      rawStackTracesIncluded: 'no',
      emailsPrivateNamesPiiIncluded: 'no',
    },
    redaction: {
      schema: 'allowlisted-fields-v1',
      textLimit,
      paths: 'excluded-by-default',
      diagnosticText: 'redacted-and-bounded',
      nativeErrors: 'category-plus-bounded-redacted-message',
      untrustedMetadata: 'excluded',
    },
  };

  return {
    bundle,
    json: `${JSON.stringify(bundle, null, 2)}\n`,
    summaryText: buildDiagnosticsSummary(bundle),
    filename: `lens-docs-studio-support-bundle-${formatBundleTimestamp(generatedAtUtc)}.json`,
  };
}

export function redactSupportBundleText(value, limit = TEXT_LIMIT) {
  const text = typeof value === 'string' && value.trim() ? value.trim() : '';
  if (!text) return '';
  const redacted = text
    .replace(/\bat\s+[\w.$<>]+\s*\([^)\r\n]*(?:\)|$)/gim, '[redacted-stack-trace]')
    .replace(/\b(?:at\s+)?[A-Za-z]:\\[^\s"'<>]+/g, '[redacted-path]')
    .replace(/\\\\[^\\\s"'<>]+\\[^\s"'<>]+/g, '[redacted-path]')
    .replace(/(?:^|\s)\/(?:Users|home|tmp|var|private|Volumes)\/[^\s"'<>]+/gi, ' [redacted-path]')
    .replace(/~\/[^\s"'<>]+/g, '[redacted-path]')
    .replace(/\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b/g, '[redacted-email]')
    .replace(/\b(?:access_token|token|api_key|apikey|secret|password|sig|connectionString|connection_string)\s*[:=]\s*[^\s"'&;]+/gi, '[redacted-secret]')
    .replace(/(https?:\/\/[^\s"'<>?]+)\?[^\s"'<>]+/gi, '$1?[redacted-query]')
    .replace(/--- End of stack trace.*$/gim, '[redacted-stack-trace]');
  const compact = redacted.replace(/\s+/g, ' ').trim();
  return compact.length > limit ? `${compact.slice(0, Math.max(0, limit - 3))}...` : compact;
}

function buildDiagnosticsSummary(bundle) {
  return [
    'Lens Docs Studio diagnostics summary',
    `App version: ${bundle.app.appVersion}`,
    `App build: ${bundle.app.appBuild}`,
    `Schema version: ${bundle.schemaVersion}`,
    `Generated UTC: ${bundle.generatedAtUtc}`,
    `Runtime mode: ${bundle.environment.runtimeMode}`,
    `Packaged/native mode: ${bundle.environment.packagedNativeMode}`,
    `WebView2 shell detected: ${bundle.environment.webView2ShellDetected}`,
    `Bridge ping state: ${bundle.diagnostics.bridgePingState}`,
    `workspace.openFolder capability: ${bundle.openFolder.workspaceOpenFolderCapability}`,
    `Open folder route: ${bundle.openFolder.routeDecision}`,
    `Browser fallback state: ${bundle.openFolder.browserFallbackState}`,
    `Last Open folder attempt: ${bundle.openFolder.lastAttemptState}`,
    `Watcher last event: ${bundle.watcher.lastEventCategory}`,
    `Watcher timestamp bucket: ${timestampBucket(bundle.watcher.lastEventAtUtc, bundle.generatedAtUtc)}`,
    'Privacy: local bundle only; no automatic upload; no document content, full private paths, secrets, tokens, connection strings, raw stack traces, screenshots, browser storage, or WebView2 user data included.',
  ].join('\n');
}

function timestampBucket(value, generatedAtUtc) {
  if (!value || value === 'not recorded') return 'not recorded';
  const eventDate = new Date(value);
  const generatedDate = new Date(generatedAtUtc);
  if (Number.isNaN(eventDate.getTime()) || Number.isNaN(generatedDate.getTime())) return 'unknown';
  const deltaMs = generatedDate.getTime() - eventDate.getTime();
  if (deltaMs < 0) return 'future-or-clock-skew';
  if (deltaMs <= 60_000) return 'within-1-minute';
  if (deltaMs <= 3_600_000) return 'within-1-hour';
  if (deltaMs <= 86_400_000) return 'within-1-day';
  return 'older-than-1-day';
}

function normaliseCapabilities(capabilities) {
  const allowed = [
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
  const source = Array.isArray(capabilities) ? capabilities : [];
  const set = new Set(source.filter((item) => allowed.includes(item)));
  return Object.fromEntries(allowed.map((capability) => [capability, set.has(capability) ? 'available' : 'not-reported']));
}

function normaliseErrorCategory(value) {
  const text = String(value || '').trim().toLowerCase();
  return SAFE_ERROR_CATEGORIES.has(text) ? text : 'unknown';
}

function safeEnum(value, allowed, fallback) {
  const text = String(value || '').trim();
  return allowed.includes(text) ? text : fallback;
}

function yesNoUnknown(value) {
  if (value === true || value === 'yes') return 'yes';
  if (value === false || value === 'no') return 'no';
  return 'unknown';
}

function safeLabel(value) {
  const text = redactSupportBundleText(String(value || ''), 64);
  return /^[\w .()+-]+$/.test(text) ? text : '';
}

function safeCommit(value) {
  const text = String(value || '').trim();
  return /^[a-f0-9]{7,40}$/i.test(text) ? text : 'unknown';
}

function boundedText(value) {
  return redactSupportBundleText(String(value || ''), TEXT_LIMIT) || 'none';
}

function boundedOperationalText(value) {
  const source = String(value || '');
  if (/(^|\n)\s{0,3}#{1,6}\s+\S|(^|\n)\s*```|(^|\n)\s*---\s*($|\n)/.test(source)) {
    return '[redacted-document-content]';
  }
  return boundedText(source);
}

function normaliseIso(value) {
  const text = String(value || '').trim();
  if (!text) return '';
  const date = new Date(text);
  return Number.isNaN(date.getTime()) ? '' : date.toISOString();
}

function formatBundleTimestamp(value) {
  return (normaliseIso(value) || new Date().toISOString())
    .replace(/[-:]/g, '')
    .replace(/\.\d{3}Z$/, 'Z');
}

function countBucket(value) {
  const count = Number(value);
  if (!Number.isFinite(count) || count < 0) return 'unknown';
  if (count === 0) return '0';
  if (count <= 5) return '1-5';
  if (count <= 20) return '6-20';
  if (count <= 100) return '21-100';
  return '101+';
}

const textLimit = TEXT_LIMIT;
