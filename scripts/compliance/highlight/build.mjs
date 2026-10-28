// Maintenance recipe only. The application runs directly from its checked-in assets.
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { rollup } from 'rollup';
import commonjs from '@rollup/plugin-commonjs';
import { minify } from 'terser';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '../../..');
const output = resolve(root, 'test-results/highlight-rebuild');
await mkdir(output, { recursive: true });
const entry = resolve(here, 'node_modules/highlight.js/es/index.js');
const bundle = await rollup({ input: entry, plugins: [commonjs({ strictRequires: false })] });
const generated = await bundle.generate({ format: 'es', sourcemap: true });
await bundle.close();
const chunk = generated.output[0];
const result = await minify(chunk.code, {
  module: true, compress: { passes: 1 }, mangle: true,
  format: { comments: false },
  sourceMap: { content: chunk.map.toString(), filename: 'highlight-11.11.1.esm.js' },
});
const map = JSON.parse(result.map);
map.sources = map.sources.map(source => {
  const path = source.replaceAll('\\', '/');
  const prefix = '/node_modules/highlight.js/';
  if (path.includes(prefix)) return 'npm:highlight.js@11.11.1/' + path.split(prefix)[1];
  if (path.includes('commonjsHelpers')) return 'rollup:commonjsHelpers';
  return path;
});
map.sourceRoot = '';
// Header and map identifier are historical CDN metadata, not copied programme code.
const banner = '/**\n'
  + ' * Bundled by jsDelivr using Rollup v2.79.2 and Terser v5.39.0.\n'
  + ' * Original file: /npm/highlight.js@11.11.1/es/index.js\n'
  + ' *\n'
  + ' * Do NOT use SRI with dynamically generated files! More information: https://www.jsdelivr.com/using-sri-with-dynamic-files\n'
  + ' */\n';
const candidate = banner + result.code + '\n//# sourceMappingURL=/sm/28ec7303afe7b37888f587daa79c82654af73c2e239f8f61fee3a3b1e6b716e1.map';
// Terser's map describes the minified line; account for the retained licence banner.
map.mappings = ';'.repeat(banner.split('\n').length - 1) + map.mappings;
const mapText = JSON.stringify(map) + '\n';
await writeFile(resolve(output, 'highlight-11.11.1.esm.js'), candidate);
await writeFile(resolve(output, 'highlight-11.11.1.esm.js.map'), mapText);
const sha = value => createHash('sha256').update(value).digest('hex');
// Git's canonical blob avoids core.autocrlf checkout changes on Windows.
const original = execFileSync('git', ['show', 'HEAD:assets/vendor/highlight-11.11.1.esm.js'], { cwd: root }).toString('utf8');
const originalBody = original.replace(/^\/\*\*[\s\S]*?\*\/\r?\n/, '').replace(/\r?\n\/\/# sourceMappingURL=[^\r\n]+\s*$/, '').trim();
const report = {
  tools: { rollup: '2.80.0', terser: '5.39.0', commonjs: '22.0.2' },
  options: { strictRequires: false, format: 'es', module: true, compressPasses: 1, mangle: true },
  entry: 'highlight.js/es/index.js', version: '11.11.1',
  unminifiedSha256: sha(chunk.code), minifiedBodySha256: sha(result.code),
  originalBodySha256: sha(originalBody), exactHistoricalBody: originalBody === result.code,
  candidateSha256: sha(candidate), candidateBytes: Buffer.byteLength(candidate),
  sourceMapSha256: sha(mapText), sourceMapSources: map.sources.length,
  originalSha256: '980aa9a979a537d5f05c76d313c5f8b48f0bccba293a75e3c271975fce968aa0',
  exactHistoricalReconstruction: sha(candidate) === '980aa9a979a537d5f05c76d313c5f8b48f0bccba293a75e3c271975fce968aa0',
};
if (sha(candidate) !== report.originalSha256 || sha(original) !== report.originalSha256) {
  throw new Error('The reconstruction or tracked asset differs from the pinned canonical Git bytes');
}
await writeFile(resolve(output, 'build-result.json'), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify(report));
