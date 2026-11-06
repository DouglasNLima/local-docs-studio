import { Marked } from '../../vendor/marked-16.4.2.esm.js';
import { stripFrontMatter } from '../utils/front-matter.js';
import { normaliseDevOpsMermaidBlocks } from '../utils/devops-markdown.js';
import { createWikilinkExtension } from '../utils/wikilinks.js';
import { createMathExtensions } from '../utils/math.js';

const parser = new Marked({ gfm: true, extensions: [detailsExtension(), createWikilinkExtension(), ...createMathExtensions()] });

// Lens's passive details format has a plain-text summary followed by Markdown.
// This local lexer extension also works without blank lines around the body.
function detailsExtension() {
  return {
    name: 'lensDetails', level: 'block',
    start: source => source.indexOf('<details'),
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
            return { type: 'lensDetails', raw: source.slice(0, offset + line.length), text: opening[1],
              tokens: this.lexer.blockTokens(source.slice(opening[0].length, offset)) };
          }
        }
        offset += line.length;
      }
      return undefined;
    },
  };
}

function figureText(raw) {
  const match = raw.trim().match(/^<figure\s+data-image-figure(?:\s*=\s*(?:"[^"<>]*"|'[^'<>]*'))?\s*>\s*<img\s+([^<>]+?)\/?\s*>\s*(?:<figcaption>([^<]*)<\/figcaption>\s*)?<\/figure>$/i);
  if (!match || (match[2]?.length || 0) > 32768) return null;
  const attributes = [...match[1].matchAll(/([a-z-]+)\s*=\s*(?:"([^"<>]*)"|'([^'<>]*)')/gi)];
  const names = attributes.map(item => item[1].toLowerCase());
  if (names.length > 3 || new Set(names).size !== names.length || names.some(name => !['src', 'alt', 'title'].includes(name))
      || match[1].replace(/([a-z-]+)\s*=\s*(?:"([^"<>]*)"|'([^'<>]*)')/gi, '').trim()) return null;
  const values = Object.fromEntries(attributes.map(item => [item[1].toLowerCase(), item[2] ?? item[3]]));
  if (!values.src || (values.alt?.length || 0) > 4096 || (values.title?.length || 0) > 4096) return null;
  return [match[2] || '', values.alt || ''];
}

// Parsing stays off the UI thread. Only inert readable text is returned; no HTML is rendered.
self.onmessage = ({ data: { generation, source, standalone } }) => {
  try {
    if (source.length > 4 * 1024 * 1024) throw new Error('Document statistics support up to 4 million source characters.');
    const blocks = [];
    if (!standalone) walk(parser.lexer(normaliseDevOpsMermaidBlocks(stripFrontMatter(source))), 0);
    self.postMessage({ generation, text: blocks.join(' ') });

    function inline(tokens, depth) {
      check(depth);
      return tokens.map(token => {
        if (token.type === 'codespan' || token.type === 'br') return ' ';
        if (token.type === 'html' || /math/i.test(token.type)) return '';
        return token.tokens ? inline(token.tokens, depth + 1) : token.text || '';
      }).join('');
    }
    function walk(tokens, depth) {
      check(depth);
      for (const token of tokens) {
        if (token.type === 'code' || /math/i.test(token.type)) continue;
        if (token.type === 'html') { const figure = figureText(token.raw); if (figure) blocks.push(...figure); }
        else if (token.type === 'lensDetails') { blocks.push(token.text); walk(token.tokens, depth + 1); }
        else if (token.type === 'table') {
          for (const cell of [...token.header, ...token.rows.flat()]) blocks.push(inline(cell.tokens, depth + 1));
        } else if (token.items) {
          for (const item of token.items) walk(item.tokens, depth + 1);
        } else if (token.type === 'blockquote') walk(token.tokens, depth + 1);
        else if (token.tokens) blocks.push(inline(token.tokens, depth + 1));
        else if (token.type === 'text') blocks.push(token.text);
      }
    }
    function check(depth) { if (depth > 64) throw new Error('The document contains excessively nested statistics content.'); }
  } catch (error) { self.postMessage({ generation, error: error.message }); }
};
