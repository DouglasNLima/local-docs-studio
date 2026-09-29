const headingSelector = 'h1, h2, h3, h4, h5, h6';
const explicitAnchorSelector = 'a[id], [data-doc-anchor][id]';
const unsafeIdPattern = /[\u0000-\u001f\u007f\s/?#\\]/;

export function prepareDocumentSections(root) {
  if (!root?.querySelectorAll) return [];
  const usedIds = new Set();

  root.querySelectorAll('[id]').forEach((element) => {
    const requested = String(element.id || '');
    if (!isSafeSectionId(requested)) {
      element.removeAttribute('id');
      return;
    }
    element.id = makeUniqueSectionId(requested, usedIds);
  });

  root.querySelectorAll(headingSelector).forEach((heading, index) => {
    if (heading.id) return;
    const base = slugifySection(heading.textContent) || `section-${index + 1}`;
    heading.id = makeUniqueSectionId(base, usedIds);
  });

  return collectDocumentSections(root);
}

export function collectDocumentSections(root) {
  if (!root?.querySelectorAll) return [];
  const seen = new Set();
  return [...root.querySelectorAll(`${headingSelector}, ${explicitAnchorSelector}`)]
    .filter((element) => {
      if (!isSafeSectionId(element.id) || seen.has(element.id)) return false;
      seen.add(element.id);
      return true;
    })
    .map((element) => {
      const heading = /^H[1-6]$/.test(element.tagName);
      const level = heading ? Number(element.tagName.slice(1)) : 0;
      const text = element.textContent?.replace(/\s+/g, ' ').trim();
      return {
        id: element.id,
        kind: heading ? 'heading' : 'anchor',
        level,
        label: text || (heading ? `Section ${element.id}` : `Anchor: ${element.id}`),
      };
    });
}

export function findDocumentSection(root, fragment) {
  const id = String(fragment || '');
  if (!root?.querySelectorAll || !isSafeSectionId(id)) return null;
  return [...root.querySelectorAll('[id]')].find((element) => element.id === id) || null;
}

export function slugifySection(value) {
  return String(value || '')
    .normalize('NFKD')
    .replace(/\p{M}+/gu, '')
    .toLowerCase()
    .trim()
    .replace(/[^\p{L}\p{N}]+/gu, '-')
    .replace(/^-+|-+$/g, '');
}

export function isSafeSectionId(value) {
  const id = String(value || '');
  return Boolean(id && id.length <= 200 && !unsafeIdPattern.test(id));
}

function makeUniqueSectionId(value, usedIds) {
  const base = String(value || 'section');
  let id = base;
  let suffix = 2;
  while (usedIds.has(id)) {
    id = `${base}-${suffix}`;
    suffix += 1;
  }
  usedIds.add(id);
  return id;
}
