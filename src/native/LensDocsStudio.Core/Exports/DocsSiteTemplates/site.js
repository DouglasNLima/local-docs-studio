// Static Docs Site output only. The native application never executes this script.
(() => {
  'use strict';
  const data = window.__LENS_DOCS_SITE_DATA__;
  const content = document.getElementById('content');
  if (!data || !Array.isArray(data.pages)) { content.textContent = 'Keep index.html beside the exported assets folder.'; return; }
  const byId = new Map(data.pages.map(page => [page.id, page]));
  const nav = document.getElementById('nav');
  const search = document.getElementById('searchInput');
  const results = document.getElementById('searchResults');
  const themeButton = document.getElementById('themeToggle');
  const scheme = matchMedia('(prefers-color-scheme: dark)');
  const storageKey = 'lens-docs-studio.docs-site.theme';
  let theme = data.defaultTheme || 'system';
  try { theme = localStorage.getItem(storageKey) || theme; } catch { }
  if (!['light', 'dark', 'system'].includes(theme)) theme = 'system';
  let currentId = '';
  document.getElementById('siteTitle').textContent = data.title;
  document.getElementById('siteDescription').textContent = data.description;
  function element(tag, text, className) {
    const node = document.createElement(tag); node.textContent = text;
    if (className) node.className = className;
    return node;
  }
  function applyTheme() {
    document.documentElement.dataset.theme = theme === 'system' ? (scheme.matches ? 'dark' : 'light') : theme;
    themeButton.textContent = theme[0].toUpperCase() + theme.slice(1) + ' theme';
  }
  themeButton.addEventListener('click', () => {
    theme = theme === 'system' ? 'light' : theme === 'light' ? 'dark' : 'system';
    try { localStorage.setItem(storageKey, theme); } catch { }
    applyTheme();
  });
  scheme.addEventListener('change', applyTheme); applyTheme();
  function renderNavigation() {
    nav.replaceChildren(); let group = '';
    for (const page of data.pages) {
      if (page.navGroup && page.navGroup !== group) { group = page.navGroup; nav.append(element('h2', group)); }
      const link = element('a', page.title + (page.draft ? ' · Draft' : ''));
      link.href = '#' + page.id;
      if (page.id === currentId) link.setAttribute('aria-current', 'page');
      nav.append(link);
      if (page.id === currentId) for (const heading of page.headings || []) {
        const anchor = element('a', heading.text, 'heading');
        anchor.href = '#' + page.id + '/' + encodeURIComponent(heading.id); nav.append(anchor);
      }
    }
  }
  function showPage() {
    if (location.hash === '#content') { content.focus(); return; }
    const [id, fragment = ''] = location.hash.slice(1).split('/', 2);
    const page = byId.get(id) || byId.get(data.homePageId) || data.pages[0];
    if (!page) return;
    currentId = page.id; document.title = page.title + ' — ' + data.title;
    // Page HTML is generated and sanitised by the native exporter. Metadata uses text nodes.
    content.innerHTML = page.html;
    for (const link of content.querySelectorAll('a[href]')) {
      if (/^(https?:|mailto:)/i.test(link.getAttribute('href'))) { link.target = '_blank'; link.rel = 'noopener noreferrer'; }
    }
    const metadata = document.getElementById('docMeta'); metadata.replaceChildren();
    for (const text of [page.path, page.description, page.navGroup, page.artifactKind, page.evidenceLevel, page.draft ? 'Draft' : '', ...(page.tags || []).map(tag => '#' + tag)].filter(Boolean)) metadata.append(element('span', text));
    for (const note of data.artifactBundle?.notes || []) metadata.append(element('span', note));
    renderNavigation();
    let headingId = ''; try { headingId = decodeURIComponent(fragment); } catch { }
    const target = headingId ? Array.from(content.querySelectorAll('[id]')).find(node => node.id === headingId) : null;
    for (let ancestor = target?.parentElement; ancestor && ancestor !== content; ancestor = ancestor.parentElement)
      if (ancestor.tagName === 'DETAILS') ancestor.open = true;
    if (target) target.tabIndex = -1;
    requestAnimationFrame(() => { (target || content).scrollIntoView({ block:'start' }); if (fragment) (target || content).focus({ preventScroll:true }); });
  }
  let searchGeneration = 0;
  function snippet(value, terms) {
    const text = value.replace(/\s+/g, ' '); const lower = text.toLowerCase();
    const matches = terms.map(term => lower.indexOf(term)).filter(index => index >= 0);
    const start = Math.max(0, (matches.length ? Math.min(...matches) : 0) - 54);
    const visible = (start ? '…' : '') + text.slice(start, start + 180) + (start + 180 < text.length ? '…' : '');
    const normalised = visible.toLowerCase(); const span = element('span', ''); let position = 0;
    while (position < visible.length) {
      let match = -1; let size = 0;
      for (const term of terms) {
        const index = normalised.indexOf(term, position);
        if (index >= 0 && (match < 0 || index < match || index === match && term.length > size)) { match = index; size = term.length; }
      }
      if (match < 0) { span.append(document.createTextNode(visible.slice(position))); break; }
      span.append(document.createTextNode(visible.slice(position, match)), element('mark', visible.slice(match, match + size)));
      position = match + size;
    }
    return span;
  }
  search.addEventListener('input', async () => {
    const generation = ++searchGeneration;
    if (search.value.length > 512) { results.hidden = false; nav.hidden = true; results.replaceChildren(element('p', 'Use up to 512 characters in a search.')); return; }
    const terms = [...new Set(search.value.trim().toLowerCase().split(/\s+/).filter(Boolean))];
    results.replaceChildren(); results.hidden = terms.length === 0; nav.hidden = terms.length !== 0;
    if (!terms.length) return;
    if (terms.length > 16) { results.append(element('p', 'Use up to 16 search terms.')); return; }
    results.append(element('p', 'Searching…'));
    const matched = []; let batchStarted = performance.now();
    for (const entry of data.entries || []) {
      const indexed = entry.searchText || (entry.searchText = {
        title:entry.title.toLowerCase(), heading:entry.headingText.toLowerCase(), path:entry.path.toLowerCase(), text:entry.text.toLowerCase()
      });
      if (terms.every(term => indexed.title.includes(term) || indexed.heading.includes(term) || indexed.path.includes(term) || indexed.text.includes(term))) {
        const score = terms.reduce((total, term) => total + (indexed.heading.includes(term) ? 12 : 0) + (indexed.title.includes(term) ? 8 : 0)
          + (indexed.path.includes(term) ? 4 : 0) + (indexed.text.includes(term) ? 1 : 0), 0);
        matched.push({ entry, score });
      }
      if (performance.now() - batchStarted >= 8) {
        await new Promise(resolve => setTimeout(resolve, 0));
        if (generation !== searchGeneration) return;
        batchStarted = performance.now();
      }
    }
    if (generation !== searchGeneration) return;
    results.replaceChildren();
    for (const { entry } of matched.sort((a, b) => b.score - a.score).slice(0, 30)) {
      const button = element('button', '', 'search-result'); button.type = 'button';
      button.append(element('strong', entry.headingId ? entry.headingText : entry.title), element('small', entry.headingId ? entry.title + ' · ' + entry.path : entry.path), snippet(entry.text, terms));
      button.addEventListener('click', () => {
        const hash = '#' + entry.pageId + (entry.headingId ? '/' + encodeURIComponent(entry.headingId) : '');
        if (location.hash === hash) showPage(); else location.hash = hash;
      });
      results.append(button);
    }
    if (!matched.length) results.append(element('p', 'No results.'));
  });
  window.addEventListener('hashchange', showPage); showPage();
})();
