const CACHE_NAME = 'local-docs-studio-v45';
const VENDOR_MANIFEST = './assets/vendor/manifest.json';
const LOCAL_ASSETS = [
  './',
  './index.html',
  './md-mmd-renderer-v5.html',
  './assets/styles/app.css',
  './assets/scripts/main.js',
  './assets/scripts/app-controller.js',
  './assets/scripts/dom.js',
  './assets/scripts/document/document-ux-service.js',
  './assets/scripts/document/selection-sync-service.js',
  './assets/scripts/document/scroll-sync-service.js',
  './assets/scripts/editor/draft-service.js',
  './assets/scripts/editor/editor-service.js',
  './assets/scripts/editor/find-replace-service.js',
  './assets/scripts/editor/paste-service.js',
  './assets/scripts/editor/table-editor-service.js',
  './assets/scripts/editor/typewriter-service.js',
  './assets/scripts/editor/workspace-search-service.js',
  './assets/scripts/files/document-import-service.js',
  './assets/scripts/exports/export-service.js',
  './assets/scripts/files/file-service.js',
  './assets/scripts/rendering/render-service.js',
  './assets/scripts/registries/content.js',
  './assets/scripts/state/config.js',
  './assets/scripts/ui/context-menu-service.js',
  './assets/scripts/ui/ui-service.js',
  './assets/scripts/utils/binary.js',
  './assets/scripts/utils/browser.js',
  './assets/scripts/utils/devops-markdown.js',
  './assets/scripts/utils/files.js',
  './assets/scripts/utils/format.js',
  './assets/scripts/utils/front-matter.js',
  './assets/scripts/utils/html-markdown.js',
  './assets/scripts/utils/idb.js',
  './assets/scripts/utils/markdown-table.js',
  './assets/scripts/utils/math.js',
  './assets/scripts/utils/search.js',
  './assets/scripts/utils/security.js',
  './assets/scripts/utils/wikilinks.js',
  './assets/scripts/utils/zip.js',
  VENDOR_MANIFEST,
  './docs/tool-guide.md',
  './manifest.webmanifest',
  './icon.svg',
];

self.addEventListener('install', (event) => {
  event.waitUntil((async () => {
    const cache = await caches.open(CACHE_NAME);
    const assets = [...LOCAL_ASSETS, ...await readVendorAssets()];
    await Promise.all(assets.map(async (url) => {
      try {
        await cache.add(new Request(url, { mode: 'same-origin' }));
      } catch {
        // The app still works online if an optional cached asset is unavailable during install.
      }
    }));
    await self.skipWaiting();
  })());
});

self.addEventListener('activate', (event) => {
  event.waitUntil((async () => {
    const names = await caches.keys();
    await Promise.all(names.filter((name) => name !== CACHE_NAME).map((name) => caches.delete(name)));
    await self.clients.claim();
  })());
});

self.addEventListener('fetch', (event) => {
  const { request } = event;
  if (request.method !== 'GET') return;

  const url = new URL(request.url);
  const isLocal = url.origin === self.location.origin;

  if (request.mode === 'navigate') {
    event.respondWith(networkFirst(request, './index.html'));
    return;
  }

  if (isLocal) {
    event.respondWith(staleWhileRevalidate(request));
  }
});

async function readVendorAssets() {
  try {
    const response = await fetch(VENDOR_MANIFEST, { cache: 'no-store' });
    if (!response.ok) return [];
    const assets = await response.json();
    return Array.isArray(assets) ? assets : [];
  } catch {
    return [];
  }
}

async function networkFirst(request, fallbackUrl) {
  const cache = await caches.open(CACHE_NAME);
  try {
    const response = await fetch(request);
    cache.put(request, response.clone());
    return response;
  } catch {
    return await cache.match(request) || await cache.match(fallbackUrl);
  }
}

async function staleWhileRevalidate(request) {
  const cache = await caches.open(CACHE_NAME);
  const cached = await cache.match(request);
  const fresh = fetch(request)
    .then((response) => {
      cache.put(request, response.clone());
      return response;
    })
    .catch(() => undefined);

  return cached || fresh || new Response('', { status: 504, statusText: 'Offline' });
}
