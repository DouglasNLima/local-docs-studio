import assert from 'node:assert/strict';
import { runInNewContext } from 'node:vm';

export async function checkServiceWorkerInstall(source) {
  const handlers = new Map();
  const vendorAssets = Array.from({ length: 150 }, (_, index) => `./assets/vendor/fixture-${index}.js`);
  const attempted = new Set();
  let active = 0;
  let peak = 0;
  let skipped = 0;
  let optionalFailure = false;
  class Request {
    constructor(url) { this.url = new URL(url, 'https://studio.example/').href; }
  }
  runInNewContext(source, {
    Request,
    self: {
      addEventListener: (name, handler) => handlers.set(name, handler),
      skipWaiting: async () => { skipped += 1; },
    },
    fetch: async () => ({ ok: true, json: async () => vendorAssets }),
    caches: { open: async () => ({
      add: async (request) => {
        active += 1;
        peak = Math.max(peak, active);
        attempted.add(request.url);
        try {
          // Hold every request across a turn so unbounded fan-out cannot pass by completing synchronously.
          await new Promise(resolve => setImmediate(resolve));
          if (request.url.endsWith('/fixture-5.js')) {
            optionalFailure = true;
            throw new Error('Optional fixture asset unavailable');
          }
        } finally { active -= 1; }
      },
    }) },
  });
  let installation;
  handlers.get('install')({ waitUntil: promise => { installation = promise; } });
  await installation;
  assert.ok(peak <= 6, `Service worker installation started ${peak} concurrent cache fetches; the limit is 6.`);
  const local = [...source.match(/const LOCAL_ASSETS = \[([\s\S]*?)\];/)[1].matchAll(/'([^']+)'/g)].map(match => match[1]);
  for (const asset of [...local, ...vendorAssets]) assert.ok(attempted.has(new URL(asset, 'https://studio.example/').href), `Cache installation omitted ${asset}`);
  assert.ok(optionalFailure, 'The optional-cache failure path was not exercised.');
  assert.equal(active, 0, 'Activation started before cache requests settled.');
  assert.equal(skipped, 1, 'Installation must activate after attempting every asset.');
  return { assets: attempted.size, peak };
}
