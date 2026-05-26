const ARTIFACT_READER_GROUPS = [
  'Summary',
  'Findings',
  'Diagrams',
  'ADRs',
  'Release notes',
  'Documentation',
  'Other',
];

export function createArtifactBundleReader({
  container,
  state,
  onOpenPath,
} = {}) {
  let expanded = false;
  let evidenceFilter = '';
  let kindFilter = '';
  let searchTerm = '';
  let currentBundle = null;
  let handlersInstalled = false;

  function render() {
    if (!container) return;
    installHandlers();

    const bundle = state.artifactBundle;
    if (bundle !== currentBundle) {
      resetFilters();
      currentBundle = bundle;
    }

    container.replaceChildren();
    container.hidden = !bundle;
    if (!bundle) return;

    const items = getSafeReaderItems(bundle, state.files);
    const visibleItems = filterItems(items);
    const detailsId = 'artifactBundleReaderDetails';

    container.appendChild(createHeader(bundle, items, detailsId));
    const details = document.createElement('div');
    details.className = 'artifact-bundle-details';
    details.id = detailsId;
    details.hidden = !expanded;
    details.append(
      createMetadata(bundle),
      createNotes(bundle),
      createEvidenceChips(bundle.evidenceLevels || []),
      createWarnings(bundle),
      createFilters(bundle, items),
      createNavigation(visibleItems)
    );
    container.appendChild(details);
  }

  function installHandlers() {
    if (handlersInstalled || !container) return;
    handlersInstalled = true;
    container.addEventListener('click', async (event) => {
      const toggle = event.target.closest('[data-artifact-reader-action="toggle"]');
      if (toggle) {
        expanded = !expanded;
        render();
        return;
      }

      const clear = event.target.closest('[data-artifact-reader-action="clearFilters"]');
      if (clear) {
        resetFilters({ keepExpanded: true });
        render();
        return;
      }

      const evidence = event.target.closest('[data-artifact-evidence-filter]');
      if (evidence) {
        evidenceFilter = evidence.dataset.artifactEvidenceFilter || '';
        render();
        return;
      }

      const kind = event.target.closest('[data-artifact-kind-filter]');
      if (kind) {
        kindFilter = kind.dataset.artifactKindFilter || '';
        render();
        return;
      }

      const item = event.target.closest('[data-artifact-open-path]');
      if (item) {
        const path = item.dataset.artifactOpenPath || '';
        const safePaths = new Set(state.files.map((file) => file.path));
        if (safePaths.has(path)) await onOpenPath?.(path);
      }
    });

    container.addEventListener('input', (event) => {
      const input = event.target.closest('[data-artifact-search]');
      if (!input) return;
      searchTerm = input.value.trim().toLowerCase();
      render();
    });
  }

  function resetFilters(options = {}) {
    evidenceFilter = '';
    kindFilter = '';
    searchTerm = '';
    if (!options.keepExpanded) expanded = false;
  }

  function createHeader(bundle, items, detailsId) {
    const header = document.createElement('div');
    header.className = 'artifact-bundle-head';

    const summary = document.createElement('div');
    summary.className = 'artifact-bundle-head-copy';
    const title = document.createElement('strong');
    title.className = 'artifact-bundle-title';
    title.textContent = bundle.title || bundle.sourceTool || 'Artefact bundle';
    const note = document.createElement('span');
    note.textContent = `${items.length} indexed item${items.length === 1 ? '' : 's'} loaded from ZIP metadata.`;
    summary.append(title, note);

    const toggle = document.createElement('button');
    toggle.type = 'button';
    toggle.className = 'artifact-bundle-toggle';
    toggle.dataset.artifactReaderAction = 'toggle';
    toggle.setAttribute('aria-expanded', String(expanded));
    toggle.setAttribute('aria-controls', detailsId);
    toggle.setAttribute('aria-label', expanded ? 'Collapse artefact bundle reader' : 'Expand artefact bundle reader');
    toggle.textContent = expanded ? 'Hide' : 'Details';

    header.append(summary, toggle);
    return header;
  }

  function createMetadata(bundle) {
    const metadata = [
      bundle.sourceTool ? ['Source', [bundle.sourceTool, bundle.sourceToolVersion].filter(Boolean).join(' ')] : null,
      bundle.generatedAtUtc ? ['Generated UTC', bundle.generatedAtUtc] : null,
      bundle.entryDocument ? ['Entry', bundle.entryDocument] : null,
      ['Documents', String(bundle.documents?.length ?? 0)],
      ['Diagrams', String(bundle.diagrams?.length ?? 0)],
      ['Warnings', String((bundle.metadataWarnings?.length ?? 0) + (bundle.manifestWarnings?.length ?? 0))],
      ['Evidence labels', String(bundle.evidenceLevels?.length ?? 0)],
    ].filter(Boolean);

    const list = document.createElement('dl');
    list.className = 'artifact-bundle-meta';
    metadata.forEach(([label, value]) => {
      const term = document.createElement('dt');
      term.textContent = label;
      const detail = document.createElement('dd');
      detail.textContent = value;
      list.append(term, detail);
    });
    return list;
  }

  function createNotes(bundle) {
    const wrap = document.createElement('div');
    wrap.className = 'artifact-bundle-notes';
    wrap.appendChild(createNote('Evidence labels are displayed as supplied by the bundle.'));
    if (hasCandidateFinding(bundle)) {
      wrap.appendChild(createNote('Candidate findings remain candidate findings.'));
    }
    return wrap;
  }

  function createNote(text) {
    const note = document.createElement('p');
    note.className = 'artifact-bundle-copy';
    note.textContent = text;
    return note;
  }

  function createEvidenceChips(levels) {
    const chips = document.createElement('div');
    chips.className = 'artifact-bundle-chips';
    levels.forEach((level) => {
      const chip = document.createElement('span');
      chip.className = 'artifact-bundle-chip';
      chip.textContent = level;
      chips.appendChild(chip);
    });
    if (!levels.length) {
      const empty = document.createElement('span');
      empty.className = 'artifact-bundle-muted';
      empty.textContent = 'No evidence labels supplied.';
      chips.appendChild(empty);
    }
    return chips;
  }

  function createWarnings(bundle) {
    const warningTexts = [
      ...(bundle.metadataWarnings ?? []).map((warning) => `Manifest warning: ${warning}`),
      ...(bundle.manifestWarnings ?? []).map((warning) => `Bundle warning: ${warning.message || warning.code || 'metadata warning'}`),
    ].slice(0, 8);

    const section = document.createElement('section');
    section.className = 'artifact-bundle-warnings';
    const heading = document.createElement('h3');
    heading.textContent = 'Warnings';
    section.appendChild(heading);

    if (!warningTexts.length) {
      const empty = document.createElement('p');
      empty.className = 'artifact-bundle-muted';
      empty.textContent = 'No bundle warnings supplied.';
      section.appendChild(empty);
      return section;
    }

    const list = document.createElement('ul');
    warningTexts.forEach((warningText) => {
      const item = document.createElement('li');
      item.textContent = warningText;
      list.appendChild(item);
    });
    section.appendChild(list);
    return section;
  }

  function createFilters(bundle, items) {
    const filters = document.createElement('section');
    filters.className = 'artifact-bundle-filters';
    const heading = document.createElement('h3');
    heading.textContent = 'Filter items';

    const searchLabel = document.createElement('label');
    searchLabel.className = 'artifact-bundle-search';
    const searchText = document.createElement('span');
    searchText.textContent = 'Search artefact metadata';
    const search = document.createElement('input');
    search.type = 'search';
    search.value = searchTerm;
    search.placeholder = 'Title, path, kind, evidence';
    search.autocomplete = 'off';
    search.dataset.artifactSearch = 'true';
    search.setAttribute('aria-label', 'Search artefact metadata');
    searchLabel.append(searchText, search);

    filters.append(
      heading,
      createFilterGroup('Evidence', ['', ...(bundle.evidenceLevels || [])], evidenceFilter, 'artifactEvidenceFilter', 'All evidence'),
      createFilterGroup('Kind', ['', ...ARTIFACT_READER_GROUPS.filter((group) => items.some((item) => item.group === group))], kindFilter, 'artifactKindFilter', 'All kinds'),
      searchLabel
    );

    if (evidenceFilter || kindFilter || searchTerm) {
      const clear = document.createElement('button');
      clear.type = 'button';
      clear.className = 'artifact-bundle-clear';
      clear.dataset.artifactReaderAction = 'clearFilters';
      clear.textContent = 'Clear filters';
      filters.appendChild(clear);
    }

    return filters;
  }

  function createFilterGroup(label, values, activeValue, datasetName, allLabel) {
    const group = document.createElement('div');
    group.className = 'artifact-bundle-filter-group';
    const groupLabel = document.createElement('span');
    groupLabel.textContent = label;
    const buttons = document.createElement('div');
    buttons.className = 'artifact-bundle-filter-buttons';

    values.forEach((value) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.dataset[datasetName] = value;
      button.setAttribute('aria-pressed', String(value === activeValue));
      button.textContent = value || allLabel;
      buttons.appendChild(button);
    });

    group.append(groupLabel, buttons);
    return group;
  }

  function createNavigation(items) {
    const section = document.createElement('section');
    section.className = 'artifact-bundle-nav';
    const heading = document.createElement('h3');
    heading.textContent = 'Bundle items';
    section.appendChild(heading);

    if (!items.length) {
      const empty = document.createElement('p');
      empty.className = 'artifact-bundle-muted';
      empty.textContent = 'No safe bundle items match these filters.';
      section.appendChild(empty);
      return section;
    }

    ARTIFACT_READER_GROUPS.forEach((group) => {
      const groupItems = items.filter((item) => item.group === group);
      if (!groupItems.length) return;

      const groupWrap = document.createElement('div');
      groupWrap.className = 'artifact-bundle-nav-group';
      const groupTitle = document.createElement('h4');
      groupTitle.textContent = group;
      groupWrap.appendChild(groupTitle);

      groupItems.forEach((entry) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = `artifact-bundle-item${entry.path === state.activePath ? ' active' : ''}`;
        button.dataset.artifactOpenPath = entry.path;
        button.title = entry.path;

        const title = document.createElement('span');
        title.className = 'artifact-bundle-item-title';
        title.textContent = entry.title || entry.path;
        const meta = document.createElement('span');
        meta.className = 'artifact-bundle-item-meta';
        meta.textContent = [entry.kind || entry.type, entry.evidenceLevel, entry.path].filter(Boolean).join(' · ');

        button.append(title, meta);
        groupWrap.appendChild(button);
      });

      section.appendChild(groupWrap);
    });

    return section;
  }

  function filterItems(items) {
    return items.filter((item) => {
      if (evidenceFilter && item.evidenceLevel !== evidenceFilter) return false;
      if (kindFilter && item.group !== kindFilter) return false;
      if (!searchTerm) return true;
      const text = [
        item.title,
        item.path,
        item.kind,
        item.evidenceLevel,
        item.group,
        item.type,
      ].filter(Boolean).join(' ').toLowerCase();
      return text.includes(searchTerm);
    });
  }

  return {
    render,
    resetFilters,
  };
}

export function getSafeReaderItems(bundle, files = []) {
  if (!bundle) return [];
  const filePaths = new Set(files.map((file) => file.path));
  return [...(bundle.documents || []), ...(bundle.diagrams || [])]
    .filter((entry) => entry?.path && filePaths.has(entry.path))
    .map((entry) => ({
      ...entry,
      group: classifyReaderGroup(entry),
      sortOrder: Number.isFinite(entry.order) ? entry.order : Number.POSITIVE_INFINITY,
    }))
    .sort((left, right) => {
      const groupOrder = ARTIFACT_READER_GROUPS.indexOf(left.group) - ARTIFACT_READER_GROUPS.indexOf(right.group);
      return groupOrder
        || left.sortOrder - right.sortOrder
        || String(left.title || left.path).localeCompare(String(right.title || right.path))
        || String(left.path).localeCompare(String(right.path));
    });
}

function classifyReaderGroup(entry) {
  const kind = String(entry.kind || '').toLowerCase();
  const path = String(entry.path || '').toLowerCase();
  if (entry.type === 'diagram' || /\b(diagram|mermaid|mmd)\b/.test(kind) || /\.(mmd|mermaid)$/.test(path)) return 'Diagrams';
  if (/\bfinding/.test(kind) || /(^|\/)findings?\//.test(path)) return 'Findings';
  if (/\badr\b|architecture decision/.test(kind) || /(^|\/)adrs?\//.test(path) || /adr-\d+/i.test(path)) return 'ADRs';
  if (/release/.test(kind) || /release[- ]?notes/.test(path)) return 'Release notes';
  if (/summary|overview/.test(kind) || /(^|\/)(readme|index)\.(md|markdown)$/i.test(path)) return 'Summary';
  if (/doc|guide|runbook|manual/.test(kind) || /(^|\/)docs?\//.test(path)) return 'Documentation';
  return 'Other';
}

function hasCandidateFinding(bundle) {
  const entries = [
    ...(bundle.evidenceLevels || []),
    ...(bundle.documents || []).map((entry) => entry.evidenceLevel),
    ...(bundle.diagrams || []).map((entry) => entry.evidenceLevel),
    ...(bundle.manifestWarnings || []).map((warning) => warning.evidenceLevel),
  ];
  return entries.includes('candidate finding');
}
