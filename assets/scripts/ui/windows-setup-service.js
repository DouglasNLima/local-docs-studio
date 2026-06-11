const SETUP_VERSION = 'phase-3e-mvp';
const WINDOWS_HOST = 'LensDocsStudio.Windows';
const SUPPORTED_EXTENSIONS = ['.md', '.markdown', '.mmd', '.mermaid', '.txt'];
const REGISTER_COMMAND = 'pwsh -NoLogo -NoProfile -File scripts/windows/Register-WindowsFileAssociations.ps1 -ExecutablePath "<path-to-LensDocsStudio.Windows.exe>"';

export function createWindowsSetupService({
  dom,
  callbacks,
  nativeBridgeClient,
  storageKeys,
  localStorageRef = localStorage,
  windowRef = window,
} = {}) {
  const {
    windowsSetupDialog,
    windowsSetupTitle,
    windowsSetupSummary,
    windowsSetupStepList,
    windowsSetupBody,
    windowsSetupBackButton,
    windowsSetupNextButton,
    windowsSetupSkipButton,
    windowsSetupCloseButton,
  } = dom;
  const {
    openWorkspace,
    openSample,
    openFeatureGuide,
    startBlankDocument,
    closeOpenMenus,
    setStatus,
  } = callbacks;

  const steps = [
    { id: 'welcome', label: 'Welcome' },
    { id: 'runtime', label: 'Runtime readiness' },
    { id: 'workspace', label: 'Workspace' },
    { id: 'associations', label: 'File associations' },
    { id: 'starter', label: 'Starter document' },
    { id: 'done', label: 'Done' },
  ];
  const summary = {
    runtimeReady: false,
    workspace: 'Not selected',
    fileAssociations: 'Guidance not opened',
    starter: 'Not selected',
  };
  let currentStepIndex = 0;
  let readiness = null;
  let readinessPromise = null;

  function installWindowsSetupHandlers() {
    if (!windowsSetupDialog) return;
    windowsSetupBackButton?.addEventListener('click', () => goToStep(currentStepIndex - 1));
    windowsSetupNextButton?.addEventListener('click', handleNext);
    windowsSetupSkipButton?.addEventListener('click', () => completeSetup('Setup skipped.'));
    windowsSetupCloseButton?.addEventListener('click', () => completeSetup('Setup skipped.'));
    windowsSetupDialog.addEventListener('cancel', (event) => {
      event.preventDefault();
      completeSetup('Setup skipped.');
    });
    windowsSetupDialog.addEventListener('click', (event) => {
      if (event.target === windowsSetupDialog) {
        completeSetup('Setup skipped.');
      }
    });
  }

  async function maybeOpenFirstRun() {
    if (isCompleted()) return;
    const probe = await getReadiness();
    if (!probe.isWindowsShell || probe.smokeMode) return;
    openWindowsSetupWizard({ manual: false, initialReadiness: probe });
  }

  async function openWindowsSetupWizard({ manual = true, initialReadiness = null } = {}) {
    closeOpenMenus?.();
    if (!windowsSetupDialog) return;
    if (!initialReadiness) {
      readinessPromise = null;
      readiness = null;
    } else {
      readiness = initialReadiness;
      readinessPromise = Promise.resolve(initialReadiness);
    }
    currentStepIndex = 0;
    renderStep();
    showDialog();

    const probe = await getReadiness();
    if (!probe.isWindowsShell && manual) {
      goToStep(1);
      setStatus?.('Windows setup is only available in the Windows desktop shell.', 'info');
      return;
    }
    if (currentStepIndex === 1 || currentStepIndex === 0) {
      renderStep();
    }
  }

  function isCompleted() {
    return localStorageRef.getItem(storageKeys.windowsSetupCompleted) === 'true';
  }

  async function getReadiness() {
    if (readiness) return readiness;
    if (readinessPromise) return readinessPromise;
    readinessPromise = resolveReadiness().then((result) => {
      readiness = result;
      return result;
    });
    return readinessPromise;
  }

  async function resolveReadiness() {
    const origin = windowRef.location?.origin || 'unknown';
    const result = {
      bridgeAvailable: nativeBridgeClient?.isAvailable?.() === true,
      isWindowsShell: false,
      smokeMode: false,
      host: '',
      appOrigin: origin,
      reportedOrigin: '',
      webView2Runtime: 'Not reported by host',
      capabilities: [],
      message: 'Windows bridge unavailable in this browser mode.',
    };
    if (!result.bridgeAvailable) return result;

    try {
      const ping = await nativeBridgeClient.ping();
      result.message = ping.message || result.message;
      if (!ping.ok) return result;
      const payload = ping.response?.payload || {};
      result.host = safeString(payload.host);
      result.reportedOrigin = safeString(payload.origin);
      result.webView2Runtime = resolveWebView2Runtime(payload);
      result.capabilities = Array.isArray(payload.capabilities)
        ? payload.capabilities.filter((capability) => typeof capability === 'string' && capability.trim())
        : [];
      result.isWindowsShell = result.host === WINDOWS_HOST && result.capabilities.includes('workspace.openFolder');
      result.smokeMode = result.capabilities.includes('smoke.nativeFixtures');
      return result;
    } catch {
      result.message = 'Windows bridge readiness check failed safely.';
      return result;
    }
  }

  function resolveWebView2Runtime(payload) {
    if (payload.webView2RuntimeAvailable === true) return 'Available';
    if (payload.webView2RuntimeAvailable === false) return 'Not reported as available';
    return 'Not reported by host';
  }

  function showDialog() {
    if (typeof windowsSetupDialog.showModal === 'function') {
      windowsSetupDialog.showModal();
      return;
    }
    windowsSetupDialog.setAttribute('open', '');
  }

  function closeDialog() {
    if (windowsSetupDialog?.open) {
      windowsSetupDialog.close();
    } else {
      windowsSetupDialog?.removeAttribute('open');
    }
  }

  function handleNext() {
    if (currentStepIndex >= steps.length - 1) {
      completeSetup('Setup completed.');
      return;
    }
    goToStep(currentStepIndex + 1);
  }

  function goToStep(nextIndex) {
    currentStepIndex = Math.max(0, Math.min(steps.length - 1, nextIndex));
    renderStep();
  }

  function renderStep() {
    if (!windowsSetupBody) return;
    const step = steps[currentStepIndex];
    windowsSetupTitle.textContent = step.label;
    windowsSetupSummary.textContent = getStepSummary(step.id);
    renderStepList();
    windowsSetupBody.innerHTML = '';
    const content = getStepContent(step.id);
    windowsSetupBody.appendChild(content);
    windowsSetupBackButton.disabled = currentStepIndex === 0;
    windowsSetupNextButton.textContent = currentStepIndex === steps.length - 1
      ? 'Start using Lens Docs Studio'
      : getNextLabel(step.id);
    windowsSetupSkipButton.hidden = currentStepIndex === steps.length - 1;
  }

  function renderStepList() {
    windowsSetupStepList.innerHTML = '';
    steps.forEach((step, index) => {
      const item = document.createElement('li');
      item.textContent = step.label;
      item.dataset.setupStepState = index === currentStepIndex ? 'active' : index < currentStepIndex ? 'done' : 'pending';
      windowsSetupStepList.appendChild(item);
    });
  }

  function getStepSummary(stepId) {
    if (stepId === 'welcome') return 'Set up the local Windows desktop shell.';
    if (stepId === 'runtime') return 'Check packaged local files and Windows bridge capabilities.';
    if (stepId === 'workspace') return 'Open a local documentation folder when you want workspace browsing.';
    if (stepId === 'associations') return 'Review safe per-user file association guidance.';
    if (stepId === 'starter') return 'Open a useful starting document or begin with a blank file.';
    return 'Finish setup and continue working locally.';
  }

  function getNextLabel(stepId) {
    if (stepId === 'welcome') return 'Get started';
    return 'Continue';
  }

  function getStepContent(stepId) {
    if (stepId === 'welcome') return renderWelcomeStep();
    if (stepId === 'runtime') return renderRuntimeStep();
    if (stepId === 'workspace') return renderWorkspaceStep();
    if (stepId === 'associations') return renderAssociationsStep();
    if (stepId === 'starter') return renderStarterStep();
    return renderDoneStep();
  }

  function renderWelcomeStep() {
    const fragment = document.createDocumentFragment();
    const paragraph = document.createElement('p');
    paragraph.className = 'windows-setup-lead';
    paragraph.textContent = 'Lens Docs Studio is a local Markdown, Mermaid, and documentation studio. The Windows desktop shell opens the same app from packaged local files, so you can work with local documents without a backend.';
    fragment.appendChild(paragraph);
    return fragment;
  }

  function renderRuntimeStep() {
    const fragment = document.createDocumentFragment();
    const list = document.createElement('div');
    list.className = 'windows-setup-status-list';
    const probe = readiness;
    const origin = probe?.reportedOrigin || probe?.appOrigin || windowRef.location?.origin || 'unknown';
    const capabilities = probe?.capabilities || [];
    const requiredCapabilities = [
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
    [
      ['Windows host detected', probe?.isWindowsShell ? 'Ready' : 'Unavailable', probe?.isWindowsShell ? 'ok' : 'warning'],
      ['Native bridge available', probe?.bridgeAvailable ? 'Ready' : 'Unavailable', probe?.bridgeAvailable ? 'ok' : 'warning'],
      ['Offline packaged assets loaded', isPackagedOrigin(origin) ? 'Packaged origin' : 'Static app loaded', 'ok'],
      ['App origin', origin, 'info'],
      ['WebView2 runtime', probe?.webView2Runtime || 'Not reported by host', probe?.webView2Runtime === 'Available' ? 'ok' : 'info'],
    ].forEach(([label, value, kind]) => {
      list.appendChild(createStatusRow(label, value, kind));
    });

    const capabilityList = document.createElement('div');
    capabilityList.className = 'windows-setup-capabilities';
    requiredCapabilities.forEach((capability) => {
      const chip = document.createElement('span');
      chip.dataset.capability = capability;
      chip.dataset.ready = String(capabilities.includes(capability));
      chip.textContent = capability;
      capabilityList.appendChild(chip);
    });
    fragment.append(list, capabilityList);
    summary.runtimeReady = Boolean(probe?.isWindowsShell && probe?.bridgeAvailable && capabilities.includes('diagnostics.ping'));
    return fragment;
  }

  function renderWorkspaceStep() {
    const fragment = document.createDocumentFragment();
    const paragraph = document.createElement('p');
    paragraph.className = 'windows-setup-lead';
    paragraph.textContent = 'Open a folder when you want workspace-style browsing, search, saves, and file watching. Open a single file later for quick edits. If Open folder does not work, use Help > Windows shell diagnostics.';
    const actions = document.createElement('div');
    actions.className = 'windows-setup-actions';
    actions.append(
      createActionButton('Open a workspace folder', async () => {
        await openWorkspace?.();
        summary.workspace = 'Workspace picker opened';
        goToStep(currentStepIndex + 1);
      }, { primary: true }),
      createActionButton('Continue without workspace', () => {
        summary.workspace = 'Skipped';
        goToStep(currentStepIndex + 1);
      }),
    );
    fragment.append(paragraph, actions);
    return fragment;
  }

  function renderAssociationsStep() {
    const fragment = document.createDocumentFragment();
    const paragraph = document.createElement('p');
    paragraph.className = 'windows-setup-lead';
    paragraph.textContent = 'File association setup is guidance-only in this wizard. It does not write registry keys.';
    const extensions = document.createElement('p');
    extensions.className = 'windows-setup-muted';
    extensions.textContent = `Supported extensions: ${SUPPORTED_EXTENSIONS.join(', ')}.`;
    const code = document.createElement('code');
    code.className = 'windows-setup-command';
    code.textContent = REGISTER_COMMAND;
    const actions = document.createElement('div');
    actions.className = 'windows-setup-actions';
    actions.append(
      createActionButton('Copy command', async () => {
        await copyText(REGISTER_COMMAND);
        summary.fileAssociations = 'Guidance command copied';
      }, { primary: true }),
      createActionButton('I will do this later', () => {
        summary.fileAssociations = 'Guidance shown';
        goToStep(currentStepIndex + 1);
      }),
    );
    fragment.append(paragraph, extensions, code, actions);
    summary.fileAssociations = 'Guidance shown';
    return fragment;
  }

  function renderStarterStep() {
    const fragment = document.createDocumentFragment();
    const paragraph = document.createElement('p');
    paragraph.className = 'windows-setup-lead';
    paragraph.textContent = 'Choose a starter document, or skip this step and keep the current workspace.';
    const actions = document.createElement('div');
    actions.className = 'windows-setup-actions';
    actions.append(
      createActionButton('Open Markdown + Mermaid sample', async () => {
        openSample?.();
        summary.starter = 'Markdown + Mermaid sample';
        goToStep(currentStepIndex + 1);
      }, { primary: true }),
      createActionButton('Open feature guide', async () => {
        await openFeatureGuide?.();
        summary.starter = 'Feature guide';
        goToStep(currentStepIndex + 1);
      }),
      createActionButton('Start blank Markdown file', async () => {
        await startBlankDocument?.();
        summary.starter = 'Blank Markdown file';
        goToStep(currentStepIndex + 1);
      }),
      createActionButton('Skip starter', () => {
        summary.starter = 'Skipped';
        goToStep(currentStepIndex + 1);
      }),
    );
    fragment.append(paragraph, actions);
    return fragment;
  }

  function renderDoneStep() {
    const fragment = document.createDocumentFragment();
    const list = document.createElement('div');
    list.className = 'windows-setup-status-list';
    list.append(
      createStatusRow('Runtime ready', summary.runtimeReady ? 'Ready' : 'Review later', summary.runtimeReady ? 'ok' : 'warning'),
      createStatusRow('Workspace', summary.workspace, summary.workspace === 'Skipped' ? 'info' : 'ok'),
      createStatusRow('File association guidance', summary.fileAssociations, 'info'),
      createStatusRow('Starter', summary.starter, summary.starter === 'Skipped' ? 'info' : 'ok'),
    );
    fragment.appendChild(list);
    return fragment;
  }

  function createStatusRow(label, value, kind) {
    const row = document.createElement('div');
    row.className = 'windows-setup-status-row';
    row.dataset.statusKind = kind;
    const labelElement = document.createElement('span');
    labelElement.textContent = label;
    const valueElement = document.createElement('strong');
    valueElement.textContent = value;
    row.append(labelElement, valueElement);
    return row;
  }

  function createActionButton(label, handler, { primary = false } = {}) {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = label;
    if (primary) button.className = 'primary';
    button.addEventListener('click', async () => {
      button.disabled = true;
      try {
        await handler();
      } finally {
        button.disabled = false;
      }
    });
    return button;
  }

  async function copyText(text) {
    try {
      await windowRef.navigator?.clipboard?.writeText(text);
      setStatus?.('File association command copied.', 'ok');
    } catch {
      setStatus?.('Copy blocked. Select the command text and copy it manually.', 'warning');
    }
  }

  function completeSetup(statusText) {
    localStorageRef.setItem(storageKeys.windowsSetupCompleted, 'true');
    localStorageRef.setItem(storageKeys.windowsSetupCompletedAt, new Date().toISOString());
    localStorageRef.setItem(storageKeys.windowsSetupVersion, SETUP_VERSION);
    closeDialog();
    setStatus?.(statusText, 'ok');
  }

  return {
    installWindowsSetupHandlers,
    maybeOpenFirstRun,
    openWindowsSetupWizard,
    isCompleted,
  };
}

function safeString(value) {
  return typeof value === 'string' ? value.trim() : '';
}

function isPackagedOrigin(origin) {
  return /^https:\/\/lens-docs-studio\.local\/?$/i.test(origin || '');
}
