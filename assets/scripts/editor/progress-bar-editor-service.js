import { clamp, escapeHtml } from '../utils/format.js';

const progressColours = [
  { id: 'accent', label: 'Accent' },
  { id: 'green', label: 'Green' },
  { id: 'blue', label: 'Blue' },
  { id: 'amber', label: 'Amber' },
  { id: 'red', label: 'Red' },
  { id: 'neutral', label: 'Neutral' },
];
const progressColourIds = new Set(progressColours.map((colour) => colour.id));
const defaultProgressLabel = 'Progress';
const defaultProgressPercent = 50;
const defaultProgressColour = 'accent';

export function createProgressBarEditorService({ editor, dom, callbacks }) {
  const {
    progressBarEditorDialog,
    progressBarEditorForm,
    progressBarEditorSummary,
    progressBarLabelInput,
    progressBarPercentInput,
    progressBarPercentRange,
    progressBarPercentOutput,
    progressBarColourOptions,
    progressBarApplyButton,
    progressBarCancelButton,
  } = dom;
  const {
    replaceEditorRange,
    setStatus,
  } = callbacks;

  let progressState = createDefaultProgressState();

  function installProgressBarEditorHandlers() {
    progressBarEditorDialog?.addEventListener('cancel', closeProgressBarEditor);
    progressBarEditorDialog?.addEventListener('click', (event) => {
      if (event.target.closest('[data-progress-bar-editor-cancel]')) closeProgressBarEditor(event);
    });
    progressBarEditorForm?.addEventListener('submit', applyProgressBar);
    progressBarCancelButton?.addEventListener('click', closeProgressBarEditor);
    progressBarApplyButton?.addEventListener('click', applyProgressBar);
    progressBarLabelInput?.addEventListener('input', () => {
      progressState.label = normaliseLabel(progressBarLabelInput.value);
    });
    progressBarPercentInput?.addEventListener('input', () => syncProgressPercent(progressBarPercentInput.value));
    progressBarPercentRange?.addEventListener('input', () => syncProgressPercent(progressBarPercentRange.value));
    progressBarColourOptions?.addEventListener('click', (event) => {
      const button = event.target.closest('[data-progress-colour-option]');
      if (!button) return;
      progressState.colour = normaliseColour(button.dataset.progressColourOption);
      renderColourOptions();
    });
  }

  function openProgressBarEditor() {
    const range = findProgressBarAt(editor.value, editor.selectionStart);
    const selectionText = editor.value.slice(editor.selectionStart, editor.selectionEnd).trim();
    progressState = range
      ? { ...range, selectionStart: range.start, selectionEnd: range.end }
      : {
          ...createDefaultProgressState(),
          label: normaliseLabel(selectionText) || defaultProgressLabel,
          selectionStart: editor.selectionStart,
          selectionEnd: editor.selectionEnd,
        };
    renderProgressBarEditor();
    progressBarEditorDialog?.showModal();
    progressBarLabelInput?.focus();
    progressBarLabelInput?.select();
  }

  function closeProgressBarEditor(event) {
    event?.preventDefault?.();
    progressBarEditorDialog?.close();
    editor.focus();
  }

  function renderProgressBarEditor() {
    if (progressBarEditorSummary) {
      progressBarEditorSummary.textContent = progressState.range
        ? 'Editing an existing progress bar.'
        : 'Creating a new progress bar.';
    }
    if (progressBarLabelInput) progressBarLabelInput.value = progressState.label;
    updatePercentFields();
    renderColourOptions();
  }

  function renderColourOptions() {
    if (!progressBarColourOptions) return;
    progressBarColourOptions.innerHTML = '';
    progressColours.forEach((colour) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = `progress-colour-option progress-colour-${colour.id}`;
      button.dataset.progressColourOption = colour.id;
      button.setAttribute('aria-label', `${colour.label} progress colour`);
      button.setAttribute('aria-pressed', String(progressState.colour === colour.id));
      button.title = colour.label;

      const swatch = document.createElement('span');
      swatch.className = 'progress-colour-swatch';
      swatch.setAttribute('aria-hidden', 'true');

      const label = document.createElement('span');
      label.textContent = colour.label;

      button.append(swatch, label);
      progressBarColourOptions.appendChild(button);
    });
  }

  function syncProgressPercent(value) {
    progressState.percent = normalisePercent(value);
    updatePercentFields();
  }

  function updatePercentFields() {
    const value = String(progressState.percent);
    if (progressBarPercentInput) progressBarPercentInput.value = value;
    if (progressBarPercentRange) progressBarPercentRange.value = value;
    if (progressBarPercentOutput) progressBarPercentOutput.value = `${value}%`;
  }

  function applyProgressBar(event) {
    event?.preventDefault?.();
    const label = normaliseLabel(progressBarLabelInput?.value) || defaultProgressLabel;
    const percent = normalisePercent(progressBarPercentInput?.value);
    const colour = normaliseColour(progressState.colour);
    const markdown = buildProgressBarMarkdown({ label, percent, colour });

    if (progressState.range) {
      replaceEditorRange(progressState.start, progressState.end, markdown, progressState.start, progressState.start + markdown.length);
      setStatus('Progress bar updated.', 'ok');
    } else {
      const selectionStart = progressState.selectionStart;
      const selectionEnd = progressState.selectionEnd;
      const leadingBreak = selectionStart > 0 && editor.value[selectionStart - 1] !== '\n' ? '\n\n' : '';
      const trailingBreak = selectionEnd < editor.value.length && editor.value[selectionEnd] !== '\n' ? '\n\n' : '';
      const replacement = `${leadingBreak}${markdown}${trailingBreak}`;
      const progressStart = selectionStart + leadingBreak.length;
      replaceEditorRange(selectionStart, selectionEnd, replacement, progressStart, progressStart + markdown.length);
      setStatus('Progress bar inserted.', 'ok');
    }

    closeProgressBarEditor();
  }

  return {
    installProgressBarEditorHandlers,
    openProgressBarEditor,
  };
}

function createDefaultProgressState() {
  return {
    range: null,
    start: 0,
    end: 0,
    label: defaultProgressLabel,
    percent: defaultProgressPercent,
    colour: defaultProgressColour,
    selectionStart: 0,
    selectionEnd: 0,
  };
}

function buildProgressBarMarkdown({ label, percent, colour }) {
  const text = normaliseLabel(label) || defaultProgressLabel;
  const safeLabel = escapeHtml(text);
  const value = normalisePercent(percent);
  const safeColour = normaliseColour(colour);
  const progressText = `${value}%`;
  const ariaLabel = escapeHtml(`${text} progress`);

  return `<figure data-progress-bar data-progress-colour="${safeColour}" aria-label="${ariaLabel}">
  <figcaption>
    <span data-progress-label>${safeLabel}</span>
    <span data-progress-value>${progressText}</span>
  </figcaption>
  <progress max="100" value="${value}">${progressText}</progress>
</figure>`;
}

function findProgressBarAt(source, position) {
  const figurePattern = /<figure\b(?=[^>]*\bdata-progress-bar(?:\s|=|>))[^>]*>[\s\S]*?<\/figure>/gi;
  let match;
  while ((match = figurePattern.exec(source))) {
    const start = match.index;
    const end = start + match[0].length;
    if (position < start || position > end) continue;

    const parsed = parseProgressBar(match[0]);
    if (!parsed) return null;
    return {
      range: true,
      start,
      end,
      ...parsed,
    };
  }
  return null;
}

function parseProgressBar(markup) {
  const template = document.createElement('template');
  template.innerHTML = String(markup || '').trim();
  const figure = template.content.querySelector('figure[data-progress-bar]');
  if (!figure) return null;

  const progress = figure.querySelector('progress');
  const valueText = figure.querySelector('[data-progress-value]')?.textContent || progress?.textContent || '';
  const rawValue = progress?.getAttribute('value') || valueText;
  return {
    label: normaliseLabel(figure.querySelector('[data-progress-label]')?.textContent) || defaultProgressLabel,
    percent: normalisePercent(rawValue),
    colour: normaliseColour(figure.dataset.progressColour),
  };
}

function normaliseLabel(value) {
  return String(value || '').replace(/\s+/g, ' ').trim();
}

function normalisePercent(value) {
  const percent = Number.parseInt(String(value || '').match(/-?\d+/)?.[0] || '', 10);
  if (!Number.isFinite(percent)) return defaultProgressPercent;
  return clamp(percent, 0, 100);
}

function normaliseColour(value) {
  const colour = String(value || '').trim().toLowerCase();
  return progressColourIds.has(colour) ? colour : defaultProgressColour;
}
