import { base64ToUint8Array, textToBase64 } from '../utils/binary.js';
import { downloadBlob } from '../utils/browser.js';
import { isSupportedFile } from '../utils/files.js';
import { escapeHtml, escapeXml, sanitiseFileName, slugify } from '../utils/format.js';
import { createZipBlob, wrapBase64 } from '../utils/zip.js';
import { formatMarkdownForDevOpsBundle } from '../utils/devops-markdown.js';
import { resolveWikilinkTarget, stripAppWikilinkActions } from '../utils/wikilinks.js';
import {
  buildCspMeta,
  buildDocsSiteCsp,
  buildPrintExportCsp,
  buildStandaloneExportCsp,
  createCspNonce,
  sanitizeRenderedHtml,
} from '../utils/security.js';

const APP_NAME = 'Local Docs Studio';
const MARKDOWN_BUNDLE_MANIFEST_NAME = 'local-docs-studio-bundle.json';

export function createExportService({
  state,
  dom,
  callbacks,
}) {
  const { preview, editor, exportTrust } = dom;
  const {
    renderPreview,
    renderMermaidDiagrams,
    buildMarkdownHtml,
    buildMermaidOnlyHtml,
    resolveModeFor,
    prepareDiagramFramesIn,
    prepareTableBlocksIn,
    getSvgBaseSize,
    getExportFileStem,
    getExportTitle,
    getExportName,
    getWordExportName,
    getDocTitleFromPath,
    closeOpenMenus,
    setStatus,
  } = callbacks;

    async function copyToClipboard(value, okMessage, failMessage) {
      try {
        await navigator.clipboard.writeText(value);
        setStatus(okMessage, 'ok');
      } catch (error) {
        setStatus(failMessage, 'danger');
        console.error(error);
      }
    }

    async function copyCodeBlock(button) {
      const block = button.closest('.code-block');
      const code = block?.querySelector('pre code');
      const text = code?.textContent ?? '';
      if (!text) {
        setStatus('No code found to copy.', 'warning');
        return;
      }

      const previousText = button.textContent;
      try {
        await navigator.clipboard.writeText(text);
        button.textContent = 'Copied';
        button.disabled = true;
        window.setTimeout(() => {
          button.textContent = previousText || 'Copy';
          button.disabled = false;
        }, 1300);
        setStatus('Code copied.', 'ok');
      } catch (error) {
        setStatus('Could not copy code.', 'danger');
        console.error(error);
      }
    }

    async function copyTableBlock(button) {
      const block = button.closest('.table-block');
      const table = block?.querySelector('table') || button.closest('table');
      const text = tableToTsv(table);
      if (!text) {
        setStatus('No table data found to copy.', 'warning');
        return;
      }

      const previousText = button.textContent;
      try {
        await navigator.clipboard.writeText(text);
        button.textContent = 'Copied';
        button.disabled = true;
        window.setTimeout(() => {
          button.textContent = previousText || 'Copy';
          button.disabled = false;
        }, 1300);
        setStatus('Table copied for Excel.', 'ok');
      } catch (error) {
        setStatus('Could not copy table.', 'danger');
        console.error(error);
      }
    }

    function tableToTsv(table) {
      if (!table) return '';
      return [...table.querySelectorAll('tr')]
        .map((row) => [...row.children]
          .filter((cell) => ['td', 'th'].includes(cell.tagName.toLowerCase()))
          .map((cell) => formatTsvCell(cell.textContent || ''))
          .join('\t'))
        .filter((row) => row.length)
        .join('\n');
    }

    function formatTsvCell(value) {
      const text = String(value || '')
        .replace(/\u00a0/g, ' ')
        .replace(/\r\n?/g, '\n')
        .replace(/[ \t]+\n/g, '\n')
        .replace(/\n[ \t]+/g, '\n')
        .trim();

      return /["\t\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
    }

    function getDiagramFrameFromAction(button) {
      return button?.closest('.diagram-frame') || null;
    }

    function getDiagramSourceFromFrame(frame) {
      return frame?.dataset.diagramSource || frame?.querySelector('.mermaid')?.dataset.source || '';
    }

    function getRenderedSvgFromFrame(frame) {
      return frame?.querySelector('.mermaid svg, svg') || null;
    }

    async function copyDiagramSource(frame, button = null) {
      const source = getDiagramSourceFromFrame(frame).trim();
      if (!source) {
        setStatus('No Mermaid source found for this diagram.', 'warning');
        return;
      }

      const previousText = button?.textContent;
      try {
        await navigator.clipboard.writeText(source);
        if (button) {
          button.textContent = 'Copied';
          button.disabled = true;
          window.setTimeout(() => {
            button.textContent = previousText || 'Copy';
            button.disabled = false;
          }, 1300);
        }
        setExportTrust('Mermaid source copied from the selected diagram.', 'ok');
        setStatus('Mermaid source copied.', 'ok');
      } catch (error) {
        setStatus('Could not copy Mermaid source.', 'danger');
        console.error(error);
      }
    }

    function exportDiagramFrameSvg(frame, options = {}) {
      const svg = getRenderedSvgFromFrame(frame);
      if (!svg) {
        setStatus('No rendered Mermaid SVG found for this diagram.', 'warning');
        return false;
      }

      const size = getSvgBaseSize(svg);
      const svgText = serialiseSvgForExport(svg, size.width, size.height);
      downloadBlob(new Blob([svgText], { type: 'image/svg+xml;charset=utf-8' }), getDiagramExportName(frame, 'svg'));
      setExportTrust('SVG ready: exported the selected rendered Mermaid diagram.', 'ok');
      setStatus('SVG diagram exported.', 'ok');
      if (options.closeMenus) closeOpenMenus();
      return true;
    }

    async function exportDiagramFramePng(frame, options = {}) {
      const svg = getRenderedSvgFromFrame(frame);
      if (!svg) {
        setStatus('No rendered Mermaid SVG found for this diagram.', 'warning');
        return false;
      }

      try {
        const size = getSvgBaseSize(svg);
        const dataUrl = await svgToPngDataUrl(svg, size.width, size.height);
        downloadBlob(dataUrlToBlob(dataUrl), getDiagramExportName(frame, 'png'));
        setExportTrust('PNG ready: exported the selected rendered Mermaid diagram.', 'ok');
        setStatus('PNG diagram exported.', 'ok');
        if (options.closeMenus) closeOpenMenus();
        return true;
      } catch (error) {
        setStatus('PNG export failed.', 'danger');
        console.error(error);
        return false;
      }
    }

    function getDiagramExportName(frame, extension) {
      const index = Number(frame?.dataset.diagramIndex || '1');
      const total = Number(frame?.dataset.diagramTotal || '1');
      const suffix = total > 1 && Number.isFinite(index) ? `-diagram-${index}` : '-diagram';
      const stem = frame?.dataset.diagramFileStem || getExportFileStem();
      return `${stem}${suffix}.${extension}`;
    }

    async function exportPreviewHtml() {
      const renderResult = await renderPreview();
      if (!renderResult?.ok && !renderResult?.cancelled) {
        setStatus('HTML export skipped because rendering failed.', 'danger');
        return;
      }
      if (renderResult?.cancelled) return;

      const html = buildExportHtml(getPreviewHtmlForStandaloneExport());
      setExportTrust(buildExportConfidence('HTML', renderResult), renderResult.diagramErrors ? 'warning' : 'ok');
      downloadBlob(new Blob([html], { type: 'text/html;charset=utf-8' }), getExportName());
      setStatus(renderResult.diagramErrors ? 'HTML exported with diagram errors.' : 'HTML exported.', renderResult.diagramErrors ? 'warning' : 'ok');
    }

    async function exportPreviewWord() {
      try {
        setStatus('Preparing Word export...');
        const renderResult = await renderPreview();
        if (!renderResult?.ok && !renderResult?.cancelled) {
          setStatus('Word export skipped because rendering failed.', 'danger');
          return;
        }
        if (renderResult?.cancelled) return;

        const content = await buildWordExportContent();
        setExportTrust(buildExportConfidence('Word', renderResult, content.images), renderResult.diagramErrors ? 'warning' : 'ok');
        downloadBlob(createDocxFromContent(content), getWordExportName());
        setStatus(renderResult.diagramErrors ? 'Word document exported with diagram errors.' : 'Word document exported.', renderResult.diagramErrors ? 'warning' : 'ok');
      } catch (error) {
        setStatus('Word export failed.', 'danger');
        console.error(error);
      }
    }

    async function exportPreviewPdf() {
      try {
        setStatus('Preparing PDF print view...');
        const renderResult = await renderPreview();
        if (!renderResult?.ok && !renderResult?.cancelled) {
          setStatus('PDF export skipped because rendering failed.', 'danger');
          return;
        }
        if (renderResult?.cancelled) return;

        const html = buildPrintHtml(getPreviewHtmlForPrintExport());
        setExportTrust(buildExportConfidence('PDF print', renderResult), renderResult.diagramErrors ? 'warning' : 'ok');
        if (!capturePrintHtmlForTests(html)) {
          openPrintFrame(html);
        }
        setStatus(renderResult.diagramErrors ? 'PDF print view opened with diagram errors.' : 'PDF print view opened.', renderResult.diagramErrors ? 'warning' : 'ok');
        closeOpenMenus();
      } catch (error) {
        setStatus('PDF print export failed.', 'danger');
        console.error(error);
      }
    }

    async function exportMarkdownBundle() {
      const records = state.files.filter((record) => isSupportedFile(record.name));
      if (!records.length) {
        setStatus('Open Markdown or Mermaid files before exporting a bundle.', 'warning');
        return false;
      }

      try {
        setStatus(`Building Markdown bundle from ${records.length} document${records.length === 1 ? '' : 's'}...`);
        const documents = [];
        const zipEntries = [];

        for (const record of records) {
          const path = normaliseBundlePath(record.path || record.name);
          const sourceText = await readRecordText(record);
          const text = state.devopsMarkdownExport
            ? formatMarkdownForDevOpsBundle(sourceText, path)
            : sourceText;
          documents.push({
            path,
            name: record.name,
            dirty: state.dirtyPaths.has(record.path),
            bytes: new TextEncoder().encode(text).byteLength,
          });
          zipEntries.push({ name: path, data: text });
        }

        const assetEntries = buildManagedAssetZipEntries();
        const manifest = buildMarkdownBundleManifest(documents, assetEntries);
        const zip = createZipBlob([
          ...zipEntries,
          ...assetEntries,
          { name: MARKDOWN_BUNDLE_MANIFEST_NAME, data: JSON.stringify(manifest, null, 2) },
        ], 'application/zip');

        downloadBlob(zip, `${sanitiseFileName(manifest.title) || 'markdown-docs'}-markdown-bundle.zip`);
        const devOpsSuffix = state.devopsMarkdownExport ? ' Azure DevOps Mermaid syntax applied.' : '';
        setExportTrust(`Markdown bundle ready: ${documents.length} document${documents.length === 1 ? '' : 's'} and ${assetEntries.length} asset${assetEntries.length === 1 ? '' : 's'}.${devOpsSuffix}`, 'ok');
        setStatus('Markdown bundle exported.', 'ok');
        closeOpenMenus();
        return true;
      } catch (error) {
        setStatus('Markdown bundle export failed.', 'danger');
        console.error(error);
        return false;
      }
    }

    async function copyRenderedHtml() {
      const renderResult = await renderPreview();
      if (!renderResult?.ok && !renderResult?.cancelled) {
        setStatus('Copy skipped because rendering failed.', 'danger');
        return;
      }
      if (renderResult?.cancelled) return;

      const html = buildExportHtml(getPreviewHtmlForStandaloneExport());
      try {
        if (navigator.clipboard?.write && window.ClipboardItem) {
          await navigator.clipboard.write([
            new ClipboardItem({
              'text/html': new Blob([html], { type: 'text/html' }),
              'text/plain': new Blob([getRenderedPlainText()], { type: 'text/plain' }),
            }),
          ]);
        } else {
          await navigator.clipboard.writeText(html);
        }

        setExportTrust(buildExportConfidence('HTML copy', renderResult), renderResult.diagramErrors ? 'warning' : 'ok');
        setStatus(renderResult.diagramErrors ? 'HTML copied with diagram errors.' : 'HTML copied.', renderResult.diagramErrors ? 'warning' : 'ok');
      } catch (error) {
        setStatus('Copy HTML failed.', 'danger');
        console.error(error);
      }
    }

    async function copyRenderedText() {
      const renderResult = await renderPreview();
      if (!renderResult?.ok && !renderResult?.cancelled) {
        setStatus('Copy skipped because rendering failed.', 'danger');
        return;
      }
      if (renderResult?.cancelled) return;

      try {
        await navigator.clipboard.writeText(getRenderedPlainText());
        setExportTrust(buildExportConfidence('Text copy', renderResult), renderResult.diagramErrors ? 'warning' : 'ok');
        setStatus(renderResult.diagramErrors ? 'Rendered text copied with diagram errors.' : 'Rendered text copied.', renderResult.diagramErrors ? 'warning' : 'ok');
      } catch (error) {
        setStatus('Copy text failed.', 'danger');
        console.error(error);
      }
    }

    function getRenderedPlainText() {
      const clone = clonePreviewForPlainText();
      const container = document.createElement('div');
      container.style.position = 'fixed';
      container.style.left = '-10000px';
      container.style.top = '0';
      container.style.width = '1px';
      container.style.height = '1px';
      container.style.overflow = 'hidden';
      container.appendChild(clone);
      document.body.appendChild(container);
      const text = container.innerText.trim();
      container.remove();
      return text;
    }

    function clonePreviewForPlainText() {
      const clone = preview.cloneNode(true);
      clone.querySelectorAll('.code-block-header, .code-copy-button, .table-block-header, .table-copy-button, .diagram-toolbar, .diagram-error-actions').forEach((element) => {
        element.remove();
      });
      stripExportOnlyUi(clone);
      unwrapTemporaryPreviewMarks(clone);
      return clone;
    }

    function getPreviewHtmlForPrintExport() {
      const clone = clonePreviewForPlainText();
      embedManagedAssetImages(clone, state.activePath);
      return clone.innerHTML;
    }

    function getPreviewHtmlForStandaloneExport() {
      const clone = preview.cloneNode(true);
      stripExportOnlyUi(clone);
      unwrapTemporaryPreviewMarks(clone);
      embedManagedAssetImages(clone, state.activePath);
      return clone.innerHTML;
    }

    function stripExportOnlyUi(root) {
      stripAppWikilinkActions(root);
      root.querySelectorAll('[data-search-index], [data-recovery-action], [data-table-editor-action]').forEach((element) => {
        element.removeAttribute('data-search-index');
        element.removeAttribute('data-recovery-action');
        element.removeAttribute('data-table-editor-action');
      });
    }

    function unwrapTemporaryPreviewMarks(root) {
      root.querySelectorAll('mark.preview-search-hit, mark.selection-sync-hit').forEach((mark) => {
        mark.replaceWith(document.createTextNode(mark.textContent || ''));
      });
    }

    function embedManagedAssetImages(root, documentPath = state.activePath) {
      root.querySelectorAll('img').forEach((image) => {
        const asset = getManagedAssetForImage(image, documentPath);
        if (!asset) return;
        image.src = getManagedAssetDataUrl(asset);
        image.dataset.managedAssetPath = asset.path;
      });
    }

    function getManagedAssetForImage(image, documentPath = state.activePath) {
      if (!state.managedAssets?.size) return null;
      const path = resolveManagedAssetPath(image.dataset.managedAssetPath || image.getAttribute('src') || '', documentPath);
      return state.managedAssets.get(path) || null;
    }

    function getManagedAssetDataUrl(asset) {
      return asset.dataUrl || `data:${asset.mimeType || 'application/octet-stream'};base64,${asset.base64 || ''}`;
    }

    function normaliseAssetPath(value) {
      return String(value)
        .replace(/^blob:.*$/i, '')
        .replace(/^\.?\//, '')
        .replace(/\\/g, '/');
    }

    function resolveManagedAssetPath(value, documentPath = '') {
      const path = normaliseAssetPath(value);
      if (!path) return '';
      if (state.managedAssets?.has(path)) return path;

      const directory = normaliseAssetPath(documentPath).split('/').slice(0, -1).join('/');
      if (!directory) return '';

      const resolved = normaliseAssetPath(`${directory}/${path}`);
      return state.managedAssets?.has(resolved) ? resolved : path;
    }

    function capturePrintHtmlForTests(html) {
      if (typeof window.__MD_MMD_CAPTURE_PRINT_HTML__ !== 'function') return false;
      window.__MD_MMD_CAPTURE_PRINT_HTML__(html);
      return true;
    }

    function openPrintFrame(html) {
      const frame = document.createElement('iframe');
      frame.className = 'print-frame';
      frame.setAttribute('aria-hidden', 'true');
      frame.style.position = 'fixed';
      frame.style.right = '0';
      frame.style.bottom = '0';
      frame.style.width = '0';
      frame.style.height = '0';
      frame.style.border = '0';
      document.body.appendChild(frame);

      const frameDocument = frame.contentDocument;
      if (!frameDocument) {
        frame.remove();
        throw new Error('Could not create a printable document.');
      }

      frameDocument.open();
      frameDocument.write(html);
      frameDocument.close();

      window.setTimeout(() => {
        frame.contentWindow?.focus();
        frame.contentWindow?.print();
        window.setTimeout(() => frame.remove(), 1000);
      }, 100);
    }

    async function copyCurrentMermaidSource() {
      const renderResult = await renderPreview();
      if (!renderResult?.ok && !renderResult?.cancelled) {
        setStatus('Copy skipped because rendering failed.', 'danger');
        return;
      }
      if (renderResult?.cancelled) return;

      const frame = getCurrentDiagramFrame();
      if (!frame) {
        setStatus('No Mermaid diagram source found to copy.', 'warning');
        return;
      }

      await copyDiagramSource(frame);
      closeOpenMenus();
    }

    async function exportCurrentDiagramSvg() {
      const renderResult = await renderPreview();
      if (!renderResult?.ok && !renderResult?.cancelled) {
        setStatus('SVG export skipped because rendering failed.', 'danger');
        return;
      }
      if (renderResult?.cancelled) return;

      const frame = getCurrentDiagramFrame();
      if (!frame) {
        setStatus('No rendered Mermaid SVG found.', 'warning');
        return;
      }

      exportDiagramFrameSvg(frame, { closeMenus: true });
    }

    async function exportCurrentDiagramPng() {
      const renderResult = await renderPreview();
      if (!renderResult?.ok && !renderResult?.cancelled) {
        setStatus('PNG export skipped because rendering failed.', 'danger');
        return;
      }
      if (renderResult?.cancelled) return;

      const frame = getCurrentDiagramFrame();
      if (!frame) {
        setStatus('No rendered Mermaid SVG found.', 'warning');
        return;
      }

      await exportDiagramFramePng(frame, { closeMenus: true });
    }

    function getCurrentDiagramFrame() {
      const contentRoot = preview.querySelector('.docs-site-content') || preview;
      const svg = contentRoot.querySelector('.diagram-frame svg, .mermaid svg');
      return svg?.closest('.diagram-frame') || null;
    }

    async function exportDocsSite() {
      const records = state.files.filter((record) => isSupportedFile(record.name));
      if (!records.length) {
        setStatus('Open a folder or files before exporting a docs site.', 'warning');
        return;
      }

      try {
        closeOpenMenus();
        const defaults = {
          title: state.folderName || getExportTitle() || 'Docs site',
          description: `Static documentation bundle with ${records.length} page${records.length === 1 ? '' : 's'}.`,
          theme: 'system',
        };
        const options = await promptDocsSiteOptions(defaults);
        if (!options) {
          setStatus('Docs site export cancelled.', 'warning');
          return;
        }

        setStatus(`Building docs site from ${records.length} page${records.length === 1 ? '' : 's'}...`);
        const sourcePages = [];
        const usedIds = new Set();
        let diagramTotal = 0;
        let diagramErrors = 0;

        for (let index = 0; index < records.length; index += 1) {
          const page = await renderRecordForDocsSite(records[index], index, usedIds);
          sourcePages.push(page);
          diagramTotal += page.diagramTotal;
          diagramErrors += page.diagramErrors;
        }

        const { pages, homePage } = buildDocsSitePages(sourcePages, options);
        rewriteDocsSiteWikilinks(pages);
        const generatedAt = new Date().toISOString();
        const searchIndex = buildDocsSiteSearchIndex({ title: options.title, description: options.description, theme: options.theme, pages, homePage });
        const manifestObject = buildDocsSiteManifest({
          title: options.title,
          description: options.description,
          theme: options.theme,
          generatedAt,
          sourcePages,
          pages,
          homePage,
          diagramTotal,
          diagramErrors,
        });
        const manifest = JSON.stringify(manifestObject, null, 2);

        const zip = createZipBlob([
          { name: 'index.html', data: buildDocsSiteIndexHtml(options.title, options.description, options.theme) },
          { name: 'assets/docs-site.css', data: buildDocsSiteCss() },
          { name: 'assets/docs-site.js', data: buildDocsSiteScript() },
          { name: 'assets/search-index.json', data: JSON.stringify(searchIndex, null, 2) },
          { name: 'site-manifest.json', data: manifest },
          { name: 'README.md', data: buildDocsExportReadme(manifestObject) },
          ...buildManagedAssetZipEntries(),
        ], 'application/zip');

        downloadBlob(zip, `${sanitiseFileName(options.title) || 'docs-site'}-docs-site.zip`);
        setExportTrust(
          `Docs site ready: ${sourcePages.length} source page${sourcePages.length === 1 ? '' : 's'}, ${Math.max(diagramTotal - diagramErrors, 0)}/${diagramTotal} diagram${diagramTotal === 1 ? '' : 's'} rendered.`,
          diagramErrors ? 'warning' : 'ok'
        );
        setStatus(diagramErrors ? 'Docs site exported with diagram errors.' : 'Docs site exported.', diagramErrors ? 'warning' : 'ok');
      } catch (error) {
        setStatus('Docs site export failed.', 'danger');
        console.error(error);
      }
    }

    function promptDocsSiteOptions(defaults) {
      return new Promise((resolve) => {
        const dialog = document.createElement('dialog');
        dialog.className = 'template-dialog docs-site-export-dialog';
        dialog.setAttribute('aria-labelledby', 'docsSiteDialogTitle');
        dialog.setAttribute('aria-describedby', 'docsSiteDialogDescription');
        dialog.innerHTML = `
          <form class="template-dialog-card">
            <div class="template-dialog-top">
              <span class="template-dialog-kicker">Docs Site Builder 2.0</span>
              <button class="template-dialog-close" type="button" data-docs-dialog-cancel aria-label="Close docs site setup">X</button>
            </div>
            <h2 id="docsSiteDialogTitle">Export Docs Site</h2>
            <p id="docsSiteDialogDescription">Configure the static GitHub Pages bundle. The export stays browser-only and uses an organized ZIP.</p>
            <div class="template-dialog-fields">
              <div class="template-field">
                <label for="docs-site-title">Site title</label>
                <input id="docs-site-title" name="title" type="text" autocomplete="off" value="${escapeHtml(defaults.title)}" placeholder="Docs site">
                <small>Used for the browser title, sidebar brand, manifest, README, and ZIP name.</small>
              </div>
              <div class="template-field">
                <label for="docs-site-description">Short description</label>
                <textarea id="docs-site-description" name="description" rows="3" placeholder="What this documentation bundle covers">${escapeHtml(defaults.description)}</textarea>
                <small>Shown on the exported home page and site header.</small>
              </div>
              <div class="template-field">
                <label for="docs-site-theme">Initial theme</label>
                <select id="docs-site-theme" name="theme">
                  <option value="system" selected>System</option>
                  <option value="light">Light</option>
                  <option value="dark">Dark</option>
                </select>
                <small>The exported site includes its own Light/Dark/System switcher.</small>
              </div>
            </div>
            <div class="template-dialog-actions">
              <button type="button" data-docs-dialog-cancel>Cancel</button>
              <button class="primary" type="submit">Export site</button>
            </div>
          </form>`;

        const cleanup = (value) => {
          dialog.removeEventListener('cancel', handleCancel);
          dialog.remove();
          resolve(value);
        };

        const handleCancel = (event) => {
          event.preventDefault();
          cleanup(null);
        };

        dialog.addEventListener('cancel', handleCancel);
        dialog.addEventListener('click', (event) => {
          if (event.target === dialog || event.target.closest('[data-docs-dialog-cancel]')) {
            cleanup(null);
          }
        });
        dialog.querySelector('form').addEventListener('submit', (event) => {
          event.preventDefault();
          const formData = new FormData(event.currentTarget);
          cleanup({
            title: String(formData.get('title') || '').trim() || defaults.title || 'Docs site',
            description: String(formData.get('description') || '').trim(),
            theme: ['system', 'light', 'dark'].includes(formData.get('theme')) ? String(formData.get('theme')) : 'system',
          });
        });

        document.body.appendChild(dialog);
        if (typeof dialog.showModal === 'function') {
          dialog.showModal();
        } else {
          dialog.setAttribute('open', '');
        }

        requestAnimationFrame(() => {
          const title = dialog.querySelector('#docs-site-title');
          title?.focus();
          title?.select();
        });
      });
    }

    async function renderRecordForDocsSite(record, index, usedIds) {
      const source = await readRecordText(record);
      const selectedMode = resolveModeFor(source, record.name);
      const dirtyHtml = selectedMode === 'mermaid'
        ? buildMermaidOnlyHtml(source)
        : await buildMarkdownHtml(source);
      const container = document.createElement('article');
      container.innerHTML = sanitizeRenderedHtml(dirtyHtml);
      prepareTableBlocksIn(container);

      const diagrams = [...container.querySelectorAll('.mermaid')];
      const renderId = ++state.renderId;
      const diagramResult = await renderMermaidDiagrams(diagrams, renderId);
      prepareDiagramFramesIn(container, false, sanitiseFileName(record.name.replace(/\.[^.]+$/, '')) || `page-${index + 1}`);
      container.querySelectorAll('.diagram-error-actions').forEach((actions) => actions.remove());
      rewriteManagedAssetImageSources(container, record.path);

      const title = extractDocumentTitle(container, record);
      const id = makeUniqueDocId(record.path, index, usedIds);
      const headings = decorateDocsSiteHeadings(container);
      const searchSections = extractDocsSiteSearchSections(container, headings, title, record.path);

      return {
        id,
        title,
        path: record.path,
        sourceName: record.name,
        html: container.innerHTML,
        headings,
        searchSections,
        diagramTotal: diagrams.length,
        diagramErrors: diagramResult.errors,
      };
    }

    function rewriteManagedAssetImageSources(root, documentPath) {
      if (!state.managedAssets?.size) return;
      root.querySelectorAll('img').forEach((image) => {
        const asset = getManagedAssetForImage(image, documentPath);
        if (!asset) return;
        image.src = asset.path;
        image.dataset.managedAssetPath = asset.path;
      });
    }

    async function readRecordText(record) {
      if (state.fileCache.has(record.path)) {
        return state.fileCache.get(record.path) ?? '';
      }

      const file = record.file ?? await record.handle?.getFile();
      if (!file) return '';
      record.file = file;
      const text = await file.text();
      state.fileCache.set(record.path, text);
      return text;
    }

    function buildManagedAssetZipEntries() {
      if (!state.managedAssets?.size) return [];
      return [...state.managedAssets.values()].map((asset) => ({
        name: asset.path,
        data: base64ToUint8Array(asset.base64 || ''),
      }));
    }

    function buildMarkdownBundleManifest(documents, assetEntries) {
      const title = state.folderName || getExportTitle() || 'Markdown bundle';
      const assets = [...(state.managedAssets?.values() || [])].map((asset) => ({
        path: asset.path,
        name: asset.name,
        mimeType: asset.mimeType,
        size: asset.size ?? base64ToUint8Array(asset.base64 || '').byteLength,
      }));

      return {
        formatVersion: 'markdown-bundle-1.0',
        generator: APP_NAME,
        title,
        exportedAt: new Date().toISOString(),
        documentCount: documents.length,
        assetCount: assetEntries.length,
        documents,
        assets,
      };
    }

    function normaliseBundlePath(value) {
      const path = String(value || '')
        .replace(/\\/g, '/')
        .replace(/^\/+/, '')
        .replace(/^\.\/+/, '');
      const parts = path.split('/').filter(Boolean).filter((part) => part !== '.' && part !== '..');
      return parts.join('/') || 'document.md';
    }

    function makeUniqueDocId(path, index, usedIds) {
      const base = slugify(path.replace(/\.[^.]+$/, '').replace(/\//g, ' ')) || `page-${index + 1}`;
      let id = base;
      let suffix = 2;
      while (usedIds.has(id)) {
        id = `${base}-${suffix}`;
        suffix += 1;
      }
      usedIds.add(id);
      return id;
    }

    function extractDocumentTitle(container, record) {
      const heading = container.querySelector('h1, h2, h3');
      return heading?.textContent?.trim() || getDocTitleFromPath(record.path);
    }

    function decorateDocsSiteHeadings(container) {
      const used = new Set();
      return [...container.querySelectorAll('h1, h2, h3, h4')].map((heading, index) => {
        const text = heading.textContent.trim() || `Section ${index + 1}`;
        const base = slugify(heading.id || text) || `section-${index + 1}`;
        let id = base;
        let suffix = 2;
        while (used.has(id)) {
          id = `${base}-${suffix}`;
          suffix += 1;
        }
        used.add(id);
        heading.id = id;
        heading.tabIndex = -1;
        return {
          id,
          text,
          level: Number(heading.tagName.slice(1)),
        };
      });
    }

    function extractDocsSiteSearchSections(container, headings, title, path) {
      const clone = cloneDocsSiteContentForText(container);
      const sections = [];
      let current = {
        headingId: '',
        headingText: title,
        level: 0,
        text: '',
      };

      const headingById = new Map(headings.map((heading) => [heading.id, heading]));
      [...clone.children].forEach((child) => {
        const tagName = child.tagName?.toLowerCase() || '';
        if (/^h[1-4]$/.test(tagName)) {
          if (current.text.trim()) sections.push(current);
          const original = headingById.get(child.id);
          current = {
            headingId: child.id,
            headingText: original?.text || child.textContent.trim() || title,
            level: original?.level || Number(tagName.slice(1)),
            text: '',
          };
        }
        current.text += ` ${normaliseDocsSiteText(child.textContent)}`;
      });

      if (current.text.trim()) sections.push(current);
      if (!sections.length) {
        sections.push({
          headingId: '',
          headingText: title,
          level: 0,
          text: normaliseDocsSiteText(clone.textContent),
        });
      }

      return sections.map((section) => ({
        ...section,
        path,
        text: normaliseDocsSiteText(section.text),
      })).filter((section) => section.text);
    }

    function cloneDocsSiteContentForText(container) {
      const clone = container.cloneNode(true);
      clone.querySelectorAll('.code-block-header, .code-copy-button, .table-block-header, .table-copy-button, .diagram-toolbar, .diagram-error-actions, script, style').forEach((element) => {
        element.remove();
      });
      stripAppWikilinkActions(clone);
      return clone;
    }

    function rewriteDocsSiteWikilinks(pages) {
      const resolvable = pages
        .filter((page) => !page.generatedHome)
        .map((page) => ({ ...page, name: page.sourceName || page.path.split('/').pop() || page.path }));
      pages.forEach((page) => {
        const template = document.createElement('template');
        template.innerHTML = page.html;
        template.content.querySelectorAll('[data-wikilink-target]').forEach((link) => {
          const target = resolveWikilinkTarget(link.getAttribute('data-wikilink-target') || '', resolvable, page.path);
          if (target?.id) {
            link.setAttribute('href', `#${target.id}`);
            link.dataset.pageId = target.id;
            link.classList.remove('wikilink-unresolved');
          } else {
            link.removeAttribute('href');
            link.classList.add('wikilink-unresolved');
          }
          link.removeAttribute('data-wikilink-target');
        });
        page.html = template.innerHTML;
      });
    }

    function normaliseDocsSiteText(value) {
      return String(value || '').replace(/\s+/g, ' ').trim();
    }

    function buildDocsSitePages(sourcePages, options) {
      const homeCandidate = sourcePages.find((page) => /(^|\/)(readme|index)\.(md|markdown)$/i.test(page.path));
      if (homeCandidate) {
        return {
          pages: sourcePages,
          homePage: {
            id: homeCandidate.id,
            title: homeCandidate.title,
            path: homeCandidate.path,
            generated: false,
          },
        };
      }

      const sourcePageCards = sourcePages.map((page) => `<a class="home-card" href="#${escapeHtml(page.id)}" data-page-id="${escapeHtml(page.id)}">
        <strong>${escapeHtml(page.title)}</strong>
        <span>${escapeHtml(page.path)}</span>
      </a>`).join('');
      const homeId = makeUniqueGeneratedPageId('home', new Set(sourcePages.map((page) => page.id)));
      const generatedHome = {
        id: homeId,
        title: options.title,
        path: 'Home',
        sourceName: 'Home',
        html: `<section class="docs-home">
          <p class="docs-home-kicker">Documentation Bundle</p>
          <h1 id="overview" tabindex="-1">${escapeHtml(options.title)}</h1>
          <p>${escapeHtml(options.description || 'Browse the exported Markdown and Mermaid documentation.')}</p>
          <div class="docs-home-stats">
            <span><strong>${sourcePages.length}</strong> page${sourcePages.length === 1 ? '' : 's'}</span>
            <span><strong>${sourcePages.reduce((sum, page) => sum + page.diagramTotal, 0)}</strong> diagram${sourcePages.reduce((sum, page) => sum + page.diagramTotal, 0) === 1 ? '' : 's'}</span>
          </div>
          <div class="home-card-grid">${sourcePageCards}</div>
        </section>`,
        headings: [{ id: 'overview', text: options.title, level: 1 }],
        searchSections: [{
          headingId: 'overview',
          headingText: options.title,
          level: 1,
          path: 'Home',
          text: normaliseDocsSiteText(`${options.title} ${options.description} ${sourcePages.map((page) => `${page.title} ${page.path}`).join(' ')}`),
        }],
        diagramTotal: 0,
        diagramErrors: 0,
        generatedHome: true,
      };

      return {
        pages: [generatedHome, ...sourcePages],
        homePage: {
          id: generatedHome.id,
          title: generatedHome.title,
          path: generatedHome.path,
          generated: true,
        },
      };
    }

    function makeUniqueGeneratedPageId(base, usedIds) {
      let id = base;
      let suffix = 2;
      while (usedIds.has(id)) {
        id = `${base}-${suffix}`;
        suffix += 1;
      }
      return id;
    }

    function buildDocsSiteSearchIndex({ title, description, theme, pages, homePage }) {
      return {
        formatVersion: 'docs-site-2.0',
        title,
        description,
        defaultTheme: theme,
        homePageId: homePage.id,
        pages: pages.map((page) => ({
          id: page.id,
          title: page.title,
          path: page.path,
          html: page.html,
          headings: page.headings,
          diagramTotal: page.diagramTotal,
          diagramErrors: page.diagramErrors,
          generatedHome: Boolean(page.generatedHome),
        })),
        entries: pages.flatMap((page) => page.searchSections.map((section, index) => ({
          id: `${page.id}--${section.headingId || `page-${index + 1}`}`,
          pageId: page.id,
          title: page.title,
          path: page.path,
          headingId: section.headingId,
          headingText: section.headingText,
          level: section.level,
          text: section.text,
        }))),
      };
    }

    function buildDocsSiteManifest({ title, description, theme, generatedAt, sourcePages, pages, homePage, diagramTotal, diagramErrors }) {
      return {
        formatVersion: 'docs-site-2.0',
        title,
        description,
        defaultTheme: theme,
        generatedAt,
        pageCount: pages.length,
        sourcePageCount: sourcePages.length,
        diagramTotal,
        diagramErrors,
        homePage,
        assets: [
          'assets/docs-site.css',
          'assets/docs-site.js',
          'assets/search-index.json',
        ],
        pages: pages.map(({ id, title: pageTitle, path, headings, diagramTotal: pageDiagrams, diagramErrors: pageErrors, generatedHome }) => ({
          id,
          title: pageTitle,
          path,
          headings: headings.map(({ id: headingId, text, level }) => ({ id: headingId, text, level })),
          diagrams: pageDiagrams,
          diagramErrors: pageErrors,
          generatedHome: Boolean(generatedHome),
        })),
      };
    }

    function buildDocsSiteIndexHtml(title, description, theme) {
      return `<!doctype html>
<html lang="en-GB" data-theme-preference="${escapeHtml(theme)}">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  ${buildCspMeta(buildDocsSiteCsp())}
  <title>${escapeHtml(title)}</title>
  <meta name="description" content="${escapeHtml(description)}">
  <link rel="stylesheet" href="./assets/docs-site.css">
</head>
<body>
  <div class="layout" id="layout">
    <aside>
      <div class="brand">
        <strong id="siteTitle"></strong>
        <small id="siteDescription"></small>
        <button id="themeToggle" type="button" aria-live="polite">System</button>
      </div>
      <div class="search">
        <label for="searchInput">Search docs</label>
        <input id="searchInput" type="search" placeholder="Search pages, headings, and text" aria-label="Search docs">
      </div>
      <nav id="nav" aria-label="Docs navigation"></nav>
      <section class="search-results" id="searchResults" aria-label="Search results" hidden></section>
    </aside>
    <main>
      <div class="doc-shell">
        <div class="doc-meta" id="docMeta"></div>
        <article id="content"></article>
      </div>
    </main>
  </div>
  <script src="./assets/docs-site.js"></scr${'ipt'}>
</body>
</html>`;
    }

    function buildDocsSiteCss() {
      return `:root {
  color-scheme: light;
  --bg: #f4f7fb;
  --surface: #ffffff;
  --surface-soft: #eef4fb;
  --border: #d5dce8;
  --text: #0f172a;
  --muted: #64748b;
  --accent: #0369a1;
  --accent-soft: #dbeafe;
  --code: #f1f5f9;
  --danger: #be123c;
  --shadow: 0 18px 48px rgba(15, 23, 42, .08);
}

:root[data-theme="dark"] {
  color-scheme: dark;
  --bg: #07111f;
  --surface: #0e1a2b;
  --surface-soft: #14243a;
  --border: #24364f;
  --text: #e5eefb;
  --muted: #9aa8bc;
  --accent: #38bdf8;
  --accent-soft: #12324a;
  --code: #08111f;
  --danger: #fb7185;
  --shadow: 0 18px 48px rgba(0, 0, 0, .28);
}

* { box-sizing: border-box; }
html, body { min-height: 100%; margin: 0; }
body { font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; color: var(--text); background: var(--bg); line-height: 1.62; }
.layout { min-height: 100vh; display: grid; grid-template-columns: minmax(16rem, 22rem) minmax(0, 1fr); }
aside { position: sticky; top: 0; height: 100vh; display: grid; grid-template-rows: auto auto minmax(0, 1fr) auto; border-right: 1px solid var(--border); background: var(--surface); }
.brand { padding: 1rem; border-bottom: 1px solid var(--border); }
.brand strong { display: block; font-size: 1rem; }
.brand small { display: block; margin-top: .18rem; color: var(--muted); }
button, input { font: inherit; }
#themeToggle { margin-top: .8rem; border: 1px solid var(--border); border-radius: 999px; background: var(--surface-soft); color: var(--text); padding: .42rem .75rem; cursor: pointer; }
.search { display: grid; gap: .35rem; padding: .8rem 1rem; border-bottom: 1px solid var(--border); }
.search label { color: var(--muted); font-size: .78rem; font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }
input { width: 100%; border: 1px solid var(--border); border-radius: 999px; padding: .62rem .78rem; color: var(--text); background: var(--bg); }
nav, .search-results { overflow: auto; padding: .7rem; }
.nav-page, .nav-heading, .search-result { display: block; width: 100%; border: 0; text-align: left; text-decoration: none; cursor: pointer; }
.nav-page { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; padding: .56rem .65rem; border-radius: .65rem; color: var(--muted); background: transparent; }
.nav-page:hover, .nav-page:focus-visible, .nav-heading:hover, .nav-heading:focus-visible, .search-result:hover, .search-result:focus-visible { color: var(--text); background: var(--surface-soft); outline: none; }
.nav-page.active { color: var(--text); background: var(--accent-soft); }
.nav-heading { margin-top: .12rem; padding: .32rem .6rem; border-radius: .55rem; color: var(--muted); background: transparent; font-size: .86rem; }
.nav-heading[data-level="2"] { padding-left: 1.05rem; }
.nav-heading[data-level="3"] { padding-left: 1.55rem; }
.nav-heading[data-level="4"] { padding-left: 2.05rem; }
.nav-heading.active { color: var(--accent); background: var(--surface-soft); }
.search-results[hidden] { display: none; }
.search-result { margin-bottom: .45rem; padding: .58rem .65rem; border-radius: .7rem; color: var(--text); background: transparent; }
.search-result strong, .search-result span { display: block; }
.search-result small, .search-result span { color: var(--muted); }
.search-result mark { border-radius: .25rem; background: #fde68a; color: #111827; padding: 0 .12rem; }
main { min-width: 0; padding: clamp(1rem, 3vw, 2.5rem); }
.doc-shell { max-width: 76rem; margin: 0 auto; }
.doc-meta { margin-bottom: 1rem; color: var(--muted); font-size: .9rem; }
article { padding: clamp(1rem, 3vw, 2.5rem); border: 1px solid var(--border); border-radius: 1rem; background: var(--surface); box-shadow: var(--shadow); }
article :first-child { margin-top: 0; }
article :last-child { margin-bottom: 0; }
h1, h2, h3, h4 { line-height: 1.2; letter-spacing: 0; scroll-margin-top: 1rem; }
h1 { font-size: clamp(2rem, 5vw, 3.6rem); }
h2 { margin-top: 2rem; }
a { color: var(--accent); }
article img { display: block; max-width: 100%; height: auto; margin: 1rem 0; border-radius: .7rem; }
pre:not(.mermaid) { overflow: auto; padding: 1rem; border: 1px solid var(--border); border-radius: .8rem; background: var(--code); }
code { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", monospace; font-size: .92em; }
:not(pre) > code { padding: .12rem .32rem; border: 1px solid var(--border); border-radius: .4rem; background: var(--code); }
${buildCodeBlockSupportCss()}
${buildTableActionSupportCss()}
${buildDiagramActionSupportCss()}
blockquote { margin-inline: 0; padding-left: 1rem; border-left: .25rem solid var(--accent); color: var(--muted); }
table { width: 100%; border-collapse: collapse; margin: 1rem 0; }
th, td { border: 1px solid var(--border); padding: .58rem .72rem; text-align: left; }
th { background: var(--surface-soft); }
.diagram-frame { overflow: auto; margin: 1rem 0; padding: 1rem; border: 1px solid var(--border); border-radius: .9rem; background: var(--surface); }
.mermaid { display: block; width: max-content; max-width: none; margin: 0; }
.mermaid svg { display: block; max-width: none !important; height: auto; }
.mermaid-error { color: var(--danger); }
.diagram-error { white-space: pre-wrap; margin: 0; padding: 1rem; border: 1px solid #fda4af; border-radius: .8rem; background: #fff1f2; color: var(--danger); }
.empty { color: var(--muted); padding: 1rem; }
.docs-home-kicker { color: var(--accent); font-size: .8rem; font-weight: 800; letter-spacing: .06em; text-transform: uppercase; }
.docs-home-stats { display: flex; flex-wrap: wrap; gap: .75rem; margin: 1.25rem 0; }
.docs-home-stats span { display: inline-flex; gap: .35rem; align-items: baseline; border: 1px solid var(--border); border-radius: 999px; padding: .45rem .7rem; color: var(--muted); background: var(--surface-soft); }
.home-card-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr)); gap: .7rem; margin-top: 1.25rem; }
.home-card { display: grid; gap: .25rem; min-width: 0; border: 1px solid var(--border); border-radius: .75rem; padding: .8rem; color: var(--text); text-decoration: none; background: var(--surface-soft); }
.home-card:hover, .home-card:focus-visible { border-color: var(--accent); outline: none; }
.home-card span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--muted); }
@media (max-width: 900px) {
  .layout { grid-template-columns: 1fr; }
  aside { position: relative; height: auto; max-height: none; border-right: 0; border-bottom: 1px solid var(--border); }
  nav, .search-results { max-height: 18rem; }
  main { padding: 1rem; }
  article { padding: 1rem; }
}`;
    }

    function buildDocsSiteScript() {
      return `(function () {
  const preferredTheme = document.documentElement.dataset.themePreference || 'system';
  const storageKey = 'md-mmd-renderer.docs-site.theme';
  const siteTitle = document.getElementById('siteTitle');
  const siteDescription = document.getElementById('siteDescription');
  const themeToggle = document.getElementById('themeToggle');
  const nav = document.getElementById('nav');
  const searchInput = document.getElementById('searchInput');
  const searchResults = document.getElementById('searchResults');
  const content = document.getElementById('content');
  const docMeta = document.getElementById('docMeta');
  let data = null;
  let pages = [];
  let byId = new Map();
  let pathMap = new Map();
  let currentId = '';
  let currentHeadingId = '';
  let themePreference = localStorage.getItem(storageKey) || preferredTheme || 'system';

  init();

  async function init() {
    try {
      const response = await fetch('./assets/search-index.json', { cache: 'no-cache' });
      if (!response.ok) throw new Error('Could not load search index.');
      data = await response.json();
      pages = data.pages || [];
      byId = new Map(pages.map((page) => [page.id, page]));
      pathMap = buildPathMap(pages);
      siteTitle.textContent = data.title || 'Docs site';
      siteDescription.textContent = data.description || pages.length + ' page' + (pages.length === 1 ? '' : 's');
      applyTheme(themePreference);
      renderNav();
      installEvents();
      const initial = parseHash(location.hash);
      showPage(initial.pageId || data.homePageId || pages[0]?.id, initial.headingId, false);
    } catch (error) {
      content.innerHTML = '<p class="empty">Could not load this docs site. Serve the exported folder from GitHub Pages or another static web server.</p>';
      console.error(error);
    }
  }

  function installEvents() {
    themeToggle.addEventListener('click', () => {
      themePreference = themePreference === 'system' ? 'light' : themePreference === 'light' ? 'dark' : 'system';
      localStorage.setItem(storageKey, themePreference);
      applyTheme(themePreference);
    });

    nav.addEventListener('click', (event) => {
      const target = event.target.closest('[data-page-id]');
      if (!target) return;
      event.preventDefault();
      showPage(target.dataset.pageId, target.dataset.headingId || '');
    });

    searchResults.addEventListener('click', (event) => {
      const target = event.target.closest('[data-page-id]');
      if (!target) return;
      event.preventDefault();
      showPage(target.dataset.pageId, target.dataset.headingId || '');
    });

    content.addEventListener('click', (event) => {
      const link = event.target.closest('a[href]');
      if (!link) return;
      const resolved = resolveInternalHref(link.getAttribute('href') || '');
      if (!resolved.pageId && !resolved.headingId) return;
      event.preventDefault();
      showPage(resolved.pageId || currentId, resolved.headingId || '');
    });

    searchInput.addEventListener('input', () => {
      const query = searchInput.value.trim();
      if (query) {
        renderSearchResults(query);
      } else {
        searchResults.hidden = true;
        nav.hidden = false;
        renderNav();
      }
    });

    window.addEventListener('hashchange', () => {
      const next = parseHash(location.hash);
      showPage(next.pageId || data.homePageId || pages[0]?.id, next.headingId, false);
    });

    window.matchMedia?.('(prefers-color-scheme: dark)').addEventListener?.('change', () => {
      if (themePreference === 'system') applyTheme(themePreference);
    });
  }

  function applyTheme(preference) {
    const systemDark = window.matchMedia?.('(prefers-color-scheme: dark)').matches;
    const resolved = preference === 'system' ? (systemDark ? 'dark' : 'light') : preference;
    document.documentElement.dataset.theme = resolved;
    themeToggle.textContent = preference === 'system' ? 'System' : preference === 'light' ? 'Light' : 'Dark';
    themeToggle.setAttribute('aria-label', 'Theme: ' + themeToggle.textContent + '. Switch theme.');
  }

  function buildPathMap(pageList) {
    const map = new Map();
    pageList.forEach((page) => {
      const normal = normalisePath(page.path);
      map.set(normal, page.id);
      map.set(normal.split('/').pop(), page.id);
    });
    return map;
  }

  function renderNav() {
    nav.innerHTML = pages.map((page) => {
      const pageActive = page.id === currentId ? ' active' : '';
      const headings = page.id === currentId ? renderHeadingNav(page) : '';
      return '<div class="nav-group"><a class="nav-page' + pageActive + '" href="#' + page.id + '" data-page-id="' + page.id + '" title="' + escapeHtml(page.path) + '">' + escapeHtml(page.title) + '</a>' + headings + '</div>';
    }).join('') || '<div class="empty">No pages exported.</div>';
  }

  function renderHeadingNav(page) {
    return (page.headings || []).map((heading) => {
      const active = heading.id === currentHeadingId ? ' active' : '';
      return '<a class="nav-heading' + active + '" href="#' + page.id + '/' + heading.id + '" data-page-id="' + page.id + '" data-heading-id="' + heading.id + '" data-level="' + heading.level + '">' + escapeHtml(heading.text) + '</a>';
    }).join('');
  }

  function renderSearchResults(query) {
    const lower = query.toLowerCase();
    const terms = lower.split(/\\s+/).filter(Boolean);
    const results = (data.entries || [])
      .map((entry) => ({ entry, score: scoreEntry(entry, terms) }))
      .filter((result) => result.score > 0)
      .sort((a, b) => b.score - a.score)
      .slice(0, 20);

    nav.hidden = true;
    searchResults.hidden = false;
    searchResults.innerHTML = results.length
      ? results.map(({ entry }) => renderSearchResult(entry, query)).join('')
      : '<div class="empty">No results.</div>';
  }

  function scoreEntry(entry, terms) {
    const title = (entry.title + ' ' + entry.headingText).toLowerCase();
    const path = entry.path.toLowerCase();
    const text = entry.text.toLowerCase();
    return terms.reduce((score, term) => {
      if (title.includes(term)) score += 8;
      if (path.includes(term)) score += 4;
      if (text.includes(term)) score += 1;
      return score;
    }, 0);
  }

  function renderSearchResult(entry, query) {
    const label = entry.headingId ? entry.headingText : entry.title;
    return '<button class="search-result" type="button" data-page-id="' + entry.pageId + '" data-heading-id="' + (entry.headingId || '') + '">' +
      '<strong>' + escapeHtml(label) + '</strong>' +
      '<small>' + escapeHtml(entry.path) + '</small>' +
      '<span>' + buildSnippet(entry.text, query) + '</span>' +
      '</button>';
  }

  function buildSnippet(text, query) {
    const lower = text.toLowerCase();
    const term = query.trim().toLowerCase().split(/\\s+/).find(Boolean) || '';
    const index = term ? lower.indexOf(term) : -1;
    const start = Math.max(index - 54, 0);
    const raw = index >= 0 ? text.slice(start, start + 150) : text.slice(0, 150);
    const escaped = escapeHtml((start > 0 ? '...' : '') + raw + (start + raw.length < text.length ? '...' : ''));
    return term ? escaped.replace(new RegExp(escapeRegExp(term), 'ig'), '<mark>$&</mark>') : escaped;
  }

  function showPage(id, headingId = '', updateHash = true) {
    const page = byId.get(id) || byId.get(data.homePageId) || pages[0];
    if (!page) return;
    currentId = page.id;
    currentHeadingId = headingId;
    document.title = page.title + ' - ' + data.title;
    docMeta.textContent = page.path + (page.diagramErrors ? ' - ' + page.diagramErrors + ' diagram error' + (page.diagramErrors === 1 ? '' : 's') : '');
    content.innerHTML = page.html || '<p>No content.</p>';
    decorateContentLinks();
    renderNav();
    if (updateHash) history.replaceState(null, '', '#' + page.id + (headingId ? '/' + headingId : ''));
    requestAnimationFrame(() => {
      const target = headingId ? content.querySelector('#' + cssEscape(headingId)) : null;
      (target || content).scrollIntoView({ block: 'start' });
      target?.focus?.({ preventScroll: true });
    });
  }

  function decorateContentLinks() {
    content.querySelectorAll('a[href]').forEach((anchor) => {
      const resolved = resolveInternalHref(anchor.getAttribute('href') || '');
      if (resolved.pageId || resolved.headingId) {
        anchor.dataset.pageId = resolved.pageId || currentId;
        if (resolved.headingId) anchor.dataset.headingId = resolved.headingId;
        anchor.removeAttribute('target');
        anchor.removeAttribute('rel');
        return;
      }
      if (/^(https?:|mailto:)/i.test(anchor.getAttribute('href') || '')) {
        anchor.target = '_blank';
        anchor.rel = 'noopener noreferrer';
      }
    });
  }

  function resolveInternalHref(href) {
    if (!href || /^(https?:|mailto:)/i.test(href)) return {};
    const parts = href.split('#');
    const path = normalisePath(decodeURIComponent(parts[0] || ''));
    const hash = parts[1] ? decodeURIComponent(parts[1]) : '';
    if (!path && hash) return { pageId: currentId, headingId: hash };
    const pageId = pathMap.get(path) || pathMap.get(path.replace(/^\\.\\//, '')) || '';
    return pageId ? { pageId, headingId: hash } : {};
  }

  function parseHash(hash) {
    const value = decodeURIComponent(String(hash || '').replace(/^#/, ''));
    const parts = value.split('/');
    return { pageId: parts[0] || '', headingId: parts[1] || '' };
  }

  function normalisePath(value) {
    return String(value).replace(/\\\\/g, '/').replace(/^\\.\\//, '').toLowerCase();
  }

  function cssEscape(value) {
    return window.CSS?.escape ? window.CSS.escape(value) : String(value).replace(/[^a-zA-Z0-9_-]/g, '\\\\$&');
  }

  function escapeRegExp(value) {
    return String(value).replace(/[.*+?^${'${'}}()|[\\]\\\\]/g, '\\\\$&');
  }

  function escapeHtml(value) {
    return String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#039;');
  }

${indentScript(buildCodeBlockCopyScript())}

${indentScript(buildTableActionScript())}

${indentScript(buildDiagramActionScript())}
})();`;
    }

    function indentScript(script) {
      return script.split('\n').map((line) => `  ${line}`).join('\n');
    }

    function buildDocsExportReadme(manifest) {
      return `# ${manifest.title}

This folder was exported by ${APP_NAME} as a static docs site.

## Contents

- ${manifest.sourcePageCount} source page${manifest.sourcePageCount === 1 ? '' : 's'}
- ${manifest.pageCount} published page${manifest.pageCount === 1 ? '' : 's'} including the home page
- ${manifest.diagramTotal} Mermaid diagram${manifest.diagramTotal === 1 ? '' : 's'}
- ${manifest.diagramErrors} diagram error${manifest.diagramErrors === 1 ? '' : 's'}
- Light, Dark, and System theme support
- Static full-text search from \`assets/search-index.json\`

## Files

- \`index.html\` is the site entry point.
- \`assets/docs-site.css\` contains the exported site theme and layout.
- \`assets/docs-site.js\` contains navigation, theme switching, search, code/table copy, and diagram actions.
- \`assets/search-index.json\` contains rendered pages and the local search index.
- \`site-manifest.json\` contains export metadata and page stats.

## Deploy

Upload the contents of this ZIP to GitHub Pages or any static web host. Keep the folder structure intact so \`index.html\` can load files from \`assets/\`.
`;
    }

    function buildExportConfidence(format, renderResult, images = []) {
      const total = renderResult.diagramTotal ?? state.diagramTotal;
      const rendered = Math.max(total - (renderResult.diagramErrors ?? 0), 0);
      const diagramText = total
        ? `${rendered}/${total} diagram${total === 1 ? '' : 's'} rendered`
        : 'No diagrams in this document';
      const diagramImages = images.filter((image) => image.kind === 'diagram');
      const managedImages = images.filter((image) => image.kind === 'managed');
      const wordImageDetails = [];
      if (diagramImages.length) {
        wordImageDetails.push(`${diagramImages.filter((image) => image.mimeType === 'image/png').length}/${diagramImages.length} diagram${diagramImages.length === 1 ? '' : 's'} rasterized for Word`);
      }
      if (managedImages.length) {
        wordImageDetails.push(`${managedImages.length} image asset${managedImages.length === 1 ? '' : 's'} embedded`);
      }
      const imageText = wordImageDetails.length
        ? wordImageDetails.join('; ')
        : format === 'Word'
          ? 'No diagram images needed for Word'
          : 'Diagrams remain in the rendered HTML';

      return `${format} ready: ${diagramText}. ${imageText}.`;
    }

    function setExportTrust(message, tone = '') {
      exportTrust.className = `export-trust${tone === 'warning' ? ' warning' : tone === 'danger' ? ' danger' : ''}`;
      exportTrust.textContent = message;
      exportTrust.hidden = !message;
    }

    async function buildWordExportContent() {
      const clone = preview.cloneNode(true);
      unwrapTemporaryPreviewMarks(clone);
      clone.querySelectorAll('.diagram-toolbar, .table-block-header').forEach((toolbar) => {
        toolbar.remove();
      });
      clone.querySelectorAll('.diagram-frame').forEach((frame) => {
        frame.style.overflow = 'visible';
      });

      const frames = [...clone.querySelectorAll('.diagram-frame')];
      const liveFrames = [...preview.querySelectorAll('.diagram-frame')];
      const images = [];
      prepareManagedImagesForWord(clone, images);

      for (let index = 0; index < frames.length; index += 1) {
        const frame = frames[index];
        const liveSvg = liveFrames[index]?.querySelector('svg');
        if (!liveSvg) continue;

        const imageInfo = await diagramSvgToWordImage(liveSvg, index + 1);
        images.push({
          kind: 'diagram',
          name: imageInfo.name,
          mimeType: imageInfo.mimeType,
          base64: imageInfo.base64,
          displayWidth: imageInfo.displayWidth,
          displayHeight: imageInfo.displayHeight,
        });

        const replacement = document.createElement('div');
        replacement.className = 'diagram-frame';
        const image = document.createElement('img');
        image.className = 'diagram-image';
        image.alt = 'Mermaid diagram';
        image.dataset.wordImageName = imageInfo.name;
        image.setAttribute('width', String(imageInfo.displayWidth));
        image.setAttribute('height', String(imageInfo.displayHeight));
        image.style.width = '100%';
        image.style.maxWidth = `${imageInfo.displayWidth}px`;
        image.style.height = 'auto';
        image.style.display = 'block';
        replacement.appendChild(image);
        frame.replaceWith(replacement);
      }

      clone.querySelectorAll('svg').forEach((svg) => {
        svg.removeAttribute('style');
      });

      return {
        root: clone,
        images,
      };
    }

    function prepareManagedImagesForWord(root, images) {
      if (!state.managedAssets?.size) return;

      root.querySelectorAll('img').forEach((image, index) => {
        const asset = getManagedAssetForImage(image);
        if (!asset?.base64) return;

        const extension = getImageExtensionForWord(asset);
        const baseName = sanitiseFileName((asset.name || `image-${index + 1}`).replace(/\.[^.]+$/, '')) || `image-${index + 1}`;
        const name = `asset-${index + 1}-${baseName}.${extension}`;
        const size = getManagedImageDisplaySize(image);

        images.push({
          kind: 'managed',
          name,
          mimeType: getImageMimeTypeForWord(asset, extension),
          base64: asset.base64,
          displayWidth: size.displayWidth,
          displayHeight: size.displayHeight,
        });

        image.dataset.wordImageName = name;
        image.classList.add('managed-image');
        image.alt = image.alt || asset.alt || 'Image';
        image.removeAttribute('srcset');
      });
    }

    function getManagedImageDisplaySize(image) {
      const naturalWidth = Number(image.naturalWidth) || 0;
      const naturalHeight = Number(image.naturalHeight) || 0;
      const attrWidth = parseFloat(image.getAttribute('width') || '') || 0;
      const attrHeight = parseFloat(image.getAttribute('height') || '') || 0;
      const rawWidth = naturalWidth || attrWidth || 640;
      const rawHeight = naturalHeight || attrHeight || Math.round(rawWidth * .5625);
      const maxWidth = 640;
      const scale = rawWidth > maxWidth ? maxWidth / rawWidth : 1;

      return {
        displayWidth: Math.max(1, Math.round(rawWidth * scale)),
        displayHeight: Math.max(1, Math.round(rawHeight * scale)),
      };
    }

    function getImageExtensionForWord(asset) {
      const name = asset.name || asset.path || '';
      const match = name.match(/\.([a-z0-9]+)$/i);
      if (match) {
        const extension = match[1].toLowerCase();
        return extension === 'jpeg' ? 'jpg' : extension;
      }
      if (/svg/i.test(asset.mimeType || '')) return 'svg';
      if (/webp/i.test(asset.mimeType || '')) return 'webp';
      if (/gif/i.test(asset.mimeType || '')) return 'gif';
      if (/jpe?g/i.test(asset.mimeType || '')) return 'jpg';
      return 'png';
    }

    function getImageMimeTypeForWord(asset, extension) {
      if (asset.mimeType) return asset.mimeType;
      if (extension === 'svg') return 'image/svg+xml';
      if (extension === 'webp') return 'image/webp';
      if (extension === 'gif') return 'image/gif';
      if (extension === 'jpg' || extension === 'jpeg') return 'image/jpeg';
      return 'image/png';
    }

    async function diagramSvgToWordImage(svg, index) {
      const size = getSvgBaseSize(svg);
      const printableWidth = 640;
      const displayWidth = Math.min(Math.round(size.width), printableWidth);
      const displayHeight = Math.max(1, Math.round((size.height / Math.max(size.width, 1)) * displayWidth));

      try {
        const dataUrl = await svgToPngDataUrl(svg, size.width, size.height);
        const base64 = dataUrl.split(',')[1] || '';

        return {
          name: `diagram-${index}.png`,
          mimeType: 'image/png',
          base64,
          displayWidth,
          displayHeight,
        };
      } catch {
        return {
          name: `diagram-${index}.svg`,
          mimeType: 'image/svg+xml',
          base64: textToBase64(serialiseSvgForExport(svg, size.width, size.height)),
          displayWidth,
          displayHeight,
        };
      }
    }

    function serialiseSvgForExport(svg, width, height) {
      const clone = svg.cloneNode(true);
      clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
      clone.setAttribute('xmlns:xlink', 'http://www.w3.org/1999/xlink');
      clone.setAttribute('width', String(width));
      clone.setAttribute('height', String(height));
      clone.style.transform = '';
      clone.style.maxWidth = 'none';
      replaceForeignObjectLabels(clone);
      return new XMLSerializer().serializeToString(clone);
    }

    function replaceForeignObjectLabels(svg) {
      svg.querySelectorAll('foreignObject').forEach((foreignObject) => {
        const text = foreignObject.textContent.replace(/\s+/g, ' ').trim();
        const width = parseFloat(foreignObject.getAttribute('width')) || 1;
        const height = parseFloat(foreignObject.getAttribute('height')) || 1;
        const svgText = document.createElementNS('http://www.w3.org/2000/svg', 'text');

        svgText.setAttribute('x', String(width / 2));
        svgText.setAttribute('y', String(height / 2));
        svgText.setAttribute('text-anchor', 'middle');
        svgText.setAttribute('dominant-baseline', 'middle');
        svgText.setAttribute('font-size', '16');
        svgText.setAttribute('fill', '#333333');
        svgText.textContent = text;
        foreignObject.replaceWith(svgText);
      });
    }

    async function svgToPngDataUrl(svg, width, height) {
      const serialisedSvg = serialiseSvgForExport(svg, width, height);
      const svgBlob = new Blob([serialisedSvg], { type: 'image/svg+xml;charset=utf-8' });
      const url = URL.createObjectURL(svgBlob);

      try {
        const image = new Image();
        image.decoding = 'async';
        await new Promise((resolve, reject) => {
          image.onload = resolve;
          image.onerror = reject;
          image.src = url;
        });

        const maxPixels = 16_000_000;
        const scale = Math.min(2, Math.max(.25, Math.sqrt(maxPixels / Math.max(width * height, 1))));
        const canvas = document.createElement('canvas');
        canvas.width = Math.max(1, Math.round(width * scale));
        canvas.height = Math.max(1, Math.round(height * scale));

        const context = canvas.getContext('2d');
        if (!context) {
          throw new Error('Canvas rendering is not available in this browser.');
        }

        context.fillStyle = '#ffffff';
        context.fillRect(0, 0, canvas.width, canvas.height);
        context.drawImage(image, 0, 0, canvas.width, canvas.height);

        return canvas.toDataURL('image/png');
      } finally {
        URL.revokeObjectURL(url);
      }
    }

    function dataUrlToBlob(dataUrl) {
      const [meta, base64] = dataUrl.split(',');
      const mime = meta.match(/^data:([^;]+)/)?.[1] || 'application/octet-stream';
      return new Blob([base64ToUint8Array(base64 || '')], { type: mime });
    }

    function buildExportHtml(content) {
      const nonce = createCspNonce();
      return `<!doctype html>
<html lang="en-GB">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  ${buildCspMeta(buildStandaloneExportCsp(nonce))}
  <title>${escapeHtml(getExportTitle())}</title>
  <style>
    body { margin: 0; padding: 2rem; font-family: ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; line-height: 1.62; color: #0f172a; background: #f8fafc; }
    article { max-width: 1120px; margin: 0 auto; }
    pre { overflow: auto; padding: 1rem; border: 1px solid #d5dce8; border-radius: .8rem; background: #f1f5f9; }
    code { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", monospace; }
    table { width: 100%; border-collapse: collapse; }
    th, td { border: 1px solid #d5dce8; padding: .58rem .72rem; text-align: left; }
    th { background: #e2e8f0; }
    img { max-width: 100%; height: auto; border-radius: .7rem; }
    blockquote { margin-inline: 0; padding-left: 1rem; border-left: .25rem solid #38bdf8; color: #475569; }
    ${buildCodeBlockSupportCss()}
    ${buildTableActionSupportCss()}
    ${buildDiagramActionSupportCss()}
    .diagram-frame { overflow: auto; margin: 1rem 0; padding: 1rem; border: 1px solid #d5dce8; border-radius: .9rem; background: #fff; }
    .mermaid { display: block; width: max-content; max-width: none; margin: 0; }
    .mermaid-error { width: 100%; color: #be123c; }
    .diagram-error { white-space: pre-wrap; margin: 0; padding: 1rem; border: 1px solid #fda4af; border-radius: .8rem; background: #fff1f2; color: #be123c; }
    svg { display: block; max-width: none !important; transform-origin: top left; }
  </style>
</head>
<body>
  <article>${content}</article>
  <script nonce="${nonce}">${buildCodeBlockCopyScript()}</scr${'ipt'}>
  <script nonce="${nonce}">${buildTableActionScript()}</scr${'ipt'}>
  <script nonce="${nonce}">${buildDiagramActionScript()}</scr${'ipt'}>
</body>
</html>`;
    }

    function buildPrintHtml(content) {
      return `<!doctype html>
<html lang="en-GB">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  ${buildCspMeta(buildPrintExportCsp())}
  <title>${escapeHtml(getExportTitle())} PDF</title>
  <style>
    @page { margin: 0.7in; }
    * { box-sizing: border-box; }
    body { margin: 0; font-family: ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; line-height: 1.58; color: #111827; background: #ffffff; }
    article { max-width: none; margin: 0; }
    h1, h2, h3, h4 { line-height: 1.2; break-after: avoid; }
    h1 { font-size: 30pt; }
    h2 { margin-top: 24pt; font-size: 21pt; }
    h3 { font-size: 16pt; }
    h4 { font-size: 13pt; }
    p, li, blockquote, table, pre, figure { break-inside: avoid; }
    a { color: #0369a1; }
    img { max-width: 100%; height: auto; }
    pre { overflow: visible; white-space: pre-wrap; word-break: break-word; padding: 10pt; border: 1px solid #d5dce8; border-radius: 6pt; background: #f8fafc; }
    code { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", monospace; font-size: 9.5pt; }
    table { width: 100%; border-collapse: collapse; }
    th, td { border: 1px solid #d5dce8; padding: 5pt 7pt; text-align: left; }
    th { background: #eef2f7; }
    blockquote { margin-inline: 0; padding-left: 12pt; border-left: 3pt solid #38bdf8; color: #475569; }
    .code-block { margin: 12pt 0; border: 1px solid #d5dce8; border-radius: 6pt; background: #f8fafc; }
    .code-block pre { margin: 0; border: 0; background: transparent; }
    .diagram-frame { overflow: visible; margin: 12pt 0; padding: 10pt; border: 1px solid #d5dce8; border-radius: 6pt; background: #ffffff; break-inside: avoid; }
    .mermaid { display: block; width: max-content; max-width: 100%; margin: 0; }
    .mermaid svg, svg { max-width: 100% !important; height: auto !important; transform: none !important; }
    .diagram-error { white-space: pre-wrap; margin: 0; padding: 10pt; border: 1px solid #fda4af; border-radius: 6pt; background: #fff1f2; color: #be123c; }
    .studio-banner { display: none; }
    ${buildPrintHighlightCss()}
    @media screen { body { padding: 2rem; } article { max-width: 920px; margin: 0 auto; } }
  </style>
</head>
<body>
  <article>${content}</article>
</body>
</html>`;
    }

    function buildPrintHighlightCss() {
      return `.hljs-keyword, .hljs-selector-tag, .hljs-built_in, .hljs-name, .hljs-tag { color: #1d4ed8; }
    .hljs-string, .hljs-title, .hljs-section, .hljs-attribute, .hljs-literal, .hljs-template-tag, .hljs-template-variable, .hljs-type, .hljs-addition { color: #15803d; }
    .hljs-comment, .hljs-quote, .hljs-deletion, .hljs-meta { color: #64748b; }
    .hljs-number, .hljs-regexp, .hljs-link, .hljs-selector-id, .hljs-selector-class { color: #b45309; }
    .hljs-variable, .hljs-symbol, .hljs-bullet, .hljs-subst { color: #a21caf; }
    .hljs-emphasis { font-style: italic; }
    .hljs-strong { font-weight: 800; }`;
    }

    function buildCodeBlockSupportCss() {
      return `.code-block { overflow: hidden; margin: 1rem 0; border: 1px solid #d5dce8; border-radius: .85rem; background: #f8fafc; }
    .code-block-header { display: flex; align-items: center; justify-content: space-between; gap: .75rem; min-width: 0; padding: .48rem .62rem; border-bottom: 1px solid #d5dce8; background: #eef4fb; }
    .code-block-language { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: #475569; font-size: .76rem; font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }
    .code-copy-button { flex: 0 0 auto; min-width: 4rem; border: 1px solid #cbd5e1; border-radius: .58rem; background: #fff; color: #0f172a; padding: .34rem .58rem; cursor: pointer; font: inherit; font-size: .76rem; line-height: 1; }
    .code-copy-button:hover, .code-copy-button:focus-visible { border-color: #0284c7; outline: none; }
    .code-copy-button:disabled { cursor: default; opacity: .72; }
    .code-block pre { overflow: auto; margin: 0; padding: 1rem; border: 0; border-radius: 0; background: transparent; }
    .code-block code { display: block; min-width: max-content; color: #0f172a; font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", monospace; font-size: .9rem; line-height: 1.58; white-space: pre; }
    .hljs-keyword, .hljs-selector-tag, .hljs-built_in, .hljs-name, .hljs-tag { color: #1d4ed8; }
    .hljs-string, .hljs-title, .hljs-section, .hljs-attribute, .hljs-literal, .hljs-template-tag, .hljs-template-variable, .hljs-type, .hljs-addition { color: #15803d; }
    .hljs-comment, .hljs-quote, .hljs-deletion, .hljs-meta { color: #64748b; }
    .hljs-number, .hljs-regexp, .hljs-link, .hljs-selector-id, .hljs-selector-class { color: #b45309; }
    .hljs-variable, .hljs-symbol, .hljs-bullet, .hljs-subst { color: #a21caf; }
    .hljs-emphasis { font-style: italic; }
    .hljs-strong { font-weight: 800; }`;
    }

    function buildCodeBlockCopyScript() {
      return `(function () {
      function fallbackCopy(text) {
        var textarea = document.createElement('textarea');
        textarea.value = text;
        textarea.setAttribute('readonly', '');
        textarea.style.position = 'fixed';
        textarea.style.left = '-9999px';
        document.body.appendChild(textarea);
        textarea.select();
        try { document.execCommand('copy'); } finally { textarea.remove(); }
      }

      async function writeText(text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
          await navigator.clipboard.writeText(text);
          return;
        }
        fallbackCopy(text);
      }

      document.addEventListener('click', async function (event) {
        var button = event.target.closest('[data-code-action="copy"]');
        if (!button) return;
        var code = button.closest('.code-block')?.querySelector('pre code');
        var text = code?.textContent || '';
        if (!text) return;
        var previous = button.textContent || 'Copy';
        try {
          await writeText(text);
          button.textContent = 'Copied';
          button.disabled = true;
          window.setTimeout(function () {
            button.textContent = previous;
            button.disabled = false;
          }, 1300);
        } catch {
          button.textContent = 'Failed';
          window.setTimeout(function () { button.textContent = previous; }, 1300);
        }
      });
    })();`;
    }

    function buildTableActionSupportCss() {
      return `.table-block { overflow: hidden; margin: 1rem 0; border: 1px solid #d5dce8; border-radius: .85rem; background: #fff; }
    .table-block-header { display: flex; align-items: center; justify-content: space-between; gap: .75rem; min-width: 0; padding: .48rem .62rem; border-bottom: 1px solid #d5dce8; background: #eef4fb; }
    .table-block-title { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: #475569; font-size: .76rem; font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }
    .table-copy-button { flex: 0 0 auto; min-width: 4rem; border: 1px solid #cbd5e1; border-radius: .58rem; background: #fff; color: #0f172a; padding: .34rem .58rem; cursor: pointer; font: inherit; font-size: .76rem; line-height: 1; }
    .table-copy-button:hover, .table-copy-button:focus-visible { border-color: #0284c7; outline: none; }
    .table-copy-button:disabled { cursor: default; opacity: .72; }
    .table-block table { width: 100%; margin: 0; border-collapse: collapse; }`;
    }

    function buildTableActionScript() {
      return `(function () {
      function fallbackCopy(text) {
        var textarea = document.createElement('textarea');
        textarea.value = text;
        textarea.setAttribute('readonly', '');
        textarea.style.position = 'fixed';
        textarea.style.left = '-9999px';
        document.body.appendChild(textarea);
        textarea.select();
        try { document.execCommand('copy'); } finally { textarea.remove(); }
      }

      async function writeText(text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
          await navigator.clipboard.writeText(text);
          return;
        }
        fallbackCopy(text);
      }

      function formatCell(value) {
        var text = String(value || '')
          .replace(/\\u00a0/g, ' ')
          .replace(/\\r\\n?/g, '\\n')
          .replace(/[ \\t]+\\n/g, '\\n')
          .replace(/\\n[ \\t]+/g, '\\n')
          .trim();
        return /["\\t\\n]/.test(text) ? '"' + text.replaceAll('"', '""') + '"' : text;
      }

      function tableToTsv(table) {
        if (!table) return '';
        return Array.from(table.querySelectorAll('tr')).map(function (row) {
          return Array.from(row.children)
            .filter(function (cell) { return /^(td|th)$/i.test(cell.tagName); })
            .map(function (cell) { return formatCell(cell.textContent || ''); })
            .join('\\t');
        }).filter(Boolean).join('\\n');
      }

      document.addEventListener('click', async function (event) {
        var button = event.target.closest('[data-table-action="copy"]');
        if (!button) return;
        var table = button.closest('.table-block')?.querySelector('table') || button.closest('table');
        var text = tableToTsv(table);
        if (!text) return;
        var previous = button.textContent || 'Copy';
        try {
          await writeText(text);
          button.textContent = 'Copied';
          button.disabled = true;
          window.setTimeout(function () {
            button.textContent = previous;
            button.disabled = false;
          }, 1300);
        } catch {
          button.textContent = 'Failed';
          window.setTimeout(function () { button.textContent = previous; }, 1300);
        }
      });
    })();`;
    }

    function buildDiagramActionSupportCss() {
      return `.diagram-toolbar { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: .55rem; min-width: 0; margin: -.25rem -.25rem .85rem; padding: .46rem .52rem; border: 1px solid #d5dce8; border-radius: .8rem; background: #eef4fb; }
    .diagram-toolbar-title { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: #475569; font-size: .76rem; font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }
    .diagram-action-group { display: flex; flex: 0 0 auto; align-items: center; gap: .35rem; }
    .diagram-action-button { min-width: 3.2rem; border: 1px solid #cbd5e1; border-radius: .58rem; background: #fff; color: #0f172a; padding: .34rem .55rem; cursor: pointer; font: inherit; font-size: .76rem; line-height: 1; }
    .diagram-action-button:hover, .diagram-action-button:focus-visible { border-color: #0284c7; outline: none; }
    .diagram-action-button:disabled { cursor: default; opacity: .72; }
    @media (max-width: 640px) { .diagram-toolbar { align-items: stretch; } .diagram-action-group { width: 100%; } .diagram-action-button { flex: 1 1 0; } }`;
    }

    function buildDiagramActionScript() {
      return `(function () {
      function fallbackCopy(text) {
        var textarea = document.createElement('textarea');
        textarea.value = text;
        textarea.setAttribute('readonly', '');
        textarea.style.position = 'fixed';
        textarea.style.left = '-9999px';
        document.body.appendChild(textarea);
        textarea.select();
        try { document.execCommand('copy'); } finally { textarea.remove(); }
      }

      async function writeText(text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
          await navigator.clipboard.writeText(text);
          return;
        }
        fallbackCopy(text);
      }

      function setTemporaryText(button, text) {
        var previous = button.textContent || '';
        button.textContent = text;
        button.disabled = true;
        window.setTimeout(function () {
          button.textContent = previous || button.getAttribute('data-fallback-label') || 'Copy';
          button.disabled = false;
        }, 1300);
      }

      function getSvgBaseSize(svg) {
        var viewBox = svg.getAttribute('viewBox');
        if (viewBox) {
          var values = viewBox.trim().split(/[\\s,]+/).map(Number);
          if (values.length === 4 && Number.isFinite(values[2]) && Number.isFinite(values[3])) {
            return { width: Math.max(values[2], 1), height: Math.max(values[3], 1) };
          }
        }
        var rect = svg.getBoundingClientRect();
        var width = parseFloat(svg.getAttribute('width')) || rect.width || 800;
        var height = parseFloat(svg.getAttribute('height')) || rect.height || 420;
        return { width: Math.max(width, 1), height: Math.max(height, 1) };
      }

      function replaceForeignObjectLabels(svg) {
        svg.querySelectorAll('foreignObject').forEach(function (foreignObject) {
          var text = foreignObject.textContent.replace(/\\s+/g, ' ').trim();
          var width = parseFloat(foreignObject.getAttribute('width')) || 1;
          var height = parseFloat(foreignObject.getAttribute('height')) || 1;
          var svgText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
          svgText.setAttribute('x', String(width / 2));
          svgText.setAttribute('y', String(height / 2));
          svgText.setAttribute('text-anchor', 'middle');
          svgText.setAttribute('dominant-baseline', 'middle');
          svgText.setAttribute('font-size', '16');
          svgText.setAttribute('fill', '#333333');
          svgText.textContent = text;
          foreignObject.replaceWith(svgText);
        });
      }

      function serialiseSvgForExport(svg, width, height) {
        var clone = svg.cloneNode(true);
        clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
        clone.setAttribute('xmlns:xlink', 'http://www.w3.org/1999/xlink');
        clone.setAttribute('width', String(width));
        clone.setAttribute('height', String(height));
        clone.style.transform = '';
        clone.style.maxWidth = 'none';
        replaceForeignObjectLabels(clone);
        return new XMLSerializer().serializeToString(clone);
      }

      function downloadBlob(blob, fileName) {
        var url = URL.createObjectURL(blob);
        var link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
      }

      function dataUrlToBlob(dataUrl) {
        var parts = String(dataUrl || '').split(',');
        var meta = parts[0] || '';
        var base64 = parts[1] || '';
        var mime = (meta.match(/^data:([^;]+)/) || [])[1] || 'application/octet-stream';
        var binary = atob(base64);
        var bytes = new Uint8Array(binary.length);
        for (var index = 0; index < binary.length; index += 1) {
          bytes[index] = binary.charCodeAt(index);
        }
        return new Blob([bytes], { type: mime });
      }

      function sanitiseFileName(value) {
        return String(value || 'rendered-document')
          .replace(/[<>:"/\\\\|?*\\u0000-\\u001F]/g, '-')
          .replace(/\\s+/g, ' ')
          .trim()
          .replace(/[. ]+$/g, '') || 'rendered-document';
      }

      function getDiagramExportName(frame, extension) {
        var index = Number(frame.dataset.diagramIndex || '1');
        var total = Number(frame.dataset.diagramTotal || '1');
        var suffix = total > 1 && Number.isFinite(index) ? '-diagram-' + index : '-diagram';
        var stem = sanitiseFileName(frame.dataset.diagramFileStem || document.title || 'rendered-document');
        return stem + suffix + '.' + extension;
      }

      function svgToPngDataUrl(svg, width, height) {
        var serialisedSvg = serialiseSvgForExport(svg, width, height);
        var svgBlob = new Blob([serialisedSvg], { type: 'image/svg+xml;charset=utf-8' });
        var url = URL.createObjectURL(svgBlob);
        return new Promise(function (resolve, reject) {
          var image = new Image();
          image.decoding = 'async';
          image.onload = function () {
            try {
              var maxPixels = 16000000;
              var scale = Math.min(2, Math.max(.25, Math.sqrt(maxPixels / Math.max(width * height, 1))));
              var canvas = document.createElement('canvas');
              canvas.width = Math.max(1, Math.round(width * scale));
              canvas.height = Math.max(1, Math.round(height * scale));
              var context = canvas.getContext('2d');
              if (!context) throw new Error('Canvas rendering is unavailable.');
              context.fillStyle = '#ffffff';
              context.fillRect(0, 0, canvas.width, canvas.height);
              context.drawImage(image, 0, 0, canvas.width, canvas.height);
              resolve(canvas.toDataURL('image/png'));
            } catch (error) {
              reject(error);
            } finally {
              URL.revokeObjectURL(url);
            }
          };
          image.onerror = function () {
            URL.revokeObjectURL(url);
            reject(new Error('PNG export failed.'));
          };
          image.src = url;
        });
      }

        document.addEventListener('click', async function (event) {
        var button = event.target.closest('[data-diagram-action]');
        if (!button) return;
        var action = button.dataset.diagramAction;
        if (action !== 'copySource' && action !== 'exportSvg' && action !== 'exportPng') return;
        var frame = button.closest('.diagram-frame');
        var svg = frame && frame.querySelector('.mermaid svg, svg');
        if (!frame) return;

        if (action === 'copySource') {
          var source = frame.dataset.diagramSource || '';
          if (!source) return;
          try {
            await writeText(source);
            setTemporaryText(button, 'Copied');
          } catch {
            setTemporaryText(button, 'Failed');
          }
          return;
        }

        if (!svg) return;
        var size = getSvgBaseSize(svg);
        if (action === 'exportSvg') {
          downloadBlob(new Blob([serialiseSvgForExport(svg, size.width, size.height)], { type: 'image/svg+xml;charset=utf-8' }), getDiagramExportName(frame, 'svg'));
          return;
        }

        try {
          var dataUrl = await svgToPngDataUrl(svg, size.width, size.height);
          downloadBlob(dataUrlToBlob(dataUrl), getDiagramExportName(frame, 'png'));
        } catch {
          setTemporaryText(button, 'Failed');
        }
      });
    })();`;
    }

    function createDocxFromContent({ root, images }) {
      const title = escapeXml(getExportTitle());
      const now = new Date().toISOString();
      const imageRelationships = images.map((image, index) => ({
        ...image,
        relationshipId: `rIdImage${index + 1}`,
        target: `media/${image.name}`,
        partName: `word/media/${image.name}`,
        docPrId: index + 1,
      }));

      const contentTypes = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Default Extension="png" ContentType="image/png"/>
  <Default Extension="svg" ContentType="image/svg+xml"/>
  <Default Extension="jpg" ContentType="image/jpeg"/>
  <Default Extension="jpeg" ContentType="image/jpeg"/>
  <Default Extension="gif" ContentType="image/gif"/>
  <Default Extension="webp" ContentType="image/webp"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
  <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
  <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
</Types>`;

      const rootRels = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>`;

      const documentRels = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
${imageRelationships.map((image) => `  <Relationship Id="${image.relationshipId}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="${escapeXml(image.target)}"/>`).join('\n')}
</Relationships>`;

      const documentXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">
  <w:body>
${buildWordBodyXml(root, imageRelationships)}
    <w:sectPr>
      <w:pgSz w:w="11906" w:h="16838"/>
      <w:pgMar w:top="840" w:right="840" w:bottom="960" w:left="840" w:header="720" w:footer="720" w:gutter="0"/>
    </w:sectPr>
  </w:body>
</w:document>`;

      const coreXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dc:title>${title}</dc:title>
  <dc:creator>${APP_NAME}</dc:creator>
  <cp:lastModifiedBy>${APP_NAME}</cp:lastModifiedBy>
  <dcterms:created xsi:type="dcterms:W3CDTF">${now}</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">${now}</dcterms:modified>
</cp:coreProperties>`;

      const appXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
  <Application>${APP_NAME}</Application>
  <DocSecurity>0</DocSecurity>
  <ScaleCrop>false</ScaleCrop>
  <Company></Company>
  <LinksUpToDate>false</LinksUpToDate>
  <SharedDoc>false</SharedDoc>
  <HyperlinksChanged>false</HyperlinksChanged>
  <AppVersion>1.0</AppVersion>
</Properties>`;

      return createZipBlob([
        { name: '[Content_Types].xml', data: contentTypes },
        { name: '_rels/.rels', data: rootRels },
        { name: 'docProps/core.xml', data: coreXml },
        { name: 'docProps/app.xml', data: appXml },
        { name: 'word/document.xml', data: documentXml },
        { name: 'word/_rels/document.xml.rels', data: documentRels },
        ...imageRelationships.map((image) => ({
          name: image.partName,
          data: base64ToUint8Array(image.base64),
        })),
      ], 'application/vnd.openxmlformats-officedocument.wordprocessingml.document');
    }

    function buildWordBodyXml(root, imageRelationships) {
      const blocks = wordBlocksFromChildren(root, { imageRelationships });
      return blocks.length ? blocks.join('\n') : buildWordParagraph([buildWordTextRun('')]);
    }

    function wordBlocksFromChildren(node, context) {
      const blocks = [];

      node.childNodes.forEach((child) => {
        blocks.push(...wordBlocksFromNode(child, context));
      });

      return blocks;
    }

    function wordBlocksFromNode(node, context) {
      if (node.nodeType === Node.TEXT_NODE) {
        return node.textContent.trim() ? [buildWordParagraph([buildWordTextRun(node.textContent)])] : [];
      }

      if (node.nodeType !== Node.ELEMENT_NODE) return [];

      const tagName = node.tagName.toLowerCase();
      if (['script', 'style', 'svg'].includes(tagName)) return [];

      if (node.classList.contains('diagram-frame')) {
        const image = node.querySelector('img.diagram-image');
        if (image) return [buildWordImageParagraph(image, context)];
        const error = node.querySelector('.diagram-error');
        if (error) return [buildWordParagraph([buildWordTextRun(error.textContent, { code: true, color: 'BE123C' })], { code: true })];
      }

      if (tagName === 'img' && node.dataset.wordImageName) {
        return [buildWordImageParagraph(node, context)];
      }

      if (node.classList.contains('code-block')) {
        return [buildWordCodeBlock(node)];
      }

      if (/^h[1-6]$/.test(tagName)) {
        const level = Number(tagName.slice(1));
        const sizes = { 1: 48, 2: 36, 3: 28, 4: 24, 5: 22, 6: 20 };
        return [buildWordParagraph(collectWordInlineRuns(node, { bold: true, size: sizes[level] ?? 24, color: '0F172A' }), {
          before: level === 1 ? 160 : 240,
          after: 120,
          keepNext: true,
        })];
      }

      if (tagName === 'p') {
        if (node.querySelector('img[data-word-image-name]')) {
          return wordBlocksFromParagraphWithImages(node, context);
        }
        return [buildWordParagraph(collectWordInlineRuns(node), { after: 160 })];
      }

      if (tagName === 'pre') {
        return [buildWordParagraph([buildWordTextRun(node.textContent, { code: true })], { code: true, after: 180 })];
      }

      if (tagName === 'blockquote') {
        return [buildWordParagraph(collectWordInlineRuns(node, { italic: true, color: '475569' }), { indentLeft: 360, after: 160 })];
      }

      if (tagName === 'ul' || tagName === 'ol') {
        return [...node.children]
          .filter((child) => child.tagName?.toLowerCase() === 'li')
          .map((item, index) => buildWordParagraph([
            buildWordTextRun(tagName === 'ol' ? `${index + 1}. ` : '- '),
            ...collectWordInlineRuns(item),
          ], { indentLeft: 360, hanging: 240, after: 80 }));
      }

      if (tagName === 'table') {
        return [buildWordTable(node)];
      }

      if (tagName === 'hr') {
        return [buildWordParagraph([], { bottomBorder: true })];
      }

      if (['div', 'section', 'article', 'main', 'figure', 'thead', 'tbody'].includes(tagName)) {
        const childBlocks = wordBlocksFromChildren(node, context);
        return childBlocks.length ? childBlocks : [];
      }

      const runs = collectWordInlineRuns(node);
      return runs.length ? [buildWordParagraph(runs, { after: 160 })] : [];
    }

    function wordBlocksFromParagraphWithImages(paragraph, context) {
      const blocks = [];
      const textClone = paragraph.cloneNode(true);
      textClone.querySelectorAll('img[data-word-image-name]').forEach((image) => image.remove());

      if (textClone.textContent.trim()) {
        blocks.push(buildWordParagraph(collectWordInlineRuns(textClone), { after: 120 }));
      }

      paragraph.querySelectorAll('img[data-word-image-name]').forEach((image) => {
        blocks.push(buildWordImageParagraph(image, context));
      });

      return blocks;
    }

    function buildWordCodeBlock(block) {
      const code = block.querySelector('pre code');
      const runs = code ? collectWordCodeRuns(code) : [];
      return buildWordParagraph(runs.length ? runs : [buildWordTextRun('', { code: true })], { code: true, after: 180 });
    }

    function collectWordCodeRuns(node, options = {}) {
      const codeOptions = { code: true, ...options };
      if (node.nodeType === Node.TEXT_NODE) {
        return node.textContent ? [buildWordTextRun(node.textContent, codeOptions)] : [];
      }

      if (node.nodeType !== Node.ELEMENT_NODE) return [];

      const nextOptions = { ...codeOptions };
      const color = getHighlightWordColor(node);
      if (color) nextOptions.color = color;
      if (node.classList.contains('hljs-emphasis')) nextOptions.italic = true;
      if (node.classList.contains('hljs-strong')) nextOptions.bold = true;

      const runs = [];
      node.childNodes.forEach((child) => {
        runs.push(...collectWordCodeRuns(child, nextOptions));
      });
      return runs;
    }

    function getHighlightWordColor(element) {
      const colorByClass = {
        'hljs-keyword': '1D4ED8',
        'hljs-selector-tag': '1D4ED8',
        'hljs-built_in': '1D4ED8',
        'hljs-name': '1D4ED8',
        'hljs-tag': '1D4ED8',
        'hljs-string': '15803D',
        'hljs-title': '15803D',
        'hljs-section': '15803D',
        'hljs-attribute': '15803D',
        'hljs-literal': '15803D',
        'hljs-template-tag': '15803D',
        'hljs-template-variable': '15803D',
        'hljs-type': '15803D',
        'hljs-addition': '15803D',
        'hljs-comment': '64748B',
        'hljs-quote': '64748B',
        'hljs-deletion': '64748B',
        'hljs-meta': '64748B',
        'hljs-number': 'B45309',
        'hljs-regexp': 'B45309',
        'hljs-link': 'B45309',
        'hljs-selector-id': 'B45309',
        'hljs-selector-class': 'B45309',
        'hljs-variable': 'A21CAF',
        'hljs-symbol': 'A21CAF',
        'hljs-bullet': 'A21CAF',
        'hljs-subst': 'A21CAF',
      };

      for (const className of element.classList) {
        if (colorByClass[className]) return colorByClass[className];
      }
      return '';
    }

    function collectWordInlineRuns(node, options = {}) {
      const runs = [];

      node.childNodes.forEach((child) => {
        if (child.nodeType === Node.TEXT_NODE) {
          if (child.textContent) runs.push(buildWordTextRun(child.textContent, options));
          return;
        }

        if (child.nodeType !== Node.ELEMENT_NODE) return;

        const tagName = child.tagName.toLowerCase();
        if (tagName === 'br') {
          runs.push('<w:r><w:br/></w:r>');
          return;
        }

        if (tagName === 'img' && (child.classList.contains('diagram-image') || child.dataset.wordImageName)) {
          return;
        }

        const nextOptions = { ...options };
        if (['strong', 'b'].includes(tagName)) nextOptions.bold = true;
        if (['em', 'i'].includes(tagName)) nextOptions.italic = true;
        if (tagName === 'code') nextOptions.code = true;
        if (tagName === 'a') {
          nextOptions.color = '0369A1';
          nextOptions.underline = true;
        }
        if (tagName === 's' || tagName === 'del') nextOptions.strike = true;

        runs.push(...collectWordInlineRuns(child, nextOptions));
      });

      return runs;
    }

    function buildWordParagraph(runs, options = {}) {
      const pPr = buildWordParagraphProperties(options);
      return `    <w:p>${pPr}${runs.join('')}</w:p>`;
    }

    function buildWordParagraphProperties(options) {
      const parts = [];
      const spacing = [];

      if (options.keepNext) parts.push('<w:keepNext/>');
      if (options.before || options.after) {
        if (options.before) spacing.push(`w:before="${options.before}"`);
        if (options.after) spacing.push(`w:after="${options.after}"`);
        parts.push(`<w:spacing ${spacing.join(' ')}/>`);
      }
      if (options.indentLeft || options.hanging) {
        parts.push(`<w:ind w:left="${options.indentLeft ?? 0}"${options.hanging ? ` w:hanging="${options.hanging}"` : ''}/>`);
      }
      if (options.code) {
        parts.push('<w:shd w:fill="F8FAFC"/>');
      }
      if (options.bottomBorder) {
        parts.push('<w:pBdr><w:bottom w:val="single" w:sz="8" w:space="1" w:color="CBD5E1"/></w:pBdr>');
      }

      return parts.length ? `<w:pPr>${parts.join('')}</w:pPr>` : '';
    }

    function buildWordTextRun(text, options = {}) {
      const rPr = buildWordRunProperties(options);
      const segments = String(text).replace(/\r\n/g, '\n').split('\n');
      const content = segments.map((segment, index) => {
        const textElement = `<w:t xml:space="preserve">${escapeXml(segment)}</w:t>`;
        return index === 0 ? textElement : `<w:br/>${textElement}`;
      }).join('');

      return `<w:r>${rPr}${content}</w:r>`;
    }

    function buildWordRunProperties(options) {
      const parts = [];
      if (options.bold) parts.push('<w:b/>');
      if (options.italic) parts.push('<w:i/>');
      if (options.underline) parts.push('<w:u w:val="single"/>');
      if (options.strike) parts.push('<w:strike/>');
      if (options.color) parts.push(`<w:color w:val="${options.color}"/>`);
      if (options.size) parts.push(`<w:sz w:val="${options.size}"/>`);
      if (options.code) {
        parts.push('<w:rFonts w:ascii="Consolas" w:hAnsi="Consolas" w:cs="Consolas"/>');
        parts.push('<w:sz w:val="19"/>');
      }
      return parts.length ? `<w:rPr>${parts.join('')}</w:rPr>` : '';
    }

    function buildWordTable(table) {
      const rows = [...table.querySelectorAll('tr')];
      const rowXml = rows.map((row) => {
        const cells = [...row.children].filter((cell) => ['td', 'th'].includes(cell.tagName.toLowerCase()));
        const cellXml = cells.map((cell) => {
          const isHeader = cell.tagName.toLowerCase() === 'th';
          const runs = collectWordInlineRuns(cell, isHeader ? { bold: true } : {});
          return `<w:tc><w:tcPr><w:tcW w:w="0" w:type="auto"/></w:tcPr>${buildWordParagraph(runs.length ? runs : [buildWordTextRun('')])}</w:tc>`;
        }).join('');
        return `<w:tr>${cellXml}</w:tr>`;
      }).join('');

      return `    <w:tbl><w:tblPr><w:tblW w:w="0" w:type="auto"/><w:tblBorders><w:top w:val="single" w:sz="4" w:color="D5DCE8"/><w:left w:val="single" w:sz="4" w:color="D5DCE8"/><w:bottom w:val="single" w:sz="4" w:color="D5DCE8"/><w:right w:val="single" w:sz="4" w:color="D5DCE8"/><w:insideH w:val="single" w:sz="4" w:color="D5DCE8"/><w:insideV w:val="single" w:sz="4" w:color="D5DCE8"/></w:tblBorders></w:tblPr>${rowXml}</w:tbl>`;
    }

    function buildWordImageParagraph(imageElement, context) {
      const imageName = imageElement.dataset.wordImageName || imageElement.getAttribute('src');
      const image = context.imageRelationships.find((item) => item.name === imageName);
      if (!image) {
        return buildWordParagraph([buildWordTextRun(imageElement.alt || 'Mermaid diagram')]);
      }

      const width = Number.isFinite(image.displayWidth) ? image.displayWidth : 640;
      const height = Number.isFinite(image.displayHeight) ? image.displayHeight : 360;
      const cx = pxToEmu(width);
      const cy = pxToEmu(height);
      const name = escapeXml(imageElement.alt || `Diagram ${image.docPrId}`);

      return `    <w:p><w:pPr><w:spacing w:before="160" w:after="160"/></w:pPr><w:r><w:drawing><wp:inline distT="0" distB="0" distL="0" distR="0"><wp:extent cx="${cx}" cy="${cy}"/><wp:docPr id="${image.docPrId}" name="${name}"/><wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect="1"/></wp:cNvGraphicFramePr><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic><pic:nvPicPr><pic:cNvPr id="${image.docPrId}" name="${escapeXml(image.name)}"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed="${image.relationshipId}"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="${cx}" cy="${cy}"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>`;
    }

    function pxToEmu(value) {
      return Math.max(1, Math.round(value * 9525));
    }

    function buildWordExportHtml(content) {
      return `<!doctype html>
<html lang="en-GB" xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:w="urn:schemas-microsoft-com:office:word" xmlns="http://www.w3.org/TR/REC-html40">
<head>
  <meta charset="utf-8">
  <title>${escapeHtml(getExportTitle())}</title>
  <!--[if Word]>
  <xml>
    <w:WordDocument>
      <w:View>Print</w:View>
      <w:Zoom>100</w:Zoom>
      <w:DoNotOptimizeForBrowser />
    </w:WordDocument>
  </xml>
  <![endif]-->
  <style>
    @page Section1 { size: 595.3pt 841.9pt; margin: 42pt 42pt 48pt 42pt; }
    div.Section1 { page: Section1; }
    body { margin: 0; font-family: Aptos, Calibri, Arial, sans-serif; font-size: 11pt; line-height: 1.45; color: #111827; background: #ffffff; }
    h1, h2, h3, h4 { font-family: Aptos Display, Aptos, Calibri, Arial, sans-serif; color: #0f172a; line-height: 1.2; margin: 18pt 0 8pt; }
    h1 { font-size: 24pt; }
    h2 { font-size: 18pt; }
    h3 { font-size: 14pt; }
    h4 { font-size: 12pt; }
    p { margin: 0 0 8pt; }
    a { color: #0369a1; text-decoration: underline; }
    pre { white-space: pre-wrap; word-wrap: break-word; margin: 10pt 0; padding: 9pt; border: 1pt solid #d5dce8; background: #f8fafc; font-family: Consolas, Courier New, monospace; font-size: 9.5pt; }
    code { font-family: Consolas, Courier New, monospace; font-size: 9.5pt; }
    table { width: 100%; border-collapse: collapse; margin: 10pt 0; }
    th, td { border: 1pt solid #d5dce8; padding: 5pt 6pt; text-align: left; vertical-align: top; }
    th { background: #e2e8f0; color: #0f172a; font-weight: bold; }
    blockquote { margin: 8pt 0; padding: 4pt 0 4pt 10pt; border-left: 3pt solid #38bdf8; color: #475569; }
    ul, ol { margin-top: 0; margin-bottom: 8pt; }
    .diagram-frame { margin: 12pt 0; padding: 9pt; border: 1pt solid #d5dce8; background: #ffffff; }
    .diagram-image { display: block; max-width: 100%; height: auto; }
    .diagram-error { white-space: pre-wrap; margin: 0; padding: 9pt; border: 1pt solid #fda4af; background: #fff1f2; color: #be123c; }
  </style>
</head>
<body>
  <div class="Section1">${content}</div>
</body>
</html>`;
    }

    function buildWordExportMhtml(content, images) {
      const boundary = `----=MDMMDRendererBoundary${Date.now()}`;
      const html = buildWordExportHtml(content);
      const parts = [
        'MIME-Version: 1.0',
        `Content-Type: multipart/related; boundary="${boundary}"; type="text/html"`,
        '',
        `--${boundary}`,
        'Content-Type: text/html; charset="utf-8"',
        'Content-Transfer-Encoding: 8bit',
        'Content-Location: file:///C:/document.html',
        '',
        html,
        '',
      ];

      images.forEach((image) => {
        parts.push(
          `--${boundary}`,
          `Content-Type: ${image.mimeType}`,
          'Content-Transfer-Encoding: base64',
          `Content-Location: ${image.name}`,
          '',
          wrapBase64(image.base64),
          ''
        );
      });

      parts.push(`--${boundary}--`, '');
      return parts.join('\\r\\n');
    }

    function createDocxFromMhtml(mhtml) {
      const title = escapeXml(getExportTitle());
      const now = new Date().toISOString();

      const contentTypes = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Default Extension="mht" ContentType="message/rfc822"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
  <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
  <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
</Types>`;

      const rootRels = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>`;

      const documentXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <w:body>
    <w:altChunk r:id="htmlChunk"/>
    <w:sectPr>
      <w:pgSz w:w="11906" w:h="16838"/>
      <w:pgMar w:top="840" w:right="840" w:bottom="960" w:left="840" w:header="720" w:footer="720" w:gutter="0"/>
    </w:sectPr>
  </w:body>
</w:document>`;

      const documentRels = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="htmlChunk" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/aFChunk" Target="afchunk.mht"/>
</Relationships>`;

      const coreXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dc:title>${title}</dc:title>
  <dc:creator>${APP_NAME}</dc:creator>
  <cp:lastModifiedBy>${APP_NAME}</cp:lastModifiedBy>
  <dcterms:created xsi:type="dcterms:W3CDTF">${now}</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">${now}</dcterms:modified>
</cp:coreProperties>`;

      const appXml = `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
  <Application>${APP_NAME}</Application>
  <DocSecurity>0</DocSecurity>
  <ScaleCrop>false</ScaleCrop>
  <Company></Company>
  <LinksUpToDate>false</LinksUpToDate>
  <SharedDoc>false</SharedDoc>
  <HyperlinksChanged>false</HyperlinksChanged>
  <AppVersion>1.0</AppVersion>
</Properties>`;

      return createZipBlob([
        { name: '[Content_Types].xml', data: contentTypes },
        { name: '_rels/.rels', data: rootRels },
        { name: 'docProps/core.xml', data: coreXml },
        { name: 'docProps/app.xml', data: appXml },
        { name: 'word/document.xml', data: documentXml },
        { name: 'word/_rels/document.xml.rels', data: documentRels },
        { name: 'word/afchunk.mht', data: mhtml },
      ], 'application/vnd.openxmlformats-officedocument.wordprocessingml.document');
    }

    const crc32Table = (() => {
      const table = new Uint32Array(256);
      for (let i = 0; i < 256; i += 1) {
        let value = i;
        for (let bit = 0; bit < 8; bit += 1) {
          value = value & 1 ? 0xedb88320 ^ (value >>> 1) : value >>> 1;
        }
        table[i] = value >>> 0;
      }
      return table;
    })();

    return {
      copyToClipboard,
      copyCodeBlock,
      copyTableBlock,
      getDiagramFrameFromAction,
      copyDiagramSource,
      exportDiagramFrameSvg,
      exportDiagramFramePng,
      exportPreviewHtml,
      exportPreviewWord,
      exportPreviewPdf,
      exportMarkdownBundle,
      copyRenderedHtml,
      copyRenderedText,
      copyCurrentMermaidSource,
      exportCurrentDiagramSvg,
      exportCurrentDiagramPng,
      exportDocsSite,
      setExportTrust,
    };
}
