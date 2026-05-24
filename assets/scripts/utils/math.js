const greek = {
  alpha: 'α',
  beta: 'β',
  gamma: 'γ',
  delta: 'δ',
  epsilon: 'ε',
  theta: 'θ',
  lambda: 'λ',
  mu: 'μ',
  pi: 'π',
  rho: 'ρ',
  sigma: 'σ',
  tau: 'τ',
  phi: 'φ',
  omega: 'ω',
  Gamma: 'Γ',
  Delta: 'Δ',
  Theta: 'Θ',
  Lambda: 'Λ',
  Pi: 'Π',
  Sigma: 'Σ',
  Phi: 'Φ',
  Omega: 'Ω',
};

const symbols = {
  times: '×',
  cdot: '·',
  div: '÷',
  pm: '±',
  leq: '≤',
  geq: '≥',
  neq: '≠',
  approx: '≈',
  infty: '∞',
  sum: '∑',
  prod: '∏',
  int: '∫',
  partial: '∂',
  nabla: '∇',
  forall: '∀',
  exists: '∃',
  in: '∈',
  notin: '∉',
  subset: '⊂',
  subseteq: '⊆',
  cup: '∪',
  cap: '∩',
  to: '→',
  rightarrow: '→',
  leftarrow: '←',
};

export function createMathExtensions({ renderMath } = {}) {
  return [
    {
      name: 'blockMath',
      level: 'block',
      start(source) {
        return source.indexOf('$$');
      },
      tokenizer(source) {
        const match = source.match(/^(?: {0,3})\$\$\s*\n?([\s\S]+?)\n?\s*\$\$(?:\n+|$)/);
        if (!match) return undefined;
        return {
          type: 'blockMath',
          raw: match[0],
          text: match[1].trim(),
        };
      },
      renderer(token) {
        return `<div class="math math-block">${renderMathSource(token.text, true, renderMath)}</div>`;
      },
    },
    {
      name: 'inlineMath',
      level: 'inline',
      start(source) {
        const dollar = source.indexOf('$');
        const paren = source.indexOf('\\(');
        if (dollar === -1) return paren;
        if (paren === -1) return dollar;
        return Math.min(dollar, paren);
      },
      tokenizer(source) {
        if (source.startsWith('\\(')) {
          const match = source.match(/^\\\(([\s\S]+?)\\\)/);
          if (!match) return undefined;
          return { type: 'inlineMath', raw: match[0], text: match[1].trim() };
        }

        if (source.startsWith('$$')) return undefined;
        const match = source.match(/^\$([^\n$]+?)\$(?!\d)/);
        if (!match || /^\s|\s$/.test(match[1])) return undefined;
        return { type: 'inlineMath', raw: match[0], text: match[1].trim() };
      },
      renderer(token) {
        return `<span class="math math-inline">${renderMathSource(token.text, false, renderMath)}</span>`;
      },
    },
  ];
}

export function renderMathMarkup(source) {
  return renderMathSequence(String(source || '').trim());
}

function renderMathSource(source, displayMode, renderMath) {
  const text = String(source || '').trim();
  if (renderMath) {
    try {
      return renderMath(text, displayMode);
    } catch {
      return renderMathMarkup(text);
    }
  }
  return renderMathMarkup(text);
}

function renderMathSequence(source) {
  let html = '';
  let index = 0;
  while (index < source.length) {
    if (source.startsWith('\\frac', index)) {
      const parsed = parseCommandArgs(source, index + 5, 2);
      if (parsed) {
        html += `<span class="math-frac"><span>${renderMathSequence(parsed.args[0])}</span><span>${renderMathSequence(parsed.args[1])}</span></span>`;
        index = parsed.end;
        continue;
      }
    }

    if (source.startsWith('\\sqrt', index)) {
      const parsed = parseCommandArgs(source, index + 5, 1);
      if (parsed) {
        html += `<span class="math-root"><span aria-hidden="true">√</span><span>${renderMathSequence(parsed.args[0])}</span></span>`;
        index = parsed.end;
        continue;
      }
    }

    const next = findNextSpecialCommand(source, index + 1);
    html += renderMathText(source.slice(index, next));
    index = next;
  }
  return html || '&nbsp;';
}

function findNextSpecialCommand(source, start) {
  const frac = source.indexOf('\\frac', start);
  const sqrt = source.indexOf('\\sqrt', start);
  const candidates = [frac, sqrt].filter((value) => value !== -1);
  return candidates.length ? Math.min(...candidates) : source.length;
}

function parseCommandArgs(source, index, count) {
  const args = [];
  let cursor = skipSpaces(source, index);
  for (let argIndex = 0; argIndex < count; argIndex += 1) {
    if (source[cursor] !== '{') return null;
    const parsed = readBraceGroup(source, cursor);
    if (!parsed) return null;
    args.push(parsed.value);
    cursor = skipSpaces(source, parsed.end);
  }
  return { args, end: cursor };
}

function readBraceGroup(source, start) {
  let depth = 0;
  for (let index = start; index < source.length; index += 1) {
    const char = source[index];
    if (char === '{') depth += 1;
    if (char === '}') {
      depth -= 1;
      if (depth === 0) {
        return {
          value: source.slice(start + 1, index),
          end: index + 1,
        };
      }
    }
  }
  return null;
}

function skipSpaces(source, index) {
  let cursor = index;
  while (/\s/.test(source[cursor] || '')) cursor += 1;
  return cursor;
}

function renderMathText(source) {
  let text = String(source || '')
    .replace(/\\([A-Za-z]+)/g, (match, command) => greek[command] || symbols[command] || command)
    .replace(/\\([{}_^])/g, '$1')
    .replace(/\\,/g, ' ')
    .replace(/~/g, ' ');

  text = escapeHtml(text);
  text = text.replace(/([A-Za-z0-9)\]α-ωΑ-Ω])\^\{([^{}]+)\}/gu, '$1<sup>$2</sup>');
  text = text.replace(/([A-Za-z0-9)\]α-ωΑ-Ω])_\{([^{}]+)\}/gu, '$1<sub>$2</sub>');
  text = text.replace(/([A-Za-z0-9)\]α-ωΑ-Ω])\^([A-Za-z0-9+\-=α-ωΑ-Ω])/gu, '$1<sup>$2</sup>');
  text = text.replace(/([A-Za-z0-9)\]α-ωΑ-Ω])_([A-Za-z0-9+\-=α-ωΑ-Ω])/gu, '$1<sub>$2</sub>');
  return text;
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}
