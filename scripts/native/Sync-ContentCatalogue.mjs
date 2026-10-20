// Development-only translation of the web reference catalogue into native embedded data.
// The installed application reads JSON and substitutes fields in C#; it does not execute JavaScript.
import { readFile, writeFile } from 'node:fs/promises';
import { createContentRegistries } from '../../assets/scripts/registries/content.js';
import { slugFromText } from '../../assets/scripts/utils/format.js';

const catalogue = createContentRegistries({
  slugFromText: value => /^\{\{[a-zA-Z]+\}\}$/.test(value) ? value.replace('{{', '{{slug:') : slugFromText(value),
  todayIso: () => '{{today}}',
});
const entries = [];
const add = (id, group, label, kind, format, namePattern, contentPattern, fields = []) =>
  entries.push({ id, group, label, kind, format, namePattern, contentPattern, fields });
const words = key => key.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/^./, c => c.toUpperCase());
add('example/sample', 'Examples', 'Markdown and Mermaid sample', 'template', 'markdown', 'sample.md', catalogue.sample);
for (const [key, value] of Object.entries(catalogue.examples))
  add(`example/${key}`, 'Examples', { readme: 'Product README', architecture: 'Application architecture', brief: 'Project brief' }[key] ?? words(key),
    'template', 'markdown', value.name, value.content);
for (const [key, value] of Object.entries(catalogue.studioTemplates))
  add(`mermaid/${key}`, 'Mermaid', value.label, 'template', 'mermaid', value.name, value.content);
for (const [key, value] of Object.entries(catalogue.studioSnippets))
  add(`mermaid-snippet/${key}`, 'Mermaid', words(key), 'snippet', 'mermaid', null, value);
for (const [group, value] of Object.entries(catalogue.generatorGroups)) {
  for (const [key, template] of Object.entries(value.templates)) {
    const fields = template.fields.map(field => ({ key: field.key, label: field.label,
      defaultValue: typeof field.fallback === 'function' ? field.fallback() : field.fallback }));
    const meta = Object.fromEntries(fields.map(field => [field.key, `{{${field.key}}}`]));
    add(`${group}/${key}`, value.label, template.label, 'template', 'markdown', template.name(meta), template.content(meta), fields);
  }
  for (const [key, content] of Object.entries(value.snippets))
    add(`${group}-snippet/${key}`, value.label, words(key), 'snippet', 'markdown', null, content);
}

// Deliberate native wording adaptations; fail if the reference changes underneath an adaptation.
function replace(id, before, after) {
  const entry = entries.find(item => item.id === id);
  if (!entry?.contentPattern.includes(before)) throw new Error(`Review native adaptation ${id}: ${before}`);
  entry.contentPattern = entry.contentPattern.replaceAll(before, after);
}
replace('example/sample', 'Marked + DOMPurify', 'Markdown parser');
replace('example/sample', 'Browser preview', 'Native preview');
replace('example/readme', 'Files stay in the browser', 'Files stay on your computer');
replace('example/readme', '| Hosting | Static GitHub Pages app |', '| Runtime | Native Windows application |');
replace('example/architecture', '# Browser-only Architecture', '# Native Application Architecture');
replace('example/architecture', 'The app keeps rendering, editing, and exporting in the browser so it can run from GitHub Pages.',
  'The app keeps editing, rendering and exporting local, with native Windows controls and portable documents.');
replace('example/architecture', 'subgraph Browser', 'subgraph Windows');
replace('example/architecture', 'Marked parser', 'Markdown parser');
replace('example/architecture', 'Sanitised preview', 'Native preview');
replace('example/architecture', '[Download]', '[Save file]');
replace('example/architecture', '- The page owns rendering, saving, exporting, and theme preferences.\n- Browser APIs provide file access where supported.\n- Fallback downloads keep the app useful in browsers with limited file permissions.',
  '- Native controls support editing, preview and document navigation.\n- Local services manage files, exports and theme preferences.\n- Guarded saves preserve original files when a conflict is detected.');
replace('example/brief', 'Save local edits when browser permissions allow it.', 'Save local edits with conflict checks.');
replace('mermaid/sequence', 'Browser', 'Renderer');
replace('mermaid-snippet/subgraph', 'Browser', 'Application');
replace('project-snippet/architectureBlock', 'Browser UI', 'Native UI');
replace('project-snippet/supportMatrix', '| Chrome / Edge | Yes | Full local save support |\n| Firefox | Partial | Download fallback for save |\n| Safari | Partial | Download fallback for save |',
  '| Supported platform | TBD | Record the tested version |\n| Optional integration | TBD | Record validation evidence |\n| Other environment | TBD | Record any limitations |');
replace('project-snippet/install', '```sh\nnpm install\nnpm run dev\n```', '1. Download the application from its trusted distribution channel.\n2. Run the installation wizard.\n3. Open the application and check its version.');
const decisionTitle = entries.find(entry => entry.id === 'project/adr').fields.find(field => field.key === 'title');
if (decisionTitle.defaultValue !== 'Use Browser-only Architecture') throw new Error('Review the native ADR default.');
decisionTitle.defaultValue = 'Use a modular architecture';

// Native-only chart examples use the grammar proved against the pinned Mermaider source.
for (const [key, label, content] of [
  ['xychart', 'XY Chart', 'xychart-beta\n    title Documentation comparisons\n    x-axis "Category" [Guides, Reference, Examples]\n    y-axis "Coverage" -10 --> 10\n    bar "Current" [-5, 0, 8]\n    line "Target" [2, 5, 10]\n'],
  ['radar', 'Radar', 'radar-beta\n    title Documentation profiles\n    axis guides["Guides"], reference["Reference"], examples["Examples"], review["Review"]\n    min 0\n    max 10\n    curve current["Current"]{2, 4, 6, 8}\n    curve target["Target"]{8, 6, 4, 2}\n'],
  ['sankey', 'Sankey', 'sankey-beta\n    Draft,Review,3.000\n    Draft,Review,2.000\n    Review,Published,5.000\n    Research,Published,2.000\n'],
]) {
  add(`mermaid/${key}`, 'Mermaid', label, 'template', 'mermaid', `${key}.mmd`, content);
  add(`mermaid-snippet/${key}`, 'Mermaid', `${label} chart`, 'snippet', 'mermaid', null, content);
}
const output = JSON.stringify({ version: 1, entries }, null, 2) + '\n';
const target = new URL('../../src/native/LensDocsStudio.Core/Content/built-in-catalogue.json', import.meta.url);
if (process.argv.includes('--check')) {
  if ((await readFile(target, 'utf8')).replaceAll('\r\n', '\n') !== output) throw new Error('Native content catalogue is stale. Run node scripts/native/Sync-ContentCatalogue.mjs and review the changes.');
} else await writeFile(target, output, 'utf8');
console.log(`Native catalogue: ${entries.filter(entry => entry.kind === 'template').length} templates and ${entries.filter(entry => entry.kind === 'snippet').length} snippets verified.`);
