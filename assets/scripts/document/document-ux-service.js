import { storageKeys } from '../state/config.js';
import { cssEscape, slugify } from '../utils/format.js';
import { resolveWikilinkTarget } from '../utils/wikilinks.js';

const SEARCH_IGNORE_SELECTOR = [
  '.code-block-header',
  '.table-block-header',
  '.diagram-toolbar',
  '.diagram-error-actions',
  'button',
  'script',
  'style',
  'svg',
].join(',');

export function createDocumentUxService({ state, dom, callbacks = {} }) {
  const {
    preview,
    outlineToggleButton,
    outlinePanel,
    outlineLinks,
    previewFindToggleButton,
    previewFindPanel,
    previewFindInput,
    previewFindCount,
    previewFindPrevButton,
    previewFindNextButton,
    previewFindClearButton,
    documentReviewToggleButton,
    documentReviewPanel,
    documentReviewSummary,
    documentReviewMetrics,
    documentReviewAlerts,
  } = dom;
  const {
    getBacklinks,
    getWorkspaceAudit,
    getGovernanceAudit,
    openBacklink,
    openGovernanceIssue,
  } = callbacks;

  const searchState = {
    matches: [],
    activeIndex: -1,
  };
  let outlineItems = [];
  let scrollFrame = 0;
  let outlineClickLockTarget = '';
  let outlineClickLockTimer = 0;
  let governanceRunId = 0;

  function installDocumentUxHandlers() {
    outlineToggleButton.addEventListener('click', toggleOutline);
    outlineLinks.addEventListener('click', handleOutlineClick);
    preview.addEventListener('scroll', scheduleActiveOutlineUpdate, { passive: true });

    previewFindToggleButton.addEventListener('click', togglePreviewFind);
    previewFindInput.addEventListener('input', () => applyPreviewSearch(previewFindInput.value));
    previewFindInput.addEventListener('keydown', (event) => {
      if (event.key === 'Enter') {
        event.preventDefault();
        if (event.shiftKey) goToSearchMatch(-1);
        else goToSearchMatch(1);
      }
      if (event.key === 'Escape') {
        closePreviewFind();
      }
    });
    previewFindPrevButton.addEventListener('click', () => goToSearchMatch(-1));
    previewFindNextButton.addEventListener('click', () => goToSearchMatch(1));
    previewFindClearButton.addEventListener('click', closePreviewFind);

    documentReviewToggleButton.addEventListener('click', toggleDocumentReview);
    documentReviewAlerts.addEventListener('click', handleReviewTargetClick);
  }

  function updateDocumentUx() {
    clearPreviewSearch({ clearInput: false });
    updatePreviewOutline();
    updateDocumentReview();
  }

  function toggleOutline() {
    state.outlineOpen = !state.outlineOpen;
    localStorage.setItem(storageKeys.outline, String(state.outlineOpen));
    updatePreviewOutline();
  }

  function updatePreviewOutline() {
    clearOutlineClickLock();
    outlineToggleButton.setAttribute('aria-pressed', String(state.outlineOpen));
    outlineItems = collectOutlineItems();
    outlineLinks.innerHTML = '';

    if (!state.outlineOpen || !outlineItems.length) {
      outlinePanel.hidden = true;
      return;
    }

    outlineItems.forEach((item) => {
      const link = document.createElement('a');
      link.href = `#${item.id}`;
      link.dataset.outlineTarget = item.id;
      link.dataset.outlineLevel = String(item.level);
      link.className = `outline-level-${item.level}`;
      link.textContent = item.text;
      outlineLinks.appendChild(link);
    });

    outlinePanel.hidden = false;
    updateActiveOutlineLink();
  }

  function collectOutlineItems() {
    const usedIds = new Set();
    return [...getContentRoot().querySelectorAll('h1, h2, h3, h4')].map((heading, index) => {
      const level = Number(heading.tagName.slice(1));
      const text = heading.textContent.trim() || `Section ${index + 1}`;
      const base = slugify(text) || `section-${index + 1}`;
      let id = base;
      let suffix = 2;
      while (usedIds.has(id)) {
        id = `${base}-${suffix}`;
        suffix += 1;
      }
      usedIds.add(id);
      heading.id = id;
      return { id, level, text, heading };
    });
  }

  function handleOutlineClick(event) {
    const link = event.target.closest('[data-outline-target]');
    if (!link) return;
    event.preventDefault();
    const target = preview.querySelector(`#${cssEscape(link.dataset.outlineTarget)}`);
    lockActiveOutlineLink(link.dataset.outlineTarget);
    scrollPreviewTarget(target);
    setActiveOutlineLink(link.dataset.outlineTarget);
  }

  function scheduleActiveOutlineUpdate() {
    if (outlineClickLockTarget) return;
    if (scrollFrame) return;
    scrollFrame = requestAnimationFrame(() => {
      scrollFrame = 0;
      updateActiveOutlineLink();
    });
  }

  function updateActiveOutlineLink() {
    if (!state.outlineOpen || !outlineItems.length) return;
    const previewTop = preview.getBoundingClientRect().top;
    const threshold = previewTop + 96;
    let active = outlineItems[0];

    outlineItems.forEach((item) => {
      if (item.heading.getBoundingClientRect().top <= threshold) {
        active = item;
      }
    });

    setActiveOutlineLink(active?.id);
  }

  function setActiveOutlineLink(id) {
    outlineLinks.querySelectorAll('[data-outline-target]').forEach((link) => {
      link.classList.toggle('active', link.dataset.outlineTarget === id);
    });
  }

  function lockActiveOutlineLink(id) {
    outlineClickLockTarget = id;
    window.clearTimeout(outlineClickLockTimer);
    outlineClickLockTimer = window.setTimeout(() => {
      const lockedTarget = outlineClickLockTarget;
      outlineClickLockTarget = '';
      setActiveOutlineLink(lockedTarget);
    }, 450);
  }

  function clearOutlineClickLock() {
    outlineClickLockTarget = '';
    window.clearTimeout(outlineClickLockTimer);
    outlineClickLockTimer = 0;
  }

  function togglePreviewFind() {
    const shouldOpen = previewFindPanel.hidden;
    previewFindPanel.hidden = !shouldOpen;
    previewFindToggleButton.setAttribute('aria-pressed', String(shouldOpen));
    if (shouldOpen) {
      previewFindInput.focus();
      previewFindInput.select();
      applyPreviewSearch(previewFindInput.value);
    } else {
      clearPreviewSearch({ clearInput: true });
    }
  }

  function closePreviewFind() {
    previewFindPanel.hidden = true;
    previewFindToggleButton.setAttribute('aria-pressed', 'false');
    clearPreviewSearch({ clearInput: true });
  }

  function applyPreviewSearch(query) {
    clearSearchHighlights();
    searchState.matches = [];
    searchState.activeIndex = -1;

    const needle = String(query || '').trim().toLowerCase();
    if (!needle) {
      updateFindCount();
      return;
    }

    const textNodes = collectSearchableTextNodes();
    textNodes.forEach((node) => highlightTextNode(node, needle));
    searchState.matches = [...preview.querySelectorAll('mark.preview-search-hit')];
    searchState.activeIndex = searchState.matches.length ? 0 : -1;
    updateSearchActiveMatch(true);
  }

  function collectSearchableTextNodes() {
    const nodes = [];
    const walker = document.createTreeWalker(getContentRoot(), NodeFilter.SHOW_TEXT, {
      acceptNode: (node) => {
        if (!node.textContent.trim()) return NodeFilter.FILTER_REJECT;
        const parent = node.parentElement;
        if (!parent || parent.closest(SEARCH_IGNORE_SELECTOR)) return NodeFilter.FILTER_REJECT;
        return NodeFilter.FILTER_ACCEPT;
      },
    });

    while (walker.nextNode()) {
      nodes.push(walker.currentNode);
    }
    return nodes;
  }

  function highlightTextNode(node, needle) {
    const text = node.textContent;
    const lower = text.toLowerCase();
    const fragment = document.createDocumentFragment();
    let cursor = 0;
    let matchIndex = lower.indexOf(needle);

    while (matchIndex !== -1) {
      if (matchIndex > cursor) {
        fragment.appendChild(document.createTextNode(text.slice(cursor, matchIndex)));
      }
      const mark = document.createElement('mark');
      mark.className = 'preview-search-hit';
      mark.textContent = text.slice(matchIndex, matchIndex + needle.length);
      fragment.appendChild(mark);
      cursor = matchIndex + needle.length;
      matchIndex = lower.indexOf(needle, cursor);
    }

    if (cursor < text.length) {
      fragment.appendChild(document.createTextNode(text.slice(cursor)));
    }
    node.replaceWith(fragment);
  }

  function goToSearchMatch(step) {
    if (!searchState.matches.length) return;
    searchState.activeIndex = (searchState.activeIndex + step + searchState.matches.length) % searchState.matches.length;
    updateSearchActiveMatch(true);
  }

  function updateSearchActiveMatch(shouldScroll = false) {
    searchState.matches.forEach((match, index) => {
      match.classList.toggle('active', index === searchState.activeIndex);
    });
    updateFindCount();
    if (shouldScroll && searchState.activeIndex >= 0) {
      scrollPreviewTarget(searchState.matches[searchState.activeIndex]);
    }
  }

  function updateFindCount() {
    const total = searchState.matches.length;
    previewFindCount.textContent = total && searchState.activeIndex >= 0 ? `${searchState.activeIndex + 1}/${total}` : `0/${total}`;
    previewFindPrevButton.disabled = total < 2;
    previewFindNextButton.disabled = total < 2;
  }

  function clearPreviewSearch({ clearInput = true } = {}) {
    clearSearchHighlights();
    searchState.matches = [];
    searchState.activeIndex = -1;
    if (clearInput) previewFindInput.value = '';
    updateFindCount();
  }

  function clearSearchHighlights() {
    preview.querySelectorAll('mark.preview-search-hit').forEach((mark) => {
      mark.replaceWith(document.createTextNode(mark.textContent));
    });
    getContentRoot().normalize();
  }

  function toggleDocumentReview() {
    state.documentReviewOpen = !state.documentReviewOpen;
    localStorage.setItem(storageKeys.documentReviewOpen, String(state.documentReviewOpen));
    updateDocumentReview();
  }

  function updateDocumentReview() {
    documentReviewToggleButton.setAttribute('aria-pressed', String(state.documentReviewOpen));
    documentReviewPanel.hidden = !state.documentReviewOpen;

    const review = buildDocumentReview();
    updateReviewSummary(review);

    documentReviewMetrics.innerHTML = '';
    review.metrics.forEach((metric) => {
      appendReviewMetric(metric.label, metric.value);
    });

    documentReviewAlerts.innerHTML = '';
    if (!review.alerts.length) {
      const item = document.createElement('div');
      item.className = 'document-review-note ok';
      item.textContent = 'No review notes. Document looks ready.';
      documentReviewAlerts.appendChild(item);
    } else {
      review.alerts.forEach((alert) => {
        const item = alert.targetId ? document.createElement('button') : document.createElement('div');
        item.className = `document-review-note ${alert.tone}`;
        item.textContent = alert.message;
        if (alert.targetId) {
          item.type = 'button';
          item.dataset.reviewTarget = alert.targetId;
        }
        documentReviewAlerts.appendChild(item);
      });
    }
    updateGovernanceAudit(review);
    updateBacklinks();
    updateWorkspaceAudit();
  }

  function updateReviewSummary(review, governanceSummary = null) {
    const governanceCount = governanceSummary?.issueCount || 0;
    const noteCount = review.alerts.length + governanceCount;
    documentReviewSummary.textContent = `${review.wordCount} words · ${review.readingMinutes} min read · ${noteCount} note${noteCount === 1 ? '' : 's'}`;
  }

  function appendReviewMetric(label, value) {
    const item = document.createElement('span');
    item.className = 'document-review-metric';
    item.textContent = `${label}: ${value}`;
    documentReviewMetrics.appendChild(item);
  }

  function buildDocumentReview() {
    const root = getContentRoot();
    const text = getReviewText(root);
    const words = text.match(/[\p{L}\p{N}]+(?:['-][\p{L}\p{N}]+)*/gu) ?? [];
    const headings = [...root.querySelectorAll('h1, h2, h3, h4')];
    const h1s = headings.filter((heading) => heading.tagName.toLowerCase() === 'h1');
    const links = [...root.querySelectorAll('a[href]')];
    const externalLinks = links.filter((link) => /^(https?:)?\/\//i.test(link.getAttribute('href') || ''));
    const unresolvedWikilinks = [...root.querySelectorAll('[data-wikilink-target]')]
      .filter((link) => !resolveWikilinkTarget(link.dataset.wikilinkTarget || '', state.files, state.activePath));
    const brokenRelativeLinks = links.filter((link) => isBrokenRelativeDocumentLink(link));
    const missingImages = [...root.querySelectorAll('img[src]')].filter((image) => isMissingManagedImage(image));
    const tables = root.querySelectorAll('table').length;
    const codeBlocks = root.querySelectorAll('.code-block').length
      + [...root.querySelectorAll('pre code')].filter((code) => !code.closest('.code-block')).length;
    const diagrams = root.querySelectorAll('.diagram-frame').length || state.lastRenderResult.diagramTotal || 0;
    const diagramErrors = root.querySelectorAll('.mermaid-error, .diagram-error').length || state.lastRenderResult.diagramErrors || 0;
    const wordCount = words.length;
    const readingMinutes = wordCount ? Math.max(1, Math.ceil(wordCount / 220)) : 0;
    const alerts = [];

    if (!wordCount && !diagrams) {
      alerts.push({ message: 'Document is empty.', tone: 'warning' });
    }

    if (wordCount && !h1s.length) {
      alerts.push({ message: 'Missing H1 title.', tone: 'warning', targetId: headings[0]?.id || '' });
    }

    const headingJump = findHeadingJump(headings);
    if (headingJump) {
      alerts.push({
        message: `Heading level jumps from H${headingJump.previousLevel} to H${headingJump.level}.`,
        tone: 'warning',
        targetId: headingJump.id,
      });
    }

    const duplicateHeading = findDuplicateHeading(headings);
    if (duplicateHeading) {
      alerts.push({
        message: `Duplicate heading "${duplicateHeading.text}" found.`,
        tone: 'info',
        targetId: duplicateHeading.id,
      });
    }

    if (diagramErrors) {
      const target = root.querySelector('.mermaid-error, .diagram-error');
      alerts.push({
        message: `${diagramErrors} Mermaid diagram error${diagramErrors === 1 ? '' : 's'} found.`,
        tone: 'danger',
        targetId: ensureElementId(target?.closest('.diagram-frame') || target, 'diagram-error'),
      });
    }

    if (externalLinks.length) {
      alerts.push({
        message: `${externalLinks.length} external link${externalLinks.length === 1 ? '' : 's'} present.`,
        tone: 'info',
        targetId: ensureElementId(externalLinks[0], 'external-link'),
      });
    }

    if (unresolvedWikilinks.length) {
      alerts.push({
        message: `${unresolvedWikilinks.length} unresolved wikilink${unresolvedWikilinks.length === 1 ? '' : 's'} found.`,
        tone: 'warning',
        targetId: ensureElementId(unresolvedWikilinks[0], 'broken-wikilink'),
      });
    }

    if (brokenRelativeLinks.length) {
      alerts.push({
        message: `${brokenRelativeLinks.length} relative document link${brokenRelativeLinks.length === 1 ? '' : 's'} do not match loaded files.`,
        tone: 'warning',
        targetId: ensureElementId(brokenRelativeLinks[0], 'broken-link'),
      });
    }

    if (missingImages.length) {
      alerts.push({
        message: `${missingImages.length} local image reference${missingImages.length === 1 ? '' : 's'} are not managed session assets.`,
        tone: 'warning',
        targetId: ensureElementId(missingImages[0], 'missing-image'),
      });
    }

    if (state.files.length > 1 && state.dirtyPaths.size) {
      alerts.push({
        message: `${state.dirtyPaths.size} file${state.dirtyPaths.size === 1 ? '' : 's'} edited in memory.`,
        tone: 'info',
      });
    }

    return {
      wordCount,
      readingMinutes,
      alerts,
      metrics: [
        { label: 'Headings', value: headings.length },
        { label: 'Links', value: links.length },
        { label: 'Broken links', value: unresolvedWikilinks.length + brokenRelativeLinks.length },
        { label: 'Images', value: root.querySelectorAll('img').length },
        { label: 'Tables', value: tables },
        { label: 'Code', value: codeBlocks },
        { label: 'Diagrams', value: diagrams },
      ],
    };
  }

  function getReviewText(root) {
    const clone = root.cloneNode(true);
    clone.querySelectorAll(`${SEARCH_IGNORE_SELECTOR}, .preview-search-hit`).forEach((element) => {
      element.remove();
    });
    return clone.textContent.replace(/\s+/g, ' ').trim();
  }

  function findHeadingJump(headings) {
    let previousLevel = 0;
    for (const heading of headings) {
      const level = Number(heading.tagName.slice(1));
      if (previousLevel && level > previousLevel + 1) {
        return { id: heading.id, level, previousLevel };
      }
      previousLevel = level;
    }
    return null;
  }

  function findDuplicateHeading(headings) {
    const seen = new Set();
    for (const heading of headings) {
      const text = heading.textContent.trim().toLowerCase();
      if (!text) continue;
      if (seen.has(text)) return { id: heading.id, text: heading.textContent.trim() };
      seen.add(text);
    }
    return null;
  }

  function handleReviewTargetClick(event) {
    const governance = event.target.closest('[data-governance-path]');
    if (governance) {
      openGovernanceIssue?.(
        governance.dataset.governancePath,
        Number(governance.dataset.governanceLine || '1'),
        Number(governance.dataset.governanceColumn || '1'),
        Number(governance.dataset.governanceLength || '1'),
      );
      return;
    }

    const backlink = event.target.closest('[data-backlink-path]');
    if (backlink) {
      openBacklink?.(backlink.dataset.backlinkPath, Number(backlink.dataset.backlinkLine || '1'));
      return;
    }

    const item = event.target.closest('[data-review-target]');
    if (!item) return;
    scrollPreviewTarget(preview.querySelector(`#${cssEscape(item.dataset.reviewTarget)}`));
  }

  function isBrokenRelativeDocumentLink(link) {
    if (link.dataset.wikilinkTarget) return false;
    const href = link.getAttribute('href') || '';
    if (!href || href.startsWith('#') || /^(https?:|mailto:|blob:|data:)/i.test(href)) return false;
    const target = href.split(/[?#]/)[0];
    if (!target || /\.(png|jpe?g|gif|webp|svg|pdf|zip)$/i.test(target)) return false;
    return !resolveWikilinkTarget(target, state.files, state.activePath);
  }

  function isMissingManagedImage(image) {
    const src = image.getAttribute('src') || '';
    if (!src || /^(https?:|blob:|data:)/i.test(src)) return false;
    const path = normaliseRelativeAssetPath(src);
    return !state.managedAssets?.has(path);
  }

  function normaliseRelativeAssetPath(value) {
    const activeDir = state.activePath.includes('/') ? state.activePath.slice(0, state.activePath.lastIndexOf('/') + 1) : '';
    return String(`${activeDir}${value}`)
      .replace(/\\/g, '/')
      .replace(/^\.\/+/, '')
      .replace(/\/\.\//g, '/')
      .replace(/[^/]+\/\.\.\//g, '');
  }

  async function updateGovernanceAudit(review) {
    if (!getGovernanceAudit || !state.documentReviewOpen) return;
    const runId = ++governanceRunId;
    const marker = document.createElement('div');
    marker.className = 'document-review-links document-governance-audit';
    marker.textContent = 'Checking Markdown governance...';
    documentReviewAlerts.appendChild(marker);

    const audit = await getGovernanceAudit();
    if (!marker.isConnected || runId !== governanceRunId || !state.documentReviewOpen) return;

    const issues = audit?.issues || [];
    const summary = audit?.summary || {
      fileCount: 0,
      issueCount: issues.length,
      warningCount: issues.filter((issue) => issue.severity !== 'info').length,
      infoCount: issues.filter((issue) => issue.severity === 'info').length,
      activeIssueCount: issues.filter((issue) => issue.path === state.activePath).length,
      workspaceIssueCount: issues.filter((issue) => issue.path !== state.activePath).length,
      ruleCounts: [],
    };

    updateReviewSummary(review, summary);
    appendReviewMetric('Governance', summary.issueCount);
    appendReviewMetric('Warnings', summary.warningCount);
    appendReviewMetric('Suggestions', summary.infoCount);

    marker.innerHTML = '';
    const title = document.createElement('strong');
    title.textContent = `Governance (${summary.issueCount})`;
    marker.appendChild(title);

    const scope = document.createElement('span');
    scope.textContent = `Scanned ${summary.fileCount} file${summary.fileCount === 1 ? '' : 's'}: ${summary.activeIssueCount} here, ${summary.workspaceIssueCount} elsewhere.`;
    marker.appendChild(scope);

    if (!issues.length) {
      const ok = document.createElement('span');
      ok.textContent = 'No Markdown governance issues found.';
      marker.appendChild(ok);
      return;
    }

    const groups = document.createElement('div');
    groups.className = 'document-governance-groups';
    summary.ruleCounts.forEach((group) => {
      const item = document.createElement('span');
      item.className = 'document-review-metric';
      item.textContent = `${group.label}: ${group.count}`;
      groups.appendChild(item);
    });
    marker.appendChild(groups);

    issues.slice(0, 12).forEach((issue) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = `document-review-note ${issue.severity === 'info' ? 'info' : 'warning'}`;
      button.dataset.governancePath = issue.path;
      button.dataset.governanceLine = String(issue.line);
      button.dataset.governanceColumn = String(issue.column);
      button.dataset.governanceLength = String(issue.length || 1);
      button.textContent = `${issue.path}:${issue.line} ${issue.message}${issue.suggestion ? ` ${issue.suggestion}` : ''}`;
      marker.appendChild(button);
    });

    if (issues.length > 12) {
      const remaining = document.createElement('span');
      remaining.textContent = `${issues.length - 12} more issue${issues.length - 12 === 1 ? '' : 's'} in the loaded workspace.`;
      marker.appendChild(remaining);
    }
  }

  async function updateBacklinks() {
    if (!getBacklinks || !state.documentReviewOpen || !state.activePath) return;
    const marker = document.createElement('div');
    marker.className = 'document-review-links';
    marker.textContent = 'Loading backlinks...';
    documentReviewAlerts.appendChild(marker);

    const backlinks = await getBacklinks();
    if (!marker.isConnected) return;
    marker.innerHTML = '';
    const title = document.createElement('strong');
    title.textContent = `Backlinks (${backlinks.length})`;
    marker.appendChild(title);
    if (!backlinks.length) {
      const empty = document.createElement('span');
      empty.textContent = 'No loaded files link here.';
      marker.appendChild(empty);
      return;
    }

    backlinks.slice(0, 12).forEach((link) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'document-review-note info';
      button.dataset.backlinkPath = link.path;
      button.dataset.backlinkLine = String(link.line);
      button.textContent = `${link.path}:${link.line} -> ${link.label || link.target}`;
      marker.appendChild(button);
    });
  }

  async function updateWorkspaceAudit() {
    if (!getWorkspaceAudit || !state.documentReviewOpen) return;
    const marker = document.createElement('div');
    marker.className = 'document-review-links';
    marker.textContent = 'Checking workspace links...';
    documentReviewAlerts.appendChild(marker);

    const audit = await getWorkspaceAudit();
    if (!marker.isConnected) return;
    marker.innerHTML = '';
    const title = document.createElement('strong');
    title.textContent = 'Workspace audit';
    marker.appendChild(title);

    const notes = [
      audit.brokenLinkCount ? `${audit.brokenLinkCount} broken workspace link${audit.brokenLinkCount === 1 ? '' : 's'}.` : '',
      audit.orphanAssetCount ? `${audit.orphanAssetCount} managed asset${audit.orphanAssetCount === 1 ? '' : 's'} not referenced by loaded Markdown.` : '',
      audit.unlinkedPageCount ? `${audit.unlinkedPageCount} page${audit.unlinkedPageCount === 1 ? '' : 's'} without backlinks.` : '',
    ].filter(Boolean);

    if (!notes.length) {
      const ok = document.createElement('span');
      ok.textContent = 'No workspace-level link or asset issues found.';
      marker.appendChild(ok);
      return;
    }

    notes.forEach((note) => {
      const item = document.createElement('div');
      item.className = 'document-review-note warning';
      item.textContent = note;
      marker.appendChild(item);
    });
  }

  function ensureElementId(element, prefix) {
    if (!element) return '';
    if (element.id) return element.id;
    let index = 1;
    let id = `${prefix}-${index}`;
    while (preview.querySelector(`#${cssEscape(id)}`)) {
      index += 1;
      id = `${prefix}-${index}`;
    }
    element.id = id;
    return id;
  }

  function scrollPreviewTarget(target) {
    if (!target) return;
    target.scrollIntoView({ block: 'start', behavior: 'smooth' });
  }

  function getContentRoot() {
    return preview.querySelector('.docs-site-content') || preview;
  }

  return {
    installDocumentUxHandlers,
    toggleOutline,
    updateDocumentUx,
    updatePreviewOutline,
    clearPreviewSearch,
  };
}
