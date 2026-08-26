import { createNativeBridgeClient } from './native-bridge-client.js';

const REQUIRED_CAPABILITIES = [
  'diagnostics.ping',
  'file.startupOpen',
  'file.open',
  'file.save',
  'file.saveAs',
  'workspace.openFolder',
  'workspace.saveFile',
  'workspace.watch',
  'workspace.refreshFile',
];

const SINGLE_FILE_UPDATED = '# Windows Smoke Single File\n\nSaved by the automated native bridge smoke.\n';
const STARTUP_FILE_INITIAL = '# Startup file\n\nInitial command-line fixture content.\n';
const STARTUP_FILE_UPDATED = '# Windows Smoke Startup File\n\nSaved by the automated native bridge smoke.\n';
const SAVE_AS_CONTENT = '# Windows Smoke Save As\n\nWritten by the automated native bridge smoke.\n';
const WORKSPACE_UPDATED = '# Windows Smoke Workspace\n\nSaved by the automated native bridge smoke.\n';
const CREATED_WORKSPACE_CONTENT = '# Windows Smoke Created File\n\nCreated by the automated native bridge smoke.\n';

export async function runNativeBridgeSmoke({
  windowRef = window,
  bridgeClient = createNativeBridgeClient({ windowRef, timeoutMs: 10000 }),
} = {}) {
  if (!bridgeClient.isAvailable()) {
    return { ran: false, reason: 'unavailable' };
  }

  const steps = [];
  const errors = [];

  const recordStep = (name, passed, details = {}) => {
    steps.push({ name, passed: Boolean(passed), ...details });
    if (!passed) {
      errors.push(details.message || `${name} failed`);
    }
  };

  let capabilities = [];
  try {
    const ping = await bridgeClient.ping();
    const payload = ping.response?.payload || {};
    capabilities = Array.isArray(payload.capabilities) ? payload.capabilities : [];
    const isMockNativeBridge = Boolean(windowRef.__nativeBridgeScenario);

    if (!capabilities.includes('smoke.nativeFixtures')) {
      return { ran: false, reason: 'smoke-capability-absent' };
    }

    recordStep('Windows shell started', ping.ok && payload.host === 'LensDocsStudio.Windows', {
      host: payload.host || '',
      message: ping.message,
    });
    recordStep('WebView2 app loaded', Boolean(windowRef.document?.querySelector('#editor')));
    recordStep('WebView2 loaded packaged static assets', isMockNativeBridge || windowRef.location?.origin === 'https://lens-docs-studio.local', {
      origin: windowRef.location?.origin || '',
      skipped: isMockNativeBridge,
    });
    recordStep('WebView2 did not require a local HTTP server', isMockNativeBridge || !/^https?:\/\/(?:localhost|127\.0\.0\.1|\[::1\])/i.test(windowRef.location?.href || ''), {
      href: windowRef.location?.href || '',
      skipped: isMockNativeBridge,
    });
    recordStep('Bridge ping returned LensDocsStudio.Windows', ping.ok && payload.host === 'LensDocsStudio.Windows');

    for (const capability of REQUIRED_CAPABILITIES) {
      recordStep(`Capabilities include ${capability}`, capabilities.includes(capability));
    }

    recordStep('Capabilities include smoke.nativeFixtures', capabilities.includes('smoke.nativeFixtures'));

    const startupLoaded = await waitForEditorValue(windowRef, STARTUP_FILE_INITIAL, 7000);
    recordStep('Startup file argument loaded', isMockNativeBridge || startupLoaded, {
      skipped: isMockNativeBridge,
      message: startupLoaded ? 'Startup file content loaded.' : 'Startup file content was not loaded.',
    });

    if (startupLoaded) {
      const editor = windowRef.document?.querySelector('#editor');
      const saveButton = windowRef.document?.querySelector('#saveButton');
      editor.value = STARTUP_FILE_UPDATED;
      const InputEventCtor = windowRef.InputEvent || windowRef.Event;
      editor.dispatchEvent(new InputEventCtor('input', { bubbles: true, inputType: 'insertText', data: '' }));
      saveButton?.click();
      const saved = await waitForStatus(windowRef, 'startup-file.md saved.', 7000);
      recordStep('Startup file argument saved through active file handle', saved, {
        message: saved ? 'Startup file save completed.' : 'Timed out waiting for startup file save.',
      });
    } else {
      recordStep('Startup file argument saved through active file handle', isMockNativeBridge, {
        skipped: isMockNativeBridge,
        message: 'Startup file was not loaded.',
      });
    }

    const fixtureFile = await bridgeClient.openSmokeFixtureFile();
    const fixturePayload = fixtureFile.response?.payload || {};
    recordStep('Single fixture file opened', fixtureFile.ok && isValidFilePayload(fixturePayload), {
      fileName: fixturePayload.name || '',
      message: fixtureFile.message,
    });

    const editor = windowRef.document?.querySelector('#editor');
    if (editor) {
      editor.value = SINGLE_FILE_UPDATED;
      const InputEventCtor = windowRef.InputEvent || windowRef.Event;
      editor.dispatchEvent(new InputEventCtor('input', { bubbles: true, inputType: 'insertText', data: '' }));
    }

    const savedFile = await bridgeClient.saveFile({
      nativeHandleId: fixturePayload.nativeHandleId,
      content: SINGLE_FILE_UPDATED,
    });
    recordStep('Single fixture file saved', savedFile.ok && savedFile.response?.payload?.saved === true, {
      message: savedFile.message,
    });

    const savedAs = await bridgeClient.saveSmokeFixtureFileAs({
      content: SAVE_AS_CONTENT,
    });
    recordStep('Save-as wrote a new file', savedAs.ok && savedAs.response?.payload?.saved === true, {
      fileName: savedAs.response?.payload?.name || '',
      message: savedAs.message,
    });

    const workspace = await bridgeClient.openSmokeFixtureWorkspace();
    const workspacePayload = workspace.response?.payload || {};
    recordStep('Fixture workspace opened', workspace.ok && isValidWorkspacePayload(workspacePayload), {
      workspaceName: workspacePayload.workspaceName || '',
      message: workspace.message,
    });

    const workspaceFile = (workspacePayload.files || []).find((file) => file.path === 'README.md')
      || (workspacePayload.files || [])[0];
    recordStep('Workspace file selected', Boolean(workspaceFile?.nativeHandleId), {
      path: workspaceFile?.path || '',
    });

    const savedWorkspaceFile = await bridgeClient.saveWorkspaceFile({
      nativeHandleId: workspaceFile?.nativeHandleId,
      content: WORKSPACE_UPDATED,
    });
    recordStep('Workspace file saved', savedWorkspaceFile.ok && savedWorkspaceFile.response?.payload?.saved === true, {
      path: savedWorkspaceFile.response?.payload?.path || workspaceFile?.path || '',
      message: savedWorkspaceFile.message,
    });

    if (capabilities.includes('workspace.createFile')) {
      const created = await bridgeClient.createWorkspaceFile({
        nativeWorkspaceId: workspacePayload.nativeWorkspaceId,
        path: 'notes/smoke-created.md',
        content: CREATED_WORKSPACE_CONTENT,
      });
      recordStep('Workspace file created, if capability exists', created.ok && created.response?.payload?.created === true, {
        path: created.response?.payload?.path || '',
        message: created.message,
      });
    } else {
      recordStep('Workspace file created, if capability exists', true, {
        skipped: true,
        message: 'workspace.createFile capability absent.',
      });
    }

    if (capabilities.includes('smoke.workspaceChange') && typeof bridgeClient.on === 'function') {
      const watcherEventPromise = waitForWorkspaceChange(bridgeClient, workspacePayload.nativeWorkspaceId, 'docs/overview.md');
      const touched = await bridgeClient.touchSmokeWorkspaceFile();
      recordStep('Smoke workspace file externally changed', touched.ok && touched.response?.payload?.changed === true, {
        path: touched.response?.payload?.path || '',
        message: touched.message,
      });
      const watcherEvent = await watcherEventPromise;
      recordStep('Workspace watcher event received', watcherEvent.received, {
        path: watcherEvent.path || '',
        message: watcherEvent.message || '',
      });
      recordStep('Workspace watcher event used a relative path', watcherEvent.received && watcherEvent.relativeOnly, {
        path: watcherEvent.path || '',
      });
    } else {
      recordStep('Workspace watcher event received', true, {
        skipped: true,
        message: 'smoke.workspaceChange capability absent.',
      });
      recordStep('Workspace watcher event used a relative path', true, {
        skipped: true,
        message: 'smoke.workspaceChange capability absent.',
      });
    }

    recordStep('Browser app did not report bridge protocol error', !errors.some((error) => /protocol/i.test(error)));
  } catch (error) {
    recordStep('Smoke completed without runner exception', false, {
      message: error?.message || String(error),
    });
  }

  const success = steps.length > 0 && steps.every((step) => step.passed);
  const result = {
    success,
    completedAt: new Date().toISOString(),
    steps,
    errors,
  };

  try {
    await bridgeClient.completeSmoke(result);
  } catch {
    // The host may already be exiting. The smoke script validates the result file.
  }

  return { ran: true, result };
}

function waitForWorkspaceChange(bridgeClient, nativeWorkspaceId, expectedPath) {
  return new Promise((resolve) => {
    const timeout = setTimeout(() => {
      unsubscribe();
      resolve({ received: false, message: 'Timed out waiting for workspace watcher event.' });
    }, 7000);

    const unsubscribe = bridgeClient.on('lensDocs.native.workspaceChanged', (message) => {
      const payload = message?.payload || {};
      if (payload.nativeWorkspaceId !== nativeWorkspaceId || !Array.isArray(payload.changes)) return;
      const change = payload.changes.find((item) => item?.path === expectedPath);
      if (!change) return;
      clearTimeout(timeout);
      unsubscribe();
      const path = String(change.path || '');
      resolve({
        received: true,
        path,
        relativeOnly: Boolean(path && !path.startsWith('/') && !/^[a-z]:/i.test(path) && !path.includes('\\')),
      });
    });
  });
}

function waitForEditorValue(windowRef, expectedValue, timeoutMs) {
  return waitUntil(timeoutMs, () => windowRef.document?.querySelector('#editor')?.value === expectedValue);
}

function waitForStatus(windowRef, expectedText, timeoutMs) {
  return waitUntil(timeoutMs, () => windowRef.document?.querySelector('#status')?.textContent?.trim() === expectedText);
}

function waitUntil(timeoutMs, predicate) {
  return new Promise((resolve) => {
    const startedAt = Date.now();
    const timer = setInterval(() => {
      if (predicate()) {
        clearInterval(timer);
        resolve(true);
        return;
      }

      if (Date.now() - startedAt >= timeoutMs) {
        clearInterval(timer);
        resolve(false);
      }
    }, 100);
  });
}

function isValidFilePayload(payload) {
  return payload
    && payload.cancelled !== true
    && typeof payload.name === 'string'
    && typeof payload.content === 'string'
    && typeof payload.nativeHandleId === 'string'
    && payload.nativeHandleId.trim();
}

function isValidWorkspacePayload(payload) {
  return payload
    && payload.cancelled !== true
    && typeof payload.workspaceName === 'string'
    && typeof payload.nativeWorkspaceId === 'string'
    && payload.nativeWorkspaceId.trim()
    && Array.isArray(payload.files)
    && payload.files.length > 0;
}
