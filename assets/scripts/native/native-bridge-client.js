export const BRIDGE_PROTOCOL_VERSION = 1;

export const nativeBridgeMessageTypes = {
  ping: 'lensDocs.native.ping',
  pong: 'lensDocs.native.pong',
  appReady: 'lensDocs.native.appReady',
  appReadyResult: 'lensDocs.native.appReadyResult',
  startupFile: 'lensDocs.native.startupFile',
  startupFileError: 'lensDocs.native.startupFileError',
  openFile: 'lensDocs.native.openFile',
  openFileResult: 'lensDocs.native.openFileResult',
  saveFile: 'lensDocs.native.saveFile',
  saveFileResult: 'lensDocs.native.saveFileResult',
  saveFileAs: 'lensDocs.native.saveFileAs',
  saveFileAsResult: 'lensDocs.native.saveFileAsResult',
  openFolder: 'lensDocs.native.openFolder',
  openFolderResult: 'lensDocs.native.openFolderResult',
  saveWorkspaceFile: 'lensDocs.native.saveWorkspaceFile',
  saveWorkspaceFileResult: 'lensDocs.native.saveWorkspaceFileResult',
  createWorkspaceFile: 'lensDocs.native.createWorkspaceFile',
  createWorkspaceFileResult: 'lensDocs.native.createWorkspaceFileResult',
  refreshWorkspaceFile: 'lensDocs.native.refreshWorkspaceFile',
  refreshWorkspaceFileResult: 'lensDocs.native.refreshWorkspaceFileResult',
  workspaceChanged: 'lensDocs.native.workspaceChanged',
  smokeOpenFixtureFile: 'lensDocs.native.smoke.openFixtureFile',
  smokeOpenFixtureFileResult: 'lensDocs.native.smoke.openFixtureFileResult',
  smokeSaveFixtureFileAs: 'lensDocs.native.smoke.saveFixtureFileAs',
  smokeSaveFixtureFileAsResult: 'lensDocs.native.smoke.saveFixtureFileAsResult',
  smokeOpenFixtureWorkspace: 'lensDocs.native.smoke.openFixtureWorkspace',
  smokeOpenFixtureWorkspaceResult: 'lensDocs.native.smoke.openFixtureWorkspaceResult',
  smokeTouchWorkspaceFile: 'lensDocs.native.smoke.touchWorkspaceFile',
  smokeTouchWorkspaceFileResult: 'lensDocs.native.smoke.touchWorkspaceFileResult',
  smokeComplete: 'lensDocs.native.smoke.complete',
  smokeCompleteResult: 'lensDocs.native.smoke.completeResult',
  error: 'lensDocs.native.error',
};

const WEB_SOURCE = 'LensDocsStudio.Web';
const DEFAULT_TIMEOUT_MS = 2500;

export function createNativeBridgeClient({
  windowRef = window,
  timeoutMs = DEFAULT_TIMEOUT_MS,
  createId = defaultCreateId,
  now = () => new Date().toISOString(),
} = {}) {
  const pendingMessages = new Map();
  const eventListeners = new Map();
  const webview = getWebView(windowRef);
  const diagnostics = {
    messageHandlerRegistered: false,
    lastRequestType: '',
    lastResponseType: '',
    lastErrorReason: '',
    lastCapabilities: [],
    requestTimedOut: false,
  };

  if (webview && typeof webview.addEventListener === 'function') {
    webview.addEventListener('message', (event) => {
      handleHostMessage(event?.data);
    });
    diagnostics.messageHandlerRegistered = true;
  }

  function isAvailable() {
    return Boolean(getWebView(windowRef));
  }

  function createMessage(type, payload = {}) {
    return {
      protocolVersion: BRIDGE_PROTOCOL_VERSION,
      id: createId(),
      type,
      source: WEB_SOURCE,
      timestamp: now(),
      payload,
    };
  }

  async function ping() {
    if (!isAvailable()) {
      return {
        ok: false,
        available: false,
        reason: 'unavailable',
        message: 'Native bridge unavailable.',
      };
    }

    const message = createMessage(nativeBridgeMessageTypes.ping);
    return await sendMessage(message, [nativeBridgeMessageTypes.pong]);
  }

  async function notifyAppReady() {
    const message = createMessage(nativeBridgeMessageTypes.appReady);
    return await sendMessage(message, [nativeBridgeMessageTypes.appReadyResult]);
  }

  async function hasCapability(capability) {
    const state = await getCapabilityState(capability);
    return state.hasCapability;
  }

  async function getCapabilityState(capability) {
    const result = await ping();
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

  function getDiagnostics() {
    return {
      ...diagnostics,
      lastCapabilities: [...diagnostics.lastCapabilities],
      available: isAvailable(),
    };
  }

  async function openFile() {
    const message = createMessage(nativeBridgeMessageTypes.openFile);
    return await sendMessage(message, [nativeBridgeMessageTypes.openFileResult]);
  }

  async function saveFile({ nativeHandleId, content }) {
    const message = createMessage(nativeBridgeMessageTypes.saveFile, {
      nativeHandleId,
      content,
    });
    return await sendMessage(message, [nativeBridgeMessageTypes.saveFileResult]);
  }

  async function saveFileAs({ suggestedName, content }) {
    const message = createMessage(nativeBridgeMessageTypes.saveFileAs, {
      suggestedName,
      content,
    });
    return await sendMessage(message, [nativeBridgeMessageTypes.saveFileAsResult]);
  }

  async function openFolder() {
    const message = createMessage(nativeBridgeMessageTypes.openFolder);
    return await sendMessage(message, [nativeBridgeMessageTypes.openFolderResult]);
  }

  async function saveWorkspaceFile({ nativeHandleId, content }) {
    const message = createMessage(nativeBridgeMessageTypes.saveWorkspaceFile, {
      nativeHandleId,
      content,
    });
    return await sendMessage(message, [nativeBridgeMessageTypes.saveWorkspaceFileResult]);
  }

  async function createWorkspaceFile({ nativeWorkspaceId, path, content }) {
    const message = createMessage(nativeBridgeMessageTypes.createWorkspaceFile, {
      nativeWorkspaceId,
      path,
      content,
    });
    return await sendMessage(message, [nativeBridgeMessageTypes.createWorkspaceFileResult]);
  }

  async function refreshWorkspaceFile({ nativeWorkspaceId, nativeHandleId, path }) {
    const message = createMessage(nativeBridgeMessageTypes.refreshWorkspaceFile, {
      nativeWorkspaceId,
      nativeHandleId,
      path,
    });
    return await sendMessage(message, [nativeBridgeMessageTypes.refreshWorkspaceFileResult]);
  }

  function on(type, listener) {
    if (typeof type !== 'string' || typeof listener !== 'function') {
      return () => {};
    }

    const listeners = eventListeners.get(type) || new Set();
    listeners.add(listener);
    eventListeners.set(type, listeners);
    return () => {
      listeners.delete(listener);
      if (!listeners.size) eventListeners.delete(type);
    };
  }

  async function openSmokeFixtureFile() {
    const message = createMessage(nativeBridgeMessageTypes.smokeOpenFixtureFile);
    return await sendMessage(message, [nativeBridgeMessageTypes.smokeOpenFixtureFileResult]);
  }

  async function saveSmokeFixtureFileAs({ content }) {
    const message = createMessage(nativeBridgeMessageTypes.smokeSaveFixtureFileAs, {
      content,
    });
    return await sendMessage(message, [nativeBridgeMessageTypes.smokeSaveFixtureFileAsResult]);
  }

  async function openSmokeFixtureWorkspace() {
    const message = createMessage(nativeBridgeMessageTypes.smokeOpenFixtureWorkspace);
    return await sendMessage(message, [nativeBridgeMessageTypes.smokeOpenFixtureWorkspaceResult]);
  }

  async function touchSmokeWorkspaceFile() {
    const message = createMessage(nativeBridgeMessageTypes.smokeTouchWorkspaceFile);
    return await sendMessage(message, [nativeBridgeMessageTypes.smokeTouchWorkspaceFileResult]);
  }

  async function completeSmoke(payload) {
    const message = createMessage(nativeBridgeMessageTypes.smokeComplete, payload);
    return await sendMessage(message, [nativeBridgeMessageTypes.smokeCompleteResult]);
  }

  function sendMessage(message, expectedTypes) {
    return new Promise((resolve) => {
      const currentWebView = getWebView(windowRef);
      if (!currentWebView) {
        recordBridgeFailure('unavailable', { timedOut: false });
        resolve({
          ok: false,
          available: false,
          reason: 'unavailable',
          message: 'Native bridge unavailable.',
        });
        return;
      }

      diagnostics.lastRequestType = message.type;
      diagnostics.lastResponseType = '';
      diagnostics.lastErrorReason = '';
      diagnostics.requestTimedOut = false;

      const timeoutId = windowRef.setTimeout?.(() => {
        pendingMessages.delete(message.id);
        recordBridgeFailure('timeout', { timedOut: true });
        resolve({
          ok: false,
          available: true,
          reason: 'timeout',
          message: 'Native bridge did not respond.',
        });
      }, timeoutMs);

      pendingMessages.set(message.id, {
        resolve,
        timeoutId,
        expectedTypes,
      });

      try {
        currentWebView.postMessage(message);
      } catch {
        clearPending(message.id);
        recordBridgeFailure('post-failed', { timedOut: false });
        resolve({
          ok: false,
          available: false,
          reason: 'post-failed',
          message: 'Native bridge could not receive the diagnostic request.',
        });
      }
    });
  }

  function handleHostMessage(rawMessage) {
    const message = normaliseHostMessage(rawMessage);
    const pending = message?.id ? pendingMessages.get(message.id) : null;
    if (!pending) {
      handleHostEvent(message);
      return;
    }

    clearPending(message.id);
    const validation = validateHostResponse(message, pending.expectedTypes);
    if (!validation.ok) {
      recordBridgeFailure(validation.reason, { responseType: message.type });
      pending.resolve({
        ok: false,
        available: true,
        reason: validation.reason,
        message: validation.message,
      });
      return;
    }

    pending.resolve({
      ok: pending.expectedTypes.includes(message.type),
      available: true,
      response: message,
      message: message.type === nativeBridgeMessageTypes.pong
        ? 'Native bridge responded.'
        : getSafeErrorMessage(message),
    });
    diagnostics.lastResponseType = message.type;
    diagnostics.lastErrorReason = message.type === nativeBridgeMessageTypes.error ? 'host-error' : '';
    diagnostics.requestTimedOut = false;
    const capabilities = message.payload?.capabilities;
    if (Array.isArray(capabilities)) {
      diagnostics.lastCapabilities = capabilities.filter((capability) => typeof capability === 'string' && capability.trim());
    }
  }

  function clearPending(id) {
    const pending = pendingMessages.get(id);
    if (pending?.timeoutId) {
      windowRef.clearTimeout?.(pending.timeoutId);
    }
    pendingMessages.delete(id);
  }

  function handleHostEvent(message) {
    if (!validateHostEvent(message)) return;
    const listeners = eventListeners.get(message.type);
    if (!listeners?.size) return;
    listeners.forEach((listener) => {
      try {
        listener(message);
      } catch (error) {
        console.error('Native bridge event listener failed.', error);
      }
    });
  }

  function recordBridgeFailure(reason, { responseType = '', timedOut = false } = {}) {
    diagnostics.lastResponseType = responseType;
    diagnostics.lastErrorReason = reason;
    diagnostics.requestTimedOut = Boolean(timedOut);
  }

  return {
    isAvailable,
    createMessage,
    ping,
    notifyAppReady,
    hasCapability,
    getCapabilityState,
    openFile,
    saveFile,
    saveFileAs,
    openFolder,
    saveWorkspaceFile,
    createWorkspaceFile,
    refreshWorkspaceFile,
    on,
    openSmokeFixtureFile,
    saveSmokeFixtureFileAs,
    openSmokeFixtureWorkspace,
    touchSmokeWorkspaceFile,
    completeSmoke,
    getDiagnostics,
  };
}

function getWebView(windowRef) {
  const webview = windowRef?.chrome?.webview;
  if (!webview || typeof webview.postMessage !== 'function') return null;
  return webview;
}

function defaultCreateId() {
  if (globalThis.crypto?.randomUUID) {
    return globalThis.crypto.randomUUID();
  }
  return `native-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function normaliseHostMessage(rawMessage) {
  if (!rawMessage) return null;
  if (typeof rawMessage === 'string') {
    try {
      return JSON.parse(rawMessage);
    } catch {
      return null;
    }
  }
  if (typeof rawMessage !== 'object') return null;
  return rawMessage;
}

function validateHostResponse(message, expectedTypes = []) {
  if (message.protocolVersion !== BRIDGE_PROTOCOL_VERSION) {
    return {
      ok: false,
      reason: 'unsupported-protocol',
      message: 'Native bridge returned an unsupported protocol response.',
    };
  }
  const supportedTypes = [...expectedTypes, nativeBridgeMessageTypes.error];
  if (!supportedTypes.includes(message.type)) {
    return {
      ok: false,
      reason: 'unsupported-message',
      message: 'Native bridge returned an unsupported response.',
    };
  }
  if (typeof message.source !== 'string' || !message.source.trim()) {
    return {
      ok: false,
      reason: 'invalid-source',
      message: 'Native bridge returned an invalid diagnostic response.',
    };
  }
  if (typeof message.timestamp !== 'string' || !message.timestamp.trim()) {
    return {
      ok: false,
      reason: 'invalid-timestamp',
      message: 'Native bridge returned an invalid diagnostic timestamp.',
    };
  }
  return { ok: true };
}

function validateHostEvent(message) {
  if (!message || message.protocolVersion !== BRIDGE_PROTOCOL_VERSION) return false;
  if (![
    nativeBridgeMessageTypes.workspaceChanged,
    nativeBridgeMessageTypes.startupFile,
    nativeBridgeMessageTypes.startupFileError,
  ].includes(message.type)) return false;
  if (typeof message.id !== 'string' || !message.id.trim()) return false;
  if (message.source !== 'LensDocsStudio.Windows') return false;
  if (typeof message.timestamp !== 'string' || !message.timestamp.trim()) return false;
  return Boolean(message.payload && typeof message.payload === 'object');
}

function getSafeErrorMessage(message) {
  const errorMessage = message?.payload?.message;
  return typeof errorMessage === 'string' && errorMessage.trim()
    ? errorMessage
    : 'Native bridge returned a safe diagnostic error.';
}
