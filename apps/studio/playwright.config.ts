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
        // Unused by the playground spec — every /v1/workspace/* call is
        // intercepted via page.route(), so no real API needs to be reachable.
        NEXT_PUBLIC_MODELLER_API_URL: 'http://localhost:0',
      },
    },
  ],
});
