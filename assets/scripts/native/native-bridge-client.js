export const BRIDGE_PROTOCOL_VERSION = 1;

export const nativeBridgeMessageTypes = {
  ping: 'lensDocs.native.ping',
  pong: 'lensDocs.native.pong',
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
    return await sendMessage(message);
  }

  function sendMessage(message) {
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
    const validation = validateHostResponse(message);
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
      ok: message.type === nativeBridgeMessageTypes.pong,
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

function validateHostResponse(message) {
  if (message.protocolVersion !== BRIDGE_PROTOCOL_VERSION) {
    return {
      ok: false,
      reason: 'unsupported-protocol',
      message: 'Native bridge returned an unsupported protocol response.',
    };
  }
  if (![nativeBridgeMessageTypes.pong, nativeBridgeMessageTypes.error].includes(message.type)) {
    return {
      ok: false,
      reason: 'unsupported-message',
      message: 'Native bridge returned an unsupported diagnostic response.',
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
