import { defineConfig } from '@playwright/test';
import base from './playwright.config.mjs';

// These tests consume native-generated export files; the web pipeline does not run them.
export default defineConfig({
  ...base,
  testDir: './tests/native-browser',
  outputDir: './artifacts/native/site-browser',
});
