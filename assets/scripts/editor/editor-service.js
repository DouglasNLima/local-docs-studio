import { isPositionInsideDevOpsMermaidBlock } from '../utils/devops-markdown.js';

const HIGHLIGHT_MODULE_PATH = '../../vendor/highlight-11.11.1.esm.js';

export function createEditorService({ editor, state, dom = {}, callbacks = {} }) {
    const { editorShell, editorLineNumbers, editorSyntaxLayer, mermaidAutocomplete } = dom;
    const {
      commandHandlers = {},
      getAutocompleteFiles = () => state.files,
    } = callbacks;
    const autocompleteState = {
      open: false,
      items: [],
      index: 0,
      prefixStart: 0,
      prefixEnd: 0,
      mode: 'mermaid',
    };
    const mermaidSuggestions = [
      {
        label: 'flowchart TD',
        detail: 'Flowchart starter',
        triggers: ['flow', 'flowchart', 'graph'],
        insert: 'flowchart TD\n  A[Start] --> B[Finish]',
      },
      {
        label: 'sequenceDiagram',
        detail: 'Sequence diagram starter',
        triggers: ['seq', 'sequence', 'participant'],
        insert: 'sequenceDiagram\n  participant User\n  participant System\n  User->>System: Request\n  System-->>User: Response',
      },
      {
        label: 'participant',
        detail: 'Sequence participant',
        triggers: ['part', 'participant'],
        insert: 'participant User',
      },
      {
        label: 'subgraph',
        detail: 'Flowchart grouping',
        triggers: ['sub', 'subgraph'],
        insert: 'subgraph Group\n  A[Item]\nend',
      },
      {
        label: 'arrow',
        detail: 'Flowchart arrow',
        triggers: ['arrow', '--', '->'],
        insert: '-->',
      },
      {
        label: 'note over',
        detail: 'Sequence note',
        triggers: ['note', 'over'],
        insert: 'Note over User,System: Important detail',
      },
      {
        label: 'alt / else',
        detail: 'Sequence branch',
        triggers: ['alt', 'else'],
        insert: 'alt condition\n  User->>System: Primary path\nelse fallback\n  System-->>User: Alternate path\nend',
      },
      {
        label: 'loop',
        detail: 'Sequence loop',
        triggers: ['loop'],
        insert: 'loop each item\n  User->>System: Repeat action\nend',
      },
      {
        label: 'classDef',
        detail: 'Flowchart style class',
        triggers: ['class', 'classdef'],
        insert: 'classDef primary fill:#FFF1E8,stroke:#FF883E,color:#111827',
      },
      {
        label: 'style node',
        detail: 'Style a node',
        triggers: ['style'],
        insert: 'style A fill:#FFF1E8,stroke:#FF883E,color:#111827',
      },
      {
        label: 'stateDiagram-v2',
        detail: 'State diagram starter',
        triggers: ['state'],
        insert: 'stateDiagram-v2\n  [*] --> Draft\n  Draft --> Review\n  Review --> [*]',
      },
      {
        label: 'erDiagram',
        detail: 'Entity relationship starter',
        triggers: ['er', 'entity'],
        insert: 'erDiagram\n  USER ||--o{ ORDER : places\n  ORDER ||--|{ LINE_ITEM : contains',
      },
      {
        label: 'gantt task',
        detail: 'Gantt task line',
        triggers: ['gantt', 'task'],
        insert: 'section Delivery\n  Design :done, des1, 2026-01-01, 3d\n  Build :active, build1, after des1, 5d',
      },
    ];
    let editorHighlightPromise = null;
    let editorHighlight = null;
    let syntaxHighlightFrame = 0;

    function installEditorEnhancements() {
      if (editorShell && editorSyntaxLayer) {
        editorShell.classList.add('syntax-highlight-enabled');
      }
      updateEditorChrome();
      editor.addEventListener('scroll', syncLineNumbers);
      editor.addEventListener('scroll', syncSyntaxLayer);
      window.addEventListener('resize', updateEditorChrome);
      mermaidAutocomplete?.addEventListener('mousedown', (event) => {
        event.preventDefault();
      });
      mermaidAutocomplete?.addEventListener('click', (event) => {
        const button = event.target.closest('[data-autocomplete-index]');
        if (!button) return;
        autocompleteState.index = Number(button.dataset.autocompleteIndex) || 0;
        acceptAutocomplete();
      });
    }

    function updateEditorChrome() {
      updateLineNumbers();
      syncLineNumbers();
      syncSyntaxLayer();
      scheduleEditorSyntaxHighlight();
      if (autocompleteState.open) {
        renderMermaidAutocomplete();
      }
    }

    function updateLineNumbers() {
      if (!editorLineNumbers) return;
      const lineCount = Math.max(editor.value.split('\n').length, 1);
      const current = Number(editorLineNumbers.dataset.lineCount || '0');
      if (current === lineCount) return;
      editorLineNumbers.dataset.lineCount = String(lineCount);
      editorLineNumbers.textContent = Array.from({ length: lineCount }, (_, index) => String(index + 1)).join('\n');
    }

    function syncLineNumbers() {
      if (!editorLineNumbers) return;
      editorLineNumbers.scrollTop = editor.scrollTop;
    }

    function syncSyntaxLayer() {
      if (!editorSyntaxLayer) return;
      editorSyntaxLayer.scrollTop = editor.scrollTop;
      editorSyntaxLayer.scrollLeft = editor.scrollLeft;
    }

    function scheduleEditorSyntaxHighlight() {
      if (!editorSyntaxLayer) return;
      if (syntaxHighlightFrame) return;
      syntaxHighlightFrame = window.requestAnimationFrame(async () => {
        syntaxHighlightFrame = 0;
        await renderEditorSyntaxHighlight();
      });
    }

    async function renderEditorSyntaxHighlight() {
      if (!editorSyntaxLayer) return;
      const value = editor.value;
      if (!value) {
        editorSyntaxLayer.textContent = '';
        return;
      }

      try {
        const hljs = await loadEditorHighlight();
        if (value !== editor.value) {
          scheduleEditorSyntaxHighlight();
          return;
        }
        const language = getEditorHighlightLanguage();
        const highlighted = hljs.getLanguage(language)
          ? hljs.highlight(value, { language, ignoreIllegals: true }).value
          : escapeHtml(value);
        editorSyntaxLayer.innerHTML = value.endsWith('\n') ? `${highlighted}\n` : highlighted;
      } catch {
        editorSyntaxLayer.textContent = value.endsWith('\n') ? `${value}\n` : value;
      }
      syncSyntaxLayer();
    }

    async function loadEditorHighlight() {
      if (!editorHighlightPromise) {
        editorHighlightPromise = import(HIGHLIGHT_MODULE_PATH).then((module) => module.default ?? module);
      }
      if (!editorHighlight) {
        editorHighlight = await editorHighlightPromise;
      }
      return editorHighlight;
    }

    function getEditorHighlightLanguage() {
      const name = state.fileName || state.activePath || '';
      if (/\.(mmd|mermaid)$/i.test(name)) return 'markdown';
      return 'markdown';
    }

    function handleMermaidAutocompleteKeydown(event) {
      const isShortcut = event.ctrlKey || event.metaKey;
      if (isShortcut && (event.key === ' ' || event.code === 'Space')) {
        if (showMermaidAutocomplete(true)) {
          event.preventDefault();
          return true;
        }
      }

      if (!autocompleteState.open) return false;

      if (event.key === 'Escape') {
        event.preventDefault();
        hideMermaidAutocomplete();
        return true;
      }

      if (event.key === 'ArrowDown') {
        event.preventDefault();
        moveAutocompleteSelection(1);
        return true;
      }

      if (event.key === 'ArrowUp') {
        event.preventDefault();
        moveAutocompleteSelection(-1);
        return true;
      }

      if (event.key === 'Enter' || event.key === 'Tab') {
        event.preventDefault();
        acceptAutocomplete();
        return true;
      }

      return false;
    }

    function updateMermaidAutocompleteFromInput() {
      updateEditorChrome();
      if (autocompleteState.open) {
        showAutocomplete(false);
        return;
      }

      const wikilinkContext = getWikilinkAutocompleteContext();
      if (wikilinkContext && wikilinkContext.prefix.length >= 1) {
        showAutocomplete(false);
        return;
      }

      const context = getMermaidAutocompleteContext();
      if (!context.inMermaid || context.prefix.length < 2) {
        hideMermaidAutocomplete();
        return;
      }

      showAutocomplete(false);
    }

    function showMermaidAutocomplete(force = false) {
      return showAutocomplete(force);
    }

    function showAutocomplete(force = false) {
      if (!mermaidAutocomplete) return false;
      const wikilinkContext = getWikilinkAutocompleteContext();
      if (wikilinkContext) {
        const items = getWikilinkSuggestions(wikilinkContext, force);
        if (items.length) {
          autocompleteState.open = true;
          autocompleteState.items = items;
          autocompleteState.index = Math.min(autocompleteState.index, items.length - 1);
          autocompleteState.prefixStart = wikilinkContext.prefixStart;
          autocompleteState.prefixEnd = wikilinkContext.prefixEnd;
          autocompleteState.mode = 'wikilink';
          renderMermaidAutocomplete();
          return true;
        }
      }

      const context = getMermaidAutocompleteContext();
      if (!context.inMermaid) {
        hideMermaidAutocomplete();
        return false;
      }

      const prefix = context.prefix.toLowerCase();
      const items = mermaidSuggestions.filter((item) => {
        if (!prefix) return force;
        return item.label.toLowerCase().includes(prefix)
          || item.triggers.some((trigger) => trigger.toLowerCase().startsWith(prefix) || trigger.toLowerCase().includes(prefix));
      });

      if (!items.length) {
        hideMermaidAutocomplete();
        return false;
      }

      autocompleteState.open = true;
      autocompleteState.items = items;
      autocompleteState.index = Math.min(autocompleteState.index, items.length - 1);
      autocompleteState.prefixStart = context.prefixStart;
      autocompleteState.prefixEnd = context.prefixEnd;
      autocompleteState.mode = 'mermaid';
      renderMermaidAutocomplete();
      return true;
    }

    function renderMermaidAutocomplete() {
      if (!mermaidAutocomplete || !autocompleteState.open) return;
      mermaidAutocomplete.hidden = false;
      mermaidAutocomplete.innerHTML = autocompleteState.items.map((item, index) => `
        <button type="button" role="option" class="${index === autocompleteState.index ? 'active' : ''}" aria-selected="${index === autocompleteState.index}" data-autocomplete-index="${index}">
          <strong>${escapeHtml(item.label)}</strong>
          <span>${escapeHtml(item.detail)}</span>
        </button>`).join('');
      mermaidAutocomplete.querySelector('.active')?.scrollIntoView({ block: 'nearest' });
    }

    function hideMermaidAutocomplete() {
      autocompleteState.open = false;
      autocompleteState.items = [];
      autocompleteState.index = 0;
      if (mermaidAutocomplete) {
        mermaidAutocomplete.hidden = true;
        mermaidAutocomplete.innerHTML = '';
      }
    }

    function moveAutocompleteSelection(delta) {
      const total = autocompleteState.items.length;
      if (!total) return;
      autocompleteState.index = (autocompleteState.index + delta + total) % total;
      renderMermaidAutocomplete();
    }

    function acceptAutocomplete() {
      const item = autocompleteState.items[autocompleteState.index];
      if (!item) return;
      const start = autocompleteState.prefixStart;
      const end = autocompleteState.prefixEnd;
      hideMermaidAutocomplete();
      replaceEditorRange(start, end, item.insert, start + item.insert.length, start + item.insert.length);
    }

    function getWikilinkAutocompleteContext() {
      const position = editor.selectionStart;
      const value = editor.value;
      const lineStart = value.lastIndexOf('\n', Math.max(position - 1, 0)) + 1;
      const lineBeforeCaret = value.slice(lineStart, position);
      const openIndex = lineBeforeCaret.lastIndexOf('[[');
      if (openIndex === -1) return null;
      const afterOpen = lineBeforeCaret.slice(openIndex + 2);
      if (afterOpen.includes(']]') || afterOpen.includes('\n')) return null;
      const next = value.slice(position, position + 2);
      return {
        prefix: afterOpen,
        prefixStart: lineStart + openIndex + 2,
        prefixEnd: position,
        hasClosing: next === ']]',
      };
    }

    function getWikilinkSuggestions(context, force) {
      const prefix = context.prefix.toLowerCase();
      const files = getAutocompleteFiles();
      return files
        .filter((file) => file.path !== state.activePath)
        .filter((file) => {
          if (!prefix) return force;
          return file.path.toLowerCase().includes(prefix) || file.name.toLowerCase().includes(prefix);
        })
        .slice(0, 12)
        .map((file) => ({
          label: file.path.replace(/\.(md|markdown|mmd|mermaid)$/i, ''),
          detail: 'Wikilink target',
          insert: `${file.path.replace(/\.(md|markdown|mmd|mermaid)$/i, '')}${context.hasClosing ? '' : ']]'}`,
        }));
    }

    function getMermaidAutocompleteContext() {
      const position = editor.selectionStart;
      const value = editor.value;
      const lineStart = value.lastIndexOf('\n', Math.max(position - 1, 0)) + 1;
      const lineBeforeCaret = value.slice(lineStart, position);
      const prefixMatch = lineBeforeCaret.match(/([A-Za-z][\w-]*|[-.=]+>?)$/);
      const prefix = prefixMatch?.[0] ?? '';
      const prefixStart = position - prefix.length;
      const standalone = /\.(mmd|mermaid)$/i.test(state.fileName || '');

      return {
        inMermaid: standalone || isCaretInsideMermaidFence(value, position) || isPositionInsideDevOpsMermaidBlock(value, position),
        prefix,
        prefixStart,
        prefixEnd: position,
      };
    }

    function isCaretInsideMermaidFence(value, position) {
      const before = value.slice(0, position);
      const lines = before.split('\n');
      let inFence = false;
      let mermaidFence = false;

      for (const line of lines) {
        const fence = line.match(/^```\s*([^\s`]*)/);
        if (!fence) continue;
        if (!inFence) {
          inFence = true;
          mermaidFence = ['mermaid', 'mmd'].includes((fence[1] || '').toLowerCase());
        } else {
          inFence = false;
          mermaidFence = false;
        }
      }

      return inFence && mermaidFence;
    }

    function escapeHtml(value) {
      return String(value)
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
    }

    function recordEditorHistoryInput(event) {
      const current = captureEditorSnapshot();
      const last = state.editorHistory.last;
      const inputType = event?.inputType ?? '';
      const insertedText = event?.data ?? '';
      const isTypingInput = /^(insertText|insertCompositionText|deleteContentBackward|deleteContentForward)$/.test(inputType)
        && insertedText.length <= 1;
      const now = Date.now();

      if (!last) {
        state.editorHistory.last = current;
        return;
      }

      if (current.value === last.value) {
        state.editorHistory.last = current;
        return;
      }

      if (isTypingInput) {
        const sameTypingGroup = state.editorHistory.typing && now - state.editorHistory.lastTypedAt < 1200;
        if (!sameTypingGroup) {
          pushEditorUndoSnapshot(last);
        }
        state.editorHistory.typing = true;
        state.editorHistory.lastTypedAt = now;
      } else {
        state.editorHistory.typing = false;
        pushEditorUndoSnapshot(last);
      }

      state.editorHistory.redo = [];
      state.editorHistory.last = current;
    }

    function pushEditorUndoSnapshot(snapshot) {
      state.editorHistory.undo.push(snapshot);
      if (state.editorHistory.undo.length > state.editorHistory.max) {
        state.editorHistory.undo.shift();
      }
    }

    function resetEditorHistory() {
      state.editorHistory.undo = [];
      state.editorHistory.redo = [];
      state.editorHistory.last = captureEditorSnapshot();
      state.editorHistory.typing = false;
      state.editorHistory.lastTypedAt = 0;
      updateEditorChrome();
      hideMermaidAutocomplete();
    }

    function undoEditorChange() {
      const target = state.editorHistory.undo.pop();
      if (!target) return;

      state.editorHistory.typing = false;
      state.editorHistory.redo.push(captureEditorSnapshot());
      restoreEditorSnapshot(target);
    }

    function redoEditorChange() {
      const target = state.editorHistory.redo.pop();
      if (!target) return;

      state.editorHistory.typing = false;
      state.editorHistory.undo.push(captureEditorSnapshot());
      restoreEditorSnapshot(target);
    }

    function captureEditorSnapshot() {
      return {
        value: editor.value,
        start: editor.selectionStart,
        end: editor.selectionEnd,
      };
    }

    function restoreEditorSnapshot(snapshot) {
      state.editorHistory.suppress = true;
      editor.value = snapshot.value;
      editor.focus();
      editor.setSelectionRange(snapshot.start, snapshot.end);
      state.editorHistory.last = captureEditorSnapshot();
      editor.dispatchEvent(new Event('input', { bubbles: true }));
      state.editorHistory.suppress = false;
      updateEditorChrome();
    }

    function handleEditorToolbarClick(event) {
      const button = event.target.closest('[data-command]');
      if (!button) return;

      event.preventDefault();
      executeMarkdownCommand(button.dataset.command);
    }

    function executeMarkdownCommand(command) {
      const commands = {
        bold: () => toggleInlineWrap('**', '**', 'bold text'),
        italic: () => toggleInlineWrap('*', '*', 'italic text'),
        strikethrough: () => toggleInlineWrap('~~', '~~', 'struck text'),
        inlineCode: () => toggleInlineWrap('`', '`', 'code'),
        heading: () => transformSelectedLines((line) => {
          const text = line.replace(/^#{1,6}\s+/, '');
          return text.trim() ? `# ${text}` : '# ';
        }),
        bulletList: () => transformSelectedLines((line) => {
          if (!line.trim()) return line;
          return /^(\s*)[-*+]\s+/.test(line) ? line : line.replace(/^(\s*)/, '$1- ');
        }),
        numberedList: () => transformSelectedLines((line, index) => {
          if (!line.trim()) return line;
          return /^(\s*)\d+\.\s+/.test(line) ? line : line.replace(/^(\s*)/, `$1${index + 1}. `);
        }),
        quote: () => transformSelectedLines((line) => {
          if (!line.trim()) return line;
          return /^(\s*)>\s?/.test(line) ? line : line.replace(/^(\s*)/, '$1> ');
        }),
        taskList: () => transformTaskList(false),
        taskListDone: () => transformTaskList(true),
        codeBlock: () => insertBlock('```\n', '\n```', 'code'),
        mermaidBlock: () => insertBlock('```mermaid\n', '\n```', 'flowchart LR\n  A[Start] --> B[Finish]'),
        link: insertLink,
        image: insertImage,
        horizontalRule: insertHorizontalRule,
        table: () => {
          if (commandHandlers.table) {
            commandHandlers.table();
            return;
          }
          insertTable();
        },
      };

      commands[command]?.();
    }

    function toggleInlineWrap(prefix, suffix, placeholder) {
      const selection = getEditorSelection();
      const selected = selection.text || placeholder;
      const hasWrap = selection.text.startsWith(prefix) && selection.text.endsWith(suffix);

      if (selection.text && hasWrap) {
        const unwrapped = selection.text.slice(prefix.length, selection.text.length - suffix.length);
        replaceEditorRange(selection.start, selection.end, unwrapped, selection.start, selection.start + unwrapped.length);
        return;
      }

      const replacement = `${prefix}${selected}${suffix}`;
      const innerStart = selection.start + prefix.length;
      const innerEnd = innerStart + selected.length;
      replaceEditorRange(selection.start, selection.end, replacement, innerStart, innerEnd);
    }

    function insertBlock(prefix, suffix, placeholder) {
      const selection = getEditorSelection();
      const selected = selection.text.trim() || placeholder;
      const leadingBreak = selection.start > 0 && editor.value[selection.start - 1] !== '\n' ? '\n\n' : '';
      const trailingBreak = selection.end < editor.value.length && editor.value[selection.end] !== '\n' ? '\n\n' : '';
      const replacement = `${leadingBreak}${prefix}${selected}${suffix}${trailingBreak}`;
      const selectedStart = selection.start + leadingBreak.length + prefix.length;
      replaceEditorRange(selection.start, selection.end, replacement, selectedStart, selectedStart + selected.length);
    }

    function insertLink() {
      const selection = getEditorSelection();
      const text = selection.text || 'link text';
      const url = 'https://example.com';
      const replacement = `[${text}](${url})`;
      const urlStart = selection.start + text.length + 3;
      replaceEditorRange(selection.start, selection.end, replacement, urlStart, urlStart + url.length);
    }

    function insertImage() {
      const selection = getEditorSelection();
      const text = selection.text || 'alt text';
      const url = 'image-url';
      const replacement = `![${text}](${url})`;
      const urlStart = selection.start + text.length + 4;
      replaceEditorRange(selection.start, selection.end, replacement, urlStart, urlStart + url.length);
    }

    function insertHorizontalRule() {
      const selection = getEditorSelection();
      const before = editor.value.slice(0, selection.start);
      const after = editor.value.slice(selection.end);
      const leadingBreak = before && !before.endsWith('\n\n') ? (before.endsWith('\n') ? '\n' : '\n\n') : '';
      const trailingBreak = after && !after.startsWith('\n\n') ? (after.startsWith('\n') ? '\n' : '\n\n') : '';
      const replacement = `${leadingBreak}---${trailingBreak}`;
      const position = selection.start + replacement.length;
      replaceEditorRange(selection.start, selection.end, replacement, position, position);
    }

    function insertTable() {
      const selection = getEditorSelection();
      const table = '| Column A | Column B |\n|---|---|\n| Value A | Value B |';
      const leadingBreak = selection.start > 0 && editor.value[selection.start - 1] !== '\n' ? '\n\n' : '';
      const trailingBreak = selection.end < editor.value.length && editor.value[selection.end] !== '\n' ? '\n\n' : '';
      const replacement = `${leadingBreak}${table}${trailingBreak}`;
      const tableStart = selection.start + leadingBreak.length;
      replaceEditorRange(selection.start, selection.end, replacement, tableStart, tableStart + table.length);
    }

    function transformSelectedLines(transform) {
      const value = editor.value;
      const selection = getEditorSelection();
      const start = value.lastIndexOf('\n', Math.max(selection.start - 1, 0)) + 1;
      const endIndex = value.indexOf('\n', selection.end);
      const end = endIndex === -1 ? value.length : endIndex;
      const original = value.slice(start, end);
      const transformed = original
        .split('\n')
        .map((line, index) => transform(line, index))
        .join('\n');

      replaceEditorRange(start, end, transformed, start, start + transformed.length);
    }

    function transformTaskList(checked) {
      const marker = checked ? 'x' : ' ';
      transformSelectedLines((line) => {
        if (!line.trim()) return line;

        const taskMatch = line.match(/^(\s*)(?:[-*+]|\d+\.)\s+\[[ xX]\]\s*(.*)$/);
        if (taskMatch) {
          return `${taskMatch[1]}- [${marker}] ${taskMatch[2]}`;
        }

        const listMatch = line.match(/^(\s*)(?:[-*+]|\d+\.)\s+(.*)$/);
        if (listMatch) {
          return `${listMatch[1]}- [${marker}] ${listMatch[2]}`;
        }

        return line.replace(/^(\s*)/, `$1- [${marker}] `);
      });
    }

    function getEditorSelection() {
      return {
        start: editor.selectionStart,
        end: editor.selectionEnd,
        text: editor.value.slice(editor.selectionStart, editor.selectionEnd),
      };
    }

    function replaceEditorRange(start, end, replacement, selectionStart, selectionEnd) {
      const value = editor.value;
      editor.value = `${value.slice(0, start)}${replacement}${value.slice(end)}`;
      editor.focus();
      editor.setSelectionRange(selectionStart, selectionEnd);
      editor.dispatchEvent(new Event('input', { bubbles: true }));
    }

    return {
      installEditorEnhancements,
      updateEditorChrome,
      recordEditorHistoryInput,
      resetEditorHistory,
      undoEditorChange,
      redoEditorChange,
      handleMermaidAutocompleteKeydown,
      updateMermaidAutocompleteFromInput,
      hideMermaidAutocomplete,
      handleEditorToolbarClick,
      executeMarkdownCommand,
      getEditorSelection,
      replaceEditorRange,
    };
}
