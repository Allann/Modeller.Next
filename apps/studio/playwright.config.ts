import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests/e2e',
  timeout: 30_000,
  // The HTML report is what deploy-studio.yml's "Upload Playwright report" step actually uploads
  // on failure — 'list' locally keeps a normal run's terminal output readable.
  reporter: process.env.CI ? 'html' : 'list',
  // case-study.spec.ts's journey hits the real deployed public API (not a mock) for its analysis
  // round trip — retry only in CI, where a genuine live-network hiccup shouldn't fail the whole
  // run, and only there: a real bug should still fail outright on a local run with 0 retries.
  retries: process.env.CI ? 2 : 0,
  use: {
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'local',
      testMatch: 'studio.spec.ts',
      use: { baseURL: 'http://localhost:3100' },
    },
    {
      name: 'playground',
      testMatch: 'playground.spec.ts',
      use: { baseURL: 'http://localhost:3101' },
    },
    {
      name: 'case-study',
      testMatch: 'case-study.spec.ts',
      use: { baseURL: 'http://localhost:3114' },
    },
  ],
  webServer: [
    {
      command: 'npm run dev',
      url: 'http://localhost:3100',
      reuseExistingServer: true,
      timeout: 60_000,
    },
    {
      command: 'npm run dev',
      url: 'http://localhost:3101',
      reuseExistingServer: true,
      timeout: 60_000,
      env: {
        PORT: '3101',
        NEXT_PUBLIC_MODELLER_STUDIO_MODE: 'playground',
        // The case-study journey uses the real API. The other playground tests
        // intercept these calls to keep their error scenarios deterministic.
        NEXT_PUBLIC_MODELLER_API_URL: 'http://localhost:5081',
      },
    },
    {
      command: 'npm run build && npm run start -- --port 3114',
      cwd: '../docs',
      url: 'http://localhost:3114',
      reuseExistingServer: true,
      timeout: 60_000,
      env: {
        NEXT_PUBLIC_PLAYGROUND_URL: 'http://localhost:3113/?example=child-care',
        MODELLER_DOCS_DIST_DIR: '.next/e2e',
      },
    },
    {
      command: 'npm run dev',
      url: 'http://localhost:3113',
      reuseExistingServer: true,
      timeout: 60_000,
      env: {
        PORT: '3113',
        NEXT_PUBLIC_MODELLER_STUDIO_MODE: 'playground',
        // The case-study test proxies these calls to the deployed API so the
        // browser journey verifies the public analysis behavior.
        NEXT_PUBLIC_MODELLER_API_URL: 'http://localhost:0',
        MODELLER_STUDIO_DIST_DIR: '.next/case-study',
      },
    },
  ],
});
