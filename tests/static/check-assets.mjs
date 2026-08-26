import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const productName = 'Lens Docs Studio';
const productTagline = 'Local Markdown, Mermaid, and documentation studio';
const productVersion = '0.1.0';
const productBuild = '58';
const productBrowserTitle = `${productName} v${productVersion} (build ${productBuild})`;
const forbiddenShellPhrases = [
  'Power Platform Lens family',
  'Review Markdown, Mermaid, and evidence artefacts from the Power Platform Lens family',
];

function fail(message) {
  throw new Error(message);
}

function toRootPath(filePath) {
  return path.resolve(root, filePath);
}

function readRootFile(filePath) {
  return readFileSync(toRootPath(filePath), 'utf8');
}

function walk(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const fullPath = path.join(directory, entry.name);
    return entry.isDirectory() ? walk(fullPath) : [fullPath];
  });
}

function assertLocalFileExists(filePath, context) {
  const resolved = toRootPath(filePath.replace(/^\.\//, ''));
  if (!existsSync(resolved) || !statSync(resolved).isFile()) {
    fail(`${context} does not exist: ${filePath}`);
  }
}

function normaliseAssetPath(asset) {
  return asset.replace(/^\.\//, '');
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

const serviceWorker = readRootFile('service-worker.js');
const localAssetsMatch = serviceWorker.match(/const LOCAL_ASSETS = \[([\s\S]*?)\];/);
if (!localAssetsMatch) fail('Could not find LOCAL_ASSETS in service-worker.js');
if (serviceWorker.includes('cdn.jsdelivr.net')) fail('service-worker.js must not depend on jsDelivr at runtime');
if (!serviceWorker.includes('assets/vendor/manifest.json')) fail('service-worker.js must load the vendor asset manifest');

const localAssets = [...localAssetsMatch[1].matchAll(/'([^']+)'/g)].map((match) => match[1]);
const localAssetSet = new Set(localAssets.map(normaliseAssetPath));
for (const asset of localAssets) {
  if (asset === './') continue;
  assertLocalFileExists(asset, 'Service worker asset');
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
  './assets/vendor/katex-0.16.25.min.css',
  './assets/vendor/chunks/mermaid.esm.min/katex-K3KEBU37.js',
  './assets/vendor/mammoth-1.12.0.browser.min.js',
  './assets/vendor/pdfjs-5.7.284.js',
  './assets/vendor/pdfjs-5.7.284.worker.js',
];
for (const asset of requiredVendorAssets) {
  if (!vendorAssets.includes(asset)) fail(`Vendor manifest is missing ${asset}`);
}
for (const asset of vendorAssets) {
  if (!asset.startsWith('./assets/vendor/')) fail(`Vendor manifest asset must stay under assets/vendor: ${asset}`);
  assertLocalFileExists(asset, 'Vendor manifest asset');
}

for (const filePath of moduleFiles) {
  const relativeModule = path.relative(root, filePath).replaceAll(path.sep, '/');
  if (!localAssetSet.has(relativeModule)) {
    fail(`Service worker does not cache module: ${relativeModule}`);
  }
}

const index = readRootFile('index.html');
if (!index.includes('./assets/styles/app.css')) fail('index.html does not load app.css');
if (!index.includes('./assets/scripts/main.js')) fail('index.html does not load main.js');
if (!index.includes('./manifest.webmanifest')) fail('index.html does not load manifest.webmanifest');
if (!index.includes('Content-Security-Policy')) fail('index.html must define a Content-Security-Policy meta tag');
if (!index.includes(`<title>${productBrowserTitle}</title>`)) fail('index.html must include the Lens Docs Studio browser title with version and build');
if (!index.includes(`<h1>${productName}</h1>`)) fail('index.html must show the Lens Docs Studio product name');
if (!index.includes(`<small>${productTagline}</small>`)) fail('index.html must show the generic product tagline');
if (!index.includes(`v${productVersion} (build ${productBuild})`)) fail('index.html must expose the app version and build in the shell');
for (const phrase of forbiddenShellPhrases) {
  if (index.includes(phrase)) fail('index.html must not use Power Platform Lens-only positioning');
}
if (index.includes('Local Docs Studio') || index.includes('Local Markdown, Mermaid, and docs export studio')) fail('index.html still contains old product identity copy');

const localReferencePattern = /\b(?:href|src)=["'](\.\/[^"']+)["']/g;
for (const match of index.matchAll(localReferencePattern)) {
  assertLocalFileExists(match[1], 'index.html local asset');
}

const runtimeExternalReferencePatterns = [
  { name: 'external scripts', pattern: /<script\b[^>]*\bsrc=["']https?:\/\//i },
  { name: 'external stylesheets', pattern: /<link\b[^>]*\bhref=["']https?:\/\/[^"']+["'][^>]*\brel=["']stylesheet["']/i },
  { name: 'stylesheet imports', pattern: /@import\s+(?:url\()?["']?https?:\/\//i },
  { name: 'remote CSS URLs', pattern: /url\(["']?https?:\/\//i },
  { name: 'CDN host references', pattern: /\b(?:cdn\.|unpkg\.|jsdelivr\.|googleapis\.|fonts\.)/i },
];
const runtimeFilesForExternalScan = [
  'index.html',
  'md-mmd-renderer-v5.html',
  'manifest.webmanifest',
  'service-worker.js',
  'assets/styles/app.css',
  'assets/vendor/manifest.json',
  ...moduleFiles.map((filePath) => path.relative(root, filePath).replaceAll(path.sep, '/')),
];
for (const filePath of runtimeFilesForExternalScan) {
  const source = readRootFile(filePath);
  for (const { name, pattern } of runtimeExternalReferencePatterns) {
    if (pattern.test(source)) {
      fail(`${filePath} contains unexpected ${name}; runtime dependencies must be local.`);
    }
  }
}

const localRuntimeFetchPattern = /fetch\(\s*['"]([^'"]+)['"]/g;
const generatedOutputFetchTargets = new Set(['./assets/search-index.json']);
for (const filePath of runtimeFilesForExternalScan) {
  const source = readRootFile(filePath);
  for (const match of source.matchAll(localRuntimeFetchPattern)) {
    const target = match[1];
    if (/^https?:\/\//i.test(target)) fail(`${filePath} fetches an external runtime URL: ${target}`);
    if (target.startsWith('./') && !generatedOutputFetchTargets.has(target)) {
      assertLocalFileExists(target, `${filePath} fetch target`);
    }
  }
}

const legacy = readFileSync(toRootPath('md-mmd-renderer-v5.html'), 'utf8');
if (!legacy.includes('url=./index.html')) fail('Legacy renderer page does not redirect to index.html');
if (!legacy.includes(`Open ${productName}`)) fail('Legacy renderer page should link to the renamed app');

const manifest = JSON.parse(readFileSync(toRootPath('manifest.webmanifest'), 'utf8'));
if (manifest.name !== productName) fail('manifest.webmanifest name must be Lens Docs Studio');
if (manifest.short_name !== 'Lens Docs') fail('manifest.webmanifest short_name must be Lens Docs');
if (manifest.description !== `${productTagline}.`) fail('manifest.webmanifest description must stay generic');
if (manifest.start_url !== './') fail('manifest.webmanifest start_url must stay GitHub Pages relative');
if (manifest.scope !== './') fail('manifest.webmanifest scope must stay GitHub Pages relative');
if (manifest.display !== 'standalone') fail('manifest.webmanifest display should be standalone');
if (!Array.isArray(manifest.icons) || !manifest.icons.length) fail('manifest.webmanifest must include at least one icon');
for (const icon of manifest.icons) {
  if (!icon.src?.startsWith('./')) fail(`Manifest icon must use a relative src: ${icon.src}`);
  assertLocalFileExists(icon.src, 'Manifest icon');
}

const appCss = readRootFile('assets/styles/app.css');
if (!appCss.includes('#FF883E')) fail('app.css must include the Lens accent #FF883E');

const firstPartyCopyFiles = [
  'AGENTS.md',
  'README.md',
  'docs/tool-guide.md',
  'docs/integration/lens-artifact-bundle-producer-guide.md',
  'docs/release/lens-docs-studio-artefact-bundle-manual-smoke.md',
  'docs/architecture/lens-docs-studio-identity.md',
  'docs/architecture/lens-artifact-bundle-contract.md',
  'index.html',
  'manifest.webmanifest',
  'md-mmd-renderer-v5.html',
];
const americanEnglishPattern = /\b(artifact|artifacts|behavior|behaviors|center|centered|centralize|centralized|centralizes|centralizing|color|colors|customize|customized|customizing|favorite|favorites|gray|localization|neighbor|neighbors|organize|organized|organizing|prioritize|prioritized|prioritizing|specialize|specialized|specializing)\b/i;
for (const copyFile of firstPartyCopyFiles) {
  const source = readFileSync(toRootPath(copyFile), 'utf8')
    .replace(/theme-color/g, '')
    .replace(/artifacts[\\/][^\s`)]+/gi, '')
    .replace(/lens-artifact-bundle[\w.-]*/gi, '')
    .replace(/artifact-bundles?/gi, '')
    .replace(/artifactBundle/g, '')
    .replace(/artifactBundleSummary/g, '')
    .replace(/artifact-bundle-[a-z0-9-]*/gi, '')
    .replace(/#[\da-f]{3,8}/gi, '');
  const match = source.match(americanEnglishPattern);
  if (match) fail(`${copyFile} contains American English spelling: ${match[0]}`);
}

const lensBundleContract = readFileSync(toRootPath('docs/architecture/lens-artifact-bundle-contract.md'), 'utf8');
const readme = readFileSync(toRootPath('README.md'), 'utf8');
const toolGuide = readFileSync(toRootPath('docs/tool-guide.md'), 'utf8');
const producerGuidePath = 'docs/integration/lens-artifact-bundle-producer-guide.md';
const manualSmokePath = 'docs/release/lens-docs-studio-artefact-bundle-manual-smoke.md';
if (!existsSync(toRootPath(producerGuidePath))) fail('Producer guide must exist');
if (!existsSync(toRootPath(manualSmokePath))) fail('Manual smoke checklist must exist');
const producerGuide = readFileSync(toRootPath(producerGuidePath), 'utf8');
const manualSmoke = readFileSync(toRootPath(manualSmokePath), 'utf8');
if (!lensBundleContract.includes('lens-artifact-bundle.json')) fail('Lens artefact bundle contract must document lens-artifact-bundle.json');
if (!lensBundleContract.includes('lens-artifact-bundle-1.0')) fail('Lens artefact bundle contract must keep the 1.0 contract version');
if (!lensBundleContract.includes('candidate finding')) fail('Lens artefact bundle contract must document candidate finding evidence');
if (!lensBundleContract.includes('Reader Panel')) fail('Lens artefact bundle contract must document the reader panel');
if (!lensBundleContract.includes('Built-in export profiles are session-only presets')) fail('Lens artefact bundle contract must document session-only export profiles');
if (!lensBundleContract.includes('Docs Site export may use safe artefact metadata')) fail('Lens artefact bundle contract must document Docs Site artefact metadata');
if (!lensBundleContract.includes('Generic Markdown Bundle export must not include `lens-artifact-bundle.json`')) fail('Lens artefact bundle contract must document generic bundle boundaries');
if (!lensBundleContract.includes('confirmed finding')) fail('Lens artefact bundle contract must document confirmed finding wording rules');
if (!lensBundleContract.includes('Round-trip certification')) fail('Lens artefact bundle contract must document round-trip certification');
if (!lensBundleContract.includes('explicit artefact review pack export')) fail('Lens artefact bundle contract must document explicit review pack export');
if (!readme.includes('Optional Lens Artefact Bundles') || !readme.includes('lens-artifact-bundle.json')) {
  fail('README.md must document optional Lens artefact bundle ZIP support');
}
if (!readme.includes('Built-in export profiles') || !readme.includes('Export artefact review pack')) {
  fail('README.md must document built-in profiles and artefact review export');
}
for (const requiredPath of [producerGuidePath, manualSmokePath, 'tests/fixtures/artifact-bundles/']) {
  if (!readme.includes(requiredPath) && !toolGuide.includes(requiredPath) && !lensBundleContract.includes(requiredPath)) {
    fail(`New artefact certification guidance is not discoverable: ${requiredPath}`);
  }
}
for (const requiredLabel of [
  'static evidence',
  'connected metadata evidence',
  'runtime evidence',
  'manual evidence',
  'candidate finding',
  'confirmed finding',
  'blocked/unavailable evidence',
]) {
  if (!producerGuide.includes(requiredLabel)) fail(`Producer guide is missing evidence label: ${requiredLabel}`);
}
for (const requiredSmokeText of [
  'generic ZIP',
  'Markdown Bundle',
  'valid rich artefact bundle',
  'artefact review pack',
  'external service calls',
  'no new artefact metadata',
]) {
  if (!manualSmoke.includes(requiredSmokeText)) fail(`Manual smoke checklist is missing: ${requiredSmokeText}`);
}

const packageJson = JSON.parse(readFileSync(toRootPath('package.json'), 'utf8'));
if (packageJson.version !== productVersion) fail('package.json version must match the published app version');
if (Object.keys(packageJson.scripts || {}).some((scriptName) => /^build($|:)/.test(scriptName))) {
  fail('package.json must not add a production build command');
}

const gitignore = readRootFile('.gitignore');
if (!gitignore.includes('artifacts/word-templates/')) fail('.gitignore must exclude locally imported Word template packs');

const lensArtifactBundleService = readFileSync(toRootPath('assets/scripts/files/lens-artifact-bundle-service.js'), 'utf8');
for (const forbiddenRuntime of ['fetch(', 'XMLHttpRequest', 'sendBeacon', 'localStorage', 'indexedDB', 'openObjectStoreDb']) {
  if (lensArtifactBundleService.includes(forbiddenRuntime)) {
    fail(`Lens artefact bundle service must not use ${forbiddenRuntime}`);
  }
}

const artifactReaderModule = readFileSync(toRootPath('assets/scripts/ui/artifact-bundle-reader.js'), 'utf8');
const exportProfileModule = readFileSync(toRootPath('assets/scripts/exports/export-profile-service.js'), 'utf8');
for (const [moduleName, source] of [
  ['artefact reader', artifactReaderModule],
  ['export profile', exportProfileModule],
]) {
  for (const forbiddenRuntime of ['fetch(', 'XMLHttpRequest', 'sendBeacon', 'indexedDB', 'openObjectStoreDb', 'https://', 'http://']) {
    if (source.includes(forbiddenRuntime)) {
      fail(`${moduleName} module must not add external service integration via ${forbiddenRuntime}`);
    }
  }
}

const workflow = readFileSync(toRootPath('.github/workflows/pages.yml'), 'utf8');
if (!workflow.includes('actions/deploy-pages@v4')) fail('Pages workflow must deploy with actions/deploy-pages@v4');
if (!workflow.includes('npm test')) fail('Pages workflow must run npm test before deploy');
if (!workflow.includes('pages-artifact')) fail('Pages workflow must upload the static Pages artefact');
if (/^permissions:/m.test(workflow)) fail('Pages workflow permissions must be scoped per job');
if (!/test:[\s\S]*?permissions:[\s\S]*?contents: read[\s\S]*?steps:/m.test(workflow)) fail('Test job must have contents: read permission only');
if (!/deploy:[\s\S]*?permissions:[\s\S]*?contents: read[\s\S]*?pages: write[\s\S]*?id-token: write[\s\S]*?environment:/m.test(workflow)) fail('Deploy job must scope Pages and id-token write permissions');

for (const requiredText of ['Publish To GitHub Pages', 'npm ci', 'npm test', 'GitHub Actions', 'Export PDF', 'Export Markdown Bundle', 'Import ZIP']) {
  if (!readme.includes(requiredText)) fail(`README.md is missing publication guidance: ${requiredText}`);
}

const windowsProject = readRootFile('src/windows/LensDocsStudio.Windows/LensDocsStudio.Windows.csproj');
for (const requiredStaticAppPath of [
  'index.html',
  'md-mmd-renderer-v5.html',
  'manifest.webmanifest',
  'icon.svg',
  'service-worker.js',
  'assets\\**\\*',
  'docs\\**\\*',
]) {
  if (!windowsProject.includes(requiredStaticAppPath)) {
    fail(`Windows project static app copy configuration is missing ${requiredStaticAppPath}`);
  }
}
if (!windowsProject.includes('StaticApp\\')) fail('Windows project must copy static assets under StaticApp');

const windowsStaticAssetScript = 'scripts/windows/Test-WindowsStaticAssets.ps1';
if (!existsSync(toRootPath(windowsStaticAssetScript))) fail(`${windowsStaticAssetScript} must exist`);

for (const requiredDocText of [
  'Windows shell / packaged local assets',
  'Windows shell / offline mode',
  'scripts/windows/Test-WindowsStaticAssets.ps1',
  'local HTTP server is not required',
]) {
  if (!readme.includes(requiredDocText) && !readRootFile('docs/architecture/windows-offline-distribution-roadmap.md').includes(requiredDocText)) {
    fail(`Offline runtime documentation is missing: ${requiredDocText}`);
  }
}

console.log(`Static checks passed for ${moduleFiles.length} module files, ${localAssets.length} shell assets, ${vendorAssets.length} vendor assets, and ${runtimeFilesForExternalScan.length} runtime external-dependency scans.`);
