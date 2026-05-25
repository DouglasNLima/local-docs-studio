export function parseFrontMatter(source) {
  const text = String(source ?? '').replace(/^[\uFEFF]/, '');
  const match = text.match(/^---[ \t]*\r?\n([\s\S]*?)\r?\n---[ \t]*(?:\r?\n|$)/);
  if (!match) {
    return {
      raw: '',
      body: text,
      metadata: {},
      lineOffset: 0,
    };
  }

  const raw = match[0];
  return {
    raw,
    body: text.slice(raw.length),
    metadata: parseFrontMatterBlock(match[1]),
    lineOffset: raw.split(/\r?\n/).length - 1,
  };
}

export function stripFrontMatter(source) {
  return parseFrontMatter(source).body;
}

export function parseFrontMatterBlock(block) {
  const metadata = {};
  String(block || '').split(/\r?\n/).forEach((line) => {
    const match = line.match(/^([A-Za-z][A-Za-z0-9_-]*)\s*:\s*(.*?)\s*$/);
    if (!match) return;
    const key = normaliseFrontMatterKey(match[1]);
    if (!key) return;
    metadata[key] = parseFrontMatterValue(match[2]);
  });
  return normaliseFrontMatterMetadata(metadata);
}

export function normaliseFrontMatterMetadata(metadata = {}) {
  const result = {};
  if (metadata.title !== undefined) result.title = String(metadata.title || '').trim();
  if (metadata.description !== undefined) result.description = String(metadata.description || '').trim();
  if (metadata.navGroup !== undefined) result.navGroup = String(metadata.navGroup || '').trim();
  if (metadata.order !== undefined) {
    const order = Number(metadata.order);
    if (Number.isFinite(order)) result.order = order;
  }
  if (metadata.draft !== undefined) result.draft = Boolean(metadata.draft);
  if (metadata.tags !== undefined) {
    const tags = Array.isArray(metadata.tags) ? metadata.tags : [metadata.tags];
    result.tags = tags.map((tag) => String(tag || '').trim()).filter(Boolean);
  }
  return result;
}

function normaliseFrontMatterKey(key) {
  const lookup = {
    title: 'title',
    description: 'description',
    order: 'order',
    tags: 'tags',
    draft: 'draft',
    navgroup: 'navGroup',
    nav_group: 'navGroup',
    'nav-group': 'navGroup',
  };
  return lookup[String(key || '').trim().toLowerCase()] || '';
}

function parseFrontMatterValue(rawValue) {
  const value = unquoteFrontMatterValue(String(rawValue || '').trim());
  if (/^(true|false)$/i.test(value)) return /^true$/i.test(value);
  if (/^-?\d+(?:\.\d+)?$/.test(value)) return Number(value);
  if (/^\[.*\]$/.test(value)) {
    return value.slice(1, -1)
      .split(',')
      .map((item) => unquoteFrontMatterValue(item.trim()))
      .filter(Boolean);
  }
  return value;
}

function unquoteFrontMatterValue(value) {
  const text = String(value || '').trim();
  const quote = text[0];
  if ((quote === '"' || quote === "'") && text[text.length - 1] === quote) {
    return text.slice(1, -1).trim();
  }
  return text;
}
