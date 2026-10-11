import { expect, test } from '@playwright/test';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

for (const mode of ['disk', 'static hosting']) {
  test(`native Docs Site navigation, search, images and themes work from ${mode}`, async ({ page }) => {
    const errors = [];
    const external = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('request', request => {
      const url = request.url();
      if (/^https?:/.test(url) && !url.startsWith('http://127.0.0.1:')) external.push(url);
    });
    const relative = 'artifacts/native/validation/docs-site/index.html';
    await page.goto(mode === 'disk' ? pathToFileURL(path.resolve(relative)).href : '/' + relative);
    await expect(page.locator('#siteTitle')).toHaveText('Native handbook');
    await expect(page.locator('#content h1')).toHaveText('Native documentation');
    await expect(page.locator('#content img')).toHaveCount(2);
    await expect.poll(() => page.locator('#content img').evaluateAll(images => images.every(image => image.complete && image.naturalWidth > 0))).toBe(true);
    await page.locator('#content').getByRole('link', { name: 'Start here' }).click();
    await expect(page.locator('#content h1')).toHaveText('Getting started');
    await expect(page.locator('#content h2')).toBeFocused();
    await expect(page.locator('#docMeta')).toContainText('Draft');
    await expect(page.locator('#content')).toContainText('Unsaved reviewed content.');
    await page.locator('#content').getByRole('link', { name: 'Home', exact: true }).click();
    await expect(page.locator('#content h1')).toHaveText('Native documentation');
    await page.goBack();
    await expect(page.locator('#content h1')).toHaveText('Getting started');
    await page.locator('#searchInput').fill('unsaved reviewed');
    await expect(page.locator('#searchResults .search-result')).toHaveCount(1);
    await page.locator('#searchResults .search-result').click();
    await expect(page.locator('#content')).toContainText('Unsaved reviewed content.');
    await page.locator('#searchInput').fill('<img onerror=run()>');
    await expect(page.locator('#searchResults')).toHaveText('No results.');
    await page.locator('#searchInput').fill('');
    await expect(page.locator('#nav')).toBeVisible();
    await page.locator('#themeToggle').click();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
    await page.screenshot({ path: test.info().outputPath('native-site-light.png'), fullPage: true });
    await page.locator('#themeToggle').click();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
    await page.screenshot({ path: test.info().outputPath('native-site-dark.png'), fullPage: true });
    expect(errors).toEqual([]);
    expect(external).toEqual([]);
  });
}
