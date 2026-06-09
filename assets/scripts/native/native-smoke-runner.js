import { createNativeBridgeClient } from './native-bridge-client.js';

const REQUIRED_CAPABILITIES = [
  'diagnostics.ping',
  'file.open',
  'file.save',
  'file.saveAs',
  'workspace.openFolder',
  'workspace.saveFile',
];

const SINGLE_FILE_UPDATED = '# Windows Smoke Single File\n\nSaved by the automated native bridge smoke.\n';
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

    if (!capabilities.includes('smoke.nativeFixtures')) {
      return { ran: false, reason: 'smoke-capability-absent' };
    }

    recordStep('Windows shell started', ping.ok && payload.host === 'LensDocsStudio.Windows', {
      host: payload.host || '',
      message: ping.message,
    });
    recordStep('WebView2 app loaded', Boolean(windowRef.document?.querySelector('#editor')));
    recordStep('Bridge ping returned LensDocsStudio.Windows', ping.ok && payload.host === 'LensDocsStudio.Windows');

    for (const capability of REQUIRED_CAPABILITIES) {
      recordStep(`Capabilities include ${capability}`, capabilities.includes(capability));
    }

    recordStep('Capabilities include smoke.nativeFixtures', capabilities.includes('smoke.nativeFixtures'));

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
