import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests/e2e',
  timeout: 30_000,
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
