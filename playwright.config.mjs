import { defineConfig, devices } from '@playwright/test';

const port = Number(process.env.LENS_TEST_PORT || 4173);
if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('LENS_TEST_PORT must be a port between 1024 and 65535.');
const baseURL = `http://127.0.0.1:${port}`;
const testBrowser = process.env.LENS_TEST_BROWSER;
if (testBrowser && !['chromium', 'edge'].includes(testBrowser)) throw new Error('LENS_TEST_BROWSER must be chromium or edge.');

export default defineConfig({
  testDir: './tests/browser',
  timeout: 90_000,
  workers: 1,
  expect: {
    timeout: 20_000,
  },
  use: {
    baseURL,
    acceptDownloads: true,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  webServer: {
    command: `python tests/browser/serve-static.py --port ${port}`,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 15_000,
  },
  projects: [
    {
      name: 'chromium',
      // Exercise the full browser; the separate headless shell crashes on Windows with 0xC0000005.
      use: { ...devices['Desktop Chrome'], channel: 'chromium' },
    },
    {
      name: 'edge',
      use: { ...devices['Desktop Edge'], channel: 'msedge' },
    },
  ].filter(project => !testBrowser || project.name === testBrowser),
});
