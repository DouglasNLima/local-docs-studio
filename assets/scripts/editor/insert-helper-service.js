import { escapeHtml, slugify } from '../utils/format.js';

const helperTypes = new Set([
  'emoji',
  'callout',
  'statusBadge',
  'detailsBlock',
  'imageFigure',
  'keyboardShortcut',
  'anchor',
]);

const emojis = [
  { glyph: '✅', label: 'Done', keywords: 'done check complete success ready' },
  { glyph: '☑️', label: 'Accepted', keywords: 'accepted approved tick checked' },
  { glyph: '❌', label: 'Rejected', keywords: 'rejected failed no blocked invalid' },
  { glyph: '⚠️', label: 'Warning', keywords: 'warning alert risk caution' },
  { glyph: '🚨', label: 'Incident', keywords: 'incident alert urgent outage' },
  { glyph: 'ℹ️', label: 'Info', keywords: 'info note help' },
  { glyph: '❓', label: 'Question', keywords: 'question ask unknown clarify' },
  { glyph: '💬', label: 'Comment', keywords: 'comment feedback discussion chat' },
  { glyph: '⏳', label: 'Waiting', keywords: 'waiting pending time progress' },
  { glyph: '🔄', label: 'Refresh', keywords: 'refresh retry repeat sync update' },
  { glyph: '🚧', label: 'In progress', keywords: 'work progress construction' },
  { glyph: '🧭', label: 'Decision', keywords: 'decision direction compass choose' },
  { glyph: '🗓️', label: 'Schedule', keywords: 'schedule calendar plan date milestone' },
  { glyph: '📅', label: 'Date', keywords: 'date calendar event due' },
  { glyph: '🚀', label: 'Launch', keywords: 'launch release deploy' },
  { glyph: '📦', label: 'Package', keywords: 'package bundle release artefact delivery' },
  { glyph: '🏷️', label: 'Tag', keywords: 'tag label category metadata' },
  { glyph: '🎯', label: 'Goal', keywords: 'goal target objective' },
  { glyph: '📈', label: 'Trend up', keywords: 'trend metric growth increase' },
  { glyph: '📉', label: 'Trend down', keywords: 'trend metric decrease drop' },
  { glyph: '📊', label: 'Metrics', keywords: 'metrics chart report analytics' },
  { glyph: '📌', label: 'Pinned', keywords: 'pin pinned important' },
  { glyph: '⭐', label: 'Important', keywords: 'important star priority favourite' },
  { glyph: '🔥', label: 'Hot', keywords: 'hot urgent priority critical' },
  { glyph: '📄', label: 'Document', keywords: 'document page file' },
  { glyph: '📝', label: 'Notes', keywords: 'notes edit writing draft' },
  { glyph: '📚', label: 'Docs', keywords: 'docs documentation library guide' },
  { glyph: '📁', label: 'Folder', keywords: 'folder workspace files' },
  { glyph: '🗂️', label: 'Index', keywords: 'index catalogue archive organise' },
  { glyph: '🔗', label: 'Link', keywords: 'link reference url' },
  { glyph: '🧩', label: 'Component', keywords: 'component module part extension' },
  { glyph: '🧱', label: 'Foundation', keywords: 'foundation block architecture base' },
  { glyph: '🗺️', label: 'Map', keywords: 'map journey route navigation' },
  { glyph: '🔍', label: 'Search', keywords: 'search find review' },
  { glyph: '🔎', label: 'Inspect', keywords: 'inspect investigate review audit' },
  { glyph: '🧪', label: 'Test', keywords: 'test qa experiment' },
  { glyph: '🐞', label: 'Bug', keywords: 'bug defect issue fix' },
  { glyph: '🧹', label: 'Clean up', keywords: 'cleanup tidy remove maintenance' },
  { glyph: '🛠️', label: 'Tools', keywords: 'tool maintenance setup' },
  { glyph: '⚙️', label: 'Settings', keywords: 'settings configuration options' },
  { glyph: '🔒', label: 'Locked', keywords: 'lock secure private' },
  { glyph: '🔓', label: 'Unlocked', keywords: 'unlock open permission' },
  { glyph: '🛡️', label: 'Security', keywords: 'security shield protect safe' },
  { glyph: '🔑', label: 'Key', keywords: 'key access secret credential' },
  { glyph: '💡', label: 'Idea', keywords: 'idea tip light' },
  { glyph: '✨', label: 'Improvement', keywords: 'improvement polish enhance sparkles' },
  { glyph: '📣', label: 'Announcement', keywords: 'announcement broadcast communicate' },
  { glyph: '📥', label: 'Inbox', keywords: 'inbox intake request received' },
  { glyph: '📤', label: 'Outbound', keywords: 'outbound send publish submit' },
  { glyph: '👥', label: 'Team', keywords: 'team people owners stakeholders' },
  { glyph: '🙌', label: 'Thanks', keywords: 'thanks celebrate appreciate' },
];

const calloutTypes = [
  { id: 'NOTE', label: 'Note' },
  { id: 'TIP', label: 'Tip' },
  { id: 'IMPORTANT', label: 'Important' },
  { id: 'WARNING', label: 'Warning' },
  { id: 'CAUTION', label: 'Caution' },
];

const statusKinds = [
  { id: 'draft', label: 'Draft' },
  { id: 'ready', label: 'Ready' },
  { id: 'blocked', label: 'Blocked' },
  { id: 'deprecated', label: 'Deprecated' },
  { id: 'beta', label: 'Beta' },
  { id: 'review', label: 'In review' },
];

const shortcutDefaults = ['Ctrl', 'Enter'];

export function createInsertHelperService({ editor, dom, callbacks }) {
  const {
    insertHelperDialog,
    insertHelperForm,
    insertHelperKicker,
    insertHelperTitle,
    insertHelperSummary,
    insertHelperFields,
    insertHelperApplyButton,
    insertHelperCancelButton,
  } = dom;
  const {
    replaceEditorRange,
    setStatus,
  } = callbacks;

  let helperState = {
    type: '',
    selectedEmoji: emojis[0],
    selectedStatus: statusKinds[1],
    selectionStart: 0,
    selectionEnd: 0,
    selectionText: '',
  };

  function installInsertHelperHandlers() {
    insertHelperDialog?.addEventListener('cancel', closeInsertHelper);
    insertHelperDialog?.addEventListener('click', (event) => {
      if (event.target.closest('[data-insert-helper-cancel]')) closeInsertHelper(event);
    });
    insertHelperForm?.addEventListener('submit', applyInsertHelper);
    insertHelperCancelButton?.addEventListener('click', closeInsertHelper);
    insertHelperApplyButton?.addEventListener('click', applyInsertHelper);
    insertHelperFields?.addEventListener('input', handleHelperInput);
    insertHelperFields?.addEventListener('click', handleHelperClick);
  }

  function openInsertHelper(type) {
    if (!helperTypes.has(type)) return;
    helperState = {
      ...helperState,
      type,
      selectedEmoji: emojis[0],
      selectedStatus: statusKinds[1],
      selectionStart: editor.selectionStart,
      selectionEnd: editor.selectionEnd,
      selectionText: editor.value.slice(editor.selectionStart, editor.selectionEnd).trim(),
    };
    renderHelper();
    insertHelperDialog?.showModal();
    insertHelperFields?.querySelector('input, textarea, select, button')?.focus();
  }

  function closeInsertHelper(event) {
    event?.preventDefault?.();
    insertHelperDialog?.close();
    editor.focus();
  }

  function renderHelper() {
    const config = getHelperConfig(helperState.type);
    insertHelperKicker.textContent = config.kicker;
    insertHelperTitle.textContent = config.title;
    insertHelperSummary.textContent = config.summary;
    insertHelperApplyButton.textContent = config.applyLabel;
    insertHelperFields.innerHTML = '';
    config.render();
  }

  function handleHelperInput(event) {
    if (event.target.id === 'emojiSearchInput') {
      renderEmojiGrid(event.target.value);
    }
  }

  function handleHelperClick(event) {
    const emojiButton = event.target.closest('[data-emoji-value]');
    if (emojiButton) {
      helperState.selectedEmoji = emojis.find((emoji) => emoji.glyph === emojiButton.dataset.emojiValue) || emojis[0];
      renderEmojiGrid(insertHelperFields.querySelector('#emojiSearchInput')?.value || '');
      return;
    }

    const statusButton = event.target.closest('[data-status-kind-option]');
    if (statusButton) {
      helperState.selectedStatus = statusKinds.find((status) => status.id === statusButton.dataset.statusKindOption) || statusKinds[1];
      renderStatusOptions();
    }
  }

  function applyInsertHelper(event) {
    event?.preventDefault?.();
    const config = getHelperConfig(helperState.type);
    const insertion = config.build();
    if (!insertion.text) return;

    insertText(insertion.text, { block: insertion.block });
    setStatus(insertion.status, 'ok');
    closeInsertHelper();
  }

  function insertText(text, { block = false } = {}) {
    const start = helperState.selectionStart;
    const end = helperState.selectionEnd;
    const leadingBreak = block && start > 0 && editor.value[start - 1] !== '\n' ? '\n\n' : '';
    const trailingBreak = block && end < editor.value.length && editor.value[end] !== '\n' ? '\n\n' : '';
    const replacement = `${leadingBreak}${text}${trailingBreak}`;
    const insertedStart = start + leadingBreak.length;
    replaceEditorRange(start, end, replacement, insertedStart, insertedStart + text.length);
  }

  function getHelperConfig(type) {
    const configs = {
      emoji: {
        kicker: 'Emoji',
        title: 'Insert emoji',
        summary: 'Choose a common documentation emoji.',
        applyLabel: 'Insert emoji',
        render: renderEmojiFields,
        build: () => ({
          text: helperState.selectedEmoji.glyph,
          block: false,
          status: 'Emoji inserted.',
        }),
      },
      callout: {
        kicker: 'Callout',
        title: 'Insert callout',
        summary: 'Create a GitHub-style Markdown note, tip, warning, or caution.',
        applyLabel: 'Insert callout',
        render: renderCalloutFields,
        build: buildCallout,
      },
      statusBadge: {
        kicker: 'Status badge',
        title: 'Insert status badge',
        summary: 'Create a compact semantic status badge.',
        applyLabel: 'Insert badge',
        render: renderStatusBadgeFields,
        build: buildStatusBadge,
      },
      detailsBlock: {
        kicker: 'Details',
        title: 'Insert details block',
        summary: 'Create a collapsible details section.',
        applyLabel: 'Insert details',
        render: renderDetailsFields,
        build: buildDetailsBlock,
      },
      imageFigure: {
        kicker: 'Image figure',
        title: 'Insert image figure',
        summary: 'Create an image with alt text and an optional caption.',
        applyLabel: 'Insert figure',
        render: renderImageFigureFields,
        build: buildImageFigure,
      },
      keyboardShortcut: {
        kicker: 'Shortcut',
        title: 'Insert keyboard shortcut',
        summary: 'Create accessible keyboard key markup.',
        applyLabel: 'Insert shortcut',
        render: renderShortcutFields,
        build: buildShortcut,
      },
      anchor: {
        kicker: 'Anchor',
        title: 'Insert anchor',
        summary: 'Create a stable link target inside the document.',
        applyLabel: 'Insert anchor',
        render: renderAnchorFields,
        build: buildAnchor,
      },
    };
    return configs[type] || configs.emoji;
  }

  function renderEmojiFields() {
    const search = input('emojiSearchInput', 'search', '');
    search.placeholder = 'Search emoji';
    insertHelperFields.append(
      field('Search', search),
      emojiGrid()
    );
    renderEmojiGrid('');
  }

  function emojiGrid() {
    const grid = document.createElement('div');
    grid.className = 'emoji-picker-grid';
    grid.id = 'emojiPickerGrid';
    grid.setAttribute('aria-label', 'Emoji choices');
    return grid;
  }

  function renderEmojiGrid(query) {
    const grid = insertHelperFields.querySelector('#emojiPickerGrid');
    if (!grid) return;
    const needle = String(query || '').trim().toLowerCase();
    const filtered = emojis.filter((emoji) => !needle || `${emoji.label} ${emoji.keywords}`.toLowerCase().includes(needle));
    grid.innerHTML = '';
    filtered.forEach((emoji) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'emoji-picker-option';
      button.dataset.emojiValue = emoji.glyph;
      button.setAttribute('aria-label', emoji.label);
      button.setAttribute('aria-pressed', String(helperState.selectedEmoji.glyph === emoji.glyph));
      button.textContent = emoji.glyph;
      grid.appendChild(button);
    });
  }

  function renderCalloutFields() {
    insertHelperFields.append(
      field('Type', select('calloutTypeInput', calloutTypes.map((type) => [type.id, type.label]), 'NOTE')),
      field('Title', input('calloutTitleInput', 'text', 'Optional heading')),
      field('Text', textarea('calloutBodyInput', helperState.selectionText || 'Write the note here.'))
    );
  }

  function buildCallout() {
    const type = readValue('calloutTypeInput') || 'NOTE';
    const title = normaliseText(readValue('calloutTitleInput'));
    const body = normaliseMultiline(readValue('calloutBodyInput')) || 'Write the note here.';
    const lines = [`> [!${type}]`];
    if (title) {
      lines.push(`> **${title}**`, '>');
    }
    body.split('\n').forEach((line) => lines.push(line ? `> ${line}` : '>'));
    return {
      text: lines.join('\n'),
      block: true,
      status: 'Callout inserted.',
    };
  }

  function renderStatusBadgeFields() {
    const textValue = helperState.selectionText || helperState.selectedStatus.label;
    insertHelperFields.append(
      field('Text', input('statusBadgeTextInput', 'text', textValue)),
      statusOptions()
    );
    renderStatusOptions();
  }

  function statusOptions() {
    const wrapper = document.createElement('div');
    wrapper.className = 'status-kind-options';
    wrapper.id = 'statusKindOptions';
    wrapper.setAttribute('aria-label', 'Status badge kind');
    return wrapper;
  }

  function renderStatusOptions() {
    const wrapper = insertHelperFields.querySelector('#statusKindOptions');
    if (!wrapper) return;
    wrapper.innerHTML = '';
    statusKinds.forEach((status) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = `status-kind-option status-kind-${status.id}`;
      button.dataset.statusKindOption = status.id;
      button.setAttribute('aria-label', `${status.label} badge`);
      button.setAttribute('aria-pressed', String(helperState.selectedStatus.id === status.id));
      button.textContent = status.label;
      wrapper.appendChild(button);
    });
  }

  function buildStatusBadge() {
    const status = helperState.selectedStatus || statusKinds[1];
    const label = normaliseText(readValue('statusBadgeTextInput')) || status.label;
    return {
      text: `<span data-status-badge data-status-kind="${status.id}" aria-label="Status: ${escapeHtml(label)}">${escapeHtml(label)}</span>`,
      block: false,
      status: 'Status badge inserted.',
    };
  }

  function renderDetailsFields() {
    insertHelperFields.append(
      field('Summary', input('detailsSummaryInput', 'text', 'More details')),
      field('Content', textarea('detailsBodyInput', helperState.selectionText || 'Add supporting details here.'))
    );
  }

  function buildDetailsBlock() {
    const summary = normaliseText(readValue('detailsSummaryInput')) || 'More details';
    const body = normaliseMultiline(readValue('detailsBodyInput')) || 'Add supporting details here.';
    return {
      text: `<details data-details-block>
  <summary>${escapeHtml(summary)}</summary>

${body}
</details>`,
      block: true,
      status: 'Details block inserted.',
    };
  }

  function renderImageFigureFields() {
    insertHelperFields.append(
      field('Image path or URL', input('imageFigureSrcInput', 'text', 'image-url')),
      field('Alt text', input('imageFigureAltInput', 'text', helperState.selectionText || 'Image description')),
      field('Caption', input('imageFigureCaptionInput', 'text', 'Optional caption'))
    );
  }

  function buildImageFigure() {
    const src = normaliseText(readValue('imageFigureSrcInput')) || 'image-url';
    const alt = normaliseText(readValue('imageFigureAltInput')) || 'Image description';
    const caption = normaliseText(readValue('imageFigureCaptionInput'));
    const captionLine = caption ? `\n  <figcaption>${escapeHtml(caption)}</figcaption>` : '';
    return {
      text: `<figure data-image-figure>
  <img src="${escapeHtml(src)}" alt="${escapeHtml(alt)}">${captionLine}
</figure>`,
      block: true,
      status: 'Image figure inserted.',
    };
  }

  function renderShortcutFields() {
    insertHelperFields.append(
      field('Keys', input('shortcutKeysInput', 'text', helperState.selectionText || shortcutDefaults.join('+')))
    );
  }

  function buildShortcut() {
    const keys = normaliseShortcutParts(readValue('shortcutKeysInput'));
    const label = keys.join(' ');
    return {
      text: `<span data-shortcut aria-label="Keyboard shortcut ${escapeHtml(label)}">${keys.map((key) => `<kbd>${escapeHtml(key)}</kbd>`).join(' + ')}</span>`,
      block: false,
      status: 'Keyboard shortcut inserted.',
    };
  }

  function renderAnchorFields() {
    insertHelperFields.append(
      field('Anchor ID', input('anchorIdInput', 'text', slugify(helperState.selectionText) || 'section-anchor')),
      field('Visible text', input('anchorTextInput', 'text', helperState.selectionText))
    );
  }

  function buildAnchor() {
    const id = slugify(readValue('anchorIdInput')) || slugify(helperState.selectionText) || 'section-anchor';
    const text = normaliseText(readValue('anchorTextInput'));
    return {
      text: `<span id="${escapeHtml(id)}" data-doc-anchor>${text ? escapeHtml(text) : ''}</span>`,
      block: false,
      status: 'Anchor inserted.',
    };
  }

  function field(labelText, control) {
    const wrapper = document.createElement('div');
    wrapper.className = 'template-field';
    const label = document.createElement('label');
    label.textContent = labelText;
    if (control.id) label.setAttribute('for', control.id);
    wrapper.append(label, control);
    return wrapper;
  }

  function input(id, type, value) {
    const control = document.createElement('input');
    control.id = id;
    control.type = type;
    control.autocomplete = 'off';
    control.value = value;
    return control;
  }

  function textarea(id, value) {
    const control = document.createElement('textarea');
    control.id = id;
    control.value = value;
    return control;
  }

  function select(id, options, value) {
    const control = document.createElement('select');
    control.id = id;
    options.forEach(([optionValue, label]) => {
      const option = document.createElement('option');
      option.value = optionValue;
      option.textContent = label;
      option.selected = optionValue === value;
      control.appendChild(option);
    });
    return control;
  }

  function readValue(id) {
    return insertHelperFields.querySelector(`#${id}`)?.value || '';
  }

  return {
    installInsertHelperHandlers,
    openInsertHelper,
  };
}

function normaliseText(value) {
  return String(value || '').replace(/\s+/g, ' ').trim();
}

function normaliseMultiline(value) {
  return String(value || '').replace(/\r\n?/g, '\n').trim();
}

function normaliseShortcutParts(value) {
  const parts = String(value || '')
    .split(/\s*\+\s*|\s*,\s*/)
    .map(normaliseText)
    .filter(Boolean);
  return parts.length ? parts : shortcutDefaults;
}
