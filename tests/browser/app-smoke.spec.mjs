import { expect, test } from '@playwright/test';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createArtifactBundleFixtureZip } from './helpers/fixtures.mjs';
import { createZipBuffer, getZipText, readZipEntries } from './helpers/zip.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const tinyPngBase64 = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=';
const tinySvg = '<svg xmlns="http://www.w3.org/2000/svg" onload="window.__auditXss=1"><rect width="1" height="1"/></svg>';

function fixturePath(name) {
  return path.join(root, 'tests', 'fixtures', name);
}

function normaliseLineEndings(value) {
  return String(value).replace(/\r\n?/g, '\n');
}

async function waitForAppReady(page) {
  const diagnostics = [];
  const recordConsole = (message) => {
    if (['error', 'warning'].includes(message.type())) {
      diagnostics.push(`${message.type()}: ${message.text()}`);
    }
  };
  const recordPageError = (error) => {
    diagnostics.push(`pageerror: ${error.message}`);
  };

  page.on('console', recordConsole);
  page.on('pageerror', recordPageError);

  try {
    for (let attempt = 1; attempt <= 2; attempt += 1) {
      try {
        await expect(page.locator('html')).toHaveAttribute('data-app-ready', 'true');
        return;
      } catch (error) {
        if (attempt === 2) {
          const details = diagnostics.length
            ? `\nApp bootstrap diagnostics:\n${diagnostics.join('\n')}`
            : '';
          throw new Error(`${error.message}${details}`);
        }
        await page.reload({ waitUntil: 'domcontentloaded' });
      }
    }
  } finally {
    page.off('console', recordConsole);
    page.off('pageerror', recordPageError);
  }
}

async function gotoApp(page) {
  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await waitForAppReady(page);
}

async function openFixture(page, name) {
  await gotoApp(page);
  const fileInput = page.locator('#fileInput');
  const fileName = path.basename(name);

  for (let attempt = 1; attempt <= 2; attempt += 1) {
    await fileInput.setInputFiles([]);
    await fileInput.setInputFiles(fixturePath(name));
    try {
      await expect(page.locator('#activeFileLabel')).toContainText(fileName, { timeout: 5_000 });
      break;
    } catch (error) {
      if (attempt === 2) throw error;
    }
  }

  await expect(page.locator('#status')).toHaveText(/Rendered/, { timeout: 60_000 });
}

async function loadSample(page) {
  await page.locator('summary').filter({ hasText: /^Create$/ }).click();
  await page.getByRole('button', { name: 'Markdown + Mermaid sample' }).click();
  await expect(page.locator('#status')).toHaveText(/Rendered/, { timeout: 60_000 });
}

async function expectMobileMenuPanelUsable(page, menuName) {
  await page.locator('summary').filter({ hasText: new RegExp(`^${menuName}$`) }).click();
  const panel = page.locator('details.menu[open] .menu-panel');
  await expect(panel).toBeVisible();

  const metrics = await panel.evaluate((element) => {
    const panelRect = element.getBoundingClientRect();
    const topbarRect = document.querySelector('.topbar')?.getBoundingClientRect();
    const firstButton = element.querySelector('button:not(:disabled)') || element.querySelector('button');
    const buttonRect = firstButton?.getBoundingClientRect();
    const target = buttonRect
      ? document.elementFromPoint(buttonRect.left + buttonRect.width / 2, buttonRect.top + buttonRect.height / 2)
      : null;

    return {
      panel: {
        x: panelRect.x,
        y: panelRect.y,
        right: panelRect.right,
        bottom: panelRect.bottom,
        width: panelRect.width,
        height: panelRect.height,
        clientHeight: element.clientHeight,
        scrollHeight: element.scrollHeight,
      },
      topbarBottom: topbarRect?.bottom ?? 0,
      firstButtonClickable: Boolean(firstButton && (target === firstButton || firstButton.contains(target))),
      horizontalOverflow: document.documentElement.scrollWidth > window.innerWidth + 1,
    };
  });
  const viewport = page.viewportSize();

  expect(metrics.panel.x).toBeGreaterThanOrEqual(0);
  expect(metrics.panel.y).toBeGreaterThanOrEqual(metrics.topbarBottom - 1);
  expect(metrics.panel.right).toBeLessThanOrEqual(viewport.width + 1);
  expect(metrics.panel.bottom).toBeLessThanOrEqual(viewport.height + 1);
  expect(metrics.panel.height).toBeGreaterThan(160);
  expect(metrics.panel.clientHeight).toBeGreaterThan(120);
  expect(metrics.firstButtonClickable).toBe(true);
  expect(metrics.horizontalOverflow).toBe(false);
}

async function renderPreviewFromViewMenu(page) {
  await page.locator('summary').filter({ hasText: /^View$/ }).click();
  await page.getByRole('button', { name: 'Render preview' }).click();
  await expect(page.locator('#status')).toHaveText(/Rendered/, { timeout: 60_000 });
}

async function renderPreviewWithShortcut(page) {
  await page.locator('#editor').focus();
  await page.keyboard.press(process.platform === 'darwin' ? 'Meta+Enter' : 'Control+Enter');
  await expect(page.locator('#status')).toHaveText(/Rendered/, { timeout: 60_000 });
}

async function dropTinyPngOnEditor(page) {
  await page.evaluate((base64) => {
    const bytes = Uint8Array.from(atob(base64), (char) => char.charCodeAt(0));
    const file = new File([bytes], 'tiny-image.png', { type: 'image/png' });
    const transfer = new DataTransfer();
    transfer.items.add(file);
    const target = document.querySelector('#editorShell');
    target.dispatchEvent(new DragEvent('drop', {
      bubbles: true,
      cancelable: true,
      dataTransfer: transfer,
    }));
  }, tinyPngBase64);
}

async function pasteTinyPngIntoEditor(page) {
  await page.locator('#editor').evaluate((editor, base64) => {
    const bytes = Uint8Array.from(atob(base64), (char) => char.charCodeAt(0));
    const file = new File([bytes], 'clipboard-image.png', { type: 'image/png' });
    const transfer = new DataTransfer();
    transfer.items.add(file);
    editor.focus();
    const event = new ClipboardEvent('paste', { bubbles: true, cancelable: true });
    Object.defineProperty(event, 'clipboardData', { value: transfer });
    editor.dispatchEvent(event);
  }, tinyPngBase64);
}

async function dropTinySvgOnEditor(page) {
  await page.evaluate((source) => {
    const file = new File([source], 'blocked-image.svg', { type: 'image/svg+xml' });
    const transfer = new DataTransfer();
    transfer.items.add(file);
    const target = document.querySelector('#editorShell');
    target.dispatchEvent(new DragEvent('drop', {
      bubbles: true,
      cancelable: true,
      dataTransfer: transfer,
    }));
  }, tinySvg);
}

async function dropVirtualFile(page, { name, type, text = '', base64 = '' }) {
  await page.evaluate((payload) => {
    const bytes = payload.base64
      ? Uint8Array.from(atob(payload.base64), (char) => char.charCodeAt(0))
      : payload.text;
    const file = new File([bytes], payload.name, { type: payload.type });
    const transfer = new DataTransfer();
    transfer.items.add(file);
    window.dispatchEvent(new DragEvent('drop', {
      bubbles: true,
      cancelable: true,
      dataTransfer: transfer,
    }));
  }, { name, type, text, base64 });
}

async function attachViewportScreenshot(page, testInfo, name) {
  const screenshotPath = testInfo.outputPath(`${name}.png`);
  await page.screenshot({ animations: 'disabled', fullPage: false, path: screenshotPath });
  await testInfo.attach(name, {
    path: screenshotPath,
    contentType: 'image/png',
  });
}

async function clickExportDownload(page, buttonName) {
  const button = page.getByRole('button', { name: buttonName });
  if (!(await button.isVisible())) {
    await page.locator('summary').filter({ hasText: /^Export$/ }).click();
  }

  const [download] = await Promise.all([
    page.waitForEvent('download'),
    button.click(),
  ]);
  expect(await download.failure()).toBeNull();
  return download.path();
}

async function writeZipFixture(testInfo, name, files) {
  const zipPath = testInfo.outputPath(name);
  await writeFile(zipPath, createZipBuffer(files, { compress: true }));
  return zipPath;
}

async function writeArtifactBundleFixtureZip(testInfo, fixtureName, name = `${fixtureName}.zip`) {
  const zipPath = testInfo.outputPath(name);
  await writeFile(zipPath, await createArtifactBundleFixtureZip(fixtureName));
  return zipPath;
}

async function tabUntilFocused(page, selector, maxTabs = 40) {
  await page.locator('body').click({ position: { x: 4, y: 4 } });
  for (let index = 0; index < maxTabs; index += 1) {
    await page.keyboard.press('Tab');
    if (await page.evaluate((targetSelector) => document.activeElement?.matches(targetSelector), selector)) {
      return;
    }
  }
  throw new Error(`Focused element matching ${selector} was not reached with Tab.`);
}

async function clickDocsSiteExportDownload(page, { title = 'Publishing Docs', description = 'Docs Site Builder 2.0 export fixture.' } = {}) {
  const button = page.getByRole('button', { name: 'Export Docs Site' });
  if (!(await button.isVisible())) {
    await page.locator('summary').filter({ hasText: /^Export$/ }).click();
  }

  await button.click();
  await expect(page.getByRole('heading', { name: 'Export Docs Site' })).toBeVisible();
  await page.getByLabel('Site title').fill(title);
  await page.getByLabel('Short description').fill(description);
  await page.getByLabel('Initial theme').selectOption('system');

  const [download] = await Promise.all([
    page.waitForEvent('download'),
    page.getByRole('button', { name: 'Export site' }).click(),
  ]);
  expect(await download.failure()).toBeNull();
  return download.path();
}

async function clickExportAction(page, buttonName) {
  const button = page.getByRole('button', { name: buttonName });
  if (!(await button.isVisible())) {
    await page.locator('summary').filter({ hasText: /^Export$/ }).click();
  }
  await button.click();
}

async function setEditorValueAndSelection(page, value, start = 0, end = value.length) {
  await page.locator('#editor').evaluate((editor, payload) => {
    editor.value = payload.value;
    editor.focus();
    editor.setSelectionRange(payload.start, payload.end);
    editor.dispatchEvent(new Event('input', { bubbles: true }));
  }, { value, start, end });
}

async function pasteIntoEditor(page, { text = '', html = '' }) {
  await page.locator('#editor').evaluate((editor, payload) => {
    editor.focus();
    const transfer = new DataTransfer();
    if (payload.html) transfer.setData('text/html', payload.html);
    if (payload.text) transfer.setData('text/plain', payload.text);

    const event = new ClipboardEvent('paste', { bubbles: true, cancelable: true });
    Object.defineProperty(event, 'clipboardData', { value: transfer });
    editor.dispatchEvent(event);

    if (!event.defaultPrevented && payload.text) {
      const start = editor.selectionStart;
      const end = editor.selectionEnd;
      editor.value = `${editor.value.slice(0, start)}${payload.text}${editor.value.slice(end)}`;
      const cursor = start + payload.text.length;
      editor.setSelectionRange(cursor, cursor);
      editor.dispatchEvent(new InputEvent('input', {
        bubbles: true,
        inputType: 'insertFromPaste',
        data: payload.text,
      }));
    }
  }, { text, html });
}

async function mockClipboardRead(page, { text = '', html = '', reject = false } = {}) {
  await page.evaluate((payload) => {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: payload.reject
        ? {
            read: async () => { throw new Error('Clipboard blocked'); },
            readText: async () => { throw new Error('Clipboard blocked'); },
          }
        : {
            read: async () => [{
              types: [
                ...(payload.html ? ['text/html'] : []),
                ...(payload.text !== undefined ? ['text/plain'] : []),
              ],
              getType: async (type) => new Blob([type === 'text/html' ? payload.html : payload.text], { type }),
            }],
            readText: async () => payload.text,
          },
    });
  }, { text, html, reject });
}

async function mockClipboardWrite(page, { readText = '' } = {}) {
  await page.evaluate((payload) => {
    window.__copiedText = '';
    window.__copiedHtml = '';
    window.__clipboardWriteTypes = [];
    class MockClipboardItem {
      constructor(items) {
        this.items = items;
      }
    }
    Object.defineProperty(window, 'ClipboardItem', {
      configurable: true,
      value: MockClipboardItem,
    });
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        readText: async () => payload.readText,
        writeText: async (text) => {
          window.__copiedText = text;
        },
        write: async (items) => {
          const item = items[0];
          window.__clipboardWriteTypes = Object.keys(item.items || {});
          if (item.items?.['text/plain']) {
            window.__copiedText = await item.items['text/plain'].text();
          }
          if (item.items?.['text/html']) {
            window.__copiedHtml = await item.items['text/html'].text();
          }
        },
      },
    });
  }, { readText });
}

async function submitAppDialog(page, { value, button = 'OK' } = {}) {
  await expect(page.locator('#appDialog')).toBeVisible();
  if (value !== undefined) {
    await page.locator('#appDialogPromptInput').fill(value);
  }
  await page.getByRole('button', { name: button }).click();
  await expect(page.locator('#appDialog')).toBeHidden();
}

async function submitAppDialogIfVisible(page, { value, button = 'OK' } = {}) {
  const dialog = page.locator('#appDialog');
  const visible = await dialog.waitFor({ state: 'visible', timeout: 1000 }).then(() => true, () => false);
  if (!visible) return false;
  if (value !== undefined) {
    await page.locator('#appDialogPromptInput').fill(value);
  }
  await page.getByRole('button', { name: button }).click();
  await expect(dialog).toBeHidden();
  return true;
}

async function clickEditAction(page, name) {
  await page.locator('summary').filter({ hasText: /^Edit$/ }).click();
  await page.getByRole('button', { name }).click();
}

async function loadVirtualWorkspace(page, files) {
  await gotoApp(page);
  await page.evaluate((items) => {
    window.confirm = () => true;
    const transfer = new DataTransfer();
    items.forEach((item) => {
      transfer.items.add(new File([item.text], item.name, { type: item.mimeType || 'text/markdown' }));
    });
    window.dispatchEvent(new DragEvent('drop', {
      bubbles: true,
      cancelable: true,
      dataTransfer: transfer,
    }));
  }, files);
  await expect(page.locator('#status')).toHaveText(/Rendered/);
}

async function installMockFileSystemAccess(page) {
  await page.addInitScript(() => {
    function createFileHandle(name, text = '') {
      return {
        kind: 'file',
        name,
        permission: 'granted',
        _text: text,
        _lastModified: Date.now(),
        async getFile() {
          return new File([this._text], this.name, {
            type: this.name.endsWith('.mmd') || this.name.endsWith('.mermaid') ? 'text/plain' : 'text/markdown',
            lastModified: this._lastModified,
          });
        },
        async createWritable() {
          const handle = this;
          let nextText = '';
          return {
            async write(value) {
              nextText += typeof value === 'string' ? value : await new Response(value).text();
            },
            async close() {
              handle._text = nextText;
              handle._lastModified += 1000;
            },
          };
        },
        async queryPermission() {
          return this.permission;
        },
        async requestPermission() {
          return this.permission;
        },
      };
    }

    function createDirectoryHandle(name) {
      const files = new Map();
      const directories = new Map();
      return {
        kind: 'directory',
        name,
        permission: 'granted',
        files,
        directories,
        async *entries() {
          for (const entry of files.entries()) yield entry;
          for (const entry of directories.entries()) yield entry;
        },
        async getFileHandle(fileName, options = {}) {
          if (!files.has(fileName)) {
            if (!options.create) throw new DOMException('Not found', 'NotFoundError');
            files.set(fileName, createFileHandle(fileName, ''));
          }
          return files.get(fileName);
        },
        async getDirectoryHandle(directoryName, options = {}) {
          if (!directories.has(directoryName)) {
            if (!options.create) throw new DOMException('Not found', 'NotFoundError');
            directories.set(directoryName, createDirectoryHandle(directoryName));
          }
          return directories.get(directoryName);
        },
        async queryPermission() {
          return this.permission;
        },
        async requestPermission() {
          return this.permission;
        },
      };
    }

    const fileHandle = createFileHandle('opened.md', '# Opened\n');
    const addedHandle = createFileHandle('added.md', '# Added\n');
    const directoryHandle = createDirectoryHandle('Project Docs');
    window.__mockFs = {
      fileHandle,
      addedHandle,
      directoryHandle,
      openPickerQueue: [[fileHandle]],
      savePickerCalls: 0,
      createFileHandle,
      createDirectoryHandle,
    };
    window.showOpenFilePicker = async () => window.__mockFs.openPickerQueue.shift() || [window.__mockFs.fileHandle];
    window.showSaveFilePicker = async () => {
      window.__mockFs.savePickerCalls += 1;
      return createFileHandle('saved-as.md', '');
    };
    window.showDirectoryPicker = async () => window.__mockFs.directoryHandle;
  });
}

async function installMockNativeBridge(page) {
  await page.addInitScript(() => {
    const listeners = [];
    window.__nativeBridgeMessages = [];
    window.__nativeBridgeSaves = [];
    window.__nativeBridgeScenario = {
      openFile: 'success',
      saveFile: 'success',
      saveFileAs: 'success',
      openFolder: 'success',
      saveWorkspaceFile: 'success',
    };

    function emit(response) {
      window.setTimeout(() => {
        listeners.forEach((listener) => listener({ data: response }));
      }, 0);
    }

    function baseResponse(message, type, payload) {
      return {
        protocolVersion: 1,
        id: message.id,
        type,
        source: 'LensDocsStudio.Windows',
        timestamp: '2026-06-09T00:00:00.000Z',
        payload,
      };
    }

    window.chrome = {
      webview: {
        postMessage(message) {
          window.__nativeBridgeMessages.push(message);
          if (message.type === 'lensDocs.native.ping') {
            emit(baseResponse(message, 'lensDocs.native.pong', {
              host: 'LensDocsStudio.Windows',
              capabilities: [
                'diagnostics.ping',
                'file.open',
                'file.save',
                'file.saveAs',
                'workspace.openFolder',
                'workspace.saveFile',
                'workspace.createFile',
              ],
            }));
            return;
          }

          if (message.type === 'lensDocs.native.openFile') {
            if (window.__nativeBridgeScenario.openFile === 'cancelled') {
              emit(baseResponse(message, 'lensDocs.native.openFileResult', { cancelled: true }));
              return;
            }
            if (window.__nativeBridgeScenario.openFile === 'malformed') {
              emit({ ...baseResponse(message, 'lensDocs.native.openFileResult', {}), protocolVersion: 999 });
              return;
            }
            emit(baseResponse(message, 'lensDocs.native.openFileResult', {
              cancelled: false,
              name: 'native-open.md',
              displayName: 'native-open.md',
              extension: '.md',
              encoding: 'utf-8',
              content: '# Native Open\n',
              nativeHandleId: 'native-handle-1',
            }));
            return;
          }

          if (message.type === 'lensDocs.native.saveFile') {
            window.__nativeBridgeSaves.push(message.payload);
            emit(baseResponse(message, 'lensDocs.native.saveFileResult', {
              saved: window.__nativeBridgeScenario.saveFile !== 'failed',
              name: 'native-open.md',
              displayName: 'native-open.md',
              encoding: 'utf-8',
            }));
            return;
          }

          if (message.type === 'lensDocs.native.saveFileAs') {
            if (window.__nativeBridgeScenario.saveFileAs === 'cancelled') {
              emit(baseResponse(message, 'lensDocs.native.saveFileAsResult', { cancelled: true }));
              return;
            }
            emit(baseResponse(message, 'lensDocs.native.saveFileAsResult', {
              cancelled: false,
              saved: true,
              name: 'native-copy.md',
              displayName: 'native-copy.md',
              encoding: 'utf-8',
              nativeHandleId: 'native-handle-2',
            }));
            return;
          }

          if (message.type === 'lensDocs.native.openFolder') {
            if (window.__nativeBridgeScenario.openFolder === 'cancelled') {
              emit(baseResponse(message, 'lensDocs.native.openFolderResult', { cancelled: true }));
              return;
            }
            if (window.__nativeBridgeScenario.openFolder === 'malformed') {
              emit({ ...baseResponse(message, 'lensDocs.native.openFolderResult', {}), protocolVersion: 999 });
              return;
            }
            emit(baseResponse(message, 'lensDocs.native.openFolderResult', {
              cancelled: false,
              workspaceName: 'Project Docs',
              nativeWorkspaceId: 'native-workspace-1',
              files: [
                {
                  name: 'README.md',
                  path: 'README.md',
                  displayPath: 'README.md',
                  extension: '.md',
                  encoding: 'utf-8',
                  content: '# Native Workspace\n',
                  nativeHandleId: 'native-workspace-file-1',
                },
                {
                  name: 'flow.mmd',
                  path: 'diagrams/flow.mmd',
                  displayPath: 'diagrams/flow.mmd',
                  extension: '.mmd',
                  encoding: 'utf-8',
                  content: 'flowchart TD\n  A-->B\n',
                  nativeHandleId: 'native-workspace-file-2',
                },
              ],
              limits: {
                maxFileSizeBytes: 5242880,
                maxFiles: 500,
                maxDepth: 12,
              },
              skipped: [
                {
                  path: 'large.md',
                  reason: 'File exceeds the 5 MB limit.',
                },
              ],
            }));
            return;
          }

          if (message.type === 'lensDocs.native.saveWorkspaceFile') {
            window.__nativeBridgeSaves.push(message.payload);
            emit(baseResponse(message, 'lensDocs.native.saveWorkspaceFileResult', {
              saved: window.__nativeBridgeScenario.saveWorkspaceFile !== 'failed',
              name: 'README.md',
              path: 'README.md',
              displayPath: 'README.md',
              encoding: 'utf-8',
            }));
            return;
          }

          if (message.type === 'lensDocs.native.createWorkspaceFile') {
            emit(baseResponse(message, 'lensDocs.native.createWorkspaceFileResult', {
              cancelled: false,
              created: true,
              name: message.payload.path.split('/').pop(),
              path: message.payload.path,
              displayPath: message.payload.path,
              extension: '.md',
              encoding: 'utf-8',
              content: message.payload.content || '',
              nativeHandleId: 'native-workspace-file-new',
            }));
          }
        },
        addEventListener(type, listener) {
          if (type === 'message') listeners.push(listener);
        },
      },
    };
  });
}

async function dispatchContextMenu(locator, point = { x: 16, y: 16 }) {
  await locator.scrollIntoViewIfNeeded();
  await locator.evaluate(() => new Promise((resolve) => requestAnimationFrame(() => resolve())));
  await locator.evaluate((element, targetPoint) => {
    const rect = element.getBoundingClientRect();
    const x = Math.min(targetPoint.x, Math.max(rect.width - 1, 0));
    const y = Math.min(targetPoint.y, Math.max(rect.height - 1, 0));
    element.dispatchEvent(new MouseEvent('contextmenu', {
      bubbles: true,
      cancelable: true,
      button: 2,
      buttons: 2,
      clientX: rect.left + x,
      clientY: rect.top + y,
    }));
  }, point);
}

async function writeZipEntriesToDirectory(entries, directory) {
  for (const [name, bytes] of entries) {
    const target = path.join(directory, ...name.split('/'));
    await mkdir(path.dirname(target), { recursive: true });
    await writeFile(target, bytes);
  }
}

function createDocxImportFixture() {
  return createZipBuffer([
    {
      name: '[Content_Types].xml',
      data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Default Extension="png" ContentType="image/png"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
  <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
  <Override PartName="/word/numbering.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml"/>
</Types>`,
    },
    {
      name: '_rels/.rels',
      data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>`,
    },
    {
      name: 'word/_rels/document.xml.rels',
      data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rIdImage1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/tiny.png"/>
</Relationships>`,
    },
    {
      name: 'word/styles.xml',
      data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:style w:type="paragraph" w:styleId="Heading1">
    <w:name w:val="heading 1"/>
    <w:basedOn w:val="Normal"/>
    <w:qFormat/>
  </w:style>
</w:styles>`,
    },
    {
      name: 'word/numbering.xml',
      data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:numbering xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:abstractNum w:abstractNumId="0">
    <w:lvl w:ilvl="0">
      <w:start w:val="1"/>
      <w:numFmt w:val="bullet"/>
      <w:lvlText w:val="*"/>
      <w:pPr><w:ind w:left="720" w:hanging="360"/></w:pPr>
    </w:lvl>
  </w:abstractNum>
  <w:num w:numId="1"><w:abstractNumId w:val="0"/></w:num>
</w:numbering>`,
    },
    {
      name: 'word/document.xml',
      data: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document
  xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
  xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
  xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
  xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">
  <w:body>
    <w:p><w:pPr><w:pStyle w:val="Heading1"/></w:pPr><w:r><w:t>Imported Word</w:t></w:r></w:p>
    <w:p><w:r><w:t xml:space="preserve">Hello </w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>bold</w:t></w:r><w:r><w:t xml:space="preserve"> body</w:t></w:r></w:p>
    <w:p><w:pPr><w:numPr><w:ilvl w:val="0"/><w:numId w:val="1"/></w:numPr></w:pPr><w:r><w:t>First item</w:t></w:r></w:p>
    <w:tbl>
      <w:tr><w:tc><w:p><w:r><w:t>Area</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Status</w:t></w:r></w:p></w:tc></w:tr>
      <w:tr><w:tc><w:p><w:r><w:t>Import</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Ready</w:t></w:r></w:p></w:tc></w:tr>
    </w:tbl>
    <w:p><w:r><w:drawing><wp:inline>
      <wp:extent cx="9525" cy="9525"/><wp:docPr id="1" name="Tiny image" descr="Word logo"/>
      <a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic>
        <pic:nvPicPr><pic:cNvPr id="0" name="tiny.png"/><pic:cNvPicPr/></pic:nvPicPr>
        <pic:blipFill><a:blip r:embed="rIdImage1"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>
        <pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="9525" cy="9525"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr>
      </pic:pic></a:graphicData></a:graphic>
    </wp:inline></w:drawing></w:r></w:p>
    <w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440"/></w:sectPr>
  </w:body>
</w:document>`,
    },
    {
      name: 'word/media/tiny.png',
      data: Buffer.from(tinyPngBase64, 'base64'),
    },
  ], { compress: true });
}

function createSimplePdfBuffer(pages) {
  const chunks = ['%PDF-1.4\n'];
  const offsets = [0];
  const pageObjectIds = pages.map((_, index) => 3 + index * 2);
  const contentObjectIds = pages.map((_, index) => 4 + index * 2);
  const fontObjectId = 3 + pages.length * 2;

  function currentOffset() {
    return Buffer.byteLength(chunks.join(''), 'binary');
  }

  function addObject(id, body) {
    offsets[id] = currentOffset();
    chunks.push(`${id} 0 obj\n${body}\nendobj\n`);
  }

  addObject(1, '<< /Type /Catalog /Pages 2 0 R >>');
  addObject(2, `<< /Type /Pages /Kids [${pageObjectIds.map((id) => `${id} 0 R`).join(' ')}] /Count ${pages.length} >>`);

  pages.forEach((text, index) => {
    const pageObjectId = pageObjectIds[index];
    const contentObjectId = contentObjectIds[index];
    const stream = `BT /F1 24 Tf 72 720 Td (${escapePdfText(text)}) Tj ET`;
    addObject(pageObjectId, `<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 ${fontObjectId} 0 R >> >> /MediaBox [0 0 612 792] /Contents ${contentObjectId} 0 R >>`);
    addObject(contentObjectId, `<< /Length ${Buffer.byteLength(stream, 'binary')} >>\nstream\n${stream}\nendstream`);
  });

  addObject(fontObjectId, '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>');

  const xrefOffset = currentOffset();
  const objectCount = fontObjectId + 1;
  chunks.push(`xref\n0 ${objectCount}\n0000000000 65535 f \n`);
  for (let id = 1; id < objectCount; id += 1) {
    chunks.push(`${String(offsets[id]).padStart(10, '0')} 00000 n \n`);
  }
  chunks.push(`trailer\n<< /Size ${objectCount} /Root 1 0 R >>\nstartxref\n${xrefOffset}\n%%EOF\n`);

  return Buffer.from(chunks.join(''), 'binary');
}

function escapePdfText(value) {
  return String(value)
    .replaceAll('\\', '\\\\')
    .replaceAll('(', '\\(')
    .replaceAll(')', '\\)');
}

test('root loads the buildless app shell', async ({ page }) => {
  await gotoApp(page);
  await expect(page).toHaveTitle(/^Lens Docs Studio v0\.1\.0 \(build \d+\)$/);
  await expect(page.locator('#app')).toBeVisible();
  await expect(page.locator('.brand h1')).toHaveText('Lens Docs Studio');
  await expect(page.locator('.brand small')).toHaveText('Local Markdown, Mermaid, and documentation studio');
  await expect(page.locator('#appVersionBadge')).toHaveText(/^v0\.1\.0 \(build \d+\)$/);
  await expect(page.locator('.brand-mark')).not.toHaveText('LD');
  await expect(page.locator('body')).not.toContainText('Review Markdown, Mermaid, and evidence artefacts from the Power Platform Lens family.');
  await expect(page.locator('meta[http-equiv="Content-Security-Policy"]')).toHaveCount(1);
  await expect(page.locator('script[type="module"][src$="/assets/scripts/main.js"]')).toHaveCount(1);
  await expect(page.locator('link[rel="stylesheet"][href$="/assets/styles/app.css"]')).toHaveCount(1);

  const brandColour = await page.locator('.brand-mark').evaluate((element) => getComputedStyle(element).color);
  expect(brandColour).toBe('rgb(255, 136, 62)');
});

test('legacy renderer URL redirects to the app', async ({ page }) => {
  await page.goto('/md-mmd-renderer-v5.html');
  await expect(page).toHaveURL(/\/index\.html$/);
  await expect(page.locator('#app')).toBeVisible();
});

test('fixture renders markdown, mermaid, code copy, and diagram actions', async ({ page }) => {
  await openFixture(page, 'mixed.md');

  await expect(page.locator('#preview h1')).toHaveText('Export Fixture');
  await expect(page.locator('.diagram-frame svg')).toHaveCount(1);
  await expect(page.locator('.diagram-frame svg')).toContainText('Open file');
  await expect(page.locator('.diagram-frame svg')).toContainText('Render preview');
  await expect(page.locator('.diagram-frame svg')).toContainText('Export type');
  await expect(page.locator('[data-code-action="copy"]')).toHaveCount(1);
  await expect(page.locator('[data-table-action="copy"]')).toHaveCount(1);
  await expect(page.locator('[data-table-action="downloadCsv"]')).toHaveCount(1);
  await expect(page.locator('[data-diagram-action="copySource"]')).toHaveCount(1);
  await expect(page.locator('[data-diagram-action="exportSvg"]')).toHaveCount(1);
  await expect(page.locator('[data-diagram-action="exportPng"]')).toHaveCount(1);
  await expect(page.locator('[data-diagram-action="copyPng"]')).toHaveCount(1);
  await expect(page.locator('.hljs-keyword, .hljs-title, .hljs-string')).not.toHaveCount(0);

  await page.evaluate(() => {
    window.__copiedTableText = '';
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        writeText: async (text) => {
          window.__copiedTableText = text;
        },
      },
    });
  });
  await page.locator('[data-table-action="copy"]').click();
  await expect.poll(() => page.evaluate(() => window.__copiedTableText)).toBe('Area\tStatus\nPreview\tReady\nExport\tVerified');
  await expect(page.locator('#status')).toHaveText(/Table copied for Excel/);

  const [csvDownload] = await Promise.all([
    page.waitForEvent('download'),
    page.locator('[data-table-action="downloadCsv"]').click(),
  ]);
  expect(await csvDownload.failure()).toBeNull();
  expect(csvDownload.suggestedFilename()).toBe('mixed-table-1.csv');
  const csvPath = await csvDownload.path();
  expect(await readFile(csvPath, 'utf8')).toBe('Area,Status\r\nPreview,Ready\r\nExport,Verified');
  await expect(page.locator('#status')).toHaveText(/Table CSV downloaded/);

  await page.evaluate(() => {
    window.__copiedPng = null;
    class MockClipboardItem {
      constructor(items) {
        this.items = items;
      }
    }
    Object.defineProperty(window, 'ClipboardItem', {
      configurable: true,
      value: MockClipboardItem,
    });
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        write: async (items) => {
          const blob = items[0].items['image/png'];
          window.__copiedPng = { count: items.length, type: blob.type, size: blob.size };
        },
        writeText: async () => {},
      },
    });
  });
  await page.locator('[data-diagram-action="copyPng"]').click();
  await expect(page.locator('#status')).toHaveText(/PNG diagram copied/);
  await expect.poll(() => page.evaluate(() => window.__copiedPng)).toMatchObject({
    count: 1,
    type: 'image/png',
  });
  expect(await page.evaluate(() => window.__copiedPng.size)).toBeGreaterThan(0);
});

test('Mermaid diagram theme selector persists and falls back safely', async ({ page }) => {
  await gotoApp(page);
  await expect(page.locator('#mermaidThemeSelect')).toHaveValue('auto');

  await page.evaluate(() => {
    localStorage.setItem('md-mmd-renderer.mermaidTheme', 'unknown-theme');
  });
  await page.reload({ waitUntil: 'domcontentloaded' });
  await waitForAppReady(page);
  await expect(page.locator('#mermaidThemeSelect')).toHaveValue('auto');

  await page.locator('#mermaidThemeSelect').selectOption('forest');
  await expect.poll(() => page.evaluate(() => localStorage.getItem('md-mmd-renderer.mermaidTheme'))).toBe('forest');

  await page.reload({ waitUntil: 'domcontentloaded' });
  await waitForAppReady(page);
  await expect(page.locator('#mermaidThemeSelect')).toHaveValue('forest');
});

test('Mermaid Auto theme follows the app theme and explicit choices stay fixed', async ({ page }) => {
  await gotoApp(page);
  await page.evaluate(() => {
    localStorage.setItem('md-mmd-renderer.theme', 'light');
    localStorage.setItem('md-mmd-renderer.mermaidTheme', 'auto');
  });
  await page.reload({ waitUntil: 'domcontentloaded' });
  await waitForAppReady(page);
  await loadSample(page);

  const firstDiagramFill = () => page.locator('.diagram-frame svg').evaluate((svg) => {
    const node = svg.querySelector('.node rect, .node polygon, .node circle, .node path');
    return node?.getAttribute('fill') || (node ? getComputedStyle(node).fill : '');
  });

  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await expect(page.locator('#mermaidThemeSelect')).toHaveValue('auto');
  const lightFill = await firstDiagramFill();

  await page.locator('#themeToggleButton').click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect.poll(firstDiagramFill).not.toBe(lightFill);

  await page.locator('#mermaidThemeSelect').selectOption('lens');
  await expect.poll(() => page.locator('.diagram-frame svg').evaluate((svg) => /#FF883E/i.test(svg.outerHTML))).toBe(true);
  const lensFill = await firstDiagramFill();

  await page.locator('#themeToggleButton').click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await expect.poll(firstDiagramFill).toBe(lensFill);
  await expect(page.locator('#mermaidThemeSelect')).toHaveValue('lens');
});

test('Mermaid labels with HTML line breaks render without SVG parser errors', async ({ page }) => {
  await gotoApp(page);
  await setEditorValueAndSelection(page, `\`\`\`mermaid
flowchart TD
  A[Parent Flow or Power App] --> B[Prepare Function Request<br/>sourceType + sourceId + maxGeneration]
  B --> C[Call Azure Function<br>POST /api/tih/validate]
  C --> D[Load Sire and Dam Ancestry Recursively]
  D --> E[Update<br/>tek_hsi_pedigreejson<br/>tek_hsi_traditionalirishhorse]
\`\`\``);

  await renderPreviewWithShortcut(page);

  const frame = page.locator('.diagram-frame');
  await expect(frame.locator('svg')).toHaveCount(1);
  await expect(page.locator('.diagram-error')).toHaveCount(0);
  await expect(frame.locator('parsererror')).toHaveCount(0);
  await expect(frame).not.toContainText('Opening and ending tag mismatch');
  await expect(frame.locator('svg')).toContainText('Prepare Function');
  await expect(frame.locator('svg')).toContainText('Request');
  await expect(frame.locator('svg')).toContainText('sourceType +');
  await expect(frame.locator('svg')).toContainText('maxGeneration');
  await expect(frame.locator('svg')).toContainText('POST /api/tih/validate');
  await expect(frame.locator('svg')).toContainText('Ancestry');
  await expect(frame.locator('svg')).toContainText('tek_hsi_traditionalirishhorse');

  const labelText = await frame.locator('svg').evaluate((svg) => {
    return [...svg.querySelectorAll('text tspan')]
      .map((tspan) => tspan.textContent?.trim())
      .filter(Boolean)
      .join('');
  });
  expect(labelText).toContain('tek_hsi_pedigreejson');
  expect(labelText).toContain('tek_hsi_traditionalirishhorse');

  const overflowingLabels = await frame.locator('svg').evaluate((svg) => {
    const tolerance = 2;
    return [...svg.querySelectorAll('g.node')].flatMap((node) => {
      const shape = node.querySelector('rect, polygon, path, circle, ellipse');
      const text = node.querySelector('text');
      if (!shape || !text) return [];

      const shapeBox = shape.getBoundingClientRect();
      const textBox = text.getBoundingClientRect();
      const overflows = textBox.left < shapeBox.left - tolerance
        || textBox.top < shapeBox.top - tolerance
        || textBox.right > shapeBox.right + tolerance
        || textBox.bottom > shapeBox.bottom + tolerance;

      return overflows ? [text.textContent] : [];
    });
  });
  expect(overflowingLabels).toEqual([]);

  const smallestNodeLabelFont = await frame.locator('svg').evaluate((svg) => {
    const sizes = [...svg.querySelectorAll('g.node text')]
      .map((text) => parseFloat(text.getAttribute('font-size') || getComputedStyle(text).fontSize || '0'))
      .filter((size) => Number.isFinite(size) && size > 0);
    return Math.min(...sizes);
  });
  expect(smallestNodeLabelFont).toBeGreaterThanOrEqual(9);
});

test('editor syntax highlighting and math rendering work without a build step', async ({ page }) => {
  await gotoApp(page);
  await setEditorValueAndSelection(page, '# Formula\n\nInline $E=mc^2$ and block:\n\n$$\n\\frac{a_1}{b^2}\n$$\n\n```js\nconst value = 1;\n```');
  await expect(page.locator('#editorSyntaxLayer .hljs-section, #editorSyntaxLayer .hljs-code')).not.toHaveCount(0);
  const editorLayerMatch = await page.locator('#editor').evaluate((editor) => {
    const editorStyle = getComputedStyle(editor);
    const layerStyle = getComputedStyle(document.querySelector('#editorSyntaxLayer'));
    return {
      editorColor: editorStyle.color,
      sameFont: editorStyle.fontFamily === layerStyle.fontFamily,
      samePadding: editorStyle.paddingTop === layerStyle.paddingTop && editorStyle.paddingLeft === layerStyle.paddingLeft,
    };
  });
  expect(editorLayerMatch.editorColor).toBe('rgba(0, 0, 0, 0)');
  expect(editorLayerMatch.sameFont).toBe(true);
  expect(editorLayerMatch.samePadding).toBe(true);

  await renderPreviewFromViewMenu(page);
  await expect(page.locator('#preview .math-inline')).toContainText('E=mc');
  await expect(page.locator('#preview .math-inline .katex')).toBeVisible();
  await expect(page.locator('#preview .math-block .katex')).toBeVisible();
  await expect(page.locator('#preview .math-block')).toContainText('a');
  await expect(page.locator('#preview .math-block')).toContainText('b');
});

test('editor syntax layer stays aligned with native selection metrics', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 720 });
  await gotoApp(page);
  const longLine = 'N -->|pedigreeUpdateRequest| Q[Update tek_pedigreeupdaterequests<br/>tek_pedigreejson<br/>tek_traditionalirishhorse]';
  const source = [
    '# Selection Alignment',
    '',
    '**Bold marker** and _italic marker_ keep editor metrics stable.',
    '',
    '```mermaid',
    'flowchart TD',
    ...Array.from({ length: 80 }, (_, index) => `  ${index % 2 ? longLine : 'A[Parent Flow or Power App] --> B[Child Pedigree Flow]'}`),
    '```',
  ].join('\n');

  await setEditorValueAndSelection(page, source);
  await expect(page.locator('#editorSyntaxLayer .hljs-strong')).toHaveCount(1);
  await expect(page.locator('#editorSyntaxLayer .hljs-emphasis')).toHaveCount(1);

  const metrics = await page.locator('#editor').evaluate((editor) => {
    const layer = document.querySelector('#editorSyntaxLayer');
    const shell = document.querySelector('#editorShell');
    const strong = layer.querySelector('.hljs-strong');
    const emphasis = layer.querySelector('.hljs-emphasis');
    const layerStyle = getComputedStyle(layer);
    return {
      editorClientWidth: editor.clientWidth,
      editorScrollHeight: editor.scrollHeight,
      editorClientHeight: editor.clientHeight,
      layerWidth: layer.getBoundingClientRect().width,
      shellScrollbarWidth: Number(shell.dataset.editorScrollbarWidth || '0'),
      actualScrollbarWidth: editor.offsetWidth - editor.clientWidth,
      layerFontWeight: layerStyle.fontWeight,
      strongFontWeight: getComputedStyle(strong).fontWeight,
      layerFontStyle: layerStyle.fontStyle,
      emphasisFontStyle: getComputedStyle(emphasis).fontStyle,
    };
  });

  expect(metrics.editorScrollHeight).toBeGreaterThan(metrics.editorClientHeight);
  expect(Math.abs(metrics.layerWidth - metrics.editorClientWidth)).toBeLessThanOrEqual(1);
  expect(metrics.shellScrollbarWidth).toBe(metrics.actualScrollbarWidth);
  expect(metrics.strongFontWeight).toBe(metrics.layerFontWeight);
  expect(metrics.emphasisFontStyle).toBe(metrics.layerFontStyle);
});

test('rendering sanitizes hostile Markdown and Mermaid output', async ({ page }) => {
  await gotoApp(page);
  await page.evaluate(() => {
    window.__auditXss = 0;
  });
  await setEditorValueAndSelection(page, `# Security Probe

<script>window.__auditXss = 1</script>

<img src=x onerror="window.__auditXss = 2">

[bad](javascript:window.__auditXss=3)

![bad](data:image/svg+xml,%3Csvg%20xmlns%3D%22http%3A//www.w3.org/2000/svg%22%20onload%3D%22window.__auditXss%3D4%22%3E%3C/svg%3E)

[ok](https://example.com)

\`\`\`mermaid
flowchart LR
  A[Start]
  click A "javascript:window.__auditXss=5"
\`\`\``);
  await renderPreviewWithShortcut(page);
  await expect(page.locator('#status')).toHaveText(/Rendered/);

  const result = await page.locator('#preview').evaluate((preview) => ({
    executed: window.__auditXss,
    scripts: preview.querySelectorAll('script').length,
    eventAttrs: [...preview.querySelectorAll('*')].flatMap((element) => [...element.attributes].filter((attribute) => /^on/i.test(attribute.name)).map((attribute) => attribute.name)),
    javascriptHrefs: [...preview.querySelectorAll('[href], [xlink\\:href]')]
      .map((element) => element.getAttribute('href') || element.getAttribute('xlink:href') || '')
      .filter((href) => /^javascript:/i.test(href)),
    dataSvgImages: [...preview.querySelectorAll('img[src]')]
      .map((image) => image.getAttribute('src') || '')
      .filter((src) => /^data:image\/svg\+xml/i.test(src)),
    externalLink: {
      target: preview.querySelector('a[href="https://example.com"]')?.getAttribute('target'),
      rel: preview.querySelector('a[href="https://example.com"]')?.getAttribute('rel'),
    },
  }));

  expect(result.executed).toBe(0);
  expect(result.scripts).toBe(0);
  expect(result.eventAttrs).toEqual([]);
  expect(result.javascriptHrefs).toEqual([]);
  expect(result.dataSvgImages).toEqual([]);
  expect(result.externalLink).toEqual({ target: '_blank', rel: 'noopener noreferrer' });
});

test('topbar menus are grouped and keyboard accessible', async ({ page }) => {
  await page.setViewportSize({ width: 1366, height: 768 });
  await gotoApp(page);

  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await expect(page.locator('[data-menu-action="newMarkdown"]')).toBeVisible();
  await expect(page.locator('[data-menu-action="openFile"]')).toBeVisible();
  await expect(page.locator('details.menu[open] .menu-heading')).toContainText(['New', 'Open', 'Import', 'Save', 'Recent']);
  const fileMenuClickable = await page.locator('details.menu[open]').evaluate((menu) => {
    const button = menu.querySelector('[data-menu-action="openFile"]');
    const rect = button.getBoundingClientRect();
    const target = document.elementFromPoint(rect.left + rect.width / 2, rect.top + rect.height / 2);
    return button === target || button.contains(target);
  });
  expect(fileMenuClickable).toBe(true);
  await page.keyboard.press('Escape');
  await expect(page.locator('summary').filter({ hasText: /^File$/ }).locator('..')).not.toHaveAttribute('open', '');

  await page.locator('summary').filter({ hasText: /^Export$/ }).click();
  await expect(page.locator('#downloadButton')).toBeVisible();
  await expect(page.locator('details.menu[open] .menu-heading')).toContainText(['Documents', 'Bundles', 'Diagrams', 'Clipboard']);
  await expect(page.locator('#exportPdfButton')).toBeVisible();
  await expect(page.locator('#exportMarkdownBundleButton')).toBeVisible();

  await page.locator('summary').filter({ hasText: /^View$/ }).click();
  await expect(page.locator('details.menu[open]')).toContainText('Preview tools');
  await expect(page.locator('details.menu[open]')).toContainText('Render preview');
  const viewPanelBox = await page.locator('details.menu[open] .menu-panel').boundingBox();
  const viewport = page.viewportSize();
  expect(viewPanelBox.x).toBeGreaterThanOrEqual(0);
  expect(viewPanelBox.x + viewPanelBox.width).toBeLessThanOrEqual(viewport.width + 1);

  await page.locator('summary').filter({ hasText: /^Create$/ }).click();
  await expect(page.locator('details.menu[open]')).toContainText('Starters');
  await expect(page.getByRole('button', { name: 'Markdown + Mermaid sample' })).toBeVisible();
  await expect(page.locator('summary').filter({ hasText: /^Examples$/ })).toHaveCount(0);
  await expect(page.locator('#openFileButton')).toHaveCount(0);
  await expect(page.locator('#openFolderButton')).toHaveCount(0);
  await expect(page.locator('#renderButton')).toHaveCount(0);

  await page.locator('summary').filter({ hasText: /^Help$/ }).click();
  await expect(page.locator('details.menu[open]')).toContainText('Mermaid snippets');
  await expect(page.locator('details.menu[open]')).toContainText('Open feature guide');
  await expect(page.locator('details.menu[open]')).toContainText('Import ZIP');
  await expect(page.locator('details.menu[open]')).toContainText('Markdown Bundle');

  const commonMenuNames = ['File', 'Edit', 'Studio', 'Export', 'View', 'Help'];
  const commonMenuWidths = [];
  for (const name of commonMenuNames) {
    await page.locator('summary').filter({ hasText: new RegExp(`^${name}$`) }).click();
    const panel = page.locator('details.menu[open] .menu-panel');
    await expect(panel).toBeVisible();
    const box = await panel.boundingBox();
    const viewportSize = page.viewportSize();
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(viewportSize.width + 1);
    commonMenuWidths.push(Math.round(box.width));
  }

  const expectedCommonWidth = commonMenuWidths[0];
  commonMenuWidths.forEach((width) => {
    expect(Math.abs(width - expectedCommonWidth)).toBeLessThanOrEqual(1);
  });

  await page.locator('summary').filter({ hasText: /^Create$/ }).click();
  const createPanelBox = await page.locator('#createMenu .menu-panel').boundingBox();
  const createViewportSize = page.viewportSize();
  expect(createPanelBox.x).toBeGreaterThanOrEqual(0);
  expect(createPanelBox.x + createPanelBox.width).toBeLessThanOrEqual(createViewportSize.width + 1);
  expect(createPanelBox.width).toBeGreaterThan(expectedCommonWidth + 120);
});

test('File menu starts a blank Markdown document', async ({ page }) => {
  await gotoApp(page);
  page.on('dialog', (dialog) => {
    throw new Error(`Unexpected native dialog: ${dialog.message()}`);
  });

  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.getByRole('button', { name: 'New Markdown file' }).click();
  await expect(page.getByRole('heading', { name: 'New Markdown file' })).toBeVisible();
  await expect(page.locator('#appDialogPromptInput')).toHaveValue('untitled.md');
  await submitAppDialog(page, { button: 'Create file' });

  await expect(page.locator('#activeFileLabel')).toHaveText('untitled.md');
  await expect(page.locator('#folderBadge')).toHaveText('Blank document');
  await expect(page.locator('#fileCount')).toHaveText('1');
  await expect(page.locator('#editor')).toHaveValue('');
  await expect(page.locator('#status')).toHaveText('Blank Markdown document ready.');
  await expect(page.locator('#saveButton')).toBeEnabled();

  await page.locator('#editor').fill('# Fresh start\n');
  await expect(page.locator('#activeFileLabel')).toContainText('untitled.md · edited in memory');
  await expect(page.locator('#saveButton')).toBeEnabled();
});

test('File System Access save writes back to the opened file without Save as', async ({ page }) => {
  await installMockFileSystemAccess(page);
  await gotoApp(page);

  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.locator('details.menu[open]').getByRole('button', { name: 'Open file' }).click();
  await expect(page.locator('#activeFileLabel')).toHaveText('opened.md');

  await page.locator('#editor').fill('# Updated\n');
  await page.locator('#saveButton').click();

  await expect(page.locator('#status')).toHaveText(/opened\.md saved/);
  const result = await page.evaluate(() => ({
    text: window.__mockFs.fileHandle._text,
    savePickerCalls: window.__mockFs.savePickerCalls,
  }));
  expect(result.text).toBe('# Updated\n');
  expect(result.savePickerCalls).toBe(0);
});

test('workspace folders can create, add, refresh, and detect changed files', async ({ page }) => {
  await installMockFileSystemAccess(page);
  await gotoApp(page);
  page.on('dialog', (dialog) => {
    throw new Error(`Unexpected native dialog: ${dialog.message()}`);
  });
  await page.evaluate(() => {
    const docs = window.__mockFs.createDirectoryHandle('docs');
    docs.files.set('seed.md', window.__mockFs.createFileHandle('seed.md', '# Seed\n'));
    window.__mockFs.directoryHandle.directories.set('docs', docs);
  });

  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.locator('details.menu[open]').getByRole('button', { name: 'Open folder' }).click();
  await expect(page.locator('#folderBadge')).toHaveText('Project Docs');

  await page.locator('#fileViewTreeButton').click();
  await page.locator('#fileList [data-tree-folder="docs"]').click();
  await expect(page.locator('#fileList [data-tree-folder="docs"]')).toHaveAttribute('aria-selected', 'true');

  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.locator('details.menu[open]').getByRole('button', { name: 'New Markdown file' }).click();
  await expect(page.locator('#appDialogPromptInput')).toHaveValue('docs/untitled.md');
  await submitAppDialog(page, { value: 'docs/notes.md', button: 'Create file' });
  await expect(page.locator('#activeFileLabel')).toHaveText('docs/notes.md');
  await expect(page.locator('#fileCount')).toHaveText('2');

  await page.locator('#editor').fill('# Notes\n');
  await page.locator('#saveButton').click();
  await expect(page.locator('#status')).toHaveText(/notes\.md saved/);
  await expect.poll(async () => await page.evaluate(() => window.__mockFs.directoryHandle.directories.get('docs').files.get('notes.md')._text)).toBe('# Notes\n');

  await page.evaluate(() => {
    window.__mockFs.openPickerQueue = [[window.__mockFs.addedHandle]];
  });
  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.locator('details.menu[open]').getByRole('button', { name: 'Add file to workspace' }).click();
  await expect(page.locator('#fileCount')).toHaveText('3');
  await expect(page.locator('#activeFileLabel')).toHaveText('added.md');

  await page.evaluate(() => {
    window.__mockFs.addedHandle._text = '# Changed outside\n';
    window.__mockFs.addedHandle._lastModified += 1000;
  });
  await page.locator('#refreshFileButton').click();
  await expect(page.locator('#editor')).toHaveValue('# Changed outside\n');

  await page.evaluate(() => {
    window.__mockFs.addedHandle._text = '# Focus reload\n';
    window.__mockFs.addedHandle._lastModified += 1000;
    window.dispatchEvent(new Event('focus'));
  });
  await submitAppDialog(page, { button: 'Reload latest' });
  await expect(page.locator('#editor')).toHaveValue('# Focus reload\n');
});

test('file browser tree view shows workspace folder hierarchy', async ({ page }, testInfo) => {
  const zipPath = await writeZipFixture(testInfo, 'tree-workspace.zip', [
    { name: 'README.md', data: '# Home\n' },
    { name: 'docs/intro.md', data: '# Intro\n' },
    { name: 'docs/guide/setup.md', data: '# Setup\n' },
    { name: 'adr/decision.md', data: '# Decision\n' },
  ]);

  await gotoApp(page);
  await page.locator('#zipInput').setInputFiles(zipPath);
  await expect(page.locator('#status')).toHaveText(/Imported 4 documents from ZIP/, { timeout: 20_000 });
  const headerMetrics = await page.evaluate(() => {
    const count = document.querySelector('#fileCount').getBoundingClientRect();
    const badge = document.querySelector('#folderBadge').getBoundingClientRect();
    return {
      countCentre: count.top + count.height / 2,
      badgeCentre: badge.top + badge.height / 2,
      countRight: count.right,
      badgeLeft: badge.left,
    };
  });
  expect(Math.abs(headerMetrics.countCentre - headerMetrics.badgeCentre)).toBeLessThan(4);
  expect(headerMetrics.badgeLeft).toBeGreaterThan(headerMetrics.countRight);

  await page.locator('#fileViewTreeButton').click();
  await expect(page.locator('#fileViewTreeButton')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('#fileList .file-tree')).toBeVisible();
  await expect(page.locator('#fileList [data-tree-folder="docs"]')).toBeVisible();
  await expect(page.locator('#fileList [data-tree-folder="docs/guide"]')).toBeVisible();
  await expect(page.locator('#fileList [data-path="docs/guide/setup.md"]')).toBeVisible();

  await page.locator('#fileList [data-path="docs/guide/setup.md"]').click();
  await expect(page.locator('#activeFileLabel')).toHaveText('docs/guide/setup.md');

  await page.locator('#treeCollapseButton').click();
  await expect(page.locator('#fileList [data-tree-folder="docs"]')).toHaveAttribute('aria-expanded', 'false');
  await expect(page.locator('#fileList [data-path="docs/guide/setup.md"]')).toBeHidden();

  await page.locator('#treeRevealButton').click();
  await expect(page.locator('#fileList [data-tree-folder="docs"]')).toHaveAttribute('aria-expanded', 'true');
  await expect(page.locator('#fileList [data-tree-folder="docs/guide"]')).toHaveAttribute('aria-expanded', 'true');
  await expect(page.locator('#fileList [data-path="docs/guide/setup.md"]')).toBeVisible();

  await page.locator('#fileSearch').fill('setup');
  await expect(page.locator('#fileList [data-tree-folder="docs"]')).toBeVisible();
  await expect(page.locator('#fileList [data-tree-folder="docs/guide"]')).toBeVisible();
  await expect(page.locator('#fileList [data-path="README.md"]')).toHaveCount(0);

  await page.locator('#fileViewListButton').click();
  await expect(page.locator('#fileViewListButton')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('#fileList .file-tree')).toHaveCount(0);
  await expect(page.locator('#fileList [data-path="docs/guide/setup.md"]')).toBeVisible();
});

test('collapsed sidebar keeps the split workspace stretched', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 820 });
  await gotoApp(page);
  await page.evaluate(() => {
    localStorage.setItem('md-mmd-renderer.sidebarCollapsed', 'true');
    localStorage.setItem('md-mmd-renderer.editorLayout', 'split');
    localStorage.setItem('md-mmd-renderer.editorWidth', '356');
  });
  await page.reload();

  await expect(page.locator('#shell')).toHaveClass(/sidebar-collapsed/);
  const metrics = await page.evaluate(() => {
    const shell = document.querySelector('#shell').getBoundingClientRect();
    const workspace = document.querySelector('.workspace').getBoundingClientRect();
    const editorToolbar = document.querySelector('#editorToolbar').getBoundingClientRect();
    return {
      overflow: document.documentElement.scrollWidth > window.innerWidth + 1,
      shellRight: shell.right,
      shellWidth: shell.width,
      workspaceRight: workspace.right,
      workspaceWidth: workspace.width,
      toolbarHeight: editorToolbar.height,
    };
  });

  expect(metrics.overflow).toBe(false);
  expect(metrics.workspaceWidth).toBeGreaterThan(metrics.shellWidth * 0.85);
  expect(metrics.workspaceRight).toBeGreaterThan(metrics.shellRight - 16);
  expect(metrics.toolbarHeight).toBeLessThan(64);
});

test('Help menu opens the feature guide as read-only Markdown', async ({ page }) => {
  await gotoApp(page);

  await page.locator('summary').filter({ hasText: /^Help$/ }).click();
  await page.getByRole('button', { name: 'Open feature guide' }).click();

  await expect(page.locator('#status')).toHaveText(/Feature guide opened read-only/, { timeout: 20_000 });
  await expect(page.locator('#activeFileLabel')).toHaveText(/tool-feature-guide\.md · read-only/);
  await expect(page.locator('#preview h1')).toHaveText('Lens Docs Studio Feature Guide');
  await expect(page.locator('#editor')).toHaveJSProperty('readOnly', true);
  await expect(page.locator('#saveButton')).toBeDisabled();
  await expect(page.locator('#editorToolbar [data-command="bold"]')).toBeDisabled();

  const before = await page.locator('#editor').inputValue();
  await page.locator('#editor').focus();
  await page.keyboard.type('x');
  await expect(page.locator('#editor')).toHaveValue(before);

  await page.locator('summary').filter({ hasText: /^Edit$/ }).click();
  await page.getByRole('button', { name: 'Paste as table' }).click();
  await expect(page.locator('#status')).toHaveText(/read-only/);
  await expect(page.locator('#editor')).toHaveValue(before);
});

test('native bridge diagnostic reports unavailable in browser mode', async ({ page }) => {
  await gotoApp(page);

  await page.locator('summary').filter({ hasText: /^Help$/ }).click();
  await page.getByRole('button', { name: 'Check Windows bridge' }).click();

  await expect(page.locator('#status')).toHaveText('Windows bridge unavailable in this browser mode.');
});

test('native bridge diagnostic sends a strict ping and reports host pong', async ({ page }) => {
  await page.addInitScript(() => {
    const listeners = [];
    window.__nativeBridgeMessages = [];
    window.chrome = {
      webview: {
        postMessage(message) {
          window.__nativeBridgeMessages.push(message);
          const response = {
            protocolVersion: 1,
            id: message.id,
            type: 'lensDocs.native.pong',
            source: 'LensDocsStudio.Windows',
            timestamp: '2026-06-09T00:00:00.000Z',
            payload: {
              host: 'LensDocsStudio.Windows',
              capabilities: ['diagnostics.ping'],
            },
          };
          window.setTimeout(() => {
            listeners.forEach((listener) => listener({ data: response }));
          }, 0);
        },
        addEventListener(type, listener) {
          if (type === 'message') listeners.push(listener);
        },
      },
    };
  });
  await gotoApp(page);

  await page.locator('summary').filter({ hasText: /^Help$/ }).click();
  await page.getByRole('button', { name: 'Check Windows bridge' }).click();

  await expect(page.locator('#status')).toHaveText(/Windows bridge available: LensDocsStudio\.Windows \(diagnostics\.ping\)\./);
  const [message] = await page.evaluate(() => window.__nativeBridgeMessages);
  expect(message).toEqual(expect.objectContaining({
    protocolVersion: 1,
    type: 'lensDocs.native.ping',
    source: 'LensDocsStudio.Web',
    payload: {},
  }));
  expect(message.id).toEqual(expect.any(String));
  expect(message.id.length).toBeGreaterThan(0);
  expect(new Date(message.timestamp).toString()).not.toBe('Invalid Date');
});

test('native bridge diagnostic handles malformed host responses safely', async ({ page }) => {
  await page.addInitScript(() => {
    const listeners = [];
    window.chrome = {
      webview: {
        postMessage(message) {
          const response = {
            protocolVersion: 999,
            id: message.id,
            type: 'lensDocs.native.pong',
            source: 'LensDocsStudio.Windows',
            timestamp: '2026-06-09T00:00:00.000Z',
            payload: {},
          };
          window.setTimeout(() => {
            listeners.forEach((listener) => listener({ data: response }));
          }, 0);
        },
        addEventListener(type, listener) {
          if (type === 'message') listeners.push(listener);
        },
      },
    };
  });
  await gotoApp(page);

  await page.locator('summary').filter({ hasText: /^Help$/ }).click();
  await page.getByRole('button', { name: 'Check Windows bridge' }).click();

  await expect(page.locator('#status')).toHaveText('Native bridge returned an unsupported protocol response.');
});

test('fake WebView2 bridge opens, saves, and saves as native files', async ({ page }) => {
  await installMockNativeBridge(page);
  await gotoApp(page);

  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.locator('details.menu[open]').getByRole('button', { name: 'Open file' }).click();

  await expect(page.locator('#activeFileLabel')).toHaveText('native-open.md');
  await expect(page.locator('#editor')).toHaveValue('# Native Open\n');
  await expect(page.locator('#status')).toHaveText('native-open.md opened from Windows.');

  await page.locator('#editor').fill('# Native Updated\n');
  await expect(page.locator('#activeFileLabel')).toContainText('edited in memory');
  await page.locator('#saveButton').click();

  await expect(page.locator('#status')).toHaveText('native-open.md saved.');
  await expect(page.locator('#activeFileLabel')).toHaveText('native-open.md');
  const savePayload = await page.evaluate(() => window.__nativeBridgeSaves.at(-1));
  expect(savePayload).toEqual({
    nativeHandleId: 'native-handle-1',
    content: '# Native Updated\n',
  });

  await page.locator('#saveAsButton').click();
  await expect(page.locator('#status')).toHaveText('native-copy.md saved.');
  await expect(page.locator('#activeFileLabel')).toHaveText('native-copy.md');

  const messages = await page.evaluate(() => window.__nativeBridgeMessages);
  expect(messages.some((message) => message.type === 'lensDocs.native.openFile')).toBe(true);
  expect(messages.some((message) => message.type === 'lensDocs.native.saveFile')).toBe(true);
  expect(messages.some((message) => message.type === 'lensDocs.native.saveFileAs')).toBe(true);
  const saveAsMessage = messages.findLast((message) => message.type === 'lensDocs.native.saveFileAs');
  expect(saveAsMessage.payload).toEqual({
    suggestedName: 'native-open.md',
    content: '# Native Updated\n',
  });
});

test('fake WebView2 bridge handles cancelled and malformed native file responses safely', async ({ page }) => {
  await installMockNativeBridge(page);
  await gotoApp(page);

  await page.evaluate(() => {
    window.__nativeBridgeScenario.openFile = 'cancelled';
  });
  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.locator('details.menu[open]').getByRole('button', { name: 'Open file' }).click();
  await expect(page.locator('#status')).toHaveText('Open file cancelled.');
  await expect(page.locator('#activeFileLabel')).toHaveText('No file selected');

  await page.evaluate(() => {
    window.__nativeBridgeScenario.openFile = 'malformed';
  });
  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.locator('details.menu[open]').getByRole('button', { name: 'Open file' }).click();
  await expect(page.locator('#status')).toHaveText(/unsupported protocol response|Using the browser fallback/);

  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.getByRole('button', { name: 'New Markdown file' }).click();
  await submitAppDialog(page, { button: 'Create file' });
  await page.locator('#editor').fill('# Draft\n');
  await expect(page.locator('#status')).toHaveText(/Rendered · edited in memory/);
  await page.evaluate(() => {
    window.__nativeBridgeScenario.saveFileAs = 'cancelled';
  });
  await page.locator('#saveAsButton').click();
  await expect(page.locator('#status')).toHaveText('Save as cancelled.');
  await expect(page.locator('#activeFileLabel')).toContainText('edited in memory');
});

test('fake WebView2 bridge opens and saves native workspace folders', async ({ page }) => {
  await installMockNativeBridge(page);
  await gotoApp(page);

  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.locator('details.menu[open]').getByRole('button', { name: 'Open folder' }).click();

  await expect(page.locator('#folderBadge')).toHaveText('Project Docs');
  await expect(page.locator('#fileCount')).toHaveText('2');
  await expect(page.locator('#fileList')).toContainText('README.md');
  await expect(page.locator('#fileList')).toContainText('diagrams/flow.mmd');
  await expect(page.locator('#activeFileLabel')).toHaveText('diagrams/flow.mmd');
  await expect(page.locator('#editor')).toHaveValue('flowchart TD\n  A-->B\n');
  await expect(page.locator('#status')).toHaveText('2 files loaded from Windows. 1 file skipped by workspace limits.');

  await page.locator('[data-path="README.md"]').click();
  await expect(page.locator('#activeFileLabel')).toHaveText('README.md');
  await expect(page.locator('#editor')).toHaveValue('# Native Workspace\n');
  await page.locator('#editor').fill('# Native Workspace Updated\n');
  await expect(page.locator('#activeFileLabel')).toContainText('edited in memory');
  await page.locator('#saveButton').click();

  await expect(page.locator('#status')).toHaveText('README.md saved.');
  await expect(page.locator('#activeFileLabel')).toHaveText('README.md');
  const savePayload = await page.evaluate(() => window.__nativeBridgeSaves.at(-1));
  expect(savePayload).toEqual({
    nativeHandleId: 'native-workspace-file-1',
    content: '# Native Workspace Updated\n',
  });
  const messages = await page.evaluate(() => window.__nativeBridgeMessages);
  expect(messages.some((message) => message.type === 'lensDocs.native.openFolder')).toBe(true);
  expect(messages.some((message) => message.type === 'lensDocs.native.saveWorkspaceFile')).toBe(true);
});

test('fake WebView2 bridge creates Markdown files in native workspaces', async ({ page }) => {
  await installMockNativeBridge(page);
  await gotoApp(page);

  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.locator('details.menu[open]').getByRole('button', { name: 'Open folder' }).click();
  await expect(page.locator('#folderBadge')).toHaveText('Project Docs');

  if (!await page.locator('details.menu[open]').isVisible()) {
    await page.locator('summary').filter({ hasText: /^File$/ }).click();
  }
  await page.getByRole('button', { name: 'New Markdown file' }).click();
  await page.locator('#appDialogPromptInput').fill('notes/new-note.md');
  await submitAppDialog(page, { button: 'Create file' });

  await expect(page.locator('#activeFileLabel')).toHaveText('notes/new-note.md');
  await expect(page.locator('#fileList')).toContainText('notes/new-note.md');
  await expect(page.locator('#editor')).toHaveValue('');
  await expect(page.locator('#status')).toHaveText('notes/new-note.md added to the Windows workspace.');
  const createMessage = await page.evaluate(() => window.__nativeBridgeMessages.findLast((message) => message.type === 'lensDocs.native.createWorkspaceFile'));
  expect(createMessage.payload).toEqual({
    nativeWorkspaceId: 'native-workspace-1',
    path: 'notes/new-note.md',
    content: '',
  });
});

test('fake WebView2 bridge handles cancelled and malformed native workspace responses safely', async ({ page }) => {
  await installMockNativeBridge(page);
  await gotoApp(page);

  await page.evaluate(() => {
    window.__nativeBridgeScenario.openFolder = 'cancelled';
  });
  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.locator('details.menu[open]').getByRole('button', { name: 'Open folder' }).click();
  await expect(page.locator('#status')).toHaveText('Open folder cancelled.');
  await expect(page.locator('#activeFileLabel')).toHaveText('No file selected');

  await page.evaluate(() => {
    window.__nativeBridgeScenario.openFolder = 'malformed';
  });
  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.locator('details.menu[open]').getByRole('button', { name: 'Open folder' }).click();
  await expect(page.locator('#status')).toHaveText(/unsupported protocol response|Using the browser fallback/);
});

test('File menu open folder falls back safely when Windows bridge lacks workspace capabilities', async ({ page }) => {
  await page.addInitScript(() => {
    const listeners = [];
    window.__nativeBridgeMessages = [];
    window.chrome = {
      webview: {
        postMessage(message) {
          window.__nativeBridgeMessages.push(message);
          window.setTimeout(() => {
            listeners.forEach((listener) => listener({
              data: {
                protocolVersion: 1,
                id: message.id,
                type: 'lensDocs.native.pong',
                source: 'LensDocsStudio.Windows',
                timestamp: '2026-06-09T00:00:00.000Z',
                payload: {
                  host: 'LensDocsStudio.Windows',
                  capabilities: ['diagnostics.ping', 'file.open', 'file.save', 'file.saveAs'],
                },
              },
            }));
          }, 0);
        },
        addEventListener(type, listener) {
          if (type === 'message') listeners.push(listener);
        },
      },
    };
    window.showDirectoryPicker = async () => {
      throw new DOMException('cancelled', 'AbortError');
    };
  });
  await gotoApp(page);

  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.locator('details.menu[open]').getByRole('button', { name: 'Open folder' }).click();

  const messages = await page.evaluate(() => window.__nativeBridgeMessages);
  expect(messages.some((message) => message.type === 'lensDocs.native.openFolder')).toBe(false);
  await expect(page.locator('#activeFileLabel')).toHaveText('No file selected');
});

test('custom context menu handles editor actions and preserves native fallbacks', async ({ page }) => {
  await gotoApp(page);
  const menu = page.locator('.context-menu');

  await setEditorValueAndSelection(page, 'alpha');
  await page.locator('#editor').click({ button: 'right', position: { x: 24, y: 20 } });
  await expect(menu).toBeVisible();
  await expect(menu.locator('[data-context-menu-action="editor-bold"]')).toBeVisible();
  await menu.locator('[data-context-menu-action="editor-bold"]').click();
  await expect(page.locator('#editor')).toHaveValue('**alpha**');

  await page.locator('#editor').click({ button: 'right', position: { x: 24, y: 20 } });
  await expect(menu).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(menu).toBeHidden();

  await page.locator('#editor').click({ button: 'right', position: { x: 24, y: 20 } });
  await expect(menu).toBeVisible();
  await menu.evaluate((element) => {
    element.scrollTop = 120;
    element.dispatchEvent(new Event('scroll'));
  });
  await expect(menu).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(menu).toBeHidden();

  await page.locator('#editor').click({ button: 'right', position: { x: 24, y: 20 } });
  await expect(menu).toBeVisible();
  await page.locator('#status').click();
  await expect(menu).toBeHidden();

  await page.locator('#fileSearch').click({ button: 'right' });
  await expect(menu).toBeHidden();

  await setEditorValueAndSelection(page, '[Example](https://example.com)');
  await expect(page.locator('#preview a')).toHaveText('Example');
  await page.locator('#preview a').click({ button: 'right' });
  await expect(menu).toBeHidden();
  await page.keyboard.press('Escape');

  await page.locator('summary').filter({ hasText: /^Help$/ }).click();
  await page.getByRole('button', { name: 'Open feature guide' }).click();
  await expect(page.locator('#editor')).toHaveJSProperty('readOnly', true);
  await dispatchContextMenu(page.locator('#editor'), { x: 24, y: 20 });
  await expect(menu).toBeVisible();
  await expect(menu.locator('[data-context-menu-action="editor-bold"]')).toBeDisabled();
  await expect(menu.locator('[data-context-menu-action="editor-paste-text"]')).toBeDisabled();
});

test('editor context menu copies Markdown with Mermaid diagrams as clipboard images', async ({ page }) => {
  await openFixture(page, 'mixed.md');
  await mockClipboardWrite(page);
  const menu = page.locator('.context-menu');

  await page.locator('#editor').evaluate((editor) => {
    editor.focus();
    editor.setSelectionRange(0, 0);
  });
  await dispatchContextMenu(page.locator('#editor'), { x: 24, y: 20 });
  await expect(menu.locator('[data-context-menu-action="editor-copy-markdown-images"]')).toBeVisible();
  await menu.locator('[data-context-menu-action="editor-copy-markdown-images"]').click();

  await expect(page.locator('#status')).toHaveText(/Markdown with images copied/, { timeout: 20_000 });
  await expect.poll(() => page.evaluate(() => window.__clipboardWriteTypes)).toEqual(['text/plain', 'text/html']);
  const copiedMarkdown = await page.evaluate(() => window.__copiedText);
  const copiedHtml = await page.evaluate(() => window.__copiedHtml);
  expect(copiedMarkdown).toContain('![Mermaid diagram 1](data:image/png;base64,');
  expect(copiedMarkdown).not.toContain('```mermaid');
  expect(copiedMarkdown).not.toContain('flowchart TD');
  expect(copiedMarkdown).toContain("const message = 'release ready';");
  expect(copiedHtml).toContain('<img');
  expect(copiedHtml).toContain('data:image/png;base64,');
});

test('custom context menu exposes preview-specific copy and export actions', async ({ page }) => {
  await openFixture(page, 'mixed.md');
  await mockClipboardWrite(page);
  const menu = page.locator('.context-menu');

  await expect(page.locator('.diagram-frame [data-diagram-action="copySource"]')).toBeVisible();
  await page.locator('.diagram-frame [data-diagram-action="copySource"]').click({ button: 'right' });
  await expect(menu.locator('[data-context-menu-action="preview-copy-diagram-source"]')).toBeVisible();
  await expect(menu.locator('[data-context-menu-action="preview-export-diagram-svg"]')).toBeVisible();
  await expect(menu.locator('[data-context-menu-action="preview-copy-diagram-png"]')).toBeVisible();
  await menu.locator('[data-context-menu-action="preview-copy-diagram-source"]').click();
  await expect.poll(() => page.evaluate(() => window.__copiedText)).toContain('flowchart TD');

  await page.locator('.diagram-frame [data-diagram-action="copySource"]').click({ button: 'right' });
  const [download] = await Promise.all([
    page.waitForEvent('download'),
    menu.locator('[data-context-menu-action="preview-export-diagram-svg"]').click(),
  ]);
  expect(await download.failure()).toBeNull();

  await dispatchContextMenu(page.locator('#preview pre code'));
  await expect(menu.locator('[data-context-menu-action="preview-copy-code"]')).toBeVisible();
  await menu.locator('[data-context-menu-action="preview-copy-code"]').click();
  await expect.poll(() => page.evaluate(() => window.__copiedText)).toContain("const message = 'release ready';");

  await dispatchContextMenu(page.locator('#preview table td').first());
  await expect(menu.locator('[data-context-menu-action="preview-copy-table"]')).toBeVisible();
  await expect(menu.locator('[data-context-menu-action="preview-download-table-csv"]')).toBeVisible();
  await menu.locator('[data-context-menu-action="preview-copy-table"]').click();
  await expect.poll(() => page.evaluate(() => window.__copiedText)).toBe('Area\tStatus\nPreview\tReady\nExport\tVerified');
});

test('visual refresh screenshot artefacts cover key shell states', async ({ page }, testInfo) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await gotoApp(page);
  await page.evaluate(() => {
    localStorage.setItem('md-mmd-renderer.theme', 'dark');
  });
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await attachViewportScreenshot(page, testInfo, 'phase-9-empty-dark');

  await loadSample(page);
  await attachViewportScreenshot(page, testInfo, 'phase-9-sample-dark');

  await page.locator('summary').filter({ hasText: /^Export$/ }).click();
  await expect(page.locator('#downloadButton')).toBeVisible();
  await attachViewportScreenshot(page, testInfo, 'phase-9-export-menu-dark');

  await gotoApp(page);
  await page.locator('summary').filter({ hasText: /^Create$/ }).click();
  await expect(page.locator('[data-create-template="requirements.devopsConclusion"]')).toBeVisible();
  const createMenuBox = await page.locator('#createMenu .menu-panel').boundingBox();
  const createViewport = page.viewportSize();
  expect(createMenuBox.x).toBeGreaterThanOrEqual(0);
  expect(createMenuBox.x + createMenuBox.width).toBeLessThanOrEqual(createViewport.width + 1);

  await gotoApp(page);
  await page.locator('summary').filter({ hasText: /^Create$/ }).click();
  await page.locator('[data-create-template="project.architecture"]').click();
  await expect(page.getByRole('heading', { name: 'Create Architecture Overview' })).toBeVisible();
  await attachViewportScreenshot(page, testInfo, 'phase-9-template-dialog-dark');

  await gotoApp(page);
  await page.evaluate(() => {
    localStorage.setItem('md-mmd-renderer.theme', 'light');
  });
  await gotoApp(page);
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await loadSample(page);
  await attachViewportScreenshot(page, testInfo, 'phase-9-sample-light');
  await gotoApp(page);
  await attachViewportScreenshot(page, testInfo, 'phase-9-empty-light');

  await page.evaluate(() => {
    localStorage.setItem('md-mmd-renderer.theme', 'dark');
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await gotoApp(page);
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await loadSample(page);
  await attachViewportScreenshot(page, testInfo, 'phase-9-mobile-sample-dark');
});

test('editor toolbar icon buttons keep markdown command behaviour', async ({ page }) => {
  await gotoApp(page);
  await expect(page.locator('#editorToolbar svg.toolbar-icon')).toHaveCount(23);
  await expect(page.locator('#editorToolbar .toolbar-section')).toHaveCount(5);

  const cases = [
    { command: 'bold', value: 'alpha', expected: '**alpha**' },
    { command: 'italic', value: 'alpha', expected: '*alpha*' },
    { command: 'strikethrough', value: 'alpha', expected: '~~alpha~~' },
    { command: 'heading', value: 'Title', expected: '# Title' },
    { command: 'bulletList', value: 'One', expected: '- One' },
    { command: 'numberedList', value: 'One\nTwo', expected: '1. One\n2. Two' },
    { command: 'taskList', value: '  One\n  - [x] Two', expected: '  - [ ] One\n  - [ ] Two' },
    { command: 'taskListDone', value: '  One\n  - [ ] Two', expected: '  - [x] One\n  - [x] Two' },
    { command: 'quote', value: 'Quote', expected: '> Quote' },
    { command: 'link', value: 'alpha', expected: '[alpha](https://example.com)' },
    { command: 'image', value: 'alpha', expected: '![alpha](image-url)' },
    { command: 'inlineCode', value: 'alpha', expected: '`alpha`' },
    { command: 'codeBlock', value: 'const x = 1;', expected: '```\nconst x = 1;\n```' },
    { command: 'horizontalRule', value: 'Before', expected: '---' },
    { command: 'mermaidBlock', value: 'flowchart LR\n  A --> B', expected: '```mermaid\nflowchart LR\n  A --> B\n```' },
  ];

  for (const testCase of cases) {
    await setEditorValueAndSelection(page, testCase.value);
    await page.locator(`[data-command="${testCase.command}"]`).click();
    await expect(page.locator('#editor')).toHaveValue(testCase.expected);
  }

  await setEditorValueAndSelection(page, 'sample', 6, 6);
  await page.locator('[data-command="bold"]').click();
  await expect(page.locator('#editor')).toHaveValue('sample **bold text**');

  await setEditorValueAndSelection(page, 'sample', 6, 6);
  await page.locator('[data-command="italic"]').click();
  await expect(page.locator('#editor')).toHaveValue('sample *italic text*');

  const toolbarOverflow = await page.locator('#editorToolbar').evaluate((toolbar) => toolbar.scrollWidth > toolbar.clientWidth + 1);
  expect(toolbarOverflow).toBe(false);
});

test('emoji picker search includes expanded documentation emojis', async ({ page }) => {
  await gotoApp(page);

  await page.locator('[data-command="emoji"]').click();
  await expect(page.locator('#insertHelperDialog')).toBeVisible();
  await page.locator('#emojiSearchInput').fill('security');
  await expect(page.getByRole('button', { name: 'Security' })).toBeVisible();
  await page.getByRole('button', { name: 'Security' }).click();
  await page.getByRole('button', { name: 'Insert emoji' }).click();

  await expect(page.locator('#editor')).toHaveValue('🛡️');
  await expect(page.locator('#status')).toHaveText(/Emoji inserted/);
});

test('table toolbar opens a visual editor for new and existing Markdown tables', async ({ page }) => {
  await gotoApp(page);
  await setEditorValueAndSelection(page, '');
  await page.locator('[data-command="table"]').click();
  await expect(page.getByRole('heading', { name: 'Edit table' })).toBeVisible();
  await expect(page.locator('#tableEditorDialog .utility-dialog-card')).toBeInViewport();
  await expect(page.locator('#tableEditorTitle')).toBeInViewport();
  await expect(page.locator('#tableEditorDialog .utility-dialog-card')).toHaveJSProperty('scrollLeft', 0);
  await expect(page.locator('#tableEditorAddRowButton svg')).toBeVisible();
  const tableActionButtonWidth = await page.locator('#tableEditorAddRowButton').evaluate((button) => button.getBoundingClientRect().width);
  expect(tableActionButtonWidth).toBeLessThanOrEqual(48);
  const cells = page.locator('#tableEditorGrid input');
  await cells.nth(0).fill('Name');
  await cells.nth(1).fill('Status');
  await cells.nth(3).fill('Preview');
  await cells.nth(4).fill('Ready');
  await page.getByRole('button', { name: 'Apply table' }).click();
  await expect(page.locator('#editor')).toHaveValue('| Name | Status | Column C |\n| --- | --- | --- |\n| Preview | Ready |  |\n|  |  |  |');

  const current = await page.locator('#editor').inputValue();
  const statusOffset = current.indexOf('Status');
  await setEditorValueAndSelection(page, current, statusOffset, statusOffset);
  await page.locator('[data-command="table"]').click();
  await expect(page.getByText(/Editing existing table/)).toBeVisible();
  await page.locator('#tableEditorGrid input').nth(1).fill('State');
  await page.getByRole('button', { name: 'Apply table' }).click();
  await expect(page.locator('#editor')).toHaveValue(/State/);
});

test('progress bar toolbar inserts semantic HTML with a preset colour', async ({ page }) => {
  await gotoApp(page);
  await setEditorValueAndSelection(page, 'Deployment readiness');
  await page.locator('[data-command="progressBar"]').click();
  await expect(page.getByRole('heading', { name: 'Edit progress bar' })).toBeVisible();
  await expect(page.locator('#progressBarEditorDialog .utility-dialog-card')).toBeInViewport();
  await expect(page.locator('#progressBarLabelInput')).toHaveValue('Deployment readiness');

  await page.locator('#progressBarPercentInput').fill('65');
  await page.getByRole('button', { name: 'Blue progress colour' }).click();
  await page.getByRole('button', { name: 'Apply progress bar' }).click();

  const expected = `<figure data-progress-bar data-progress-colour="blue" aria-label="Deployment readiness progress">
  <figcaption>
    <span data-progress-label>Deployment readiness</span>
    <span data-progress-value>65%</span>
  </figcaption>
  <progress max="100" value="65">65%</progress>
</figure>`;
  await expect(page.locator('#editor')).toHaveValue(expected);

  await renderPreviewWithShortcut(page);
  const progressBlock = page.locator('#preview figure[data-progress-bar]');
  await expect(progressBlock).toHaveAttribute('data-progress-colour', 'blue');
  await expect(progressBlock.locator('[data-progress-label]')).toHaveText('Deployment readiness');
  await expect(progressBlock.locator('[data-progress-value]')).toHaveText('65%');
  await expect(progressBlock.locator('progress')).toHaveAttribute('value', '65');
  await expect(progressBlock.locator('progress')).toHaveAttribute('max', '100');
});

test('progress bar toolbar edits an existing generated progress bar', async ({ page }) => {
  await gotoApp(page);
  const source = `<figure data-progress-bar data-progress-colour="green" aria-label="Initial progress">
  <figcaption>
    <span data-progress-label>Initial</span>
    <span data-progress-value>25%</span>
  </figcaption>
  <progress max="100" value="25">25%</progress>
</figure>`;
  const cursor = source.indexOf('25%');
  await setEditorValueAndSelection(page, source, cursor, cursor);

  await page.locator('[data-command="progressBar"]').click();
  await expect(page.locator('#progressBarEditorSummary')).toHaveText('Editing an existing progress bar.');
  await expect(page.locator('#progressBarLabelInput')).toHaveValue('Initial');
  await expect(page.locator('#progressBarPercentInput')).toHaveValue('25');
  await page.locator('#progressBarLabelInput').fill('Migration complete');
  await page.locator('#progressBarPercentInput').fill('80');
  await page.getByRole('button', { name: 'Red progress colour' }).click();
  await page.getByRole('button', { name: 'Apply progress bar' }).click();

  await expect(page.locator('#editor')).toHaveValue(`<figure data-progress-bar data-progress-colour="red" aria-label="Migration complete progress">
  <figcaption>
    <span data-progress-label>Migration complete</span>
    <span data-progress-value>80%</span>
  </figcaption>
  <progress max="100" value="80">80%</progress>
</figure>`);

  await renderPreviewWithShortcut(page);
  const progressBlock = page.locator('#preview figure[data-progress-bar]');
  await expect(progressBlock).toHaveAttribute('data-progress-colour', 'red');
  await expect(progressBlock.locator('[data-progress-label]')).toHaveText('Migration complete');
  await expect(progressBlock.locator('[data-progress-value]')).toHaveText('80%');
  await expect(progressBlock.locator('progress')).toHaveAttribute('value', '80');
  await expect(progressBlock.locator('progress')).toHaveAttribute('max', '100');
});

test('rich insert toolbar helpers create emoji, callouts, badges, details, figures, shortcuts, and anchors', async ({ page }) => {
  await gotoApp(page);

  async function openHelper(command) {
    await setEditorValueAndSelection(page, '');
    await page.locator(`[data-command="${command}"]`).click();
    await expect(page.locator('#insertHelperDialog')).toBeVisible();
  }

  await openHelper('emoji');
  await page.getByRole('button', { name: 'Launch' }).click();
  await page.locator('#insertHelperApplyButton').click();
  await expect(page.locator('#editor')).toHaveValue('🚀');

  await openHelper('callout');
  await page.locator('#calloutTypeInput').selectOption('WARNING');
  await page.locator('#calloutTitleInput').fill('Deployment note');
  await page.locator('#calloutBodyInput').fill('Confirm production readiness.');
  await page.locator('#insertHelperApplyButton').click();
  await expect(page.locator('#editor')).toHaveValue('> [!WARNING]\n> **Deployment note**\n>\n> Confirm production readiness.');

  await openHelper('statusBadge');
  await page.locator('#statusBadgeTextInput').fill('Blocked by API');
  await page.getByRole('button', { name: 'Blocked badge' }).click();
  await page.locator('#insertHelperApplyButton').click();
  await expect(page.locator('#editor')).toHaveValue('<span data-status-badge data-status-kind="blocked" aria-label="Status: Blocked by API">Blocked by API</span>');

  await openHelper('detailsBlock');
  await page.locator('#detailsSummaryInput').fill('Release evidence');
  await page.locator('#detailsBodyInput').fill('Screenshots and logs are attached.');
  await page.locator('#insertHelperApplyButton').click();
  await expect(page.locator('#editor')).toHaveValue('<details data-details-block>\n  <summary>Release evidence</summary>\n\nScreenshots and logs are attached.\n</details>');

  await openHelper('imageFigure');
  await page.locator('#imageFigureSrcInput').fill('assets/images/roadmap.png');
  await page.locator('#imageFigureAltInput').fill('Roadmap diagram');
  await page.locator('#imageFigureCaptionInput').fill('Quarterly delivery roadmap');
  await page.locator('#insertHelperApplyButton').click();
  await expect(page.locator('#editor')).toHaveValue('<figure data-image-figure>\n  <img src="assets/images/roadmap.png" alt="Roadmap diagram">\n  <figcaption>Quarterly delivery roadmap</figcaption>\n</figure>');

  await openHelper('keyboardShortcut');
  await page.locator('#shortcutKeysInput').fill('Ctrl+Shift+P');
  await page.locator('#insertHelperApplyButton').click();
  await expect(page.locator('#editor')).toHaveValue('<span data-shortcut aria-label="Keyboard shortcut Ctrl Shift P"><kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>P</kbd></span>');

  await openHelper('anchor');
  await page.locator('#anchorIdInput').fill('release-ready');
  await page.locator('#anchorTextInput').fill('Release ready');
  await page.locator('#insertHelperApplyButton').click();
  await expect(page.locator('#editor')).toHaveValue('<span id="release-ready" data-doc-anchor>Release ready</span>');

  await setEditorValueAndSelection(page, [
    '<span data-status-badge data-status-kind="ready" aria-label="Status: Ready">Ready</span>',
    '<details data-details-block>',
    '  <summary>Release evidence</summary>',
    '',
    'Screenshots and logs are attached.',
    '</details>',
    '<figure data-image-figure>',
    '  <img src="assets/images/roadmap.png" alt="Roadmap diagram">',
    '  <figcaption>Quarterly delivery roadmap</figcaption>',
    '</figure>',
    '<span data-shortcut aria-label="Keyboard shortcut Ctrl Shift P"><kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>P</kbd></span>',
    '<span id="release-ready" data-doc-anchor>Release ready</span>',
  ].join('\n'));
  await renderPreviewWithShortcut(page);
  await expect(page.locator('#preview [data-status-badge]')).toHaveAttribute('data-status-kind', 'ready');
  await expect(page.locator('#preview details[data-details-block] summary')).toHaveText('Release evidence');
  await expect(page.locator('#preview figure[data-image-figure] figcaption')).toHaveText('Quarterly delivery roadmap');
  await expect(page.locator('#preview [data-shortcut] kbd')).toHaveCount(3);
  await expect(page.locator('#preview [data-doc-anchor]')).toHaveAttribute('id', 'release-ready');
});

test('quick switcher, workspace content search, and editor find/replace work together', async ({ page }) => {
  await loadVirtualWorkspace(page, [
    { name: 'first.md', text: '# First\nAlpha target lives here.\nAlpha target again.' },
    { name: 'second.md', text: '# Second\nBeta content points elsewhere.' },
  ]);

  await page.keyboard.press('Control+P');
  await expect(page.locator('#workspaceSearchDialog')).toBeVisible();
  await page.locator('#workspaceSearchInput').fill('second');
  await page.keyboard.press('Enter');
  await expect(page.locator('#activeFileLabel')).toContainText('second.md');

  await page.keyboard.press('Control+Shift+F');
  await page.locator('#workspaceSearchInput').fill('target lives');
  await expect(page.locator('#workspaceSearchCount')).toHaveText(/1 result/);
  await page.keyboard.press('Enter');
  await expect(page.locator('#activeFileLabel')).toContainText('first.md');

  await page.keyboard.press('Control+H');
  await page.locator('#findReplaceFindInput').fill('Alpha (target)');
  await page.locator('#findReplaceRegexToggle').check();
  await page.locator('#findReplaceReplaceInput').fill('Omega $1');
  await page.getByRole('button', { name: 'Replace all' }).click();
  await expect(page.locator('#editor')).toHaveValue(/Omega target lives here/);
  await expect(page.locator('#editor')).toHaveValue(/Omega target again/);
});

test('wikilinks navigate, backlinks appear in review, and Docs Site export strips app actions', async ({ page }) => {
  await loadVirtualWorkspace(page, [
    { name: 'index.md', text: '# Index\nSee [[second|the second page]].\n' },
    { name: 'second.md', text: '# Second\nBacklinked page.\n' },
  ]);

  await expect(page.locator('#preview a.wikilink')).toHaveText('the second page');
  await page.locator('#preview a.wikilink').click();
  await expect(page.locator('#activeFileLabel')).toContainText('second.md');

  await page.getByRole('button', { name: 'Document review' }).click();
  const backlinkAudit = page.locator('.document-review-links').filter({ hasText: 'Backlinks (1)' });
  await expect(backlinkAudit).toContainText('index.md:2');

  const docsPath = await clickDocsSiteExportDownload(page, { title: 'Wiki Docs', description: 'Wikilink export check.' });
  const entries = await readZipEntries(docsPath);
  const searchIndex = JSON.parse(getZipText(entries, 'assets/search-index.json'));
  const indexPage = searchIndex.pages.find((item) => item.path === 'index.md');
  expect(indexPage.html).not.toContain('data-wikilink-target');
  expect(indexPage.html).toContain('href="#second"');
});

test('document audit flags broken references and docs map opens as read-only markdown', async ({ page }) => {
  await loadVirtualWorkspace(page, [
    {
      name: 'index.md',
      text: '# Home\n\n[[Missing Page]]\n\n[Missing link](missing.md)\n\n[Second](second.md)\n\n![Missing image](assets/images/missing.png)\n',
    },
    { name: 'second.md', text: '# Second\n\nBack to [Home](index.md).\n' },
    { name: 'orphan.md', text: '# Orphan\n\nNo incoming links yet.\n' },
  ]);

  await page.locator('#documentReviewToggleButton').click();
  await expect(page.locator('#documentReviewAlerts')).toContainText('unresolved wikilink');
  await expect(page.locator('#documentReviewAlerts')).toContainText('relative document link');
  await expect(page.locator('#documentReviewAlerts')).toContainText('local image reference');
  await expect(page.locator('#documentReviewAlerts')).toContainText('Workspace audit');
  await expect(page.locator('#documentReviewAlerts')).toContainText('page without backlinks');

  await page.locator('summary').filter({ hasText: /^View$/ }).click();
  await page.getByRole('button', { name: 'Open docs map' }).click();
  await expect(page.locator('#activeFileLabel')).toHaveText(/docs-map\.md · read-only/);
  await expect(page.locator('#editor')).toHaveValue(/## Unresolved Links/);
  await expect(page.locator('#preview')).toContainText('Documentation Map', { timeout: 20_000 });
  await expect(page.locator('#preview .diagram-frame')).toHaveCount(1, { timeout: 20_000 });
});

test('Markdown governance flags lint issues, navigates to source, and stays out of exports', async ({ page }) => {
  await loadVirtualWorkspace(page, [
    {
      name: 'README.md',
      text: `# Governance Home

### Skipped Heading

![](assets/logo.png)

[Missing guide](missing.md)

[[Ghost Page]]

| Feature | Value |
| -- | --- |
| One | Two | Three |

The color choice is organized for release.

TODO: finish the checklist before release.

\`\`\`js
// TODO: this code marker is ignored.
const color = 'blue';
\`\`\`

[Guide](guide.md)
`,
    },
    {
      name: 'guide.md',
      text: `# Guide

FIXME: replace this note.

The favorite center text should be reviewed.
`,
    },
  ]);

  await page.locator('#documentReviewToggleButton').click();
  await expect(page.locator('#documentReviewAlerts')).toContainText('Governance');
  await expect(page.locator('#documentReviewAlerts')).toContainText('Heading hierarchy');
  await expect(page.locator('#documentReviewAlerts')).toContainText('Internal links');
  await expect(page.locator('#documentReviewAlerts')).toContainText('Alt text');
  await expect(page.locator('#documentReviewAlerts')).toContainText('Tables');
  await expect(page.locator('#documentReviewAlerts')).toContainText('British English');
  await expect(page.locator('#documentReviewAlerts')).toContainText('TODO/FIXME');
  await expect(page.locator('#documentReviewSummary')).toContainText(/notes/);
  await expect(page.locator('#documentReviewMetrics')).toContainText('Governance:');

  const guideIssue = page.locator('[data-governance-path="guide.md"]').filter({ hasText: 'FIXME marker' });
  await expect(guideIssue).toBeVisible();
  await guideIssue.click();
  await expect(page.locator('#activeFileLabel')).toContainText('guide.md');
  await expect.poll(() => page.locator('#editor').evaluate((editor) => editor.value.slice(editor.selectionStart, editor.selectionEnd))).toBe('FIXME');
  await expect(page.locator('#status')).toHaveText(/Opened governance issue at guide\.md:3/);

  const htmlPath = await clickExportDownload(page, 'Export HTML');
  const html = await readFile(htmlPath, 'utf8');
  expect(html).not.toContain('Markdown governance');
  expect(html).not.toContain('data-governance-path');
  expect(html).not.toContain('TODO/FIXME');
});

test('local draft recovery and large deletion protection guard browser-local edits', async ({ page }) => {
  const file = { name: 'draft.md', mimeType: 'text/markdown', buffer: Buffer.from('# Draft\nThis paragraph should survive local recovery because it is long enough to trigger the deletion guard when most of it disappears.\n') };
  await gotoApp(page);
  await page.locator('#fileInput').setInputFiles(file);
  await expect(page.locator('#status')).toHaveText(/Rendered/);
  await setEditorValueAndSelection(page, '# Draft\nRecovered browser-local draft.\n');
  await page.waitForTimeout(800);

  await page.reload();
  await page.locator('#fileInput').setInputFiles(file);
  await expect(page.getByRole('heading', { name: 'Recover local draft?' })).toBeVisible();
  await page.getByRole('button', { name: 'Restore draft' }).click();
  await expect(page.locator('#editor')).toHaveValue(/Recovered browser-local draft/);

  await setEditorValueAndSelection(page, '# Draft\n', 8, 8);
  await page.locator('#saveButton').click();
  await expect(page.getByRole('heading', { name: 'Large deletion detected' })).toBeVisible();
  await page.getByRole('button', { name: 'Undo deletion' }).click();
  await expect(page.locator('#editor')).toHaveValue(/This paragraph should survive local recovery/);
});

test('manual local snapshots can be created, compared, restored, and deleted', async ({ page }) => {
  await openFixture(page, 'plain.md');

  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.getByRole('button', { name: 'Create snapshot' }).click();
  await expect(page.locator('#status')).toHaveText(/Snapshot created/);

  await setEditorValueAndSelection(page, '# Changed\n\nTemporary edit.\n');
  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.getByRole('button', { name: 'Manage snapshots' }).click();
  await expect(page.getByRole('heading', { name: /Snapshots for plain\.md/ })).toBeVisible();
  await page.getByText('Compare with current document').click();
  await expect(page.locator('.snapshot-diff')).toContainText('Publishing Fixture');
  await page.getByRole('button', { name: 'Restore' }).click();
  await expect(page.locator('#editor')).toHaveValue(/Publishing Fixture/);
  await expect(page.locator('#status')).toHaveText(/Restored snapshot/);

  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await page.getByRole('button', { name: 'Manage snapshots' }).click();
  await page.getByRole('button', { name: 'Delete' }).click();
  await expect(page.locator('.snapshot-list')).toContainText('No snapshots');
});

test('local templates, snippets, and export profiles persist in the browser', async ({ page }) => {
  await openFixture(page, 'plain.md');
  page.on('dialog', (dialog) => {
    throw new Error(`Unexpected native dialog: ${dialog.message()}`);
  });

  await page.locator('summary').filter({ hasText: /^Create$/ }).click();
  await page.getByRole('button', { name: 'Save document as template' }).click();
  await submitAppDialog(page, { value: 'Fixture Template', button: 'Save template' });
  await page.locator('summary').filter({ hasText: /^Create$/ }).click();
  await expect(page.getByRole('button', { name: 'Fixture Template' })).toBeVisible();

  const selectedText = 'Publishing Fixture';
  await page.locator('#editor').evaluate((editor, text) => {
    const start = editor.value.indexOf(text);
    editor.focus();
    editor.setSelectionRange(start, start + text.length);
  }, selectedText);
  await page.getByRole('button', { name: 'Save selection as snippet' }).click();
  await submitAppDialog(page, { value: 'Fixture Snippet', button: 'Save snippet' });
  await page.locator('summary').filter({ hasText: /^Create$/ }).click();
  await expect(page.getByRole('button', { name: 'Fixture Snippet' })).toBeVisible();

  await setEditorValueAndSelection(page, '# Changed\n');
  await page.getByRole('button', { name: 'Fixture Template' }).click();
  await submitAppDialog(page, { button: 'Discard changes' });
  await expect(page.locator('#editor')).toHaveValue(/Publishing Fixture/);
  await page.locator('#editor').evaluate((editor) => {
    editor.setSelectionRange(editor.value.length, editor.value.length);
  });
  await page.locator('summary').filter({ hasText: /^Create$/ }).click();
  await page.getByRole('button', { name: 'Fixture Snippet' }).click();
  await expect(page.locator('#editor')).toHaveValue(/Publishing Fixture[\s\S]*Publishing Fixture/);

  await page.locator('summary').filter({ hasText: /^Export$/ }).click();
  await page.locator('#devopsMarkdownExportToggle').check();
  await page.getByRole('button', { name: 'Save export profile' }).click();
  await submitAppDialog(page, { value: 'DevOps Profile', button: 'Save profile' });
  await page.locator('summary').filter({ hasText: /^Export$/ }).click();
  await page.locator('#devopsMarkdownExportToggle').uncheck();
  await page.getByRole('button', { name: 'Apply export profile' }).click();
  await expect(page.locator('#devopsMarkdownExportToggle')).toBeChecked();
});

test('built-in export profiles are session-only and do not persist DevOps settings', async ({ page }) => {
  await openFixture(page, 'mixed.md');
  const initialKeys = await page.evaluate(() => Object.keys(localStorage).sort());
  await expect(page.locator('#devopsMarkdownExportToggle')).not.toBeChecked();

  await page.locator('summary').filter({ hasText: /^Export$/ }).click();
  await expect(page.getByRole('button', { name: /Generic documentation/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /GitHub Pages docs site/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /Azure DevOps Wiki markdown/ })).toBeVisible();
  await expect(page.locator('#builtInExportProfiles').getByRole('button', { name: /Artefact review pack/ })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Export artefact review pack' })).toBeDisabled();

  await page.getByRole('button', { name: /GitHub Pages docs site/ }).click();
  await expect(page.locator('#activeExportProfileLabel')).toContainText('GitHub Pages docs site');
  await expect.poll(() => page.evaluate(() => Object.keys(localStorage).sort())).toEqual(initialKeys);

  await page.getByRole('button', { name: /Generic documentation/ }).click();
  await expect(page.locator('#activeExportProfileLabel')).toContainText('Generic documentation');
  await expect.poll(() => page.evaluate(() => Object.keys(localStorage).sort())).toEqual(initialKeys);

  await page.getByRole('button', { name: /Azure DevOps Wiki markdown/ }).click();
  await expect(page.locator('#activeExportProfileLabel')).toContainText('Azure DevOps Wiki markdown');
  await expect(page.locator('#devopsMarkdownExportToggle')).toBeChecked();
  await expect.poll(() => page.evaluate(() => localStorage.getItem('md-mmd-renderer.devopsMarkdownExport'))).toBeNull();
  await expect.poll(() => page.evaluate(() => Object.keys(localStorage).sort())).toEqual(initialKeys);

  const bundlePath = await clickExportDownload(page, 'Export Markdown Bundle');
  const entries = await readZipEntries(bundlePath);
  expect(normaliseLineEndings(getZipText(entries, 'mixed.md'))).toContain('::: mermaid');
  expect(entries.has('lens-artifact-bundle.json')).toBe(false);

  await page.reload();
  await expect(page.locator('#activeExportProfileLabel')).toContainText('None');
  await page.locator('summary').filter({ hasText: /^Export$/ }).click();
  await expect(page.locator('#devopsMarkdownExportToggle')).not.toBeChecked();
});

test('writer shortcut is disabled while input maximise handles focused writing', async ({ page }) => {
  await gotoApp(page);
  await loadSample(page);
  await page.evaluate(() => {
    localStorage.setItem('md-mmd-renderer.typewriterMode', 'true');
  });
  await page.reload();
  await expect(page.locator('#app')).not.toHaveClass(/typewriter-mode/);

  await page.keyboard.press('Control+Backslash');
  await expect(page.locator('#app')).not.toHaveClass(/typewriter-mode/);
  await expect(page.locator('#typewriterToggleButton')).toHaveCount(0);

  await page.locator('#inputMaximizeButton').click();
  await expect(page.locator('#app')).toHaveClass(/input-maximized/);
  await expect(page.locator('.preview-pane')).toBeHidden();
  const maximizedWidth = await page.locator('.input-pane').evaluate((pane) => pane.getBoundingClientRect().width);
  expect(maximizedWidth).toBeGreaterThan(800);
  await page.keyboard.press('Control+F11');
  await expect(page.locator('#app')).toHaveClass(/focus-mode/);
  await expect(page.locator('#app')).toHaveClass(/input-maximized/);
  await page.keyboard.press('Escape');
  await expect(page.locator('#app')).not.toHaveClass(/focus-mode/);
  await expect(page.locator('#app')).toHaveClass(/input-maximized/);
  await page.locator('#inputMaximizeButton').click();
  await expect(page.locator('#app')).not.toHaveClass(/input-maximized/);
  await expect(page.locator('.preview-pane')).toBeVisible();
});

test('paste auto-converts formatted HTML and spreadsheet tables while leaving plain text alone', async ({ page }) => {
  await gotoApp(page);

  await setEditorValueAndSelection(page, 'Intro', 5, 5);
  await pasteIntoEditor(page, { text: 'Name\tQty\nApples\t4\nPears\t7' });
  await expect(page.locator('#editor')).toHaveValue('Intro\n\n| Name | Qty |\n| --- | --- |\n| Apples | 4 |\n| Pears | 7 |');
  await expect(page.locator('#status')).toHaveText(/Table pasted as Markdown/);
  await expect(page.locator('#preview table')).toHaveCount(1, { timeout: 20_000 });

  await setEditorValueAndSelection(page, '');
  await pasteIntoEditor(page, {
    html: '<table><tr><th>Area</th><th>Status</th></tr><tr><td>Preview</td><td>Ready</td></tr></table>',
    text: 'Area\tStatus\nPreview\tReady',
  });
  await expect(page.locator('#editor')).toHaveValue('| Area | Status |\n| --- | --- |\n| Preview | Ready |');
  await expect(page.locator('#preview table')).toHaveCount(1, { timeout: 20_000 });

  await setEditorValueAndSelection(page, '');
  await pasteIntoEditor(page, {
    html: '<h2>Title</h2><p>Hello <strong>world</strong> <a href="https://example.com">link</a></p><ul><li>One</li><li>Two</li></ul>',
    text: 'Title\nHello world link\nOne\nTwo',
  });
  await expect(page.locator('#editor')).toHaveValue('## Title\n\nHello **world** [link](https://example.com)\n\n- One\n- Two');
  await expect(page.locator('#status')).toHaveText(/HTML pasted as Markdown/);
  await expect(page.locator('#preview h2')).toHaveText('Title', { timeout: 20_000 });

  await setEditorValueAndSelection(page, '');
  await pasteIntoEditor(page, { text: 'Just normal text\nwith words.' });
  await expect(page.locator('#editor')).toHaveValue('Just normal text\nwith words.');
});

test('paste preserves full Markdown documents when clipboard HTML contains tables', async ({ page }) => {
  await gotoApp(page);
  const markdown = [
    '# Imported Story',
    '',
    '| Area | Status |',
    '| --- | --- |',
    '| Preview | Ready |',
    '',
    '```mermaid',
    'flowchart TD',
    '  A[Start] --> B[Finish]',
    '```',
    '',
    '## Notes',
    '',
    '| Later | Value |',
    '| --- | --- |',
    '| Keep | Everything |',
  ].join('\n');

  await setEditorValueAndSelection(page, '');
  await pasteIntoEditor(page, {
    html: '<table><tr><th>Area</th><th>Status</th></tr><tr><td>Preview</td><td>Ready</td></tr></table>',
    text: markdown,
  });

  await expect(page.locator('#editor')).toHaveValue(markdown);
  await expect(page.locator('#status')).toHaveText(/Plain text pasted/);
  await renderPreviewWithShortcut(page);
  await expect(page.locator('#preview table')).toHaveCount(2);
  await expect(page.locator('#preview .diagram-frame svg')).toHaveCount(1);
});

test('Paste Special inserts table, text, code block, and supports next-paste fallback', async ({ page }) => {
  await gotoApp(page);

  await setEditorValueAndSelection(page, '');
  await mockClipboardRead(page, { text: 'Feature,Status\nPaste,Ready' });
  await clickEditAction(page, 'Paste as table');
  await expect(page.locator('#editor')).toHaveValue('| Feature | Status |\n| --- | --- |\n| Paste | Ready |');
  await expect(page.locator('#status')).toHaveText(/Table pasted as Markdown/);

  await setEditorValueAndSelection(page, 'Before:', 7, 7);
  await mockClipboardRead(page, { text: 'raw\tplain' });
  await clickEditAction(page, 'Paste as text');
  await expect(page.locator('#editor')).toHaveValue('Before:raw\tplain');
  await expect(page.locator('#status')).toHaveText(/Plain text pasted/);

  await setEditorValueAndSelection(page, '');
  await mockClipboardRead(page, { text: 'const value = 1;' });
  await clickEditAction(page, 'Paste as code block');
  await expect(page.locator('#editor')).toHaveValue('```\nconst value = 1;\n```');
  await expect(page.locator('#status')).toHaveText(/Code block pasted/);

  await setEditorValueAndSelection(page, '');
  await mockClipboardRead(page, { reject: true });
  await clickEditAction(page, 'Paste as table');
  await expect(page.locator('#status')).toHaveText(/Clipboard access blocked/);
  await pasteIntoEditor(page, { text: 'Column A\tColumn B\nLeft\tRight' });
  await expect(page.locator('#editor')).toHaveValue('| Column A | Column B |\n| --- | --- |\n| Left | Right |');
});

test('Paste Special supports quote, HTML Markdown, lists, checklist, numbered list, and Mermaid', async ({ page }) => {
  await gotoApp(page);

  await setEditorValueAndSelection(page, '');
  await mockClipboardRead(page, { text: 'Alpha\n\nBeta' });
  await clickEditAction(page, 'Paste as quote');
  await expect(page.locator('#editor')).toHaveValue('> Alpha\n>\n> Beta');
  await expect(page.locator('#status')).toHaveText(/Quote pasted/);

  await setEditorValueAndSelection(page, '');
  await mockClipboardRead(page, {
    html: '<h2>Title</h2><p>Hello <strong>world</strong> <a href="https://example.com">link</a></p><ul><li>One</li><li>Two</li></ul>',
    text: 'Title\nHello world link\nOne\nTwo',
  });
  await clickEditAction(page, 'Paste HTML as Markdown');
  await expect(page.locator('#editor')).toHaveValue('## Title\n\nHello **world** [link](https://example.com)\n\n- One\n- Two');
  await expect(page.locator('#status')).toHaveText(/HTML pasted as Markdown/);

  await setEditorValueAndSelection(page, '');
  await mockClipboardRead(page, { text: '- Alpha\n2. Beta\n[ ] Gamma' });
  await clickEditAction(page, 'Paste as list');
  await expect(page.locator('#editor')).toHaveValue('- Alpha\n- Beta\n- Gamma');
  await expect(page.locator('#status')).toHaveText(/List pasted/);

  await setEditorValueAndSelection(page, '');
  await mockClipboardRead(page, { html: '<ul><li>Review docs</li><li>Ship release</li></ul>' });
  await clickEditAction(page, 'Paste as checklist');
  await expect(page.locator('#editor')).toHaveValue('- [ ] Review docs\n- [ ] Ship release');
  await expect(page.locator('#status')).toHaveText(/Checklist pasted/);

  await setEditorValueAndSelection(page, '');
  await mockClipboardRead(page, { text: 'Alpha\nBeta' });
  await clickEditAction(page, 'Paste as numbered list');
  await expect(page.locator('#editor')).toHaveValue('1. Alpha\n2. Beta');
  await expect(page.locator('#status')).toHaveText(/Numbered list pasted/);

  await setEditorValueAndSelection(page, '');
  await mockClipboardRead(page, { text: 'flowchart LR\n  A --> B' });
  await clickEditAction(page, 'Paste as Mermaid block');
  await expect(page.locator('#editor')).toHaveValue('```mermaid\nflowchart LR\n  A --> B\n```');
  await expect(page.locator('#status')).toHaveText(/Mermaid block pasted/);
});

test('editor line numbers, Mermaid autocomplete, and layout modes work', async ({ page }) => {
  await openFixture(page, 'plain.md');

  await page.locator('#editor').fill('# One\n\nBody text\n\n## Two');
  await expect(page.locator('#editorLineNumbers')).toContainText('5');

  await page.locator('#inputMaximizeButton').click();
  await expect(page.locator('#app')).toHaveClass(/input-maximized/);
  await expect(page.locator('#inputMaximizeButton')).toHaveAttribute('aria-label', 'Restore split editor');
  await expect(page.locator('#inputMaximizeButton')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('.preview-pane')).toBeHidden();
  await page.locator('#inputMaximizeButton').click();
  await expect(page.locator('#app')).not.toHaveClass(/input-maximized/);
  await expect(page.locator('#inputMaximizeButton')).toHaveAttribute('aria-label', 'Maximise editor');
  await expect(page.locator('#inputMaximizeButton')).toHaveAttribute('aria-pressed', 'false');

  await page.locator('summary').filter({ hasText: /^View$/ }).click();
  await page.locator('#layoutModeControl [data-layout-mode="preview"]').click();
  await expect(page.locator('#app')).toHaveClass(/layout-preview/);

  await page.reload();
  await expect(page.locator('#app')).toHaveClass(/layout-preview/);
  await page.locator('summary').filter({ hasText: /^View$/ }).click();
  await page.locator('#layoutModeControl [data-layout-mode="split"]').click();
  await expect(page.locator('#app')).toHaveClass(/layout-split/);

  await openFixture(page, 'autocomplete.mmd');
  await page.locator('#editor').focus();
  await page.keyboard.press('Control+Space');
  await expect(page.locator('#mermaidAutocomplete')).toBeVisible();
  await page.keyboard.press('Enter');
  await expect(page.locator('#editor')).toHaveValue(/flowchart TD/);
  await expect(page.locator('#mermaidAutocomplete')).toBeHidden();
});

test('editor find icon opens inline search with keyboard navigation', async ({ page }) => {
  await gotoApp(page);
  await setEditorValueAndSelection(page, '# Review\n\nFirst review note.\n\nSecond review note.\n\n[Source only](editor-only-target)', 0, 0);
  await renderPreviewWithShortcut(page);

  await page.keyboard.press('Control+F');
  await expect(page.locator('#editorFindPanel')).toBeVisible();
  await expect(page.locator('#findReplaceDialog')).toBeHidden();
  const beforeTypingSearch = await page.locator('#editor').inputValue();
  await page.keyboard.type('review');
  await expect(page.locator('#editorFindInput')).toHaveValue('review');
  await expect(page.locator('#editorFindInput')).toBeFocused();
  await expect(page.locator('#editor')).toHaveValue(beforeTypingSearch);
  await expect(page.locator('#editorFindCount')).toHaveText('1/3');
  await expect(page.locator('#editorFindLayer .editor-find-hit')).toHaveCount(3);
  await expect(page.locator('#editorFindLayer .editor-find-hit.active')).toHaveCount(1);
  await expect(page.locator('mark.selection-sync-hit')).toHaveCount(0);
  await expect(page.locator('mark.preview-search-hit')).toHaveCount(0);

  const beforeEnter = await page.locator('#editor').inputValue();
  await page.keyboard.press('Enter');
  await expect(page.locator('#editorFindCount')).toHaveText('2/3');
  await expect(page.locator('#editor')).toHaveValue(beforeEnter);

  await page.keyboard.press('Shift+Enter');
  await expect(page.locator('#editorFindCount')).toHaveText('1/3');
  await expect(page.locator('#editor')).toHaveValue(beforeEnter);

  await page.keyboard.press('Escape');
  await expect(page.locator('#editorFindPanel')).toBeHidden();

  await page.keyboard.press('Control+F');
  await expect(page.locator('#editorFindPanel')).toBeVisible();
  await page.locator('#editorFindInput').fill('editor-only-target');
  await expect(page.locator('#editorFindCount')).toHaveText('1/1');
  await expect.poll(() => page.locator('#editor').evaluate((editor) => editor.value.slice(editor.selectionStart, editor.selectionEnd))).toBe('editor-only-target');
  await expect(page.locator('#editorFindLayer .editor-find-hit')).toHaveCount(1);
  await expect(page.locator('#editorFindLayer .editor-find-hit.active')).toHaveText('editor-only-target');
  await expect(page.locator('mark.selection-sync-hit')).toHaveCount(0);
  await expect(page.locator('mark.preview-search-hit')).toHaveCount(0);
  await page.locator('#editorFindInput').fill('review');
  await expect(page.locator('#editorFindCount')).toHaveText('1/3');
  await page.locator('#editorFindNextButton').click();
  await expect(page.locator('#editorFindCount')).toHaveText('2/3');
  await page.locator('#editorFindPrevButton').click();
  await expect(page.locator('#editorFindCount')).toHaveText('1/3');
  await page.locator('#editorFindInput').focus();
  await page.keyboard.press('Escape');
  await expect(page.locator('#editorFindPanel')).toBeHidden();

  await page.locator('#editorFindToggleButton').click();
  await expect(page.locator('#editorFindPanel')).toBeVisible();
  await expect(page.locator('#editorFindToggleButton')).toHaveAttribute('aria-pressed', 'true');
  await page.locator('#editorFindClearButton').click();
  await expect(page.locator('#editorFindPanel')).toBeHidden();
  await expect(page.locator('#editorFindToggleButton')).toHaveAttribute('aria-pressed', 'false');
  await expect(page.locator('#editorFindLayer .editor-find-hit')).toHaveCount(0);
});

test('focus mode exposes a visible exit button and keeps Escape fallback', async ({ page }) => {
  await gotoApp(page);

  await expect(page.locator('#focusModeExitButton')).toBeHidden();
  await expect(page.locator('#focusModeButton')).toHaveAttribute('title', 'Focus Mode (Ctrl+F11)');

  const f11Result = await page.evaluate(() => {
    const event = new KeyboardEvent('keydown', { key: 'F11', bubbles: true, cancelable: true });
    const notPrevented = window.dispatchEvent(event);
    return {
      defaultPrevented: !notPrevented,
      focusMode: document.querySelector('#app').classList.contains('focus-mode'),
    };
  });
  expect(f11Result).toEqual({ defaultPrevented: false, focusMode: false });

  const ctrlF11Result = await page.evaluate(() => {
    const event = new KeyboardEvent('keydown', { key: 'F11', ctrlKey: true, bubbles: true, cancelable: true });
    const notPrevented = window.dispatchEvent(event);
    return {
      defaultPrevented: !notPrevented,
      focusMode: document.querySelector('#app').classList.contains('focus-mode'),
    };
  });
  expect(ctrlF11Result).toEqual({ defaultPrevented: true, focusMode: true });
  await expect(page.getByRole('button', { name: 'Exit Focus Mode' })).toBeVisible();

  await page.getByRole('button', { name: 'Exit Focus Mode' }).click();
  await expect(page.locator('#app')).not.toHaveClass(/focus-mode/);
  await expect(page.locator('#focusModeExitButton')).toBeHidden();

  await page.locator('#focusModeButton').click();
  await expect(page.locator('#app')).toHaveClass(/focus-mode/);
  await expect(page.getByRole('button', { name: 'Exit Focus Mode' })).toBeVisible();

  await page.getByRole('button', { name: 'Exit Focus Mode' }).click();
  await expect(page.locator('#app')).not.toHaveClass(/focus-mode/);
  await expect(page.locator('#focusModeExitButton')).toBeHidden();

  await page.locator('#focusModeButton').click();
  await expect(page.locator('#app')).toHaveClass(/focus-mode/);
  await page.keyboard.press('Escape');
  await expect(page.locator('#app')).not.toHaveClass(/focus-mode/);
});

test('preview toolbar wraps without overlapping in a narrow preview pane', async ({ page }) => {
  await page.setViewportSize({ width: 1366, height: 768 });
  await gotoApp(page);
  await loadSample(page);

  await page.evaluate(() => {
    document.documentElement.style.setProperty('--editor-width', '720px');
  });
  await page.locator('#documentReviewToggleButton').click();
  await expect(page.locator('#documentReviewPanel')).toBeVisible();

  const titleHasOverflow = await page.locator('.preview-pane-title').evaluate((title) => title.scrollWidth > title.clientWidth + 2);
  expect(titleHasOverflow).toBe(false);

  const overlap = await page.locator('.preview-pane-title').evaluate((title) => {
    const controls = [...title.querySelectorAll('.document-controls > *, .zoom-controls > *')]
      .filter((element) => {
        const rect = element.getBoundingClientRect();
        const style = getComputedStyle(element);
        return style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.height > 0;
      })
      .map((element) => ({
        label: element.textContent.trim() || element.getAttribute('aria-label') || element.id,
        rect: element.getBoundingClientRect(),
      }));

    for (let index = 0; index < controls.length; index += 1) {
      for (let nextIndex = index + 1; nextIndex < controls.length; nextIndex += 1) {
        const a = controls[index].rect;
        const b = controls[nextIndex].rect;
        const intersects = a.left < b.right - 1
          && a.right > b.left + 1
          && a.top < b.bottom - 1
          && a.bottom > b.top + 1;
        if (intersects) return `${controls[index].label} overlaps ${controls[nextIndex].label}`;
      }
    }
    return '';
  });
  expect(overlap).toBe('');

  const reviewStartsAfterToolbar = await page.locator('.preview-pane').evaluate((pane) => {
    const title = pane.querySelector('.preview-pane-title').getBoundingClientRect();
    const review = pane.querySelector('#documentReviewPanel').getBoundingClientRect();
    return review.top >= title.bottom - 1;
  });
  expect(reviewStartsAfterToolbar).toBe(true);
});

test('Azure DevOps Mermaid blocks render in preview, autocomplete, and Docs Site export', async ({ page }) => {
  await openFixture(page, 'devops-mermaid.md');
  await expect(page.locator('.diagram-frame svg')).toHaveCount(1);
  await expect(page.locator('#preview')).toContainText('Azure DevOps Mermaid Fixture');

  const source = await page.locator('#editor').inputValue();
  const caret = source.indexOf('sequenceDiagram') + 'sequenceDiagram\n'.length;
  await setEditorValueAndSelection(page, source, caret, caret);
  await page.keyboard.press('Control+Space');
  await expect(page.locator('#mermaidAutocomplete')).toBeVisible();
  await page.keyboard.press('Escape');

  const docsPath = await clickDocsSiteExportDownload(page, {
    title: 'DevOps Mermaid Docs',
    description: 'Azure DevOps Mermaid export fixture.',
  });
  const entries = await readZipEntries(docsPath);
  const manifest = JSON.parse(getZipText(entries, 'site-manifest.json'));
  const searchIndex = JSON.parse(getZipText(entries, 'assets/search-index.json'));
  expect(manifest.diagramTotal).toBe(1);
  expect(manifest.diagramErrors).toBe(0);
  expect(searchIndex.pages.some((pageData) => pageData.html.includes('data-diagram-action="exportSvg"'))).toBe(true);
});

test('selection follow highlights editor selections in preview and respects the toggle', async ({ page }) => {
  await openFixture(page, 'document-ux.md');
  await expect(page.locator('#scrollSyncToggle')).toHaveAttribute('aria-label', 'Follow editor selection');

  await page.locator('#editor').evaluate((editor, target) => {
    const start = editor.value.indexOf(target);
    editor.focus();
    editor.setSelectionRange(start, start + target.length);
    editor.dispatchEvent(new Event('select', { bubbles: true }));
  }, 'navigation');
  await expect(page.locator('mark.selection-sync-hit')).toHaveText('navigation');

  await page.locator('#editor').evaluate((editor, target) => {
    const start = editor.value.indexOf(target);
    editor.focus();
    editor.setSelectionRange(start, start + target.length);
    editor.dispatchEvent(new Event('select', { bubbles: true }));
  }, 'documentUx');
  await expect(page.locator('mark.selection-sync-hit')).toHaveText('documentUx');

  const repeatedSource = [
    '# Repeated Selection Fixture',
    '',
    'Mermaid appears first in the introduction.',
    '',
    '| Feature | Shortcut or action |',
    '|---|---|',
    '| Mermaid target | Select the later occurrence |',
    '',
    'Mermaid appears again after the table.',
  ].join('\n');
  await setEditorValueAndSelection(page, repeatedSource, 0, 0);
  await renderPreviewWithShortcut(page);
  await page.locator('#editor').evaluate((editor, target) => {
    const start = editor.value.indexOf(target);
    editor.focus();
    editor.setSelectionRange(start, start + 'Mermaid'.length);
    editor.dispatchEvent(new Event('select', { bubbles: true }));
  }, 'Mermaid target');
  await expect(page.locator('table mark.selection-sync-hit')).toHaveText('Mermaid');

  const fenceSource = [
    '# Fence Selection Fixture',
    '',
    'Markdown is visible prose.',
    '',
    '```markdown',
    '# Fence body',
    '```',
  ].join('\n');
  await setEditorValueAndSelection(page, fenceSource, 0, 0);
  await renderPreviewWithShortcut(page);
  await page.locator('#editor').evaluate((editor, target) => {
    const start = editor.value.indexOf(target);
    editor.focus();
    editor.setSelectionRange(start, start + target.length);
    editor.dispatchEvent(new Event('select', { bubbles: true }));
  }, 'markdown');
  await page.waitForTimeout(180);
  await expect(page.locator('mark.selection-sync-hit')).toHaveCount(0);

  await page.locator('#scrollSyncToggle').uncheck();
  await expect(page.locator('mark.selection-sync-hit')).toHaveCount(0);

  await page.locator('#editor').evaluate((editor, target) => {
    const start = editor.value.indexOf(target);
    editor.focus();
    editor.setSelectionRange(start, start + target.length);
    editor.dispatchEvent(new Event('select', { bubbles: true }));
  }, 'review');
  await page.waitForTimeout(180);
  await expect(page.locator('mark.selection-sync-hit')).toHaveCount(0);
});


test('broken mermaid fixture shows error actions without export actions', async ({ page }) => {
  await gotoApp(page);
  const source = [
    '# Broken Mermaid Fixture',
    '',
    ...Array.from({ length: 120 }, (_, index) => `Filler line ${index + 1} with enough text to wrap inside the editor pane.`),
    '',
    '```mermaid',
    'flowchart TD',
    '  A -->',
    '```',
  ].join('\n');

  await setEditorValueAndSelection(page, source);
  await renderPreviewWithShortcut(page);

  await expect(page.locator('#status')).toHaveText(/diagram error/);
  await expect(page.locator('[data-diagram-action="copyError"]')).toHaveCount(1);
  await expect(page.locator('[data-diagram-action="jumpSource"]')).toHaveCount(1);
  await expect(page.locator('[data-diagram-action="exportSvg"]')).toHaveCount(0);
  await expect(page.locator('[data-diagram-action="exportPng"]')).toHaveCount(0);
  await expect(page.locator('[data-diagram-action="copyPng"]')).toHaveCount(0);

  await page.locator('#editor').evaluate((editor) => {
    editor.scrollTop = 0;
    editor.setSelectionRange(0, 0);
  });
  await page.locator('[data-diagram-action="jumpSource"]').click();
  const jumpState = await page.locator('#editor').evaluate((editor, expectedSource) => ({
    selectedText: editor.value.slice(editor.selectionStart, editor.selectionEnd),
    scrollTop: editor.scrollTop,
  }), 'flowchart TD\n  A -->');
  expect(jumpState.selectedText).toBe('flowchart TD\n  A -->');
  expect(jumpState.scrollTop).toBeGreaterThan(0);
});

test('preview outline supports H1-H4 and tracks the active section', async ({ page }) => {
  await openFixture(page, 'document-ux.md');

  await page.locator('#outlineToggleButton').click();
  await expect(page.locator('#outlinePanel')).toBeVisible();
  await expect(page.locator('#outlineLinks [data-outline-level="1"]')).toHaveText('Document UX Fixture');
  await expect(page.locator('#outlineLinks [data-outline-level="2"]')).toHaveText('Product Area');
  await expect(page.locator('#outlineLinks [data-outline-level="3"]')).toHaveText('Review Flow');
  await expect(page.locator('#outlineLinks [data-outline-level="4"]')).toHaveText('Deep Detail');

  await page.locator('#outlineLinks [data-outline-target="deep-detail"]').evaluate((link) => link.click());
  await expect(page.locator('#outlineLinks [data-outline-target="deep-detail"]')).toHaveClass(/active/);
});

test('preview search highlights document text and ignores preview controls', async ({ page }) => {
  await openFixture(page, 'document-ux.md');

  await page.locator('#previewFindToggleButton').click();
  await page.locator('#previewFindInput').fill('review');
  await expect(page.locator('mark.preview-search-hit')).not.toHaveCount(0);
  await expect(page.locator('#previewFindCount')).toHaveText(/1\/\d+/);

  await page.locator('#previewFindNextButton').click();
  await expect(page.locator('#previewFindCount')).toHaveText(/2\/\d+/);

  await page.locator('#outlineToggleButton').click();
  await expect(page.locator('#outlinePanel')).toBeVisible();
  const previewToolOverlap = await page.locator('.preview-pane').evaluate((pane) => {
    const findPanel = pane.querySelector('#previewFindPanel');
    const outlinePanel = pane.querySelector('#outlinePanel');
    const findRect = findPanel.getBoundingClientRect();
    const outlineRect = outlinePanel.getBoundingClientRect();
    if (findRect.bottom > outlineRect.top + 1) {
      return 'find panel overlaps outline panel';
    }

    const controls = [...findPanel.children]
      .filter((element) => {
        const rect = element.getBoundingClientRect();
        const style = getComputedStyle(element);
        return style.display !== 'none' && rect.width > 0 && rect.height > 0;
      })
      .map((element) => ({ label: element.textContent.trim() || element.id, rect: element.getBoundingClientRect() }));
    for (let index = 0; index < controls.length; index += 1) {
      for (let nextIndex = index + 1; nextIndex < controls.length; nextIndex += 1) {
        const a = controls[index].rect;
        const b = controls[nextIndex].rect;
        const overlaps = a.left < b.right - 1
          && a.right > b.left + 1
          && a.top < b.bottom - 1
          && a.bottom > b.top + 1;
        if (overlaps) return `${controls[index].label} overlaps ${controls[nextIndex].label}`;
      }
    }
    return '';
  });
  expect(previewToolOverlap).toBe('');

  await openFixture(page, 'mixed.md');
  await page.locator('#previewFindToggleButton').click();
  await page.locator('#previewFindInput').fill('SVG');
  await expect(page.locator('mark.preview-search-hit')).toHaveCount(0);
  await expect(page.locator('#previewFindCount')).toHaveText('0/0');

  await page.locator('#previewFindClearButton').click();
  await expect(page.locator('#previewFindPanel')).toBeHidden();
});

test('document review shows metrics and non-blocking review notes', async ({ page }) => {
  await openFixture(page, 'document-ux.md');
  await page.locator('#documentReviewToggleButton').click();

  await expect(page.locator('#documentReviewPanel')).toBeVisible();
  await expect(page.locator('#documentReviewSummary')).toContainText(/words/);
  await expect(page.locator('#documentReviewMetrics')).toContainText('Headings: 4');
  await expect(page.locator('#documentReviewMetrics')).toContainText('Links: 1');
  await expect(page.locator('#documentReviewMetrics')).toContainText('Code: 1');
  await expect(page.locator('#documentReviewAlerts')).toContainText('external link');

  await openFixture(page, 'no-h1.md');
  await expect(page.locator('#documentReviewAlerts')).toContainText('Missing H1 title');
  await expect(page.locator('#documentReviewAlerts')).toContainText('Heading level jumps');

  await openFixture(page, 'broken-mermaid.md');
  await expect(page.locator('#documentReviewAlerts')).toContainText('Mermaid diagram error');
});

test('HTML export is standalone and keeps interactive preview actions', async ({ page }) => {
  await openFixture(page, 'mixed.md');
  await page.locator('#previewFindToggleButton').click();
  await page.locator('#previewFindInput').fill('Export');
  await expect(page.locator('mark.preview-search-hit')).not.toHaveCount(0);
  await page.locator('#documentReviewToggleButton').click();
  await page.locator('#editor').evaluate((editor) => {
    const target = 'Export Fixture';
    const start = editor.value.indexOf(target);
    editor.focus();
    editor.setSelectionRange(start, start + target.length);
    editor.dispatchEvent(new Event('select', { bubbles: true }));
  });
  await expect(page.locator('mark.selection-sync-hit')).not.toHaveCount(0);

  const filePath = await clickExportDownload(page, 'Export HTML');
  const html = await readFile(filePath, 'utf8');

  expect(html).toContain('<title>mixed</title>');
  expect(html).toContain('Content-Security-Policy');
  expect(html).toContain("script-src 'nonce-");
  expect(html).toContain('Export Fixture');
  expect(html).toContain('data-code-action="copy"');
  expect(html).toContain('data-table-action="copy"');
  expect(html).toContain('data-table-action="downloadCsv"');
  expect(html).toContain('data-diagram-action="copySource"');
  expect(html).toContain('data-diagram-action="exportSvg"');
  expect(html).toContain('data-diagram-action="copyPng"');
  expect(html).toContain('function fallbackCopy');
  expect(html).toContain('function downloadText');
  const scriptNonces = [...html.matchAll(/<script nonce="([^"]+)">/g)].map((match) => match[1]);
  expect(scriptNonces).toHaveLength(3);
  expect(new Set(scriptNonces).size).toBe(1);
  expect(html).toContain('.hljs-keyword');
  expect(html).not.toContain('preview-search-hit');
  expect(html).not.toContain('selection-sync-hit');
  expect(html).not.toContain('Find in preview');
  expect(html).not.toContain('Document review');
  await expect(page.locator('#status')).toHaveText(/HTML exported/);
});

test('Word export is a valid native DOCX package without interactive UI text', async ({ page }) => {
  await openFixture(page, 'mixed.md');

  const filePath = await clickExportDownload(page, 'Export Word');
  const entries = await readZipEntries(filePath);
  const contentTypes = getZipText(entries, '[Content_Types].xml');
  const documentXml = getZipText(entries, 'word/document.xml');
  const relsXml = getZipText(entries, 'word/_rels/document.xml.rels');

  expect(contentTypes).toContain('wordprocessingml.document.main+xml');
  expect(documentXml).toContain('Export Fixture');
  expect(documentXml).toContain('release ready');
  expect(documentXml).toContain('Preview');
  expect(documentXml).toContain('Verified');
  expect(documentXml).toContain('<w:drawing>');
  expect(relsXml).toContain('relationships/image');
  expect([...entries.keys()].some((name) => /^word\/media\/diagram-1\.(png|svg)$/.test(name))).toBe(true);
  expect(documentXml).not.toContain('Copy');
  expect(documentXml).not.toContain('SVG');
  expect(documentXml).not.toContain('PNG');
  expect(documentXml).not.toContain('preview-search-hit');
  expect(documentXml).not.toContain('selection-sync-hit');
  expect(documentXml).not.toContain('Document review');
  await expect(page.locator('#exportTrust')).toHaveText(/Word ready: 1\/1 diagram rendered/);
});

test('dropped image assets render and travel through HTML, Word, and Docs Site exports', async ({ page }) => {
  await openFixture(page, 'plain.md');
  await page.locator('#editor').focus();
  await page.locator('#editor').evaluate((editor) => {
    editor.setSelectionRange(editor.value.length, editor.value.length);
  });

  await dropTinyPngOnEditor(page);
  await expect(page.locator('#editor')).toHaveValue(/!\[tiny image\]\(assets\/images\/tiny-image\.png\)/);
  await expect(page.locator('#preview img[data-managed-asset-path="assets/images/tiny-image.png"]')).toHaveAttribute('src', /^blob:/);

  const htmlPath = await clickExportDownload(page, 'Export HTML');
  const html = await readFile(htmlPath, 'utf8');
  expect(html).toContain('data:image/png;base64');
  expect(html).toContain('data-managed-asset-path="assets/images/tiny-image.png"');

  const wordPath = await clickExportDownload(page, 'Export Word');
  const wordEntries = await readZipEntries(wordPath);
  const documentXml = getZipText(wordEntries, 'word/document.xml');
  const relsXml = getZipText(wordEntries, 'word/_rels/document.xml.rels');
  expect(documentXml).toContain('<w:drawing>');
  expect(relsXml).toContain('relationships/image');
  expect([...wordEntries.keys()].some((name) => /^word\/media\/asset-\d+-tiny-image\.png$/.test(name))).toBe(true);

  const docsPath = await clickDocsSiteExportDownload(page, {
    title: 'Image Asset Docs',
    description: 'Image asset export fixture.',
  });
  const docsEntries = await readZipEntries(docsPath);
  const searchIndex = JSON.parse(getZipText(docsEntries, 'assets/search-index.json'));
  expect(docsEntries.has('assets/images/tiny-image.png')).toBe(true);
  expect(searchIndex.pages.some((pageData) => pageData.html.includes('assets/images/tiny-image.png'))).toBe(true);
});

test('asset manager previews and renames managed image references', async ({ page }) => {
  await openFixture(page, 'plain.md');
  await page.locator('#editor').focus();
  await page.locator('#editor').evaluate((editor) => {
    editor.setSelectionRange(editor.value.length, editor.value.length);
  });
  await dropTinyPngOnEditor(page);
  await expect(page.locator('#editor')).toHaveValue(/assets\/images\/tiny-image\.png/);

  await page.locator('summary').filter({ hasText: /^View$/ }).click();
  await page.getByRole('button', { name: 'Manage assets' }).click();
  await expect(page.getByRole('heading', { name: 'Managed assets' })).toBeVisible();
  await expect(page.locator('.asset-library-item img')).toBeVisible();
  await expect(page.locator('[data-asset-action="remove"]')).toBeDisabled();

  await page.locator('.asset-library-main input').fill('assets/images/renamed-image.png');
  await page.getByRole('button', { name: 'Rename' }).click();
  await expect(page.locator('#status')).toHaveText(/Renamed asset/);
  await expect(page.locator('#editor')).toHaveValue(/assets\/images\/renamed-image\.png/);
  await expect(page.locator('#preview img[data-managed-asset-path="assets/images/renamed-image.png"]')).toHaveAttribute('src', /^blob:/);
});

test('clipboard image paste creates the same managed assets as drag and drop', async ({ page }) => {
  await openFixture(page, 'plain.md');
  await page.locator('#editor').focus();
  await page.locator('#editor').evaluate((editor) => {
    editor.setSelectionRange(editor.value.length, editor.value.length);
  });

  await pasteTinyPngIntoEditor(page);
  await expect(page.locator('#editor')).toHaveValue(/!\[clipboard image\]\(assets\/images\/clipboard-image\.png\)/);
  await expect(page.locator('#preview img[data-managed-asset-path="assets/images/clipboard-image.png"]')).toHaveAttribute('src', /^blob:/);

  const bundlePath = await clickExportDownload(page, 'Export Markdown Bundle');
  const entries = await readZipEntries(bundlePath);
  expect(entries.has('assets/images/clipboard-image.png')).toBe(true);
});

test('SVG image assets are blocked on drag and drop', async ({ page }) => {
  await openFixture(page, 'plain.md');
  await page.locator('#editor').focus();
  await dropTinySvgOnEditor(page);

  await expect(page.locator('#status')).toHaveText(/SVG images are not imported for security/);
  await expect(page.locator('#editor')).not.toHaveValue(/blocked-image\.svg|assets\/images/);
  await expect(page.locator('#preview img[data-managed-asset-path]')).toHaveCount(0);
});

test('PDF export prepares clean print HTML with rendered content and embedded assets', async ({ page }) => {
  await page.addInitScript(() => {
    window.__MD_MMD_CAPTURE_PRINT_HTML__ = (html) => {
      window.__MD_MMD_LAST_PRINT_HTML__ = html;
    };
  });
  await openFixture(page, 'mixed.md');
  await page.locator('#editor').focus();
  await page.locator('#editor').evaluate((editor) => {
    editor.setSelectionRange(editor.value.length, editor.value.length);
  });
  await dropTinyPngOnEditor(page);

  await clickExportAction(page, 'Export PDF');
  await expect.poll(() => page.evaluate(() => window.__MD_MMD_LAST_PRINT_HTML__ || '')).toContain('Export Fixture');
  const html = await page.evaluate(() => window.__MD_MMD_LAST_PRINT_HTML__);

  expect(html).toContain('@page');
  expect(html).toContain('Content-Security-Policy');
  expect(html).toContain('data:image/png;base64');
  expect(html).toContain('<svg');
  expect(html).toContain('release ready');
  expect(html).not.toContain('data-code-action="copy"');
  expect(html).not.toContain('data-diagram-action="exportSvg"');
  expect(html).not.toContain('preview-search-hit');
  expect(html).not.toContain('selection-sync-hit');
  expect(html).not.toContain('Document review');
  await expect(page.locator('#status')).toHaveText(/PDF print view opened/);
});

test('Markdown Bundle export round-trips edited docs and image assets', async ({ page }) => {
  await openFixture(page, 'plain.md');
  await page.locator('#editor').fill('# Bundle Edited\n\nBundle body');
  await dropTinyPngOnEditor(page);

  const bundlePath = await clickExportDownload(page, 'Export Markdown Bundle');
  const entries = await readZipEntries(bundlePath);
  const manifest = JSON.parse(getZipText(entries, 'lens-docs-studio-bundle.json'));

  expect(manifest.formatVersion).toBe('markdown-bundle-1.0');
  expect(manifest.generator).toBe('Lens Docs Studio');
  expect(manifest.documentCount).toBe(1);
  expect(manifest.assetCount).toBe(1);
  expect(manifest.documents[0].dirty).toBe(true);
  expect(getZipText(entries, 'plain.md')).toContain('Bundle Edited');
  expect(entries.has('assets/images/tiny-image.png')).toBe(true);

  await page.locator('#zipInput').setInputFiles(bundlePath);
  await submitAppDialogIfVisible(page, { button: 'Discard changes' });
  await expect(page.locator('#status')).toHaveText(/Imported 1 document and 1 image asset from Markdown bundle/);
  await expect(page.locator('#editor')).toHaveValue(/Bundle Edited/);
  await expect(page.locator('#preview img[data-managed-asset-path="assets/images/tiny-image.png"]')).toHaveAttribute('src', /^blob:/);
});

test('Markdown Bundle import accepts current and legacy manifest names', async ({ page }, testInfo) => {
  const manifestNames = [
    'lens-docs-studio-bundle.json',
    'local-docs-studio-bundle.json',
    'md-mmd-renderer-bundle.json',
  ];

  await gotoApp(page);

  for (const manifestName of manifestNames) {
    const zipPath = testInfo.outputPath(`${manifestName}.zip`);
    await writeFile(zipPath, createZipBuffer([
      {
        name: 'README.md',
        data: `# Bundle ${manifestName}\n\nImported through ${manifestName}.`,
      },
      {
        name: manifestName,
        data: JSON.stringify({
          formatVersion: 'markdown-bundle-1.0',
          generator: manifestName.startsWith('lens') ? 'Lens Docs Studio' : 'Local Docs Studio',
          title: `Bundle ${manifestName}`,
          exportedAt: '2026-05-26T00:00:00.000Z',
          documentCount: 1,
          assetCount: 0,
          documents: [{ path: 'README.md', name: 'README.md', dirty: false, bytes: 0 }],
          assets: [],
        }),
      },
    ], { compress: true }));

    await page.locator('#zipInput').setInputFiles(zipPath);
    await expect(page.locator('#status')).toHaveText(/Imported 1 document from Markdown bundle/, { timeout: 20_000 });
    await expect(page.locator('#folderBadge')).toHaveText(`Bundle ${manifestName}`);
    await expect(page.locator('#editor')).toHaveValue(new RegExp(`Imported through ${manifestName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`));
  }
});

test('Markdown Bundle DevOps option converts Mermaid fences only when enabled', async ({ page }) => {
  await openFixture(page, 'mixed.md');

  const defaultBundlePath = await clickExportDownload(page, 'Export Markdown Bundle');
  const defaultEntries = await readZipEntries(defaultBundlePath);
  const defaultMarkdown = normaliseLineEndings(getZipText(defaultEntries, 'mixed.md'));
  expect(defaultMarkdown).toContain('```mermaid\nflowchart TD');
  expect(defaultMarkdown).not.toContain('::: mermaid');

  await page.locator('summary').filter({ hasText: /^Export$/ }).click();
  await page.locator('#devopsMarkdownExportToggle').check();
  const devopsBundlePath = await clickExportDownload(page, 'Export Markdown Bundle');
  const devopsEntries = await readZipEntries(devopsBundlePath);
  const devopsMarkdown = normaliseLineEndings(getZipText(devopsEntries, 'mixed.md'));
  expect(devopsMarkdown).toContain('::: mermaid\ngraph TD');
  expect(devopsMarkdown).toContain('\n:::');
  expect(devopsMarkdown).not.toContain('```mermaid');
  await expect(page.locator('#exportTrust')).toHaveText(/Azure DevOps Mermaid syntax applied/);
});

test('fixture-based valid basic artefact bundle import opens editable records', async ({ page }, testInfo) => {
  const zipPath = await writeArtifactBundleFixtureZip(testInfo, 'valid-basic');

  await gotoApp(page);
  await page.locator('#zipInput').setInputFiles(zipPath);
  await expect(page.locator('#status')).toHaveText(/Imported 3 documents from artefact bundle\. Opened README\.md\./, { timeout: 20_000 });
  await expect(page.locator('#editor')).toHaveValue(/declared entry document/);
  await expect(page.locator('#preview h1')).toHaveText('Basic Artefact Bundle');
  expect(await page.locator('#editor').evaluate((editor) => editor.readOnly)).toBe(false);

  const summary = page.locator('#artifactBundleSummary');
  await expect(summary).toBeVisible();
  await summary.getByRole('button', { name: 'Expand artefact bundle reader' }).click();
  await expect(summary).toContainText('Basic Artefact Bundle');
  await expect(summary).toContainText('Basic Finding');
  await expect(summary).toContainText('Basic Flow');
  await expect(summary).toContainText('candidate finding');

  await summary.getByRole('button', { name: /Basic Flow/ }).click();
  await expect(page.locator('#activeFileLabel')).toContainText('diagrams/basic-flow.mmd');
  await expect(page.locator('#editor')).toHaveValue(/flowchart LR/);
  await expect(page.locator('.diagram-frame svg')).toHaveCount(1);
});

test('fixture-based valid rich artefact bundle import shows safe reader metadata', async ({ page }, testInfo) => {
  const zipPath = await writeArtifactBundleFixtureZip(testInfo, 'valid-rich');

  await gotoApp(page);
  await page.locator('#zipInput').setInputFiles(zipPath);
  await expect(page.locator('#status')).toHaveText(/Imported 8 documents and 1 image asset from artefact bundle\. Opened README\.md\./, { timeout: 20_000 });
  await expect(page.locator('#editor')).toHaveValue(/rich bundle exercises/);
  await expect(page.locator('#preview h1')).toHaveText('Rich Artefact Bundle');

  const summary = page.locator('#artifactBundleSummary');
  await expect(summary).toBeVisible();
  await expect(summary.getByRole('button', { name: 'Expand artefact bundle reader' })).toHaveAttribute('aria-expanded', 'false');
  await summary.getByRole('button', { name: 'Expand artefact bundle reader' }).click();
  await expect(summary.getByRole('button', { name: 'Collapse artefact bundle reader' })).toHaveAttribute('aria-expanded', 'true');
  await expect(summary).toContainText('Rich Artefact Bundle');
  await expect(summary).toContainText('Security Lens 4.2.0');
  await expect(summary).toContainText('2026-05-26T12:34:56.000Z');
  await expect(summary).toContainText('README.md');
  await expect(summary).toContainText('7');
  await expect(summary).toContainText('1');
  await expect(summary).toContainText('static evidence');
  await expect(summary).toContainText('connected metadata evidence');
  await expect(summary).toContainText('runtime evidence');
  await expect(summary).toContainText('manual evidence');
  await expect(summary).toContainText('candidate finding');
  await expect(summary).toContainText('confirmed finding');
  await expect(summary).toContainText('blocked/unavailable evidence');
  await expect(summary).toContainText('Evidence labels are displayed as supplied by the bundle.');
  await expect(summary).toContainText('Candidate findings remain candidate findings.');
  await expect(summary).toContainText('Bundle warning: Connected metadata was unavailable during export.');
  await expect(summary).toContainText('Manual evidence requires reviewer confirmation outside Lens Docs Studio.');
  await expect(summary).not.toContainText('https://example.com/unsafe.md');
  const storageKeysBeforeFilters = await page.evaluate(() => Object.keys(localStorage).sort());

  await expect(page.locator('#fileList .file-item').nth(0)).toHaveAttribute('data-path', 'README.md');
  await expect(page.locator('#fileList .file-item').nth(1)).toHaveAttribute('data-path', 'summary/executive-summary.md');
  await expect(page.locator('#fileList .file-item').nth(2)).toHaveAttribute('data-path', 'findings/candidate-risk.md');
  await expect(page.locator('#fileList .file-item').nth(3)).toHaveAttribute('data-path', 'findings/confirmed-control.md');
  expect(await page.locator('#editor').evaluate((editor) => editor.readOnly)).toBe(false);

  await summary.getByRole('button', { name: /Candidate Risk/ }).click();
  await expect(page.locator('#activeFileLabel')).toContainText('findings/candidate-risk.md');
  await expect(page.locator('#editor')).toHaveValue(/candidate finding and must remain candidate/);

  await summary.locator('[data-artifact-evidence-filter="candidate finding"]').click();
  await expect(summary.getByRole('button', { name: /Candidate Risk/ })).toBeVisible();
  await expect(summary.getByRole('button', { name: /Confirmed Control/ })).toHaveCount(0);
  await summary.locator('[data-artifact-evidence-filter=""]').click();

  await summary.locator('[data-artifact-kind-filter="ADRs"]').click();
  await expect(summary.getByRole('button', { name: /ADR 001/ })).toBeVisible();
  await expect(summary.getByRole('button', { name: /Candidate Risk/ })).toHaveCount(0);
  await summary.locator('[data-artifact-kind-filter=""]').click();

  await summary.getByLabel('Search artefact metadata').fill('adr-001');
  await expect(summary.getByRole('button', { name: /ADR 001/ })).toBeVisible();
  await expect(summary.getByRole('button', { name: /Trigger Timeline/ })).toHaveCount(0);
  await summary.getByRole('button', { name: 'Clear filters' }).click();
  await expect(summary.getByRole('button', { name: /Trigger Timeline/ })).toBeVisible();
  await expect(page.locator('#fileList .file-item')).toHaveCount(8);
  await expect.poll(() => page.evaluate(() => Object.keys(localStorage).sort())).toEqual(storageKeysBeforeFilters);

  await summary.getByRole('button', { name: /Trigger Timeline/ }).click();
  await expect(page.locator('#activeFileLabel')).toContainText('diagrams/trigger-timeline.mmd');
  await expect(page.locator('#editor')).toHaveValue(/flowchart TD/);
  expect(await page.locator('#editor').evaluate((editor) => editor.readOnly)).toBe(false);
  await expect(page.locator('.diagram-frame svg')).toHaveCount(1);

  await summary.getByRole('button', { name: 'Collapse artefact bundle reader' }).click();
  await expect(summary.getByRole('button', { name: 'Expand artefact bundle reader' })).toHaveAttribute('aria-expanded', 'false');
});

test('artefact reader keyboard controls are accessible and filters stay local', async ({ page }, testInfo) => {
  const zipPath = await writeArtifactBundleFixtureZip(testInfo, 'valid-rich');
  const basicZipPath = await writeArtifactBundleFixtureZip(testInfo, 'valid-basic', 'valid-basic-reset.zip');

  await gotoApp(page);
  await page.locator('#zipInput').setInputFiles(zipPath);
  await expect(page.locator('#status')).toHaveText(/Imported 8 documents and 1 image asset from artefact bundle/, { timeout: 20_000 });
  const initialStorageKeys = await page.evaluate(() => Object.keys(localStorage).sort());

  const summary = page.locator('#artifactBundleSummary');
  await tabUntilFocused(page, '#artifactBundleSummary .artifact-bundle-toggle');
  await page.keyboard.press('Enter');
  await expect(summary.getByRole('button', { name: 'Collapse artefact bundle reader' })).toHaveAttribute('aria-expanded', 'true');

  await summary.locator('.artifact-bundle-toggle').focus();
  await page.keyboard.press('Space');
  await expect(summary.getByRole('button', { name: 'Expand artefact bundle reader' })).toHaveAttribute('aria-expanded', 'false');

  await summary.locator('.artifact-bundle-toggle').focus();
  await page.keyboard.press('Enter');
  await expect(summary.getByRole('button', { name: 'Collapse artefact bundle reader' })).toHaveAttribute('aria-expanded', 'true');
  await expect(summary.getByLabel('Search artefact metadata')).toBeVisible();

  await tabUntilFocused(page, '#artifactBundleSummary [data-artifact-evidence-filter="candidate finding"]');
  await expect(summary.locator('[data-artifact-evidence-filter="candidate finding"]')).toHaveAttribute('aria-pressed', 'false');
  await page.keyboard.press('Enter');
  await expect(summary.locator('[data-artifact-evidence-filter="candidate finding"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(summary.getByRole('button', { name: /Candidate Risk/ })).toBeVisible();
  await expect(summary.getByRole('button', { name: /Confirmed Control/ })).toHaveCount(0);
  await expect(page.locator('#fileList .file-item')).toHaveCount(8);

  await tabUntilFocused(page, '#artifactBundleSummary [data-artifact-open-path="findings/candidate-risk.md"]');
  await page.keyboard.press('Enter');
  await expect(page.locator('#activeFileLabel')).toContainText('findings/candidate-risk.md');
  await expect(page.locator('#editor')).toHaveValue(/candidate finding and must remain candidate/);

  await page.locator('#zipInput').setInputFiles(basicZipPath);
  await expect(page.locator('#status')).toHaveText(/Imported 3 documents from artefact bundle\. Opened README\.md\./, { timeout: 20_000 });
  await summary.getByRole('button', { name: 'Expand artefact bundle reader' }).click();
  await expect(summary.getByLabel('Search artefact metadata')).toHaveValue('');
  await expect(summary.locator('[data-artifact-evidence-filter=""]')).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('#fileList .file-item')).toHaveCount(3);
  await expect.poll(() => page.evaluate(() => Object.keys(localStorage).sort())).toEqual(initialStorageKeys);

  await page.reload();
  await expect(page.locator('#artifactBundleSummary')).toBeHidden();
  await expect.poll(() => page.evaluate(() => Object.keys(localStorage).sort())).toEqual(initialStorageKeys);
});

test('invalid Lens artefact bundle manifest falls back to safe ZIP import', async ({ page }, testInfo) => {
  const pageErrors = [];
  page.on('pageerror', (error) => pageErrors.push(error.message));
  const zipPath = await writeArtifactBundleFixtureZip(testInfo, 'invalid-manifest');

  await gotoApp(page);
  await page.locator('#zipInput').setInputFiles(zipPath);
  await expect(page.locator('#status')).toHaveText(/Imported 2 documents from ZIP\. Artefact bundle manifest could not be read\./, { timeout: 20_000 });
  await expect(page.locator('#artifactBundleSummary')).toBeHidden();
  await expect(page.locator('#editor')).toHaveValue(/safe source import continues/);
  await expect(page.locator('#fileList [data-path="README.md"]')).toBeVisible();
  expect(pageErrors).toEqual([]);
});

test('Lens artefact bundle missing entry document keeps fallback selection and warns', async ({ page }, testInfo) => {
  const zipPath = await writeZipFixture(testInfo, 'missing-entry-lens-artefact-bundle.zip', [
    { name: 'README.md', data: '# Fallback Entry\n\nExisting first document opened.' },
    {
      name: 'lens-artifact-bundle.json',
      data: JSON.stringify({
        formatVersion: 'lens-artifact-bundle-1.0',
        title: 'Missing Entry Pack',
        entryDocument: 'missing.md',
        documents: [{ path: 'README.md', title: 'Fallback', kind: 'summary', order: 10 }],
      }),
    },
  ]);

  await gotoApp(page);
  await page.locator('#zipInput').setInputFiles(zipPath);
  await expect(page.locator('#status')).toHaveText(/entry document was not found/, { timeout: 20_000 });
  await expect(page.locator('#editor')).toHaveValue(/Existing first document opened/);
  const summary = page.locator('#artifactBundleSummary');
  await summary.getByRole('button', { name: 'Expand artefact bundle reader' }).click();
  await expect(summary).toContainText('Manifest warning: entry document was not found.');
});

test('Lens artefact bundle ignores unsafe metadata paths without exposing them', async ({ page }, testInfo) => {
  const zipPath = await writeArtifactBundleFixtureZip(testInfo, 'unsafe-paths');

  await gotoApp(page);
  await page.locator('#zipInput').setInputFiles(zipPath);
  await expect(page.locator('#status')).toHaveText(/entry document path was ignored/, { timeout: 20_000 });
  await expect(page.locator('#fileList .file-item')).toHaveCount(2);
  await expect(page.locator('#editor')).toHaveValue(/Only safe imported documents/);
  const summary = page.locator('#artifactBundleSummary');
  await summary.getByRole('button', { name: 'Expand artefact bundle reader' }).click();
  await expect(summary).toContainText('Safe Home');
  await expect(summary).toContainText('Safe Visible');
  await expect(page.locator('body')).not.toContainText('../secrets.md');
  await expect(page.locator('body')).not.toContainText('/absolute.md');
  await expect(page.locator('body')).not.toContainText('C:\\secrets.md');
  await expect(page.locator('body')).not.toContainText('https://example.com/file.md');
  await expect(page.locator('body')).not.toContainText('bad');

  const reviewPath = await clickExportDownload(page, 'Export artefact review pack');
  const reviewEntries = await readZipEntries(reviewPath);
  const exportedManifestText = getZipText(reviewEntries, 'lens-artifact-bundle.json');
  expect(exportedManifestText).toContain('Safe Home');
  expect(exportedManifestText).toContain('Safe Visible');
  expect(exportedManifestText).not.toContain('../secrets.md');
  expect(exportedManifestText).not.toContain('/absolute.md');
  expect(exportedManifestText).not.toContain('C:\\\\secrets.md');
  expect(exportedManifestText).not.toContain('https://example.com/file.md');
  expect(exportedManifestText).not.toContain('bad\\u0001path.md');
});

test('Lens artefact bundle preserves candidate evidence wording', async ({ page }, testInfo) => {
  const zipPath = await writeZipFixture(testInfo, 'candidate-lens-artefact-bundle.zip', [
    { name: 'README.md', data: '# Candidate Evidence\n\nCandidate wording remains unchanged.' },
    {
      name: 'lens-artifact-bundle.json',
      data: JSON.stringify({
        formatVersion: 'lens-artifact-bundle-1.0',
        title: 'Candidate Evidence Pack',
        entryDocument: 'README.md',
        evidenceLevels: ['candidate finding'],
        documents: [{ path: 'README.md', title: 'Candidate', evidenceLevel: 'candidate finding', order: 10 }],
      }),
    },
  ]);

  await gotoApp(page);
  await page.locator('#zipInput').setInputFiles(zipPath);
  const summary = page.locator('#artifactBundleSummary');
  await summary.getByRole('button', { name: 'Expand artefact bundle reader' }).click();
  await expect(summary).toContainText('candidate finding', { timeout: 20_000 });
  await expect(summary).not.toContainText('confirmed finding');
});

test('Lens artefact bundle metadata does not trigger external fetches', async ({ page }, testInfo) => {
  const zipPath = await writeZipFixture(testInfo, 'no-fetch-lens-artefact-bundle.zip', [
    { name: 'README.md', data: '# No Fetch\n\nManifest URLs are inert metadata.' },
    {
      name: 'lens-artifact-bundle.json',
      data: JSON.stringify({
        formatVersion: 'lens-artifact-bundle-1.0',
        title: 'No Fetch Pack',
        sourceTool: 'https://example.com/tool',
        entryDocument: 'README.md',
        documents: [
          { path: 'README.md', title: 'Safe', order: 10 },
          { path: 'https://example.com/file.md', title: 'Remote', order: 20 },
        ],
      }),
    },
  ]);

  await gotoApp(page);
  await page.evaluate(() => {
    const calls = [];
    const originalFetch = window.fetch.bind(window);
    window.__lensArtifactFetchCalls = calls;
    window.fetch = (...args) => {
      calls.push(String(args[0]?.url || args[0]));
      return originalFetch(...args);
    };
  });

  await page.locator('#zipInput').setInputFiles(zipPath);
  await expect(page.locator('#status')).toHaveText(/Imported 1 document from artefact bundle\. Opened README\.md\./, { timeout: 20_000 });
  await expect.poll(() => page.evaluate(() => window.__lensArtifactFetchCalls)).toEqual([]);
});

test('artefact review pack export is explicit and round-trips safe rich metadata', async ({ page }, testInfo) => {
  const zipPath = await writeArtifactBundleFixtureZip(testInfo, 'valid-rich');

  await gotoApp(page);
  await page.locator('#zipInput').setInputFiles(zipPath);
  await expect(page.locator('#status')).toHaveText(/Imported 8 documents and 1 image asset from artefact bundle/, { timeout: 20_000 });

  const summary = page.locator('#artifactBundleSummary');
  await summary.getByRole('button', { name: 'Expand artefact bundle reader' }).click();
  await summary.getByLabel('Search artefact metadata').fill('candidate');
  await expect(summary.getByRole('button', { name: /Candidate Risk/ })).toBeVisible();
  await summary.getByRole('button', { name: /Candidate Risk/ }).click();
  await expect(page.locator('#activeFileLabel')).toContainText('findings/candidate-risk.md');

  await page.locator('summary').filter({ hasText: /^Export$/ }).click();
  const storageKeysBeforeProfile = await page.evaluate(() => Object.keys(localStorage).sort());
  await expect(page.getByRole('button', { name: 'Export artefact review pack' })).toBeEnabled();
  await page.locator('#builtInExportProfiles').getByRole('button', { name: /Artefact review pack/ }).click();
  await expect(page.locator('#activeExportProfileLabel')).toContainText('Artefact review pack');
  await expect.poll(() => page.evaluate(() => Object.keys(localStorage).sort())).toEqual(storageKeysBeforeProfile);

  const genericPath = await clickExportDownload(page, 'Export Markdown Bundle');
  const genericEntries = await readZipEntries(genericPath);
  expect(genericEntries.has('lens-docs-studio-bundle.json')).toBe(true);
  expect(genericEntries.has('lens-artifact-bundle.json')).toBe(false);

  const reviewPath = await clickExportDownload(page, 'Export artefact review pack');
  const reviewEntries = await readZipEntries(reviewPath);
  const reviewManifest = JSON.parse(getZipText(reviewEntries, 'lens-artifact-bundle.json'));
  expect(reviewManifest.reviewedWith).toBe('Lens Docs Studio');
  expect(reviewManifest.sourceTool).toBe('Security Lens');
  expect(reviewManifest.sourceToolVersion).toBe('4.2.0');
  expect(reviewManifest.generatedAtUtc).toBe('2026-05-26T12:34:56.000Z');
  expect(reviewManifest.entryDocument).toBe('README.md');
  expect(reviewManifest.evidenceLevels).toEqual([
    'static evidence',
    'connected metadata evidence',
    'runtime evidence',
    'manual evidence',
    'candidate finding',
    'confirmed finding',
    'blocked/unavailable evidence',
  ]);
  expect(reviewManifest.documents.map((item) => item.path).sort()).toEqual([
    'README.md',
    'adr/adr-001.md',
    'docs/runbook.md',
    'findings/candidate-risk.md',
    'findings/confirmed-control.md',
    'release-notes/v1.md',
    'summary/executive-summary.md',
  ]);
  expect(reviewManifest.diagrams.map((item) => item.path)).toEqual(['diagrams/trigger-timeline.mmd']);
  expect(JSON.stringify(reviewManifest)).toContain('candidate finding');
  expect(JSON.stringify(reviewManifest)).toContain('confirmed finding');
  expect(JSON.stringify(reviewManifest)).not.toContain('https://example.com/unsafe.md');
  expect(JSON.stringify(reviewManifest)).not.toContain('Unsafe Remote');
  expect(reviewEntries.has('lens-docs-studio-bundle.json')).toBe(true);
  expect(reviewEntries.has('assets/tiny.png')).toBe(true);
  await expect(page.locator('#exportTrust')).toHaveText(/Safe artefact metadata included/);

  await page.locator('#zipInput').setInputFiles(reviewPath);
  await submitAppDialogIfVisible(page, { button: 'Discard changes' });
  await expect(page.locator('#status')).toHaveText(/Imported 8 documents and 1 image asset from artefact bundle\. Opened README\.md\./, { timeout: 20_000 });
  await expect(page.locator('#editor')).toHaveValue(/rich bundle exercises/);
  await expect(page.locator('#artifactBundleSummary')).toContainText('Rich Artefact Bundle');
  await page.locator('#artifactBundleSummary').getByRole('button', { name: 'Expand artefact bundle reader' }).click();
  await expect(page.locator('#artifactBundleSummary')).toContainText('candidate finding');
  await expect(page.locator('#artifactBundleSummary')).toContainText('confirmed finding');
  await page.locator('#artifactBundleSummary').getByRole('button', { name: /Trigger Timeline/ }).click();
  await expect(page.locator('#editor')).toHaveValue(/flowchart TD/);
  expect(await page.locator('#editor').evaluate((editor) => editor.readOnly)).toBe(false);
});

test('ordinary Markdown Bundle export stays free of artefact manifests after artefact import', async ({ page }, testInfo) => {
  const zipPath = await writeArtifactBundleFixtureZip(testInfo, 'valid-rich');

  await gotoApp(page);
  await page.locator('#zipInput').setInputFiles(zipPath);
  await expect(page.locator('#status')).toHaveText(/Imported 8 documents and 1 image asset from artefact bundle/, { timeout: 20_000 });

  const bundlePath = await clickExportDownload(page, 'Export Markdown Bundle');
  const entries = await readZipEntries(bundlePath);
  expect(entries.has('lens-docs-studio-bundle.json')).toBe(true);
  expect(entries.has('lens-artifact-bundle.json')).toBe(false);

  await page.locator('#zipInput').setInputFiles(bundlePath);
  await submitAppDialogIfVisible(page, { button: 'Discard changes' });
  await expect(page.locator('#status')).toHaveText(/Imported 8 documents and 1 image asset from Markdown bundle/, { timeout: 20_000 });
  await expect(page.locator('#artifactBundleSummary')).toBeHidden();
  await page.locator('#fileList [data-path="README.md"]').click();
  await expect(page.locator('#editor')).toHaveValue(/rich bundle exercises/);
  expect(await page.locator('#editor').evaluate((editor) => editor.readOnly)).toBe(false);
  await page.locator('#fileList [data-path="diagrams/trigger-timeline.mmd"]').click();
  await expect(page.locator('#editor')).toHaveValue(/flowchart TD/);
});

test('ZIP import accepts compressed generic docs and handles ZIPs without sources', async ({ page }, testInfo) => {
  const zipPath = await writeArtifactBundleFixtureZip(testInfo, 'generic-zip', 'generic-docs.zip');

  await gotoApp(page);
  await page.locator('#zipInput').setInputFiles(zipPath);
  await expect(page.locator('#status')).toHaveText(/Imported 2 documents and 1 image asset from ZIP/, { timeout: 20_000 });
  await expect(page.locator('#artifactBundleSummary')).toBeHidden();
  await expect(page.locator('#status')).toHaveText(/SVG images are not imported for security/);
  await expect(page.locator('#editor')).toHaveValue(/Generic ZIP Import/);
  await expect(page.locator('#preview img[data-managed-asset-path="docs/images/logo.png"]')).toHaveAttribute('src', /^blob:/);
  await expect(page.locator('#preview img[data-managed-asset-path="docs/images/blocked.svg"]')).toHaveCount(0);
  await expect(page.locator('.diagram-frame svg')).toHaveCount(1);

  const noDocsPath = testInfo.outputPath('no-source-docs.zip');
  await writeFile(noDocsPath, createZipBuffer([
    { name: 'index.html', data: '<h1>Rendered only</h1>' },
    { name: 'site-manifest.json', data: '{"formatVersion":"docs-site-2.0"}' },
  ], { compress: true }));

  await page.locator('#zipInput').setInputFiles(noDocsPath);
  await expect(page.locator('#status')).toHaveText(/No editable Markdown or Mermaid source files/);
  await expect(page.locator('#editor')).toHaveValue(/Generic ZIP Import/);
});

test('document import converts HTML and DOCX into editable Markdown', async ({ page }, testInfo) => {
  const htmlPath = testInfo.outputPath('import-html.html');
  await writeFile(htmlPath, `<!doctype html>
<html><body>
  <script>window.__bad = true;</script>
  <h1>Imported HTML</h1>
  <p>Hello <strong>safe</strong> <a href="javascript:alert(1)">bad link</a> <a href="https://example.com">good</a></p>
  <ul><li>First</li><li>Second</li></ul>
  <table><tr><th>Feature</th><th>Status</th></tr><tr><td>HTML</td><td>Ready</td></tr></table>
  <pre><code class="language-js">const value = 1;</code></pre>
  <img alt="HTML logo" src="data:image/png;base64,${tinyPngBase64}">
  <img alt="Bad SVG" src="data:image/svg+xml;base64,PHN2ZyBvbmxvYWQ9ImFsZXJ0KDEpIj48L3N2Zz4=">
</body></html>`);

  await gotoApp(page);
  await page.locator('#documentInput').setInputFiles(htmlPath);
  await expect(page.locator('#status')).toHaveText(/Imported 1 converted document and 1 image asset/);
  await expect(page.locator('#saveButton')).toBeEnabled();

  const htmlMarkdown = normaliseLineEndings(await page.locator('#editor').inputValue());
  expect(htmlMarkdown).toContain('# Imported HTML');
  expect(htmlMarkdown).toContain('Hello **safe** bad link [good](https://example.com)');
  expect(htmlMarkdown).toContain('- First\n- Second');
  expect(htmlMarkdown).toContain('| Feature | Status |');
  expect(htmlMarkdown).toContain('```js\nconst value = 1;\n```');
  expect(htmlMarkdown).toContain('![HTML logo](assets/images/import-html-html-logo.png)');
  expect(htmlMarkdown).not.toContain('javascript:');
  expect(htmlMarkdown).not.toContain('data:image/svg');
  await expect(page.locator('#preview img[data-managed-asset-path="assets/images/import-html-html-logo.png"]')).toHaveAttribute('src', /^blob:/);

  const docxPath = testInfo.outputPath('import-word.docx');
  await writeFile(docxPath, createDocxImportFixture());
  let releaseWorker;
  let resolveWorkerRequested;
  const workerGate = new Promise((resolve) => { releaseWorker = resolve; });
  const workerRequested = new Promise((resolve) => { resolveWorkerRequested = resolve; });
  await page.route('**/assets/scripts/files/document-import-worker.js', async (route) => {
    resolveWorkerRequested();
    await workerGate;
    await route.continue();
  });

  await page.locator('#documentInput').setInputFiles(docxPath);
  await submitAppDialog(page, { button: 'Discard changes' });
  await workerRequested;
  await expect(page.locator('#status')).toHaveText(/Converting 1 document to Markdown/);
  await expect(page.locator('#status')).toHaveClass(/busy/);
  await expect(page.locator('#status')).toHaveAttribute('aria-busy', 'true');
  releaseWorker();
  await expect(page.locator('#status')).toHaveText(/Imported 1 converted document and 1 image asset/, { timeout: 20_000 });
  await expect(page.locator('#status')).not.toHaveAttribute('aria-busy', 'true');

  const wordMarkdown = normaliseLineEndings(await page.locator('#editor').inputValue());
  expect(wordMarkdown).toContain('# Imported Word');
  expect(wordMarkdown).toContain('Hello **bold** body');
  expect(wordMarkdown).toContain('First item');
  expect(wordMarkdown).toContain('| Area | Status |');
  expect(wordMarkdown).toContain('![Word logo](assets/images/import-word-word-logo.png)');
  await expect(page.locator('#preview img[data-managed-asset-path="assets/images/import-word-word-logo.png"]')).toHaveAttribute('src', /^blob:/);
});

test('wide preview images fit without horizontal scrolling', async ({ page }) => {
  await gotoApp(page);
  await setEditorValueAndSelection(page, [
    '# Wide Screenshot',
    '',
    `<img alt="wide screenshot" src="data:image/png;base64,${tinyPngBase64}" width="2400" height="120">`,
  ].join('\n'));
  await renderPreviewWithShortcut(page);

  const metrics = await page.locator('#preview').evaluate((preview) => {
    const image = preview.querySelector('img[alt="wide screenshot"]');
    const imageRect = image.getBoundingClientRect();
    const style = getComputedStyle(preview);
    const contentWidth = preview.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);

    return {
      contentWidth,
      imageWidth: imageRect.width,
      scrollWidth: preview.scrollWidth,
      clientWidth: preview.clientWidth,
    };
  });

  expect(metrics.imageWidth).toBeLessThanOrEqual(metrics.contentWidth + 1);
  expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth + 1);
});

test('document import drag and drop handles HTML and PDF text extraction', async ({ page }) => {
  await gotoApp(page);
  await dropVirtualFile(page, {
    name: 'drop.html',
    type: 'text/html',
    text: '<h1>Dropped HTML</h1><p>Converted by drag and drop.</p>',
  });
  await expect(page.locator('#status')).toHaveText(/Imported 1 converted document/);
  await expect(page.locator('#editor')).toHaveValue(/# Dropped HTML/);

  await gotoApp(page);
  await dropVirtualFile(page, {
    name: 'future.pdf',
    type: 'application/pdf',
    base64: createSimplePdfBuffer(['First PDF page', 'Second PDF page']).toString('base64'),
  });
  await expect(page.locator('#status')).toHaveText(/Imported 1 converted document/, { timeout: 20_000 });

  const pdfMarkdown = normaliseLineEndings(await page.locator('#editor').inputValue());
  expect(pdfMarkdown).toContain('# future');
  expect(pdfMarkdown).toContain('> Imported from PDF as text-only Markdown.');
  expect(pdfMarkdown).toContain('## Page 1');
  expect(pdfMarkdown).toContain('First PDF page');
  expect(pdfMarkdown).toContain('## Page 2');
  expect(pdfMarkdown).toContain('Second PDF page');
});

test('Docs Site export contains the expected static site package', async ({ page }, testInfo) => {
  await gotoApp(page);
  await page.locator('#folderInput').setInputFiles(fixturePath('docs-site'));
  await expect(page.locator('#status')).toHaveText(/Rendered/, { timeout: 20_000 });

  const filePath = await clickDocsSiteExportDownload(page);
  const entries = await readZipEntries(filePath);
  const html = getZipText(entries, 'index.html');
  const css = getZipText(entries, 'assets/docs-site.css');
  const dataScript = getZipText(entries, 'assets/docs-site-data.js');
  const script = getZipText(entries, 'assets/docs-site.js');
  const searchIndex = JSON.parse(getZipText(entries, 'assets/search-index.json'));
  const manifest = JSON.parse(getZipText(entries, 'site-manifest.json'));
  const readme = getZipText(entries, 'README.md');

  expect([...entries.keys()].sort()).toEqual([
    'README.md',
    'assets/docs-site-data.js',
    'assets/docs-site.css',
    'assets/docs-site.js',
    'assets/search-index.json',
    'index.html',
    'site-manifest.json',
  ]);
  expect(manifest.formatVersion).toBe('docs-site-2.0');
  expect(manifest.defaultTheme).toBe('system');
  expect(manifest.pageCount).toBe(4);
  expect(manifest.sourcePageCount).toBe(4);
  expect(manifest.diagramTotal).toBe(2);
  expect(manifest.diagramErrors).toBe(0);
  expect(manifest.artifactBundle).toBeUndefined();
  expect(searchIndex.artifactBundle).toBeUndefined();
  expect(manifest.homePage.generated).toBe(false);
  expect(searchIndex.formatVersion).toBe('docs-site-2.0');
  expect(searchIndex.homePageId).toBe(manifest.homePage.id);
  expect(searchIndex.pages).toHaveLength(4);
  expect(searchIndex.entries.some((entry) => entry.text.includes('release ready'))).toBe(true);
  expect(searchIndex.entries.some((entry) => entry.text.includes('Copy') || entry.text.includes('SVG') || entry.text.includes('PNG'))).toBe(false);
  expect(html).toContain('assets/docs-site.css');
  expect(html).toContain('assets/docs-site-data.js');
  expect(html).toContain('assets/docs-site.js');
  expect(html).toContain('Content-Security-Policy');
  expect(html).toContain('data-theme-preference="system"');
  expect(html).not.toContain('document.documentElement.dataset.themePreference=');
  expect(css).toContain('[data-theme="dark"]');
  expect(dataScript).toContain('window.__LENS_DOCS_SITE_DATA__');
  expect(dataScript).toContain('release ready');
  expect(script).toContain('__LENS_DOCS_SITE_DATA__');
  expect(script).toContain('assets/search-index.json');
  expect(script).toContain('renderSearchResults');
  expect(searchIndex.pages.some((pageData) => pageData.html.includes('data-code-action="copy"'))).toBe(true);
  expect(searchIndex.pages.some((pageData) => pageData.html.includes('data-table-action="copy"'))).toBe(true);
  expect(searchIndex.pages.some((pageData) => pageData.html.includes('data-table-action="downloadCsv"'))).toBe(true);
  expect(searchIndex.pages.some((pageData) => pageData.html.includes('exportPng'))).toBe(true);
  expect(searchIndex.pages.some((pageData) => pageData.html.includes('copyPng'))).toBe(true);
  expect(html).not.toContain('preview-search-hit');
  expect(html).not.toContain('selection-sync-hit');
  expect(html).not.toContain('Document review');
  expect(readme).toContain('## Open Or Deploy');
  expect(readme).toContain('Open `index.html` directly');
  expect(readme).toContain('GitHub Pages');
  await expect(page.locator('#status')).toHaveText(/Docs site exported/);

  const outputDirectory = testInfo.outputPath('docs-site-export');
  await writeZipEntriesToDirectory(entries, outputDirectory);
  const publicPath = path.relative(root, path.join(outputDirectory, 'index.html')).replaceAll(path.sep, '/');
  await page.goto(`/${publicPath}`);

  await expect(page.locator('#siteTitle')).toHaveText('Publishing Docs');
  await expect(page.locator('#siteDescription')).toHaveText('Docs Site Builder 2.0 export fixture.');
  await expect(page.locator('#themeToggle')).toHaveText('System');
  await expect(page.locator('#content h1')).toHaveText('Docs Home');
  await page.locator('#themeToggle').click();
  await expect(page.locator('#themeToggle')).toHaveText('Light');
  await page.locator('#themeToggle').click();
  await expect(page.locator('#themeToggle')).toHaveText('Dark');

  await page.locator('#searchInput').fill('release ready');
  await expect(page.locator('#searchResults .search-result')).not.toHaveCount(0);
  await page.locator('#searchResults .search-result').first().click();
  await expect(page.locator('#content')).toContainText('release ready');
  await expect(page.locator('[data-code-action="copy"]')).toHaveCount(1);
  await expect(page.locator('[data-table-action="copy"]')).toHaveCount(1);
  await expect(page.locator('[data-table-action="downloadCsv"]')).toHaveCount(1);
  await expect(page.locator('[data-diagram-action="exportPng"]')).toHaveCount(1);
  await expect(page.locator('[data-diagram-action="copyPng"]')).toHaveCount(1);

  await page.locator('#searchInput').fill('SVG');
  await expect(page.locator('#searchResults')).toContainText('No results.');
  await page.locator('#searchInput').fill('');
  await page.getByRole('link', { name: 'Larger Fixture' }).click();
  await page.getByRole('link', { name: 'Section Three' }).click();
  await expect(page).toHaveURL(/#docs-site-large\/section-three$/);

  await page.goto(pathToFileURL(path.join(outputDirectory, 'index.html')).href, { waitUntil: 'domcontentloaded' });
  await expect(page.locator('#siteTitle')).toHaveText('Publishing Docs');
  await expect(page.locator('#content h1')).toHaveText('Docs Home');
  await page.locator('#searchInput').fill('release ready');
  await expect(page.locator('#searchResults .search-result')).not.toHaveCount(0);

  await page.setViewportSize({ width: 390, height: 844 });
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1);
  expect(overflow).toBe(false);
});

test('front matter drives Docs Site metadata without rendering as content', async ({ page }) => {
  await loadVirtualWorkspace(page, [
    {
      name: 'guide.md',
      text: '---\ntitle: API Guide\ndescription: Internal API docs\norder: 1\ntags: [api, auth]\ndraft: true\nnavGroup: Guides\n---\n# Rendered Guide\nBody text.',
    },
    {
      name: 'README.md',
      text: '---\ntitle: Project Home\norder: 10\nnavGroup: Overview\n---\n# Home\nWelcome.',
    },
  ]);

  await expect(page.locator('#preview')).not.toContainText('navGroup');
  const zipPath = await clickDocsSiteExportDownload(page, { title: 'Metadata Docs', description: 'Front matter fixture.' });
  const entries = await readZipEntries(zipPath);
  const searchIndex = JSON.parse(getZipText(entries, 'assets/search-index.json'));
  const manifest = JSON.parse(getZipText(entries, 'site-manifest.json'));
  const guide = searchIndex.pages.find((item) => item.path === 'guide.md');

  expect(searchIndex.pages.map((item) => item.path).slice(0, 2)).toEqual(['guide.md', 'README.md']);
  expect(guide.title).toBe('API Guide');
  expect(guide.description).toBe('Internal API docs');
  expect(guide.tags).toEqual(['api', 'auth']);
  expect(guide.draft).toBe(true);
  expect(guide.navGroup).toBe('Guides');
  expect(guide.html).not.toContain('title: API Guide');
  expect(manifest.pages.find((item) => item.path === 'guide.md').draft).toBe(true);
});

test('Docs Site export uses safe artefact metadata as display-only fallback', async ({ page }, testInfo) => {
  const zipPath = await writeArtifactBundleFixtureZip(testInfo, 'front-matter-precedence');

  await gotoApp(page);
  await page.locator('#zipInput').setInputFiles(zipPath);
  await expect(page.locator('#status')).toHaveText(/Imported 2 documents from artefact bundle/, { timeout: 20_000 });

  const docsPath = await clickDocsSiteExportDownload(page, {
    title: 'Artefact Docs',
    description: 'Artefact metadata export fixture.',
  });
  const entries = await readZipEntries(docsPath);
  const html = getZipText(entries, 'index.html');
  const dataScript = getZipText(entries, 'assets/docs-site-data.js');
  const searchIndexText = getZipText(entries, 'assets/search-index.json');
  const manifestText = getZipText(entries, 'site-manifest.json');
  const readme = getZipText(entries, 'README.md');
  const manifest = JSON.parse(getZipText(entries, 'site-manifest.json'));
  const searchIndex = JSON.parse(getZipText(entries, 'assets/search-index.json'));
  const homePage = searchIndex.pages.find((item) => item.path === 'README.md');
  const findingPage = searchIndex.pages.find((item) => item.path === 'findings/security-summary.md');

  expect(manifest.artifactBundle.title).toBe('Docs Artefact Pack');
  expect(manifest.artifactBundle.sourceTool).toBe('Security Lens');
  expect(manifest.artifactBundle.notes).toContain('Evidence labels are displayed as supplied by the bundle.');
  expect(manifest.artifactBundle.notes).toContain('Candidate findings remain candidate findings.');
  expect(searchIndex.artifactBundle.title).toBe('Docs Artefact Pack');
  expect(searchIndex.artifactBundle.sourceToolVersion).toBe('3.0.0');
  expect(homePage.title).toBe('Front Matter Home');
  expect(homePage.order).toBe(99);
  expect(homePage.navGroup).toBe('Front Matter');
  expect(findingPage.title).toBe('Manifest Finding');
  expect(findingPage.order).toBe(2);
  expect(findingPage.navGroup).toBe('Findings');
  expect(findingPage.artifactKind).toBe('finding');
  expect(findingPage.evidenceLevel).toBe('candidate finding');
  expect(manifest.pages.find((item) => item.path === 'findings/security-summary.md').artifactKind).toBe('finding');
  expect(manifest.pages.find((item) => item.path === 'findings/security-summary.md').evidenceLevel).toBe('candidate finding');
  expect(searchIndex.entries.some((entry) => entry.text.includes('candidate finding'))).toBe(true);
  expect(searchIndex.entries.some((entry) => entry.text.includes('finding'))).toBe(true);
  [html, dataScript, searchIndexText, manifestText, readme].forEach((content) => {
    expect(content).not.toContain('../unsafe.md');
    expect(content).not.toContain('Unsafe Export');
    expect(content).not.toContain('http://');
    expect(content).not.toContain('https://');
    expect(content).not.toContain('<script src="http');
  });

  const outputDirectory = testInfo.outputPath('artefact-docs-site-export');
  await writeZipEntriesToDirectory(entries, outputDirectory);
  const publicPath = path.relative(root, path.join(outputDirectory, 'index.html')).replaceAll(path.sep, '/');
  await page.goto(`/${publicPath}`);
  await expect(page.locator('#artifactNotes')).toContainText('Evidence labels are displayed as supplied by the bundle.');
  await expect(page.locator('#artifactNotes')).toContainText('Candidate findings remain candidate findings.');
  await page.getByRole('link', { name: 'Manifest Finding' }).click();
  await expect(page.locator('#docMeta')).toContainText('candidate finding');
});

test('theme, preview maximise, and mobile layout stay usable', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await gotoApp(page);
  await loadSample(page);

  await page.locator('#themeToggleButton').click();
  await page.locator('summary').filter({ hasText: /^View$/ }).click();
  await page.locator('[data-view-action="maximizePreview"]').click();
  await page.locator('#previewFindToggleButton').click();
  await page.locator('#documentReviewToggleButton').click();
  await page.locator('#outlineToggleButton').click();

  await expect(page.locator('#app')).toHaveClass(/preview-maximized/);
  await expectMobileMenuPanelUsable(page, 'Export');
});

test('mobile topbar menus open as fixed usable panels', async ({ page }) => {
  for (const width of [390, 680, 740, 768]) {
    await page.setViewportSize({ width, height: 844 });
    await gotoApp(page);

    for (const menuName of ['File', 'Create', 'Export', 'View']) {
      await expectMobileMenuPanelUsable(page, menuName);
      await page.keyboard.press('Escape');
    }
  }
});
