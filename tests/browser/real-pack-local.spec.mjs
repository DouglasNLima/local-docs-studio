import { existsSync } from 'node:fs';
import path from 'node:path';
import { expect, test } from '@playwright/test';

const packSources = {
  ZIP: process.env.LENS_DOCS_REAL_PACK_ZIP || '',
  folder: process.env.LENS_DOCS_REAL_PACK_FOLDER || '',
};
const appUrl = process.env.LENS_DOCS_LOCAL_APP_URL || 'http://127.0.0.1:4173/';
const appOrigin = new URL(appUrl).origin;

for (const [sourceKind, sourcePath] of Object.entries(packSources)) {
  test(`owner-supplied documentation pack navigates locally from ${sourceKind}`, async ({ page, context }) => {
    test.skip(!sourcePath || !existsSync(sourcePath), 'The owner-supplied validation pack is unavailable here.');

    const packName = sourceKind === 'ZIP'
      ? path.basename(sourcePath, path.extname(sourcePath))
      : path.basename(sourcePath);
    const root = `${packName}/`;
    const flowPath = `${root}FlowErrorLogLab.md`;
    const onboardingPath = `${root}Onboarding.md`;
    const readmePath = `${root}README.md`;
    const developerPath = `${root}docs/02-Developer-Integration.md`;
    const sourcesPath = `${root}reference/Sources-and-Assurance.md`;
    const documentRequests = [];
    const downloads = [];
    page.on('request', (request) => {
      const url = new URL(request.url());
      if (url.origin === appOrigin && /\.md$/i.test(url.pathname) && !url.pathname.endsWith('/docs/tool-guide.md')) {
        documentRequests.push(url.pathname);
      }
    });
    page.on('download', (download) => downloads.push(download.suggestedFilename()));

    await page.goto(appUrl, { waitUntil: 'domcontentloaded' });
    await expect(page.locator('html')).toHaveAttribute('data-app-ready', 'true');
    await page.locator(sourceKind === 'ZIP' ? '#zipInput' : '#folderInput').setInputFiles(sourcePath);
    await expect(page.locator(`#fileList [data-path="${flowPath}"]`)).toBeVisible();

    const originalPageCount = context.pages().length;
    await page.locator(`#fileList [data-path="${flowPath}"]`).click();
    await expect(page.locator('#activeFileLabel')).toContainText(flowPath);
    await page.locator('#preview a[href="README.md"]').filter({ hasText: 'Modular documentation' }).click();
    await expect(page.locator('#activeFileLabel')).toContainText(readmePath);
    await expect(page.locator(`#fileList [data-path="${readmePath}"]`)).toHaveClass(/active/);
    await page.locator('#documentBackButton').click();
    await expect(page.locator('#activeFileLabel')).toContainText(flowPath);

    await page.locator('#preview a[href="#chapter-01"]').click();
    await expect(page.locator('#activeFileLabel')).toContainText(flowPath);
    await expect(page.locator('#preview #chapter-01')).toHaveCount(1);
    await expect.poll(() => page.locator('#preview').evaluate((preview) => preview.scrollTop)).toBeGreaterThan(0);
    await page.locator('#documentBackButton').click();
    await expect(page.locator('#activeFileLabel')).toContainText(flowPath);

    await page.locator(`#fileList [data-path="${onboardingPath}"]`).click();
    await expect(page.locator('#activeFileLabel')).toContainText(onboardingPath);
    await page.locator('#preview a[href="docs/02-Developer-Integration.md"]').filter({ hasText: 'Developer integration' }).click();
    await expect(page.locator('#activeFileLabel')).toContainText(developerPath);
    await page.locator('#preview a[href="../reference/Sources-and-Assurance.md#p03"]').click();
    await expect(page.locator('#activeFileLabel')).toContainText(sourcesPath);
    await expect(page.locator('#preview #p03')).toHaveCount(1);
    await expect.poll(() => page.locator('#preview').evaluate((preview) => preview.scrollTop)).toBeGreaterThan(0);

    await page.locator('#documentBackButton').click();
    await expect(page.locator('#activeFileLabel')).toContainText(developerPath);
    await page.locator('#documentBackButton').click();
    await expect(page.locator('#activeFileLabel')).toContainText(onboardingPath);
    await page.locator('#documentForwardButton').click();
    await expect(page.locator('#activeFileLabel')).toContainText(developerPath);

    expect(context.pages()).toHaveLength(originalPageCount);
    expect(downloads).toEqual([]);
    expect(documentRequests).toEqual([]);
    await expect(page).toHaveURL(appUrl);
  });
}
