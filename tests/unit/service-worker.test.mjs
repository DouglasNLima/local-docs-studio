import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import vm from 'node:vm';

const serviceWorkerSource = await readFile(new URL('../../service-worker.js', import.meta.url), 'utf8');

function createServiceWorkerHarness() {
  const listeners = new Map();
  const requests = [];
  const staleResponses = new Map();
  const cache = {
    async add() {},
    async match(request) {
      const url = typeof request === 'string' ? request : request.url;
      return staleResponses.get(url)?.clone();
    },
    async put(request, response) {
      staleResponses.set(request.url, response.clone());
    },
  };

  const context = vm.createContext({
    URL,
    Request,
    Response,
    console,
    caches: {
      async delete() { return true; },
      async keys() { return []; },
      async open() { return cache; },
    },
    async fetch(request) {
      requests.push(request);
      return new Response(`fresh:${new URL(request.url).pathname}`, { status: 200 });
    },
    self: {
      clients: { async claim() {} },
      location: {
        href: 'https://example.test/MarkdownReader/service-worker.js',
        origin: 'https://example.test',
      },
      registration: { scope: 'https://example.test/MarkdownReader/' },
      addEventListener(type, listener) {
        listeners.set(type, listener);
      },
      async skipWaiting() {},
    },
  });
  vm.runInContext(serviceWorkerSource, context, { filename: 'service-worker.js' });

  return {
    requests,
    staleResponses,
    async fetch(url, { mode = 'cors' } = {}) {
      let responsePromise;
      listeners.get('fetch')({
        request: new Request(url, { mode }),
        respondWith(value) {
          responsePromise = Promise.resolve(value);
        },
      });
      return responsePromise;
    },
  };
}

test('uses the network first for app shell files so one load cannot mix release versions', async () => {
  const harness = createServiceWorkerHarness();
  const moduleUrl = 'https://example.test/MarkdownReader/assets/scripts/rendering/render-service.js';
  harness.staleResponses.set(moduleUrl, new Response('obsolete module'));

  const response = await harness.fetch(moduleUrl);

  assert.equal(await response.text(), 'fresh:/MarkdownReader/assets/scripts/rendering/render-service.js');
  assert.equal(harness.requests.length, 1);
  assert.equal(harness.requests[0].cache, 'no-cache');
});

test('keeps stale-while-revalidate for same-origin content outside the versioned app shell', async () => {
  const harness = createServiceWorkerHarness();
  const contentUrl = 'https://example.test/MarkdownReader/local-preview-image.png';
  harness.staleResponses.set(contentUrl, new Response('cached content'));

  const response = await harness.fetch(contentUrl);

  assert.equal(await response.text(), 'cached content');
  assert.equal(harness.requests.length, 1);
});
