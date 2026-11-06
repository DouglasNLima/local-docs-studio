// A slash is a command only on an otherwise empty prose line. Escape leaves it
// literal. Parsing is bounded and is never tied to preview rendering.
export const slashCommands = [
  ...Array.from({ length: 6 }, (_, index) => ({ id: `heading:${index + 1}`, label: `Heading ${index + 1}` })),
  ...Object.entries({ paragraph: 'Paragraph', bulletList: 'Bullet list', numberedList: 'Numbered list', taskList: 'Checklist',
    quote: 'Quote', codeBlock: 'Code block', mermaidBlock: 'Mermaid diagram', table: 'Table', link: 'Link',
    documentLink: 'Document link', image: 'Image', imageFigure: 'Image figure', callout: 'Callout',
    detailsBlock: 'Collapsible section', horizontalRule: 'Horizontal rule', dateTime: 'Date/time', snippet: 'Built-in snippet' })
    .map(([id, label]) => ({ id, label })),
];

export function slashContext(source, caret, previous = null) {
  if (source.length > 4 * 1024 * 1024 || caret < 0 || caret > source.length) return null;
  const line = source.lastIndexOf('\n', caret - 1) + 1;
  const match = /^ {0,3}\/([a-z0-9 -]{0,64})$/i.exec(source.slice(line, caret));
  if (!match || /[^\r\n]/.test(source.slice(caret, source.indexOf('\n', caret) < 0 ? source.length : source.indexOf('\n', caret)))) return null;
  const prefix = source.slice(0, line);
  const result = { start: line + match[0].indexOf('/'), end: caret, text: '', query: match[1], prefix };
  if (previous?.prefix === prefix) return result;
  let fence = null; let frontMatter = false; let html = false; let rawTag = '';
  for (const [index, row] of source.slice(0, line).split('\n').entries()) {
    if (index === 0 && row.trimEnd() === '---') { frontMatter = true; continue; }
    if (frontMatter) { if (/^(---|\.\.\.)\r?$/.test(row)) frontMatter = false; continue; }
    if (rawTag) { if (row.toLowerCase().includes(`</${rawTag}>`)) rawTag = ''; continue; }
    if (html) { if (row.includes('-->')) html = false; continue; }
    const opening = /^ {0,3}(`{3,}|~{3,}|:::)(.*)$/.exec(row);
    if (opening) {
      if (!fence) fence = { marker: opening[1][0], size: opening[1].length };
      else if (opening[1][0] === fence.marker && opening[1].length >= fence.size && !opening[2].trim()) fence = null;
      continue;
    }
    if (fence) continue;
    if (/^\s*<!--/.test(row)) html = true;
    if (row.includes('-->')) html = false;
    const tag = /^\s*<(pre|code|script|style|textarea)(?:\s|>)/i.exec(row)?.[1]?.toLowerCase();
    if (tag && !row.toLowerCase().includes(`</${tag}>`)) rawTag = tag;
  }
  return fence || html || rawTag || frontMatter ? null : result;
}

export function createSlashCommandService({ editor, state, execute, available = () => '', closeOther = () => {} }) {
  const popup = document.createElement('div'); popup.id = 'slashCommands'; popup.className = 'slash-commands';
  popup.hidden = true; popup.setAttribute('role', 'listbox'); popup.setAttribute('aria-label', 'Markdown slash commands');
  const announcement = document.createElement('span'); announcement.className = 'slash-announcement'; announcement.setAttribute('aria-live', 'polite');
  document.body.append(popup, announcement);
  let context = null; let items = []; let selected = 0; let composing = false; let dismissed = false; let path = null;
  let frame = 0;
  let anchor = null;
  function close() {
    context = null; anchor = null; popup.hidden = true; editor.removeAttribute('aria-activedescendant'); editor.removeAttribute('aria-controls');
    announcement.textContent = '';
  }
  function refresh(event) {
    if (event?.isComposing || composing || editor.readOnly || editor.disabled || editor.selectionStart !== editor.selectionEnd
        || /\.(mmd|mermaid)$/i.test(state.activePath || state.fileName || '')) { close(); return; }
    if (event?.inputType === 'insertText' && event.data === '/') dismissed = false;
    if (dismissed || event && !event.inputType) { close(); return; }
    if (!context && event?.inputType !== 'insertText') { close(); return; }
    const next = slashContext(editor.value, editor.selectionStart, context);
    if (!next) { close(); return; }
    closeOther(); context = next; path = state.activePath;
    items = slashCommands.filter(item => item.label.toLowerCase().includes(next.query.toLowerCase())); selected = 0;
    render();
  }
  function render() {
    popup.replaceChildren(); popup.hidden = false;
    if (!items.length) { popup.textContent = 'No matching commands. Escape keeps the text.'; announcement.textContent = popup.textContent; }
    items.forEach((item, index) => {
      const row = document.createElement('div'); row.id = `slash-command-${index}`; row.dataset.index = String(index);
      row.setAttribute('role', 'option'); row.setAttribute('aria-selected', String(index === selected));
      const reason = available(item.id); row.setAttribute('aria-disabled', String(Boolean(reason)));
      row.textContent = item.label + (reason ? ` — ${reason}` : ''); popup.append(row);
    });
    editor.setAttribute('aria-controls', popup.id);
    const active = popup.querySelector('[aria-selected="true"]');
    if (active) { editor.setAttribute('aria-activedescendant', active.id); announcement.textContent = active.textContent; }
    position(); active?.scrollIntoView({ block: 'nearest' });
  }
  function position() {
    if (popup.hidden || frame) return;
    frame = requestAnimationFrame(() => {
      frame = 0; if (popup.hidden) return;
      const style = getComputedStyle(editor); const rect = editor.getBoundingClientRect();
      if (!anchor || anchor.start !== context.start || anchor.width !== editor.clientWidth || anchor.font !== style.font) {
        const mirror = document.createElement('div'); mirror.className = 'slash-caret-mirror';
        for (const key of ['font', 'lineHeight', 'padding', 'letterSpacing', 'tabSize', 'whiteSpace', 'overflowWrap', 'wordBreak']) mirror.style[key] = style[key];
        mirror.style.width = `${editor.clientWidth}px`;
        const marker = document.createElement('span'); marker.textContent = '\u200b';
        mirror.append(document.createTextNode(editor.value.slice(0, context.start)), marker); document.body.append(mirror);
        anchor = { start: context.start, width: editor.clientWidth, font: style.font, x: marker.offsetLeft, y: marker.offsetTop }; mirror.remove();
      }
      const x = rect.left + anchor.x - editor.scrollLeft;
      const y = rect.top + anchor.y - editor.scrollTop; const height = parseFloat(style.lineHeight) || 20;
      popup.style.maxHeight = `${Math.max(80, Math.min(280, innerHeight - 24))}px`;
      popup.style.left = `${Math.max(8, Math.min(x, innerWidth - popup.offsetWidth - 8))}px`;
      popup.style.top = `${Math.max(8, Math.min(y + height + popup.offsetHeight > innerHeight - 8 ? y - popup.offsetHeight : y + height, innerHeight - popup.offsetHeight - 8))}px`;
    });
  }
  function accept() {
    const item = items[selected]; const current = slashContext(editor.value, editor.selectionStart);
    if (!item || available(item.id) || !context || path !== state.activePath || !current || current.start !== context.start || editor.readOnly) return;
    const range = context; close(); dismissed = true; execute(item.id, range);
  }
  function keydown(event) {
    if (composing || event.isComposing || popup.hidden) return false;
    if (event.key === 'Escape') { dismissed = true; close(); }
    else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      selected = (selected + (event.key === 'ArrowDown' ? 1 : -1) + items.length) % (items.length || 1); render();
    } else if (event.key === 'Enter') accept(); else { if (['ArrowLeft', 'ArrowRight', 'Home', 'End', 'PageUp', 'PageDown', 'Tab'].includes(event.key)) close(); return false; }
    event.preventDefault(); event.stopPropagation(); return true;
  }
  popup.addEventListener('pointerdown', event => event.preventDefault());
  popup.addEventListener('click', event => { const row = event.target.closest('[data-index]'); if (row) { selected = Number(row.dataset.index); accept(); } });
  editor.addEventListener('input', refresh);
  editor.addEventListener('compositionstart', () => { composing = true; close(); });
  editor.addEventListener('compositionend', () => { composing = false; dismissed = true; close(); });
  editor.addEventListener('blur', close);
  editor.addEventListener('click', () => { dismissed = true; close(); });
  editor.addEventListener('scroll', position); window.addEventListener('resize', position);
  editor.addEventListener('lens-buffer-changed', () => { if (path !== state.activePath) close(); });
  return { keydown, close };
}
