import { mermaidStarters, storageKeys } from '../state/config.js';
import { getCodeLanguage, isMermaidLanguage, isSupportedFile } from '../utils/files.js';
import { clamp, escapeHtml, roundToStep } from '../utils/format.js';
import { countMarkdownMermaidBlocks, normaliseDevOpsMermaidBlocks } from '../utils/devops-markdown.js';
import { sanitizeMermaidSvg, sanitizeRenderedHtml } from '../utils/security.js';
import { createWikilinkExtension } from '../utils/wikilinks.js';
import { createMathExtensions } from '../utils/math.js';

const MERMAID_MODULE_PATH = '../../vendor/mermaid-11.15.0.esm.min.js';
const MARKED_MODULE_PATH = '../../vendor/marked-16.4.2.esm.js';
const HIGHLIGHT_MODULE_PATH = '../../vendor/highlight-11.11.1.esm.js';
const KATEX_MODULE_PATH = '../../vendor/chunks/mermaid.esm.min/katex-K3KEBU37.js';

export function createRenderingService({
  state,
  dom,
  callbacks,
}) {
  const {
    editor,
    preview,
    diagramCount,
    zoomOutButton,
    zoomInButton,
    fitZoomButton,
    resetZoomButton,
    zoomValue,
  } = dom;
  const {
    setStatus,
    setExportTrust,
    updatePreviewOutline,
    beforePreviewRender,
    afterPreviewRender,
    getExportFileStem,
    getDocTitleFromPath,
  } = callbacks;

  let markedPromise = null;
  let hljsPromise = null;
  let hljs = null;
  let katexPromise = null;
  let katex = null;
  let mermaidPromise = null;
  let mermaid = null;

  async function loadMarked() {
    if (!markedPromise) {
      markedPromise = Promise.all([
        import(MARKED_MODULE_PATH),
        loadKatex().catch(() => null),
      ]).then(([{ marked, Renderer }, katexRenderer]) => {
        marked.setOptions({
          gfm: true,
          breaks: false,
          renderer: createMarkdownRenderer(Renderer),
        });
        marked.use({
          extensions: [
            createWikilinkExtension(),
            ...createMathExtensions({
              renderMath: katexRenderer
                ? (source, displayMode) => katexRenderer.renderToString(source, {
                    displayMode,
                    output: 'html',
                    throwOnError: false,
                    trust: false,
                  })
                : undefined,
            }),
          ],
        });
        return marked;
      });
    }

    return markedPromise;
  }

  async function loadKatex() {
    if (!katexPromise) {
      katexPromise = import(KATEX_MODULE_PATH).then((module) => module.default ?? module);
    }
    if (!katex) {
      katex = await katexPromise;
    }
    return katex;
  }

  function createMarkdownRenderer(Renderer) {
    const markdownRenderer = new Renderer();

    markdownRenderer.code = (token) => {
      if (isMermaidLanguage(token.lang)) {
        return buildMermaidBlock(token.text, token.line);
      }

      return buildCodeBlockHtml(token.text, token.lang, token.line);
    };

    const originalParagraph = markdownRenderer.paragraph.bind(markdownRenderer);
    markdownRenderer.paragraph = (token) => {
      const html = originalParagraph(token);
      if (token.line !== undefined) {
        return html.replace(/^<p/, `<p data-line="${token.line}"`);
      }
      return html;
    };

    const originalBlockquote = markdownRenderer.blockquote.bind(markdownRenderer);
    markdownRenderer.blockquote = (token) => {
      const html = originalBlockquote(token);
      if (token.line !== undefined) {
        return html.replace(/^<blockquote/, `<blockquote data-line="${token.line}"`);
      }
      return html;
    };

    const originalList = markdownRenderer.list.bind(markdownRenderer);
    markdownRenderer.list = (token) => {
      const html = originalList(token);
      if (token.line !== undefined) {
        return html.replace(/^<(ul|ol)/, `<$1 data-line="${token.line}"`);
      }
      return html;
    };

    const originalListitem = markdownRenderer.listitem.bind(markdownRenderer);
    markdownRenderer.listitem = (token) => {
      const html = originalListitem(token);
      if (token.line !== undefined) {
        return html.replace(/^<li/, `<li data-line="${token.line}"`);
      }
      return html;
    };

    const originalTable = markdownRenderer.table.bind(markdownRenderer);
    markdownRenderer.table = (token) => {
      const html = originalTable(token);
      if (token.line !== undefined) {
        return html.replace(/^<table/, `<table data-line="${token.line}"`);
      }
      return html;
    };

    const originalHr = markdownRenderer.hr.bind(markdownRenderer);
    markdownRenderer.hr = (token) => {
      const html = originalHr(token);
      if (token.line !== undefined) {
        return html.replace(/^<hr/, `<hr data-line="${token.line}"`);
      }
      return html;
    };

    const originalHeading = markdownRenderer.heading.bind(markdownRenderer);
    markdownRenderer.heading = (token) => {
      const html = originalHeading(token);
      if (token.line !== undefined) {
        return html.replace(/^<h([1-6])/, `<h$1 data-line="${token.line}"`);
      }
      return html;
    };

    return markdownRenderer;
  }

  async function loadHighlight() {
    if (!hljsPromise) {
      hljsPromise = import(HIGHLIGHT_MODULE_PATH).then((module) => {
        hljs = module.default;
        return hljs;
      });
    }

    return hljsPromise;
  }

  async function loadMermaid() {
    if (!mermaidPromise) {
      mermaidPromise = import(MERMAID_MODULE_PATH).then((module) => {
        mermaid = module.default;
        mermaid.initialize({
          startOnLoad: false,
          securityLevel: 'strict',
          theme: 'default',
          flowchart: {
            htmlLabels: false,
          },
        });
        return mermaid;
      });
    }

    return mermaidPromise;
  }

    async function renderPreview() {
      clearTimeout(state.debounceId);
      state.debounceId = 0;

      const renderId = ++state.renderId;
      const source = editor.value.replace(/^[\u200B\u200C\u200D\u200E\u200F\uFEFF]/, '');
      const selectedMode = resolveModeFor(source, state.fileName);

      try {
        setStatus('Rendering...');
        setExportTrust('', '');

        const dirtyHtml = selectedMode === 'mermaid'
          ? buildMermaidOnlyHtml(source)
          : await buildMarkdownHtml(source);

        const renderedHtml = sanitizeRenderedHtml(dirtyHtml);
        const modeBanner = buildModeBanner(source, selectedMode);
        const previewHtml = `${modeBanner}${renderedHtml}`;
        beforePreviewRender?.();
        preview.innerHTML = state.docsPreview ? buildDocsPreviewShell(previewHtml) : previewHtml;
        hydrateManagedAssetImages(preview);
        prepareTableBlocksIn(preview);

        preview.querySelectorAll('a[href]').forEach((anchor) => {
          if (anchor.dataset.docPath || anchor.dataset.wikilinkTarget) return;
          anchor.setAttribute('target', '_blank');
          anchor.setAttribute('rel', 'noopener noreferrer');
        });

        const diagrams = [...preview.querySelectorAll('.mermaid')];
        const diagramResult = await renderMermaidDiagrams(diagrams, renderId);

        if (diagramResult.cancelled || renderId !== state.renderId) {
          return { ok: false, cancelled: true, diagramErrors: 0 };
        }

        prepareDiagramFrames();

        if (renderId !== state.renderId) {
          return { ok: false, cancelled: true, diagramErrors: diagramResult.errors };
        }

        updateDiagramControls(diagrams.length);
        state.lastRenderResult = {
          ok: true,
          diagramErrors: diagramResult.errors,
          diagramTotal: diagrams.length,
        };
        updatePreviewOutline();
        afterPreviewRender?.();
        setRenderStatus(diagramResult.errors);
        return { ok: true, cancelled: false, diagramErrors: diagramResult.errors, diagramTotal: diagrams.length };
      } catch (error) {
        if (renderId !== state.renderId) return { ok: false, cancelled: true, diagramErrors: 0 };
        beforePreviewRender?.();
        preview.innerHTML = `<pre class="error">${escapeHtml(error?.message ?? String(error))}</pre>`;
        updateDiagramControls(0);
        state.lastRenderResult = { ok: false, diagramErrors: 0, diagramTotal: 0 };
        updatePreviewOutline();
        afterPreviewRender?.();
        setStatus('Error while rendering.', 'danger');
        return { ok: false, cancelled: false, diagramErrors: 0 };
      }
    }

    async function buildMarkdownHtml(source) {
      const cleaned = normaliseDevOpsMermaidBlocks(stripLeadingFrontMatter(source));
      await preloadHighlightForSource(cleaned);
      const marked = await loadMarked();
      const tokens = marked.lexer(cleaned);
      assignLineNumbers(tokens, cleaned);
      return marked.parser(tokens);
    }

    async function preloadHighlightForSource(source) {
      if (hljs || !getHighlightCandidateLanguages(source).length) return;
      await loadHighlight();
    }

    function getHighlightCandidateLanguages(source) {
      const languages = [];
      let fence = '';

      String(source).split(/\r?\n/).forEach((line) => {
        if (!fence) {
          const opener = line.match(/^[ \t]{0,3}(`{3,}|~{3,})[ \t]*([^`\s~{]*)?/);
          if (!opener) return;

          fence = opener[1][0];
          const language = normaliseCodeLanguage(opener[2] || '');
          if (language && !isMermaidLanguage(language)) {
            languages.push(language);
          }
          return;
        }

        if (line.trimStart().startsWith(fence.repeat(3))) {
          fence = '';
        }
      });

      return languages;
    }

    function assignLineNumbers(tokens, source) {
      let offset = 0;

      function walk(tokenList) {
        for (const token of tokenList) {
          if (token.raw) {
            const index = source.indexOf(token.raw, offset);
            if (index !== -1) {
              const linesBefore = source.slice(0, index).split('\n').length - 1;
              token.line = linesBefore;
              offset = index + token.raw.length;
            }
          }

          if (token.tokens) {
            walk(token.tokens);
          }
          if (token.items) {
            walk(token.items);
          }
        }
      }

      walk(tokens);
    }

    function buildMermaidOnlyHtml(source) {
      return buildMermaidBlock(source, 0);
    }

    function buildMermaidBlock(source, line) {
      const lineAttr = line !== undefined ? ` data-line="${line}"` : '';
      return `<pre class="mermaid"${lineAttr}>${escapeHtml(source.trim())}</pre>`;
    }

    function buildCodeBlockHtml(source, language, line) {
      const highlighted = highlightCodeHtml(source, language);
      const label = highlighted.language || 'text';
      const className = highlighted.highlighted ? `hljs language-${escapeHtml(highlighted.language)}` : 'hljs';
      const copyLabel = `Copy ${label} code`;
      const lineAttr = line !== undefined ? ` data-line="${line}"` : '';

      return `<figure class="code-block"${lineAttr} data-code-language="${escapeHtml(label)}">
        <figcaption class="code-block-header">
          <span class="code-block-language">${escapeHtml(label)}</span>
          <button class="code-copy-button" type="button" data-code-action="copy" aria-label="${escapeHtml(copyLabel)}">Copy</button>
        </figcaption>
        <pre><code class="${className}">${highlighted.html}</code></pre>
      </figure>`;
    }

    function highlightCodeHtml(source, language) {
      const normalised = normaliseCodeLanguage(language);
      if (normalised && hljs?.getLanguage(normalised)) {
        return {
          html: hljs.highlight(source, { language: normalised, ignoreIllegals: true }).value,
          language: normalised,
          highlighted: true,
        };
      }

      return {
        html: escapeHtml(source),
        language: normalised,
        highlighted: false,
      };
    }

    function normaliseCodeLanguage(language) {
      const raw = getCodeLanguage(language);
      if (!raw) return '';

      const aliases = {
        js: 'javascript',
        jsx: 'javascript',
        ts: 'typescript',
        tsx: 'typescript',
        py: 'python',
        rb: 'ruby',
        sh: 'bash',
        shell: 'bash',
        zsh: 'bash',
        yml: 'yaml',
        md: 'markdown',
        mdown: 'markdown',
        csharp: 'cs',
        'c#': 'cs',
      };

      return aliases[raw] || raw;
    }

    function buildModeBanner(source, selectedMode) {
      if (state.studioMode) {
        return buildStudioBanner(source, selectedMode);
      }

      if (state.generatorMode) {
        return buildGeneratorBanner(source, selectedMode);
      }

      return '';
    }

    function buildStudioBanner(source, selectedMode) {
      const diagramCount = selectedMode === 'mermaid'
        ? (source.trim() ? 1 : 0)
        : countMarkdownMermaidBlocks(source);
      const modeText = selectedMode === 'mermaid' ? 'standalone Mermaid source' : 'Markdown with Mermaid blocks';
      return `<div class="studio-banner"><strong>Mermaid Diagram Studio</strong><span>${escapeHtml(modeText)} · ${diagramCount} diagram${diagramCount === 1 ? '' : 's'}</span></div>`;
    }

    function buildGeneratorBanner(source, selectedMode) {
      const diagramCount = selectedMode === 'mermaid'
        ? (source.trim() ? 1 : 0)
        : countMarkdownMermaidBlocks(source);
      const sourceType = selectedMode === 'mermaid' ? 'Mermaid source' : 'editable Markdown';
      return `<div class="studio-banner"><strong>${escapeHtml(state.generatorMode)}</strong><span>Local template · ${escapeHtml(sourceType)} · ${diagramCount} diagram${diagramCount === 1 ? '' : 's'}</span></div>`;
    }

    function buildDocsPreviewShell(contentHtml) {
      const files = state.files.filter((file) => isSupportedFile(file.name));
      const title = escapeHtml(state.folderName || 'Docs site');
      const links = files.map((file) => {
        const active = file.path === state.activePath ? ' active' : '';
        return `<a class="docs-site-link${active}" href="#" data-doc-path="${escapeHtml(file.path)}" title="${escapeHtml(file.path)}">${escapeHtml(getDocTitleFromPath(file.path))}</a>`;
      }).join('');

      return `<section class="docs-site-preview">
        <nav class="docs-site-nav" aria-label="Docs site navigation">
          <div class="docs-site-nav-header">
            <strong>${title}</strong>
            <small>${files.length} page${files.length === 1 ? '' : 's'} ready for export</small>
          </div>
          <div class="docs-site-nav-list">${links || '<span class="menu-note">Open a folder to preview a docs site.</span>'}</div>
        </nav>
        <article class="docs-site-content">${contentHtml}</article>
      </section>`;
    }

    async function renderMermaidDiagrams(diagrams, renderId) {
      let errors = 0;
      if (!diagrams.length) return { cancelled: false, errors };

      for (let index = 0; index < diagrams.length; index += 1) {
        if (renderId !== state.renderId) return { cancelled: true, errors };

        const diagram = diagrams[index];
        const diagramSource = diagram.textContent.trim();
        diagram.dataset.source = diagramSource;

        if (!diagramSource) {
          renderDiagramError(diagram, new Error('Mermaid diagram is empty.'));
          errors += 1;
          continue;
        }

        try {
          const id = `mdmmd-diagram-${renderId}-${index}`;
          const validation = await validateMermaidSource(diagramSource);
          if (!validation.ok) {
            renderDiagramError(diagram, validation.error, diagramSource);
            errors += 1;
            continue;
          }

          const { svg, bindFunctions } = await renderMermaidSvg(id, diagramSource);

          if (renderId !== state.renderId) return { cancelled: true, errors };

          diagram.classList.remove('mermaid-error');
          diagram.removeAttribute('data-processed');
          diagram.innerHTML = sanitizeMermaidSvg(svg);
          bindFunctions?.(diagram);
        } catch (error) {
          renderDiagramError(diagram, error, diagramSource);
          errors += 1;
        }
      }

      return { cancelled: false, errors };
    }

    async function renderMermaidSvg(id, source) {
      const renderer = await loadMermaid();
      const renderTask = state.mermaidRenderChain
        .catch(() => undefined)
        .then(() => renderer.render(id, source));

      state.mermaidRenderChain = renderTask.catch(() => undefined);
      return renderTask;
    }

    async function validateMermaidSource(source) {
      const parser = await loadMermaid();
      if (typeof parser.parse !== 'function') {
        return { ok: true };
      }

      try {
        const result = await parser.parse(source, { suppressErrors: false });
        return result === false
          ? { ok: false, error: new Error('Mermaid validation failed.') }
          : { ok: true };
      } catch (error) {
        return { ok: false, error };
      }
    }

    function renderDiagramError(diagram, error, source = '') {
      const message = `Mermaid validation failed before render:\n${error?.message ?? String(error)}`;
      diagram.classList.add('mermaid-error');
      diagram.dataset.source = source || diagram.dataset.source || '';
      diagram.innerHTML = '';

      const box = document.createElement('div');
      box.className = 'diagram-error';
      const pre = document.createElement('pre');
      pre.textContent = message;

      const actions = document.createElement('div');
      actions.className = 'diagram-error-actions';

      const copy = document.createElement('button');
      copy.type = 'button';
      copy.dataset.diagramAction = 'copyError';
      copy.textContent = 'Copy error';

      const jump = document.createElement('button');
      jump.type = 'button';
      jump.dataset.diagramAction = 'jumpSource';
      jump.textContent = 'Jump to source';

      actions.append(copy, jump);
      box.append(pre, actions);
      diagram.appendChild(box);
    }

    function hydrateManagedAssetImages(root) {
      if (!state.managedAssets?.size) return;
      root.querySelectorAll('img[src]').forEach((image) => {
        const path = resolveManagedAssetPath(image.getAttribute('src') || '', state.activePath);
        const asset = path ? state.managedAssets.get(path) : null;
        if (!asset?.objectUrl) return;
        image.dataset.managedAssetPath = asset.path;
        image.src = asset.objectUrl;
      });
    }

    function resolveManagedAssetPath(value, documentPath = '') {
      const path = normaliseAssetPath(value);
      if (!path) return '';
      if (state.managedAssets.has(path)) return path;

      const directory = normaliseAssetPath(documentPath).split('/').slice(0, -1).join('/');
      if (!directory) return '';

      const resolved = normaliseAssetPath(`${directory}/${path}`);
      return state.managedAssets.has(resolved) ? resolved : '';
    }

    function normaliseAssetPath(value) {
      return String(value)
        .replace(/^blob:.*$/i, '')
        .replace(/^\.?\//, '')
        .replace(/\\/g, '/');
    }

    function setRenderStatus(diagramErrors) {
      const dirtySuffix = state.dirtyPaths.has(state.activePath) ? ' · edited in memory' : '';

      if (diagramErrors) {
        setStatus(`Rendered with ${diagramErrors} diagram error${diagramErrors === 1 ? '' : 's'}${dirtySuffix}`, 'warning');
        return;
      }

      setStatus(`Rendered${dirtySuffix}`, 'ok');
    }

    function resolveMode(source) {
      return resolveModeFor(source, state.fileName);
    }

    function resolveModeFor(source, fileName) {
      if (/\.(mmd|mermaid)$/i.test(fileName)) return 'mermaid';

      const trimmed = getMermaidProbe(source);
      const looksLikeMermaid = mermaidStarters.some((starter) => trimmed.startsWith(starter));
      const hasMarkdownSignals = /(^|\n)#{1,6}\s|(^|\n)```|\[[^\]]+\]\([^)]+\)|(^|\n)\|.+\|/m.test(source);

      return looksLikeMermaid && !hasMarkdownSignals ? 'mermaid' : 'markdown';
    }

    function getMermaidProbe(source) {
      return stripLeadingFrontMatter(source).trimStart();
    }

    function stripLeadingFrontMatter(source) {
      const frontMatter = String(source).match(/^---[ \t]*\r?\n[\s\S]*?\r?\n---[ \t]*(?:\r?\n|$)/);

      if (!frontMatter) return source;
      return String(source).slice(frontMatter[0].length);
    }

    function prepareDiagramFrames() {
      prepareDiagramFramesIn(preview, true, getExportFileStem());
    }

    function prepareTableBlocksIn(root) {
      const contentRoot = root.querySelector?.('.docs-site-content') || root;
      const tables = [...contentRoot.querySelectorAll('table')].filter((table) => !table.closest('.table-block'));
      tables.forEach((table, index) => {
        const wrapper = document.createElement('figure');
        wrapper.className = 'table-block';
        wrapper.dataset.tableIndex = String(index + 1);

        const header = document.createElement('figcaption');
        header.className = 'table-block-header';

        const title = document.createElement('span');
        title.className = 'table-block-title';
        title.textContent = `Table ${index + 1}`;

        const copy = document.createElement('button');
        copy.type = 'button';
        copy.className = 'table-copy-button';
        copy.dataset.tableAction = 'copy';
        copy.setAttribute('aria-label', `Copy table ${index + 1} for Excel`);
        copy.textContent = 'Copy';

        header.append(title, copy);
        table.parentNode.insertBefore(wrapper, table);
        wrapper.append(header, table);
      });
    }

    function prepareDiagramFramesIn(root, applyZoom = false, fileStem = getExportFileStem()) {
      const diagrams = [...root.querySelectorAll('.mermaid')];

      const diagramTotal = diagrams.length;

      diagrams.forEach((diagram, index) => {
        if (!diagram.closest('.diagram-frame')) {
          const frame = document.createElement('div');
          frame.className = 'diagram-frame';
          diagram.parentNode.insertBefore(frame, diagram);
          frame.appendChild(diagram);
        }

        const frame = diagram.closest('.diagram-frame');
        const svg = diagram.querySelector('svg');
        if (!frame || !svg) {
          frame?.querySelector('.diagram-toolbar')?.remove();
          return;
        }

        decorateDiagramFrame(frame, diagram, index + 1, diagramTotal, fileStem);

        const size = getSvgBaseSize(svg);
        svg.dataset.baseWidth = String(size.width);
        svg.dataset.baseHeight = String(size.height);
        svg.style.width = `${size.width}px`;
        svg.style.height = `${size.height}px`;
        svg.style.maxWidth = 'none';
        svg.style.transformOrigin = 'top left';
      });

      if (applyZoom) {
        applyDiagramZoom();
      }
    }

    function decorateDiagramFrame(frame, diagram, index, total, fileStem) {
      frame.dataset.diagramIndex = String(index);
      frame.dataset.diagramTotal = String(total);
      frame.dataset.diagramSource = diagram.dataset.source || '';
      frame.dataset.diagramFileStem = fileStem || getExportFileStem();

      let toolbar = frame.querySelector(':scope > .diagram-toolbar');
      if (!toolbar) {
        toolbar = document.createElement('div');
        toolbar.className = 'diagram-toolbar';
        frame.insertBefore(toolbar, frame.firstChild);
      }

      toolbar.innerHTML = '';

      const title = document.createElement('span');
      title.className = 'diagram-toolbar-title';
      title.textContent = `Diagram ${index}`;

      const actions = document.createElement('div');
      actions.className = 'diagram-action-group';
      actions.append(
        createDiagramActionButton('copySource', 'Copy', `Copy Mermaid source for diagram ${index}`),
        createDiagramActionButton('exportSvg', 'SVG', `Export diagram ${index} as SVG`),
        createDiagramActionButton('exportPng', 'PNG', `Export diagram ${index} as PNG`)
      );

      toolbar.append(title, actions);
    }

    function createDiagramActionButton(action, label, ariaLabel) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'diagram-action-button';
      button.dataset.diagramAction = action;
      button.setAttribute('aria-label', ariaLabel);
      button.textContent = label;
      return button;
    }

    function getSvgBaseSize(svg) {
      const viewBox = svg.getAttribute('viewBox');
      if (viewBox) {
        const values = viewBox.trim().split(/[\s,]+/).map(Number);
        if (values.length === 4 && Number.isFinite(values[2]) && Number.isFinite(values[3])) {
          return {
            width: Math.max(values[2], 1),
            height: Math.max(values[3], 1),
          };
        }
      }

      const width = parseFloat(svg.getAttribute('width')) || svg.getBoundingClientRect().width || 800;
      const height = parseFloat(svg.getAttribute('height')) || svg.getBoundingClientRect().height || 420;
      return {
        width: Math.max(width, 1),
        height: Math.max(height, 1),
      };
    }

    function setDiagramZoom(value) {
      state.diagramZoom = clamp(roundToStep(value, .05), .25, 3);
      localStorage.setItem(storageKeys.diagramZoom, String(state.diagramZoom));
      applyDiagramZoom();
    }

    function applyDiagramZoom() {
      const zoom = state.diagramZoom;
      const frames = [...preview.querySelectorAll('.diagram-frame')];

      frames.forEach((frame) => {
        const diagram = frame.querySelector('.mermaid');
        const svg = frame.querySelector('svg');
        if (!diagram || !svg) return;

        const width = parseFloat(svg.dataset.baseWidth) || svg.getBoundingClientRect().width || 800;
        const height = parseFloat(svg.dataset.baseHeight) || svg.getBoundingClientRect().height || 420;

        diagram.style.width = `${width * zoom}px`;
        diagram.style.height = `${height * zoom}px`;
        svg.style.transform = `scale(${zoom})`;
        frame.dataset.zoom = `${Math.round(zoom * 100)}%`;
      });

      zoomValue.textContent = `${Math.round(zoom * 100)}%`;
      zoomOutButton.disabled = zoom <= .25 || !state.diagramTotal;
      zoomInButton.disabled = zoom >= 3 || !state.diagramTotal;
      resetZoomButton.disabled = !state.diagramTotal || zoom === 1;
      fitZoomButton.disabled = !state.diagramTotal;
    }

    function fitDiagramsToWidth() {
      const frames = [...preview.querySelectorAll('.diagram-frame')];
      if (!frames.length) return;

      const availableWidth = Math.max(preview.clientWidth - 72, 160);
      const widest = frames.reduce((max, frame) => {
        const svg = frame.querySelector('svg');
        const width = parseFloat(svg?.dataset.baseWidth ?? '0');
        return Math.max(max, width);
      }, 0);

      if (!widest) return;
      setDiagramZoom(availableWidth / widest);
    }

    function updateDiagramControls(count) {
      state.diagramTotal = count;
      diagramCount.textContent = `${count} diagram${count === 1 ? '' : 's'}`;
      applyDiagramZoom();
    }

    return {
      renderPreview,
      buildMarkdownHtml,
      buildMermaidOnlyHtml,
      renderMermaidDiagrams,
      prepareDiagramFramesIn,
      prepareTableBlocksIn,
      getSvgBaseSize,
      setDiagramZoom,
      applyDiagramZoom,
      fitDiagramsToWidth,
      updateDiagramControls,
      resolveMode,
      resolveModeFor,
    };
}
