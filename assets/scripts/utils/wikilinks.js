import { escapeHtml } from './format.js';

export const wikilinkPattern = /\[\[([^\]\n]+?)\]\]/g;
export const markdownLinkPattern = /\[([^\]\n]+)\]\((?![a-z][a-z0-9+.-]*:|#)([^)\n]+)\)/gi;

export function splitWikilink(rawValue) {
  const raw = String(rawValue || '').trim();
  const [target, ...labelParts] = raw.split('|');
  const label = labelParts.join('|').trim();
  return {
    target: normaliseWikilinkTarget(target),
    label: label || target.trim(),
  };
}

export function normaliseWikilinkTarget(value) {
  return String(value || '')
    .trim()
    .replace(/\\/g, '/')
    .replace(/^\.\/+/, '')
    .replace(/\/+/g, '/');
}

export function createWikilinkExtension() {
  return {
    name: 'wikilink',
    level: 'inline',
    start(source) {
      return source.indexOf('[[');
    },
    tokenizer(source) {
      const match = source.match(/^\[\[([^\]\n]+?)\]\]/);
      if (!match) return undefined;
      const { target, label } = splitWikilink(match[1]);
      return {
        type: 'wikilink',
        raw: match[0],
        text: label || target,
        target,
      };
    },
    renderer(token) {
      const target = normaliseWikilinkTarget(token.target);
      const label = token.text || target;
      return `<a class="wikilink" href="#" data-wikilink-target="${escapeHtml(target)}">${escapeHtml(label)}</a>`;
    },
  };
}

export function resolveWikilinkTarget(target, files, activePath = '') {
  const clean = normaliseWikilinkTarget(target).replace(/^\/+/, '');
  if (!clean) return null;

  const candidates = files.map((file) => ({
    file,
    path: normaliseWikilinkTarget(file.path),
    pathNoExt: stripExtension(normaliseWikilinkTarget(file.path)),
    name: normaliseWikilinkTarget(file.name),
    nameNoExt: stripExtension(normaliseWikilinkTarget(file.name)),
  }));
  const activeDir = activePath.includes('/') ? activePath.slice(0, activePath.lastIndexOf('/') + 1) : '';
  const relative = activeDir ? normaliseWikilinkTarget(`${activeDir}${clean}`) : clean;

  const keys = [clean, stripExtension(clean), relative, stripExtension(relative)];
  for (const key of keys) {
    const exact = candidates.find(({ path, pathNoExt, name, nameNoExt }) => (
      key === path || key === pathNoExt || key === name || key === nameNoExt
    ));
    if (exact) return exact.file;
  }

  const lowerKeys = keys.map((key) => key.toLowerCase());
  return candidates.find(({ path, pathNoExt, name, nameNoExt }) => lowerKeys.includes(path.toLowerCase())
    || lowerKeys.includes(pathNoExt.toLowerCase())
    || lowerKeys.includes(name.toLowerCase())
    || lowerKeys.includes(nameNoExt.toLowerCase()))?.file || null;
}

export function collectWikilinkTargets(source) {
  const targets = [];
  String(source || '').replace(wikilinkPattern, (raw, value, index) => {
    const link = splitWikilink(value);
    targets.push({ raw, target: link.target, label: link.label, index, kind: 'wikilink' });
    return raw;
  });
  return targets;
}

export function collectMarkdownRelativeTargets(source) {
  const targets = [];
  String(source || '').replace(markdownLinkPattern, (raw, label, href, index) => {
    const target = normaliseWikilinkTarget(String(href || '').split(/[?#]/)[0]);
    if (target) targets.push({ raw, target, label, index, kind: 'markdown' });
    return raw;
  });
  return targets;
}

export function findWikilinkBacklinks({ source, fromPath, activePath, files }) {
  const links = [
    ...collectWikilinkTargets(source),
    ...collectMarkdownRelativeTargets(source),
  ];
  return links
    .map((link) => ({ link, target: resolveWikilinkTarget(link.target, files, fromPath) }))
    .filter(({ target }) => target?.path === activePath)
    .map(({ link }) => link);
}

export function rewriteWikilinksInHtml(html, { pages = [], currentPath = '' } = {}) {
  const template = document.createElement('template');
  template.innerHTML = html;
  template.content.querySelectorAll('[data-wikilink-target]').forEach((link) => {
    const target = resolveWikilinkTarget(link.getAttribute('data-wikilink-target') || '', pages, currentPath);
    if (target?.id) {
      link.setAttribute('href', `./${target.id}.html`);
      link.classList.remove('wikilink-unresolved');
    } else {
      link.removeAttribute('href');
      link.classList.add('wikilink-unresolved');
    }
    link.removeAttribute('data-wikilink-target');
  });
  return template.innerHTML;
}

export function stripAppWikilinkActions(root) {
  root.querySelectorAll('[data-wikilink-target]').forEach((link) => {
    link.removeAttribute('data-wikilink-target');
    if ((link.getAttribute('href') || '') === '#') {
      link.removeAttribute('href');
    }
  });
}

function stripExtension(path) {
  return String(path || '').replace(/\.(md|markdown|mmd|mermaid)$/i, '');
}
