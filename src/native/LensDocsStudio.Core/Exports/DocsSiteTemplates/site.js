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
    for (const text of [page.path, page.description, page.navGroup, page.draft ? 'Draft' : '', ...(page.tags || []).map(tag => '#' + tag)].filter(Boolean)) metadata.append(element('span', text));
    renderNavigation();
    let headingId = ''; try { headingId = decodeURIComponent(fragment); } catch { }
    const target = headingId ? Array.from(content.querySelectorAll('[id]')).find(node => node.id === headingId) : null;
    if (target) target.tabIndex = -1;
    requestAnimationFrame(() => { (target || content).scrollIntoView({ block:'start' }); if (fragment) (target || content).focus({ preventScroll:true }); });
  }
  search.addEventListener('input', () => {
    const terms = search.value.trim().toLocaleLowerCase().split(/\s+/).filter(Boolean);
    results.replaceChildren(); results.hidden = terms.length === 0; nav.hidden = terms.length !== 0;
    if (!terms.length) return;
    const matched = data.entries.map(entry => {
      const title = (entry.title + ' ' + entry.headingText).toLocaleLowerCase();
      const text = (entry.text + ' ' + entry.path).toLocaleLowerCase();
      return { entry, score:terms.every(term => title.includes(term) || text.includes(term)) ? terms.reduce((score, term) => score + (title.includes(term) ? 8 : 1), 0) : 0 };
    }).filter(item => item.score > 0).sort((a, b) => b.score - a.score).slice(0, 30);
    for (const { entry } of matched) {
      const button = element('button', '', 'search-result'); button.type = 'button';
      button.append(element('strong', entry.title), element('small', entry.path), element('span', entry.text.replace(/\s+/g, ' ').slice(0, 180)));
      button.addEventListener('click', () => { location.hash = entry.pageId; showPage(); }); results.append(button);
    }
    if (!matched.length) results.append(element('p', 'No results.'));
  });
  window.addEventListener('hashchange', showPage); showPage();
})();
