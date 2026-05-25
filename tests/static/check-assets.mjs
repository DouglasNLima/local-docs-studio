import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

function fail(message) {
  throw new Error(message);
}

function toRootPath(filePath) {
  return path.resolve(root, filePath);
}

function walk(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const fullPath = path.join(directory, entry.name);
    return entry.isDirectory() ? walk(fullPath) : [fullPath];
  });
}

const moduleFiles = walk(toRootPath('assets/scripts')).filter((filePath) => filePath.endsWith('.js'));

for (const filePath of [...moduleFiles, toRootPath('service-worker.js')]) {
  execFileSync('node', ['--check', filePath], { stdio: 'inherit' });
}

const importPattern = /import\s+(?:[^'"]+\s+from\s+)?['"]([^'"]+)['"]/g;
for (const filePath of moduleFiles) {
  const source = readFileSync(filePath, 'utf8');
  for (const match of source.matchAll(importPattern)) {
    const specifier = match[1];
    if (/^https?:/.test(specifier)) {
      fail(`Runtime modules must use local vendored imports, not ${specifier} from ${path.relative(root, filePath)}`);
    }
    const resolved = path.resolve(path.dirname(filePath), specifier);
    if (!existsSync(resolved)) {
      fail(`Missing relative import ${specifier} from ${path.relative(root, filePath)}`);
    }
  }
}

const serviceWorker = readFileSync(toRootPath('service-worker.js'), 'utf8');
const localAssetsMatch = serviceWorker.match(/const LOCAL_ASSETS = \[([\s\S]*?)\];/);
if (!localAssetsMatch) fail('Could not find LOCAL_ASSETS in service-worker.js');
if (serviceWorker.includes('cdn.jsdelivr.net')) fail('service-worker.js must not depend on jsDelivr at runtime');
if (!serviceWorker.includes('assets/vendor/manifest.json')) fail('service-worker.js must load the vendor asset manifest');

const localAssets = [...localAssetsMatch[1].matchAll(/'([^']+)'/g)].map((match) => match[1]);
const localAssetSet = new Set(localAssets.map((asset) => asset.replace(/^\.\//, '')));
for (const asset of localAssets) {
  if (asset === './') continue;
  const assetPath = toRootPath(asset.replace(/^\.\//, ''));
  if (!existsSync(assetPath) || !statSync(assetPath).isFile()) {
    fail(`Service worker asset does not exist: ${asset}`);
  }
}

const vendorDirectory = toRootPath('assets/vendor');
if (!existsSync(vendorDirectory) || !statSync(vendorDirectory).isDirectory()) fail('assets/vendor must contain local runtime dependencies');
const vendorManifestPath = toRootPath('assets/vendor/manifest.json');
if (!existsSync(vendorManifestPath)) fail('assets/vendor/manifest.json is required for offline caching');
const vendorAssets = JSON.parse(readFileSync(vendorManifestPath, 'utf8'));
if (!Array.isArray(vendorAssets) || !vendorAssets.length) fail('assets/vendor/manifest.json must list vendored assets');
const requiredVendorAssets = [
  './assets/vendor/mermaid-11.15.0.esm.min.js',
  './assets/vendor/marked-16.4.2.esm.js',
  './assets/vendor/dompurify-3.4.5.es.js',
  './assets/vendor/highlight-11.11.1.esm.js',
  './assets/vendor/fflate-0.8.2.browser.js',
];
for (const asset of requiredVendorAssets) {
  if (!vendorAssets.includes(asset)) fail(`Vendor manifest is missing ${asset}`);
}
for (const asset of vendorAssets) {
  if (!asset.startsWith('./assets/vendor/')) fail(`Vendor manifest asset must stay under assets/vendor: ${asset}`);
  const assetPath = toRootPath(asset.replace(/^\.\//, ''));
  if (!existsSync(assetPath) || !statSync(assetPath).isFile()) fail(`Vendor manifest asset does not exist: ${asset}`);
}

for (const filePath of moduleFiles) {
  const relativeModule = path.relative(root, filePath).replaceAll(path.sep, '/');
  if (!localAssetSet.has(relativeModule)) {
    fail(`Service worker does not cache module: ${relativeModule}`);
  }
}

const index = readFileSync(toRootPath('index.html'), 'utf8');
if (!index.includes('./assets/styles/app.css')) fail('index.html does not load app.css');
if (!index.includes('./assets/scripts/main.js')) fail('index.html does not load main.js');
if (!index.includes('./manifest.webmanifest')) fail('index.html does not load manifest.webmanifest');
if (!index.includes('Content-Security-Policy')) fail('index.html must define a Content-Security-Policy meta tag');

const localReferencePattern = /\b(?:href|src)=["'](\.\/[^"']+)["']/g;
for (const match of index.matchAll(localReferencePattern)) {
  const referencedPath = toRootPath(match[1].replace(/^\.\//, ''));
  if (!existsSync(referencedPath)) {
    fail(`index.html references a missing local asset: ${match[1]}`);
  }
}

const legacy = readFileSync(toRootPath('md-mmd-renderer-v5.html'), 'utf8');
if (!legacy.includes('url=./index.html')) fail('Legacy renderer page does not redirect to index.html');

const manifest = JSON.parse(readFileSync(toRootPath('manifest.webmanifest'), 'utf8'));
if (manifest.start_url !== './') fail('manifest.webmanifest start_url must stay GitHub Pages relative');
if (manifest.scope !== './') fail('manifest.webmanifest scope must stay GitHub Pages relative');
if (manifest.display !== 'standalone') fail('manifest.webmanifest display should be standalone');
if (!Array.isArray(manifest.icons) || !manifest.icons.length) fail('manifest.webmanifest must include at least one icon');
for (const icon of manifest.icons) {
  if (!icon.src?.startsWith('./')) fail(`Manifest icon must use a relative src: ${icon.src}`);
  const iconPath = toRootPath(icon.src.replace(/^\.\//, ''));
  if (!existsSync(iconPath) || !statSync(iconPath).isFile()) {
    fail(`Manifest icon does not exist: ${icon.src}`);
  }
}

const workflow = readFileSync(toRootPath('.github/workflows/pages.yml'), 'utf8');
if (!workflow.includes('actions/deploy-pages@v4')) fail('Pages workflow must deploy with actions/deploy-pages@v4');
if (!workflow.includes('npm test')) fail('Pages workflow must run npm test before deploy');
if (!workflow.includes('pages-artifact')) fail('Pages workflow must upload the static Pages artefact');
if (/^permissions:/m.test(workflow)) fail('Pages workflow permissions must be scoped per job');
if (!/test:[\s\S]*?permissions:[\s\S]*?contents: read[\s\S]*?steps:/m.test(workflow)) fail('Test job must have contents: read permission only');
if (!/deploy:[\s\S]*?permissions:[\s\S]*?contents: read[\s\S]*?pages: write[\s\S]*?id-token: write[\s\S]*?environment:/m.test(workflow)) fail('Deploy job must scope Pages and id-token write permissions');

const readme = readFileSync(toRootPath('README.md'), 'utf8');
for (const requiredText of ['Publish To GitHub Pages', 'npm ci', 'npm test', 'GitHub Actions', 'Export PDF', 'Export Markdown Bundle', 'Import ZIP']) {
  if (!readme.includes(requiredText)) fail(`README.md is missing publication guidance: ${requiredText}`);
}

console.log(`Static checks passed for ${moduleFiles.length} module files, ${localAssets.length} shell assets, and ${vendorAssets.length} vendor assets.`);
