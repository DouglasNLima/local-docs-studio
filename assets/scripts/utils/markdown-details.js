// Lens's passive details format has a plain-text summary followed by Markdown.
// This local lexer extension also works without blank lines around the body.
export function createDetailsExtension() {
  return {
    name: 'lensDetails', level: 'block',
    start: source => source.indexOf('<details'),
    renderer(token) { return `<details${token.lensBlock ? ' data-details-block' : ''}${token.open ? ' open' : ''}><summary>${token.text}</summary>\n${this.parser.parse(token.tokens)}</details>`; },
    tokenizer(source) {
      const opening = source.match(/^ {0,3}<details(?:[ \t]+(?:data-details-block|open)(?:[ \t]*=[ \t]*(?:"[^"<>]*"|'[^'<>]*'))?)*[ \t]*>\r?\n {0,3}<summary>([^<\r\n]{1,4096})<\/summary>[ \t]*\r?\n/i);
      if (!opening) return undefined;
      const rest = source.slice(opening[0].length, 1024 * 1024);
      let depth = 1; let fence = null; let comment = false; let rawTag = null; let offset = opening[0].length;
      for (const line of rest.match(/[^\n]*(?:\n|$)/g) || []) {
        const trimmed = line.trimEnd();
        if (fence) {
          const close = trimmed.match(/^ {0,3}(`{3,}|~{3,})[ \t]*$/);
          if (close && close[1][0] === fence[0] && close[1].length >= fence.length) fence = null;
        } else if (comment) { if (line.includes('-->')) comment = false; }
        else if (rawTag) { if (line.toLowerCase().includes(`</${rawTag}>`)) rawTag = null; }
        else if (line.includes('<!--')) comment = !line.slice(line.indexOf('<!--') + 4).includes('-->');
        else {
          const marker = trimmed.match(/^ {0,3}(`{3,}|~{3,})/);
          const raw = trimmed.match(/^ {0,3}<(script|style|pre|textarea)(?:\s|>)/i);
          if (marker) fence = marker[1];
          else if (raw) { if (!line.toLowerCase().includes(`</${raw[1].toLowerCase()}>`)) rawTag = raw[1].toLowerCase(); }
          else if (/^ {0,3}<details(?:\s|>)/i.test(trimmed)) { if (++depth > 8) return undefined; }
          else if (/^ {0,3}<\/details>[ \t]*$/i.test(trimmed) && --depth === 0) {
            return { type: 'lensDetails', raw: source.slice(0, offset + line.length), text: opening[1], bodyOffset: opening[0].length, open: /[ \t]open(?:[ \t=>])/.test(opening[0].split('\n')[0]),
              lensBlock: /\bdata-details-block\b/i.test(opening[0].split('\n')[0]),
              tokens: this.lexer.blockTokens(source.slice(opening[0].length, offset)) };
          }
        }
        offset += line.length;
      }
      return undefined;
    },
  };
}
