import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';

test.use({ serviceWorkers: 'allow' });

test('public licence, security and signing policies survive offline cache activation', async ({ page, context }) => {
  // Seed an obsolete cache before app registration to exercise the existing cleanup.
  await page.goto('/manifest.webmanifest');
  await page.evaluate(async () => {
    const cache = await caches.open('lens-docs-studio-v68');
    await cache.put('./obsolete-policy.txt', new Response('obsolete'));
  });
  await page.goto('/');
  await expect(page.locator('html')).toHaveAttribute('data-app-ready', 'true');
  await expect.poll(() => page.evaluate(() => Boolean(navigator.serviceWorker.controller))).toBe(true);
  const publicPaths = ['LICENSE', 'LICENSING.md', 'SECURITY.md', 'THIRD_PARTY_NOTICES.md',
    'docs/open-source/main-scope.json', 'docs/open-source/main-rights-ledger.json',
    'docs/open-source/main-provenance.json', 'docs/open-source/main-promotion.md',
    'docs/open-source/third-party-closeout.md', 'docs/open-source/licences/windows-app-sdk.txt',
    'docs/open-source/licences/webview2-sdk.txt', 'docs/open-source/licences/playwright.txt',
    'docs/open-source/licences/playwright-notice.txt',
    'assets/vendor/THIRD-PARTY-NOTICES.txt',
    'docs/open-source/code-signing-policy.md', 'docs/open-source/privacy.md',
    'docs/open-source/downloads.md', 'docs/open-source/public-policy-review.md'];
  await expect.poll(() => page.evaluate(async paths => {
    const cache = await caches.open('lens-docs-studio-v69');
    return Promise.all(paths.map(async path => Boolean(await cache.match(new URL(path, location.href)))));
  }, publicPaths)).toEqual(publicPaths.map(() => true));
  expect(await page.evaluate(() => caches.keys())).not.toContain('lens-docs-studio-v68');

  await context.setOffline(true);
  const responses = await page.evaluate(async paths => Promise.all(paths.map(async path => {
    const response = await fetch(path);
    return { path, status: response.status, text: await response.text() };
  })), publicPaths);
  expect(responses.map(response => response.status)).toEqual(publicPaths.map(() => 200));
  expect(responses[0].text).toBe(readFileSync(new URL('../../LICENSE', import.meta.url), 'utf8'));
  expect(responses[2].text).toContain('PVR_ENABLED_EXTERNAL_FLOW_UNVERIFIED');
  expect(responses[2].text).toContain('not been tested');
  for (const path of publicPaths.filter(path => /(?:code-signing-policy|privacy|downloads|public-policy-review)\.md$/.test(path))) {
    expect(responses.find(response => response.path === path).text)
      .toBe(readFileSync(new URL('../../' + path, import.meta.url), 'utf8'));
  }
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-app-ready', 'true');
  await context.setOffline(false);
});
