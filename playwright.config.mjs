import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/browser',
  timeout: 90_000,
  workers: 1,
  expect: {
    timeout: 20_000,
  },
  use: {
    baseURL: 'http://127.0.0.1:4173',
    acceptDownloads: true,
    // Avoid exhausting Windows loopback sockets across the full browser suite; opt in where PWA caching is under test.
    serviceWorkers: 'block',
    trace: 'on-first-retry',
  },
  webServer: {
    // Keep the Windows test server on IPv4 to avoid dual-stack socket aborts during long suites.
    command: 'python -m http.server 4173 --bind 127.0.0.1',
    url: 'http://127.0.0.1:4173',
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
