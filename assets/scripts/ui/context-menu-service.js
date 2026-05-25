export function createContextMenuService({
  state,
  dom,
  callbacks,
}) {
  const { editor, preview } = dom;
  const {
    closeOpenMenus,
    copyCodeBlock,
    copyDiagramSource,
    copyRenderedHtml,
    copyRenderedText,
    copyTableBlock,
    downloadTableCsv,
    copyToClipboard,
    executeMarkdownCommand,
    exportDiagramFramePng,
    exportDiagramFrameSvg,
    focusEditorAtSource,
    getEditorSelection,
    hideMermaidAutocomplete,
    isActiveReadOnly,
    redoEditorChange,
    replaceEditorRange,
    setStatus,
    undoEditorChange,
  } = callbacks;

  let menu = null;
  let lastInvoker = null;

  function installContextMenuHandlers() {
    menu = document.createElement('div');
    menu.className = 'context-menu';
    menu.setAttribute('role', 'menu');
    menu.setAttribute('aria-label', 'Context menu');
    menu.hidden = true;
    document.body.appendChild(menu);

    document.addEventListener('contextmenu', handleContextMenu);
    document.addEventListener('pointerdown', handleDocumentPointerDown, true);
    document.addEventListener('scroll', handleDocumentScroll, true);
    window.addEventListener('resize', closeContextMenu);
    window.addEventListener('blur', closeContextMenu);
    window.addEventListener('keydown', handleWindowKeydown);
    menu.addEventListener('keydown', handleMenuKeydown);
  }

  function handleContextMenu(event) {
    const target = getElementTarget(event.target);
    if (!target) return;

    if (isNativeContextTarget(target)) {
      closeContextMenu();
      return;
    }

    const items = getContextItems(target);
    if (!items.length) {
      closeContextMenu();
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    lastInvoker = target;
    closeOpenMenus();
    hideMermaidAutocomplete();
    openContextMenu(items, event.clientX, event.clientY);
  }

  function getContextItems(target) {
    if (target === editor) {
      return getEditorItems();
    }

    if (preview.contains(target)) {
      return getPreviewItems(target);
    }

    return [];
  }

  function getEditorItems() {
    const readOnly = isActiveReadOnly();
    const selection = getEditorSelection();
    const hasSelection = Boolean(selection.text);
    const disableMutation = readOnly;

    return cleanMenuItems([
      item('editor-undo', 'Undo', () => undoEditorChange(), {
        disabled: disableMutation || !state.editorHistory.undo.length,
      }),
      item('editor-redo', 'Redo', () => redoEditorChange(), {
        disabled: disableMutation || !state.editorHistory.redo.length,
      }),
      separator(),
      item('editor-copy', 'Copy', copyEditorSelection, { disabled: !hasSelection }),
      item('editor-cut', 'Cut', cutEditorSelection, { disabled: disableMutation || !hasSelection }),
      item('editor-paste-text', 'Paste as text', pasteEditorText, { disabled: disableMutation }),
      separator(),
      commandItem('editor-bold', 'Bold', 'bold', disableMutation),
      commandItem('editor-italic', 'Italic', 'italic', disableMutation),
      commandItem('editor-link', 'Link', 'link', disableMutation),
      commandItem('editor-inline-code', 'Inline code', 'inlineCode', disableMutation),
      separator(),
      commandItem('editor-heading', 'Heading', 'heading', disableMutation),
      commandItem('editor-bullet-list', 'Bullet list', 'bulletList', disableMutation),
      commandItem('editor-numbered-list', 'Numbered list', 'numberedList', disableMutation),
      commandItem('editor-task-list', 'Checklist item', 'taskList', disableMutation),
      separator(),
      commandItem('editor-code-block', 'Code block', 'codeBlock', disableMutation),
      commandItem('editor-mermaid-block', 'Mermaid block', 'mermaidBlock', disableMutation),
      commandItem('editor-table', 'Table', 'table', disableMutation),
    ]);
  }

  function getPreviewItems(target) {
    const frame = getDiagramFrame(target);
    const codeCopyButton = target.closest('.code-block')?.querySelector('[data-code-action="copy"]');
    const tableCopyButton = target.closest('.table-block')?.querySelector('[data-table-action="copy"]');
    const tableCsvButton = target.closest('.table-block')?.querySelector('[data-table-action="downloadCsv"]');
    const renderedSvg = frame?.querySelector('.mermaid svg, svg');
    const diagramSource = frame?.dataset.diagramSource || frame?.querySelector('.mermaid')?.dataset.source || '';

    return cleanMenuItems([
      frame ? item('preview-copy-diagram-source', 'Copy Mermaid source', () => copyDiagramSource(frame), {
        disabled: !diagramSource.trim(),
      }) : null,
      frame ? item('preview-export-diagram-svg', 'Export diagram as SVG', () => exportDiagramFrameSvg(frame), {
        disabled: !renderedSvg,
      }) : null,
      frame ? item('preview-export-diagram-png', 'Export diagram as PNG', () => exportDiagramFramePng(frame), {
        disabled: !renderedSvg,
      }) : null,
      frame ? item('preview-jump-diagram-source', 'Jump to source', () => focusEditorAtSource(diagramSource), {
        disabled: !diagramSource.trim(),
      }) : null,
      frame ? separator() : null,
      codeCopyButton ? item('preview-copy-code', 'Copy code block', () => copyCodeBlock(codeCopyButton)) : null,
      tableCopyButton ? item('preview-copy-table', 'Copy table for Excel', () => copyTableBlock(tableCopyButton)) : null,
      tableCsvButton ? item('preview-download-table-csv', 'Download table as CSV', () => downloadTableCsv(tableCsvButton)) : null,
      (codeCopyButton || tableCopyButton || tableCsvButton) ? separator() : null,
      item('preview-copy-text', 'Copy rendered text', () => copyRenderedText()),
      item('preview-copy-html', 'Copy rendered HTML', () => copyRenderedHtml()),
    ]);
  }

  function commandItem(id, label, command, disabled) {
    return item(id, label, () => {
      editor.focus();
      executeMarkdownCommand(command);
    }, { disabled });
  }

  function item(id, label, action, options = {}) {
    return {
      id,
      label,
      action,
      disabled: Boolean(options.disabled),
    };
  }

  function separator() {
    return { separator: true };
  }

  function cleanMenuItems(items) {
    const cleaned = [];
    for (const entry of items.filter(Boolean)) {
      if (entry.separator) {
        if (!cleaned.length || cleaned[cleaned.length - 1].separator) continue;
        cleaned.push(entry);
        continue;
      }
      cleaned.push(entry);
    }

    while (cleaned[cleaned.length - 1]?.separator) cleaned.pop();
    return cleaned;
  }

  function openContextMenu(items, clientX, clientY) {
    renderMenuItems(items);
    menu.hidden = false;
    menu.style.visibility = 'hidden';
    menu.style.left = '0px';
    menu.style.top = '0px';

    const rect = menu.getBoundingClientRect();
    const gap = 8;
    const left = Math.max(gap, Math.min(clientX, window.innerWidth - rect.width - gap));
    const top = Math.max(gap, Math.min(clientY, window.innerHeight - rect.height - gap));
    menu.style.left = `${left}px`;
    menu.style.top = `${top}px`;
    menu.style.visibility = '';

    getEnabledButtons()[0]?.focus();
  }

  function renderMenuItems(items) {
    menu.innerHTML = '';
    items.forEach((entry) => {
      if (entry.separator) {
        const separatorElement = document.createElement('div');
        separatorElement.className = 'context-menu-separator';
        separatorElement.setAttribute('role', 'separator');
        menu.appendChild(separatorElement);
        return;
      }

      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'context-menu-item';
      button.setAttribute('role', 'menuitem');
      button.dataset.contextMenuAction = entry.id;
      button.textContent = entry.label;
      button.disabled = entry.disabled;
      button.addEventListener('click', async () => {
        if (button.disabled) return;
        closeContextMenu({ restoreFocus: false });
        await entry.action();
      });
      menu.appendChild(button);
    });
  }

  function closeContextMenu(options = {}) {
    if (!menu || menu.hidden) return;
    menu.hidden = true;
    menu.innerHTML = '';

    if (options.restoreFocus && lastInvoker instanceof HTMLElement) {
      lastInvoker.focus();
    }
    lastInvoker = null;
  }

  function handleDocumentPointerDown(event) {
    if (!menu || menu.hidden) return;
    const target = getElementTarget(event.target);
    if (target && menu.contains(target)) return;
    closeContextMenu();
  }

  function handleDocumentScroll(event) {
    if (!menu || menu.hidden) return;
    const target = getElementTarget(event.target);
    if (target && menu.contains(target)) return;
    closeContextMenu();
  }

  function handleWindowKeydown(event) {
    if (event.key === 'Escape') {
      closeContextMenu();
    }
  }

  function handleMenuKeydown(event) {
    const buttons = getEnabledButtons();
    if (!buttons.length) {
      if (event.key === 'Escape' || event.key === 'Tab') {
        event.preventDefault();
        closeContextMenu();
      }
      return;
    }

    const currentIndex = Math.max(0, buttons.indexOf(document.activeElement));
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      buttons[(currentIndex + 1) % buttons.length].focus();
      return;
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      buttons[(currentIndex - 1 + buttons.length) % buttons.length].focus();
      return;
    }

    if (event.key === 'Home') {
      event.preventDefault();
      buttons[0].focus();
      return;
    }

    if (event.key === 'End') {
      event.preventDefault();
      buttons[buttons.length - 1].focus();
      return;
    }

    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      document.activeElement?.click();
      return;
    }

    if (event.key === 'Escape' || event.key === 'Tab') {
      event.preventDefault();
      closeContextMenu();
    }
  }

  function getEnabledButtons() {
    return [...menu.querySelectorAll('.context-menu-item:not(:disabled)')];
  }

  async function copyEditorSelection() {
    const selection = getEditorSelection();
    if (!selection.text) {
      setStatus('No editor selection to copy.', 'warning');
      return;
    }
    await copyToClipboard(selection.text, 'Selection copied.', 'Could not copy selection.');
  }

  async function cutEditorSelection() {
    if (isActiveReadOnly()) {
      setStatus('This guide is read-only. Open or create a Markdown file to edit.', 'warning');
      return;
    }

    const selection = getEditorSelection();
    if (!selection.text) {
      setStatus('No editor selection to cut.', 'warning');
      return;
    }

    try {
      await navigator.clipboard.writeText(selection.text);
      replaceEditorRange(selection.start, selection.end, '', selection.start, selection.start);
      setStatus('Selection cut.', 'ok');
    } catch (error) {
      setStatus('Could not cut selection.', 'danger');
      console.error(error);
    }
  }

  async function pasteEditorText() {
    if (isActiveReadOnly()) {
      setStatus('This guide is read-only. Open or create a Markdown file to edit.', 'warning');
      return;
    }

    try {
      const text = await navigator.clipboard.readText();
      if (!text) {
        setStatus('No readable text found on clipboard.', 'warning');
        return;
      }

      const selection = getEditorSelection();
      replaceEditorRange(selection.start, selection.end, text, selection.start + text.length, selection.start + text.length);
      setStatus('Plain text pasted.', 'ok');
    } catch (error) {
      setStatus('Clipboard access blocked. Use the next paste to insert text.', 'warning');
      console.error(error);
    }
  }

  function getDiagramFrame(target) {
    return target.closest('.diagram-frame') || target.closest('.mermaid')?.closest('.diagram-frame') || null;
  }

  function isNativeContextTarget(target) {
    if (target === editor) return false;
    if (target.closest('dialog')) return true;
    if (target.closest('input, textarea, select, [contenteditable="true"]')) return true;
    if (preview.contains(target) && target.closest('a[href]')) return true;
    return false;
  }

  function getElementTarget(target) {
    if (target instanceof Element) return target;
    if (target instanceof Node) return target.parentElement;
    return null;
  }

  return {
    installContextMenuHandlers,
    closeContextMenu,
  };
}
