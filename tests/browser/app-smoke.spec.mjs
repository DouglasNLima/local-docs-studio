import { expect, test } from '@playwright/test';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
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

async function openFixture(page, name) {
  await page.goto('/');
  await page.locator('#fileInput').setInputFiles(fixturePath(name));
  await expect(page.locator('#status')).toHaveText(/Rendered/);
}

async function loadSample(page) {
  await page.locator('summary').filter({ hasText: /^Examples$/ }).click();
  await page.getByRole('button', { name: 'Markdown + Mermaid sample' }).click();
  await expect(page.locator('#status')).toHaveText(/Rendered/);
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
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        readText: async () => payload.readText,
        writeText: async (text) => {
          window.__copiedText = text;
        },
      },
    });
  }, { readText });
}

async function clickEditAction(page, name) {
  await page.locator('summary').filter({ hasText: /^Edit$/ }).click();
  await page.getByRole('button', { name }).click();
}

async function loadVirtualWorkspace(page, files) {
  await page.goto('/');
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

test('root loads the buildless app shell', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveTitle('Local Docs Studio');
  await expect(page.locator('#app')).toBeVisible();
  await expect(page.locator('meta[http-equiv="Content-Security-Policy"]')).toHaveCount(1);
  await expect(page.locator('script[type="module"][src$="/assets/scripts/main.js"]')).toHaveCount(1);
  await expect(page.locator('link[rel="stylesheet"][href$="/assets/styles/app.css"]')).toHaveCount(1);
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
  await expect(page.locator('[data-diagram-action="copySource"]')).toHaveCount(1);
  await expect(page.locator('[data-diagram-action="exportSvg"]')).toHaveCount(1);
  await expect(page.locator('[data-diagram-action="exportPng"]')).toHaveCount(1);
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
});

test('editor syntax highlighting and math rendering work without a build step', async ({ page }) => {
  await page.goto('/');
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

  await page.locator('#renderButton').click();
  await expect(page.locator('#preview .math-inline')).toContainText('E=mc');
  await expect(page.locator('#preview .math-inline .katex')).toBeVisible();
  await expect(page.locator('#preview .math-block .katex')).toBeVisible();
  await expect(page.locator('#preview .math-block')).toContainText('a');
  await expect(page.locator('#preview .math-block')).toContainText('b');
});

test('rendering sanitizes hostile Markdown and Mermaid output', async ({ page }) => {
  await page.goto('/');
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
  await page.locator('#renderButton').click();
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
  await page.goto('/');

  await page.locator('summary').filter({ hasText: /^File$/ }).click();
  await expect(page.locator('[data-menu-action="openFile"]')).toBeVisible();
  await expect(page.locator('details.menu[open] .menu-heading')).toContainText(['Open', 'Import', 'Save', 'Recent']);
  await page.keyboard.press('Escape');
  await expect(page.locator('summary').filter({ hasText: /^File$/ }).locator('..')).not.toHaveAttribute('open', '');

  await page.locator('summary').filter({ hasText: /^Export$/ }).click();
  await expect(page.locator('#downloadButton')).toBeVisible();
  await expect(page.locator('details.menu[open] .menu-heading')).toContainText(['Documents', 'Bundles', 'Diagrams', 'Clipboard']);
  await expect(page.locator('#exportPdfButton')).toBeVisible();
  await expect(page.locator('#exportMarkdownBundleButton')).toBeVisible();

  await page.locator('summary').filter({ hasText: /^View$/ }).click();
  await expect(page.locator('details.menu[open]')).toContainText('Preview tools');
  const viewPanelBox = await page.locator('details.menu[open] .menu-panel').boundingBox();
  const viewport = page.viewportSize();
  expect(viewPanelBox.x).toBeGreaterThanOrEqual(0);
  expect(viewPanelBox.x + viewPanelBox.width).toBeLessThanOrEqual(viewport.width + 1);

  await page.locator('summary').filter({ hasText: /^Help$/ }).click();
  await expect(page.locator('details.menu[open]')).toContainText('Mermaid snippets');
  await expect(page.locator('details.menu[open]')).toContainText('Open feature guide');
  await expect(page.locator('details.menu[open]')).toContainText('Import ZIP');
  await expect(page.locator('details.menu[open]')).toContainText('Markdown Bundle');
});

test('Help menu opens the feature guide as read-only Markdown', async ({ page }) => {
  await page.goto('/');

  await page.locator('summary').filter({ hasText: /^Help$/ }).click();
  await page.getByRole('button', { name: 'Open feature guide' }).click();

  await expect(page.locator('#status')).toHaveText(/Feature guide opened read-only/);
  await expect(page.locator('#activeFileLabel')).toHaveText(/tool-feature-guide\.md · read-only/);
  await expect(page.locator('#preview h1')).toHaveText('Local Docs Studio Feature Guide');
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

test('custom context menu handles editor actions and preserves native fallbacks', async ({ page }) => {
  await page.goto('/');
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

test('custom context menu exposes preview-specific copy and export actions', async ({ page }) => {
  await openFixture(page, 'mixed.md');
  await mockClipboardWrite(page);
  const menu = page.locator('.context-menu');

  await expect(page.locator('.diagram-frame [data-diagram-action="copySource"]')).toBeVisible();
  await page.locator('.diagram-frame [data-diagram-action="copySource"]').click({ button: 'right' });
  await expect(menu.locator('[data-context-menu-action="preview-copy-diagram-source"]')).toBeVisible();
  await expect(menu.locator('[data-context-menu-action="preview-export-diagram-svg"]')).toBeVisible();
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
  await menu.locator('[data-context-menu-action="preview-copy-table"]').click();
  await expect.poll(() => page.evaluate(() => window.__copiedText)).toBe('Area\tStatus\nPreview\tReady\nExport\tVerified');
});

test('visual refresh screenshot artifacts cover key shell states', async ({ page }, testInfo) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/');
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

  await page.goto('/');
  await page.locator('summary').filter({ hasText: /^Create$/ }).click();
  await expect(page.locator('[data-create-template="requirements.devopsConclusion"]')).toBeVisible();
  const createMenuBox = await page.locator('#createMenu .menu-panel').boundingBox();
  const createViewport = page.viewportSize();
  expect(createMenuBox.x).toBeGreaterThanOrEqual(0);
  expect(createMenuBox.x + createMenuBox.width).toBeLessThanOrEqual(createViewport.width + 1);

  await page.goto('/');
  await page.locator('summary').filter({ hasText: /^Create$/ }).click();
  await page.locator('[data-create-template="project.architecture"]').click();
  await expect(page.getByRole('heading', { name: 'Create Architecture Overview' })).toBeVisible();
  await attachViewportScreenshot(page, testInfo, 'phase-9-template-dialog-dark');

  await page.goto('/');
  await page.evaluate(() => {
    localStorage.setItem('md-mmd-renderer.theme', 'light');
  });
  await page.goto('/');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await loadSample(page);
  await attachViewportScreenshot(page, testInfo, 'phase-9-sample-light');
  await page.goto('/');
  await attachViewportScreenshot(page, testInfo, 'phase-9-empty-light');

  await page.evaluate(() => {
    localStorage.setItem('md-mmd-renderer.theme', 'dark');
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await loadSample(page);
  await attachViewportScreenshot(page, testInfo, 'phase-9-mobile-sample-dark');
});

test('editor toolbar icon buttons keep markdown command behavior', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('#editorToolbar svg.toolbar-icon')).toHaveCount(15);
  await expect(page.locator('#editorToolbar .toolbar-section')).toHaveCount(4);

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

  const toolbarOverflow = await page.locator('#editorToolbar').evaluate((toolbar) => toolbar.scrollWidth > toolbar.clientWidth + 1);
  expect(toolbarOverflow).toBe(false);
});

test('table toolbar opens a visual editor for new and existing Markdown tables', async ({ page }) => {
  await page.goto('/');
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

  await page.getByRole('button', { name: 'Review' }).click();
  await expect(page.locator('.document-review-links')).toContainText('Backlinks (1)');
  await expect(page.locator('.document-review-links')).toContainText('index.md:2');

  const docsPath = await clickDocsSiteExportDownload(page, { title: 'Wiki Docs', description: 'Wikilink export check.' });
  const entries = await readZipEntries(docsPath);
  const searchIndex = JSON.parse(getZipText(entries, 'assets/search-index.json'));
  const indexPage = searchIndex.pages.find((item) => item.path === 'index.md');
  expect(indexPage.html).not.toContain('data-wikilink-target');
  expect(indexPage.html).toContain('href="#second"');
});

test('local draft recovery and large deletion protection guard browser-local edits', async ({ page }) => {
  const file = { name: 'draft.md', mimeType: 'text/markdown', buffer: Buffer.from('# Draft\nThis paragraph should survive local recovery because it is long enough to trigger the deletion guard when most of it disappears.\n') };
  await page.goto('/');
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
  await page.getByRole('button', { name: 'Save' }).click();
  await expect(page.getByRole('heading', { name: 'Large deletion detected' })).toBeVisible();
  await page.getByRole('button', { name: 'Undo deletion' }).click();
  await expect(page.locator('#editor')).toHaveValue(/This paragraph should survive local recovery/);
});

test('writer shortcut is disabled while input maximize handles focused writing', async ({ page }) => {
  await page.goto('/');
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

test('spreadsheet paste auto-converts TSV and HTML tables while leaving plain text alone', async ({ page }) => {
  await page.goto('/');

  await setEditorValueAndSelection(page, 'Intro', 5, 5);
  await pasteIntoEditor(page, { text: 'Name\tQty\nApples\t4\nPears\t7' });
  await expect(page.locator('#editor')).toHaveValue('Intro\n\n| Name | Qty |\n| --- | --- |\n| Apples | 4 |\n| Pears | 7 |');
  await expect(page.locator('#status')).toHaveText(/Table pasted as Markdown/);
  await expect(page.locator('#preview table')).toHaveCount(1);

  await setEditorValueAndSelection(page, '');
  await pasteIntoEditor(page, {
    html: '<table><tr><th>Area</th><th>Status</th></tr><tr><td>Preview</td><td>Ready</td></tr></table>',
    text: 'Area\tStatus\nPreview\tReady',
  });
  await expect(page.locator('#editor')).toHaveValue('| Area | Status |\n| --- | --- |\n| Preview | Ready |');
  await expect(page.locator('#preview table')).toHaveCount(1);

  await setEditorValueAndSelection(page, '');
  await pasteIntoEditor(page, { text: 'Just normal text\nwith words.' });
  await expect(page.locator('#editor')).toHaveValue('Just normal text\nwith words.');
});

test('Paste Special inserts table, text, code block, and supports next-paste fallback', async ({ page }) => {
  await page.goto('/');

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
  await page.goto('/');

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
  await expect(page.locator('#inputMaximizeButton')).toHaveText('Restore');
  await expect(page.locator('.preview-pane')).toBeHidden();
  await page.locator('#inputMaximizeButton').click();
  await expect(page.locator('#app')).not.toHaveClass(/input-maximized/);
  await expect(page.locator('#inputMaximizeButton')).toHaveText('Maximize');

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

test('focus mode exposes a visible exit button and keeps Escape fallback', async ({ page }) => {
  await page.goto('/');

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
  await page.goto('/');
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

test('selection sync highlights editor selections in preview and respects the sync toggle', async ({ page }) => {
  await openFixture(page, 'document-ux.md');

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
  await page.locator('#renderButton').click();
  await expect(page.locator('#status')).toHaveText(/Rendered/);
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
  await page.locator('#renderButton').click();
  await expect(page.locator('#status')).toHaveText(/Rendered/);
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
  await openFixture(page, 'broken-mermaid.md');

  await expect(page.locator('#status')).toHaveText(/diagram error/);
  await expect(page.locator('[data-diagram-action="copyError"]')).toHaveCount(1);
  await expect(page.locator('[data-diagram-action="jumpSource"]')).toHaveCount(1);
  await expect(page.locator('[data-diagram-action="exportSvg"]')).toHaveCount(0);
  await expect(page.locator('[data-diagram-action="exportPng"]')).toHaveCount(0);
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
  expect(html).toContain('data-diagram-action="copySource"');
  expect(html).toContain('data-diagram-action="exportSvg"');
  expect(html).toContain('function fallbackCopy');
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
  const manifest = JSON.parse(getZipText(entries, 'local-docs-studio-bundle.json'));

  expect(manifest.formatVersion).toBe('markdown-bundle-1.0');
  expect(manifest.documentCount).toBe(1);
  expect(manifest.assetCount).toBe(1);
  expect(manifest.documents[0].dirty).toBe(true);
  expect(getZipText(entries, 'plain.md')).toContain('Bundle Edited');
  expect(entries.has('assets/images/tiny-image.png')).toBe(true);

  page.once('dialog', (dialog) => dialog.accept());
  await page.locator('#zipInput').setInputFiles(bundlePath);
  await expect(page.locator('#status')).toHaveText(/Imported 1 document and 1 image asset from Markdown bundle/);
  await expect(page.locator('#editor')).toHaveValue(/Bundle Edited/);
  await expect(page.locator('#preview img[data-managed-asset-path="assets/images/tiny-image.png"]')).toHaveAttribute('src', /^blob:/);
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

test('ZIP import accepts compressed generic docs and handles ZIPs without sources', async ({ page }, testInfo) => {
  const zipPath = testInfo.outputPath('generic-docs.zip');
  await writeFile(zipPath, createZipBuffer([
    {
      name: 'docs/README.md',
      data: '# Imported Docs\n\n![Logo](images/logo.png)\n\n::: mermaid\nsequenceDiagram\n    Christie->>Josh: Hello Josh, how are you?\n    Josh-->>Christie: Great!\n    Christie->>Josh: See you later!\n:::',
    },
    {
      name: 'docs/story.mmd',
      data: 'flowchart LR\n  C[Zip] --> D[Mermaid]',
    },
    {
      name: 'docs/images/logo.png',
      data: Buffer.from(tinyPngBase64, 'base64'),
    },
    {
      name: 'docs/images/blocked.svg',
      data: tinySvg,
    },
    {
      name: '__MACOSX/._ignored',
      data: 'ignored',
    },
  ], { compress: true }));

  await page.goto('/');
  await page.locator('#zipInput').setInputFiles(zipPath);
  await expect(page.locator('#status')).toHaveText(/Imported 2 documents and 1 image asset from ZIP/);
  await expect(page.locator('#status')).toHaveText(/SVG images are not imported for security/);
  await expect(page.locator('#editor')).toHaveValue(/Imported Docs/);
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
  await expect(page.locator('#editor')).toHaveValue(/Imported Docs/);
});

test('Docs Site export contains the expected static site package', async ({ page }, testInfo) => {
  await page.goto('/');
  await page.locator('#folderInput').setInputFiles(fixturePath('docs-site'));
  await expect(page.locator('#status')).toHaveText(/Rendered/);

  const filePath = await clickDocsSiteExportDownload(page);
  const entries = await readZipEntries(filePath);
  const html = getZipText(entries, 'index.html');
  const css = getZipText(entries, 'assets/docs-site.css');
  const script = getZipText(entries, 'assets/docs-site.js');
  const searchIndex = JSON.parse(getZipText(entries, 'assets/search-index.json'));
  const manifest = JSON.parse(getZipText(entries, 'site-manifest.json'));
  const readme = getZipText(entries, 'README.md');

  expect([...entries.keys()].sort()).toEqual([
    'README.md',
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
  expect(manifest.homePage.generated).toBe(false);
  expect(searchIndex.formatVersion).toBe('docs-site-2.0');
  expect(searchIndex.homePageId).toBe(manifest.homePage.id);
  expect(searchIndex.pages).toHaveLength(4);
  expect(searchIndex.entries.some((entry) => entry.text.includes('release ready'))).toBe(true);
  expect(searchIndex.entries.some((entry) => entry.text.includes('Copy') || entry.text.includes('SVG') || entry.text.includes('PNG'))).toBe(false);
  expect(html).toContain('assets/docs-site.css');
  expect(html).toContain('assets/docs-site.js');
  expect(html).toContain('Content-Security-Policy');
  expect(html).toContain('data-theme-preference="system"');
  expect(html).not.toContain('document.documentElement.dataset.themePreference=');
  expect(css).toContain('[data-theme="dark"]');
  expect(script).toContain('assets/search-index.json');
  expect(script).toContain('renderSearchResults');
  expect(searchIndex.pages.some((pageData) => pageData.html.includes('data-code-action="copy"'))).toBe(true);
  expect(searchIndex.pages.some((pageData) => pageData.html.includes('data-table-action="copy"'))).toBe(true);
  expect(searchIndex.pages.some((pageData) => pageData.html.includes('exportPng'))).toBe(true);
  expect(html).not.toContain('preview-search-hit');
  expect(html).not.toContain('selection-sync-hit');
  expect(html).not.toContain('Document review');
  expect(readme).toContain('## Deploy');
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
  await expect(page.locator('[data-diagram-action="exportPng"]')).toHaveCount(1);

  await page.locator('#searchInput').fill('SVG');
  await expect(page.locator('#searchResults')).toContainText('No results.');
  await page.locator('#searchInput').fill('');
  await page.getByRole('link', { name: 'Larger Fixture' }).click();
  await page.getByRole('link', { name: 'Section Three' }).click();
  await expect(page).toHaveURL(/#docs-site-large\/section-three$/);

  await page.setViewportSize({ width: 390, height: 844 });
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1);
  expect(overflow).toBe(false);
});

test('theme, preview maximize, and mobile layout stay usable', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await loadSample(page);

  await page.locator('summary').filter({ hasText: /^View$/ }).click();
  await page.getByRole('button', { name: /Switch to/ }).click();
  await page.getByRole('button', { name: 'Maximize preview' }).click();
  await page.locator('#previewFindToggleButton').click();
  await page.locator('#documentReviewToggleButton').click();
  await page.locator('#outlineToggleButton').click();

  await expect(page.locator('#app')).toHaveClass(/preview-maximized/);
  await page.locator('summary').filter({ hasText: /^Export$/ }).click();
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1);
  expect(overflow).toBe(false);
});
