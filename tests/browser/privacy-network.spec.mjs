import { test, expect } from '@playwright/test';

// Synthetic observation only: never send a real image recipient document data.
test('clean browser session uses the local host and renders a synthetic remote image on request', async ({ page }) => {
  const requests = [];
  await page.route('**/*', async route => {
    const request = route.request();
    if (new URL(request.url()).hostname === 'privacy-image.example.test') {
      requests.push({ url: request.url(), method: request.method(), body: request.postData() });
      await route.fulfill({ contentType: 'image/png', body: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=', 'base64') });
    } else {
      expect(new URL(request.url()).hostname).toBe('127.0.0.1');
      await route.continue();
    }
  });
  await page.goto('/');
  await expect(page.locator('html')).toHaveAttribute('data-app-ready', 'true');
  await page.waitForTimeout(1500);
  expect(requests).toEqual([]);
  await page.locator('#editor').fill('# Local synthetic note\n\n![Remote](https://privacy-image.example.test/image.png?test=only)\n');
  await expect.poll(() => requests.length).toBeGreaterThan(0);
  expect(requests.every(request => request.method === 'GET' && request.body === null)).toBe(true);
  expect(requests[0].url).toBe('https://privacy-image.example.test/image.png?test=only');
  // This bounded observation excludes platform traffic and other document types.
});
