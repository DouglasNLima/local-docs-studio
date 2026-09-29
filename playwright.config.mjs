import { defineConfig, devices } from '@playwright/test';

const testPort = Number(process.env.LENS_DOCS_TEST_PORT || 4173);
const testUrl = `http://127.0.0.1:${testPort}`;

export default defineConfig({
  testDir: './tests/browser',
  timeout: 90_000,
  workers: 1,
  expect: {
    timeout: 20_000,
  },
  use: {
    baseURL: testUrl,
    acceptDownloads: true,
    // Avoid exhausting Windows loopback sockets across the full browser suite; opt in where PWA caching is under test.
    serviceWorkers: 'block',
    trace: 'on-first-retry',
  },
  webServer: {
    // Use a quiet static server to keep long Windows browser suites from exhausting loopback sockets.
    command: `node tests/static/serve-for-playwright.mjs ${testPort}`,
    url: testUrl,
    reuseExistingServer: true,
    timeout: 15_000,
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'edge',
      use: { ...devices['Desktop Edge'], channel: 'msedge' },
    },
  ],
});
