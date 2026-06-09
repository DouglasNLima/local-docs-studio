export const BRIDGE_PROTOCOL_VERSION = 1;

export const nativeBridgeMessageTypes = {
  ping: 'lensDocs.native.ping',
  pong: 'lensDocs.native.pong',
  openFile: 'lensDocs.native.openFile',
  openFileResult: 'lensDocs.native.openFileResult',
  saveFile: 'lensDocs.native.saveFile',
  saveFileResult: 'lensDocs.native.saveFileResult',
  saveFileAs: 'lensDocs.native.saveFileAs',
  saveFileAsResult: 'lensDocs.native.saveFileAsResult',
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
  const webview = getWebView(windowRef);

  if (webview && typeof webview.addEventListener === 'function') {
    webview.addEventListener('message', (event) => {
      handleHostMessage(event?.data);
    });
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

  async function hasCapability(capability) {
    const result = await ping();
    if (!result.ok) return false;
    const capabilities = result.response?.payload?.capabilities;
    return Array.isArray(capabilities) && capabilities.includes(capability);
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

  function sendMessage(message, expectedTypes) {
    return new Promise((resolve) => {
      const currentWebView = getWebView(windowRef);
      if (!currentWebView) {
        resolve({
          ok: false,
          available: false,
          reason: 'unavailable',
          message: 'Native bridge unavailable.',
        });
        return;
      }

      const timeoutId = windowRef.setTimeout?.(() => {
        pendingMessages.delete(message.id);
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
    if (!pending) return;

    clearPending(message.id);
    const validation = validateHostResponse(message, pending.expectedTypes);
    if (!validation.ok) {
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
  }

  function clearPending(id) {
    const pending = pendingMessages.get(id);
    if (pending?.timeoutId) {
      windowRef.clearTimeout?.(pending.timeoutId);
    }
    pendingMessages.delete(id);
  }

  return {
    isAvailable,
    createMessage,
    ping,
    hasCapability,
    openFile,
    saveFile,
    saveFileAs,
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

function getSafeErrorMessage(message) {
  const errorMessage = message?.payload?.message;
  return typeof errorMessage === 'string' && errorMessage.trim()
    ? errorMessage
    : 'Native bridge returned a safe diagnostic error.';
}
